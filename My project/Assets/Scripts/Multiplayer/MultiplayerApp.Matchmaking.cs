using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Matchmaker.Models;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.Multiplayer.Services;

namespace PickleVerse.Multiplayer
{
    public sealed partial class MultiplayerApp
    {
        public async Task FindAsync()
        {
            if (State!=FlowState.WaitingRoom || Custom || !IsCaptain || Searching) return;
            MultiplayerSceneConfiguration.Validate();
            CancelLocal(); observedSearch=Guid.NewGuid().ToString("N"); publishedMatch="";
            cancelledSearch=""; measured.Clear(); ignoredMatches.Clear();
            searchCancellation=new CancellationTokenSource(); clock.Start(Time.realtimeSinceStartupAsDouble);
            if (Mode==PlayMode.Singles) rankedSearch.Start(Time.realtimeSinceStartupAsDouble);
            SetState(FlowState.Delay,"Đang chờ trước khi tìm trận.");
            int requestGeneration=operation;
            if(Mode==PlayMode.Doubles && Room==null)
            {
                preparingSearch=true;
                try { await CreateRoomAsync(false,PlayMode.Doubles); }
                finally { preparingSearch=false; }
                if(requestGeneration!=operation) return;
            }
            if (Room!=null)
            {
                roomData.phase="delay"; roomData.searchId=observedSearch; roomData.startedUtcTicks=DateTime.UtcNow.Ticks; roomData.matchId=""; roomData.notice=""; roomData.generation++;
                int generation=operation;
                Room.AsHost().IsLocked=true; await SaveRoomAsync();
                if(generation!=operation && Room!=null && IsCaptain)
                { roomData.phase="idle"; Room.AsHost().IsLocked=false; await SaveRoomAsync(); }
            }
        }
        public async Task CancelSearchAsync()
        {
            if(ReadyCheckActive) { await DeclineMatchAsync(); return; }
            if (!Searching || cancelling) return;
            cancelling=true; cancelledSearch=observedSearch;
            try
            {
            if (Room!=null && !IsCaptain)
            {
                CancelLocal();
                Room.CurrentPlayer.SetProperty("cancel",new PlayerProperty(cancelledSearch)); await Room.SaveCurrentPlayerDataAsync();
                await AbortMatchAsync("Một thành viên nhóm đã hủy."); SetState(FlowState.WaitingRoom,"Đã hủy tìm trận cho nhóm."); ExitCourt(); return;
            }
            CancelLocal(); await AbortMatchAsync("Một người đã hủy tìm trận.");
            if (Room!=null && IsCaptain) { roomData.phase="idle"; roomData.matchId=""; roomData.notice="Đã hủy tìm trận cho nhóm."; Room.AsHost().IsLocked=false; await SaveRoomAsync(); }
            SetState(FlowState.WaitingRoom,"Đã hủy tìm trận.");
            ExitCourt();
            }
            finally { cancelling=false; Changed?.Invoke(); }
        }
        void CancelLocal() { operation++; rankedSearch.Cancel(); searchCancellation?.Cancel(); searchCancellation?.Dispose(); searchCancellation=null; clock.Stop(); }
        async Task SaveRoomAsync()
        {
            // SDK refresh/event snapshots can arrive while a save is in flight.
            // Version the host's state before publishing so they cannot undo it.
            roomData.revision++;
            var currentRoom = Room;
            currentRoom.AsHost().SetProperty("room", new SessionProperty(JsonConvert.SerializeObject(roomData), VisibilityPropertyOptions.Member));
            await currentRoom.AsHost().SavePropertiesAsync();
            nextPoll = Time.realtimeSinceStartupAsDouble + 4;
        }

        Task publishingQos;
        int publishingQosGeneration = -1;
        Task PublishQosAsync()
        {
            if (publishingQosGeneration == operation && publishingQos != null && !publishingQos.IsCompleted) return publishingQos;
            publishingQosGeneration = operation;
            publishingQos = PublishQosForSearchAsync(operation, searchCancellation?.Token ?? CancellationToken.None);
            return publishingQos;
        }
        async Task PublishQosForSearchAsync(int generation, CancellationToken token)
        {
            var currentRoom = Room;
            var result=await groupMatcher.MeasureAsync(Settings.doublesQueue,token);
            token.ThrowIfCancellationRequested();
            if (generation != operation || currentRoom != Room || !Searching) return;
            measured[PlayerId]=result;
            if(currentRoom!=null) { currentRoom.CurrentPlayer.SetProperty("qos",new PlayerProperty(JsonConvert.SerializeObject(result))); await currentRoom.SaveCurrentPlayerDataAsync(); }
        }
        async Task SearchAsync(int generation)
        {
            if(generation!=operation || searchCancellation==null) return;
            var token=searchCancellation.Token;
            try
            {
                if(Room!=null && IsCaptain) { roomData.phase="searching"; await SaveRoomAsync(); }
                if(Mode==PlayMode.Singles)
                {
                    await SearchRankedSinglesAsync(generation,token);
                }
                else
                {
                    if(!measured.ContainsKey(PlayerId)) await PublishQosAsync();
                    var members=Members(); var qos=new Dictionary<string,List<QosResult>>();
                    while(!token.IsCancellationRequested)
                    {
                        foreach(var member in members)
                        {
                            var json=Prop(Room.GetPlayer(member.id),"qos");
                            if(!string.IsNullOrEmpty(json)) qos[member.id]=JsonConvert.DeserializeObject<List<QosResult>>(json);
                        }
                        if(qos.Count==members.Count) break;
                        await Task.Delay(500,token);
                    }
                    token.ThrowIfCancellationRequested();
                    var result=await groupMatcher.FindAsync(Settings.doublesQueue,members,qos,token);
                    if(generation!=operation) return;
                    var roster=GroupMatchmaker.Roster(result.MatchId,result.MatchProperties.Region,result.MatchProperties.Teams,result.MatchProperties.Players,Mode);
                    roomData.roster=roster; roomData.matchId=roster.matchId; roomData.phase="match";
                    publishedMatch=roster.matchId; await SaveRoomAsync(); await ConnectMatchAsync(roster,generation);
                }
            }
            catch(Exception e)
            {
                if(generation!=operation || token.IsCancellationRequested) return;
                if (e is TimeoutException) Debug.Log("Matchmaking hết thời gian: "+e.Message);
                else Debug.LogWarning("Matchmaking attempt failed: "+e);
                await CancelSearchAsync(); SetState(FlowState.WaitingRoom,"Không tìm được trận: "+e.Message);
            }
        }
        SessionOptions MatchOptions(int count,string region)
        {
            int generation=operation;
            return new SessionOptions { Name="PickleVerse Match",Type="match",MaxPlayers=count,IsPrivate=true,
                PlayerProperties=new Dictionary<string,PlayerProperty> { ["identity"]=new PlayerProperty(PlayerId),["cancelled"]=new PlayerProperty("0"),["readyDecision"]=new PlayerProperty("pending") } }.WithRelayNetwork(new RelayNetworkOptions(region,true)).WithNetworkHandler(new SessionNetworkHandler(Network,()=>generation==operation && !quitting));
        }
    }
}
