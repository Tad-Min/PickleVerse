using System;
using System.Collections.Generic;
using System.Linq;

namespace PickleVerse.Multiplayer
{
    public enum PlayMode { Singles = 1, Doubles = 2 }
    public enum FlowState { Menu, WaitingRoom, Delay, Searching, Connecting, InMatch, Reconnecting, Results }
    public enum ShotKind { None, Groundstroke, Dink, Volley, Drop, Smash, ResetVolley, SpeedUp, Punch, Serve }

    [Serializable] public class RoomMember
    {
        public string id, name, gender, group;
        public int team;
        public int elo = 100;
        public bool ready;
        public bool bot;
    }
    [Serializable] public class RoomData
    {
        public bool custom;
        public PlayMode mode = PlayMode.Doubles;
        public string searchId = "", phase = "idle", matchId = "", notice = "";
        public long startedUtcTicks;
        public int generation;
        public int revision;
        public int readyEpoch;
        public Dictionary<string,int> teams=new Dictionary<string,int>(),teamVersions=new Dictionary<string,int>();
        public Dictionary<string,string> handledTeamRequests=new Dictionary<string,string>();
        public MatchRoster roster;
    }
    [Serializable] public class MatchRoster
    {
        public string matchId, region;
        public PlayMode mode;
        public bool custom;
        public List<RoomMember> members = new List<RoomMember>();
        // Optional JSON field: older match payloads remain daytime. The host captures this once.
        public bool night;
        public MatchRoster WithEnvironment(bool selectedNight) => new MatchRoster
        {matchId=matchId,region=region,mode=mode,custom=custom,members=members,night=selectedNight};
        public int Count => members.Count;
        public int HumanCount => members.FindAll(p=>!p.bot).Count;
        public bool RankedBot => !custom && mode==PlayMode.Singles && members.Exists(p=>p.bot);
        public RoomMember Find(string id) => members.Find(p => p.id == id);
        public bool IsValid()
        {
            var ids = new HashSet<string>();
            int[] counts = new int[2];
            if (members.Exists(p=>p.bot) && (custom || mode!=PlayMode.Singles || HumanCount!=1)) return false;
            foreach (var p in members)
            {
                if (string.IsNullOrEmpty(p.id) || !ids.Add(p.id) || p.team < 0 || p.team > 1) return false;
                counts[p.team]++;
            }
            return custom ? counts[0] >= 1 && counts[1] >= 1 && counts[0] <= (int)mode && counts[1] <= (int)mode
                : counts[0] == (int)mode && counts[1] == (int)mode;
        }
    }


}
