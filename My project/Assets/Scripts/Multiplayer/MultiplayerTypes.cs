using System;
using System.Collections.Generic;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    public enum PlayMode { Singles = 1, Doubles = 2 }
    public enum FlowState { Menu, WaitingRoom, Delay, Searching, Connecting, InMatch, Reconnecting, Results }
    public enum ShotKind { None, Groundstroke, Dink, Volley, Drop, Smash, ResetVolley, SpeedUp, Punch, Serve }
    public enum ModifierMode { OneShot, Hold }

    [Serializable] public class RoomMember
    {
        public string id, name, gender, group;
        public int team;
        public bool ready;
    }
    [Serializable] public class RoomData
    {
        public bool custom;
        public PlayMode mode = PlayMode.Doubles;
        public string searchId = "", phase = "idle", matchId = "", notice = "";
        public long startedUtcTicks;
        public int generation;
        public int readyEpoch;
        [SerializeField] public Dictionary<string,int> teams=new Dictionary<string,int>(),teamVersions=new Dictionary<string,int>();
        [SerializeField] public Dictionary<string,string> handledTeamRequests=new Dictionary<string,string>();
        public MatchRoster roster;
    }
    [Serializable] public class MatchRoster
    {
        public string matchId, region;
        public PlayMode mode;
        public List<RoomMember> members = new List<RoomMember>();
        public int Count => members.Count;
        public RoomMember Find(string id) => members.Find(p => p.id == id);
        public bool IsValid()
        {
            var ids = new HashSet<string>();
            int[] counts = new int[2];
            foreach (var p in members)
            {
                if (string.IsNullOrEmpty(p.id) || !ids.Add(p.id) || p.team < 0 || p.team > 1) return false;
                counts[p.team]++;
            }
            return counts[0] == (int)mode && counts[1] == (int)mode;
        }
    }

    /// <summary>Uses a monotonic clock. Search starts at 6.000 seconds, never at 5.</summary>
    public sealed class SearchClock
    {
        public const double DelaySeconds = 6, SearchSeconds = 120;
        public bool Running { get; private set; }
        double began;
        public void Start(double now, double alreadyElapsed = 0) { began = now - Math.Max(0, alreadyElapsed); Running = true; }
        public void Stop() { Running = false; }
        public double Elapsed(double now) => Running ? Math.Max(0, now - began) : 0;
        public bool CanSubmit(double now) => Running && Elapsed(now) >= DelaySeconds;
        public bool TimedOut(double now) => Running && Elapsed(now) >= DelaySeconds + SearchSeconds;
        public string Display(double now) { var seconds = (int)Elapsed(now); return $"{seconds / 60:00}:{seconds % 60:00}"; }
    }

    public struct ShotIntent
    {
        public ShotKind kind;
        public bool attack, crossCourt;
    }

    /// <summary>Collects chord keys until the first base-key release, then waits for all keys up.</summary>
    public sealed class ShotChord
    {
        int held, selected;
        bool blocked, attack, cross, modifierInvalid;
        public ModifierMode ModifierMode;
        public void Reset() { held = selected = 0; blocked = attack = cross = modifierInvalid = false; }
        public void Modifier(bool uDown, bool iDown, bool uHeld, bool iHeld)
        {
            if (ModifierMode == ModifierMode.Hold) { attack = uHeld; cross = iHeld; }
            else { if (uDown) attack = true; if (iDown) cross = true; }
            if (attack && cross) modifierInvalid = true;
        }
        public ShotIntent Step(int currentMask)
        {
            if (blocked) { held = currentMask; if (currentMask == 0) { Reset(); } return default; }
            selected |= currentMask;
            if (selected == 7) modifierInvalid = true;
            bool release = (held & ~currentMask) != 0;
            held = currentMask;
            if (!release) return default;
            var kind = selected == 1 ? ShotKind.Groundstroke : selected == 2 ? ShotKind.Dink : selected == 4 ? ShotKind.Volley : selected == 3 ? ShotKind.Drop : selected == 5 ? ShotKind.Smash : selected == 6 ? ShotKind.ResetVolley : ShotKind.None;
            if (modifierInvalid) kind = ShotKind.None;
            if (attack && selected!=1 && selected!=2 && selected!=4) kind=ShotKind.None;
            if (attack && kind == ShotKind.Dink) kind = ShotKind.SpeedUp;
            if (attack && kind == ShotKind.Volley) kind = ShotKind.Punch;
            var result = new ShotIntent { kind = kind, attack = attack, crossCourt = cross };
            blocked = true;
            attack = cross = modifierInvalid = false;
            if (currentMask == 0) Reset();
            return result;
        }
    }

}
