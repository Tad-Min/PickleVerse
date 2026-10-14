using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    public sealed partial class MultiplayerApp
    {
        ISession startingNetworkSession;
        Action<SessionError> networkStartFailed;
        bool AllSessionMembersPresent => Match!=null && Roster!=null && Roster.members.Where(m=>!m.bot).All(m=>Match.Players.Any(p=>p.Id==m.id));
        async Task StartAcceptedNetworkAsync()
        {
            var session=Match;
            if(session==null || !readyApproved || !session.IsHost || startingNetworkSession==session ||
                Network.IsListening || session.Network.State==NetworkState.Starting || session.Network.State==NetworkState.Started)return;
            startingNetworkSession=session;int generation=operation;
            connectionDeadline=Time.realtimeSinceStartupAsDouble+45;
            SetNotice("Mọi người đã xác nhận • Đang kết nối vào sân…");
            try
            {
                SetConnectionIdentity();
                await MatchRelayConnection.StartAsync(Roster.region,options=>session.AsHost().Network.StartRelayNetworkAsync(options),
                    ()=>Match==session && operation==generation && !quitting && !leavingModePage);
                if(Match==session && generation==operation && Network.IsHost)ClientIdentities[Network.LocalClientId]=PlayerId;
            }
            catch(OperationCanceledException) { }
            catch(Exception e)
            {
                if(Match!=session || generation!=operation)return;
                string detail=MatchRelayConnection.Describe(e);
                Debug.LogWarning("Gameplay connection failed after ready check: "+detail);
                await RetryMatchAsync("Chưa kết nối được sân: "+detail);
            }
            finally {if(startingNetworkSession==session)startingNetworkSession=null;}
        }
        void AttachNetworkFailure(ISession session)
        {
            networkStartFailed=error=>
            {
                // The host's awaited start owns its error and optional region fallback.
                if(Match==session && readyApproved && !session.IsHost && !intentionalShutdown)
                    _=Safe(RetryMatchAsync("Chưa kết nối được sân ("+error+"); đang tìm lại…"));
            };
            if(session.Network!=null)session.Network.StartFailed+=networkStartFailed;
        }
        void DetachNetworkFailure(ISession session)
        {if(session.Network!=null && networkStartFailed!=null)session.Network.StartFailed-=networkStartFailed;networkStartFailed=null;}
    }
}
