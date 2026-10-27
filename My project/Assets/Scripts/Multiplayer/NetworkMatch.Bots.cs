using System.Collections.Generic;
using System.Linq;
using PickleVerse.LocalMatch;
using PickleVerse.Progression;
using PickleVerse.Rules;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    public sealed partial class NetworkMatch
    {
        readonly Dictionary<string, MultiplayerBot> bots = new Dictionary<string, MultiplayerBot>();
        public Vector3 BallPosition => ballPosition;
        public Vector3 BallVelocity => ballVelocity;
        public int BotShots { get; private set; }
        public int BotReturns { get; private set; }
        public int BotContactAttempts { get; private set; }
        public int BotMisses { get; private set; }
        public void AutomateLocalPlayerForSmoke()
        {
            if (!IsServer || !Debug.isDebugBuild || (!Application.isEditor && !System.Environment.GetCommandLineArgs().Contains("-pkvCase")))
                throw new System.InvalidOperationException("Chỉ cho phép điều khiển thử trong phiên smoke development.");
            foreach (var player in players.Values)
                if (player.IsLocalHuman)
                    bots[player.Identity.ToString()] = new MultiplayerBot(197);
        }
        Vector3 LaunchBall(NetworkPlayer player, PickleVerse.LocalMatch.ShotIntent shot)
        {
            if (player.Automated)
            {
                BotShots++;
                if (shot.kind != PickleVerse.LocalMatch.ShotKind.Serve)
                    BotReturns++;
            }
            if (player.Automated && bots.TryGetValue(player.Identity.ToString(), out var bot))
            {
                var target = shots.Target(player.Actor, shot, ballPosition, Rules.serverWorldX);
                if (shot.kind != PickleVerse.LocalMatch.ShotKind.Serve)
                {
                    target.x += Mathf.Sin(BotShots * 13.37f) * bot.AimError;
                    target.z += Mathf.Cos(BotShots * 7.11f) * bot.AimError;
                }
                return ShotTrajectory.Launch(shot, ballPosition, target);
            }
            return shots.Launch(player.Actor, shot, ballPosition, Rules.serverWorldX);
        }
        bool BotMissedContact(NetworkPlayer player) => player.Automated &&
            bots.TryGetValue(player.Identity.ToString(), out var brain) && brain.Contact.Missed(Rules.strokeCount);
        bool TryBotContact(NetworkPlayer player)
        {
            if (!player.Automated)
                return true;
            var id = player.Identity.ToString();
            var chance = bots[id].Contact;
            bool decided = chance.HasDecision(Rules.strokeCount);
            bool hit = chance.Resolve(bots[id].ContactRandom, Roster.Find(id).elo, Rules.strokeCount,
                isServe: player.Actor.Intent.kind == PickleVerse.LocalMatch.ShotKind.Serve);
            if (!decided)
            {
                BotContactAttempts++;
                if (!hit)
                {
                    BotMisses++;
                    ReplayEventRpc("Miss", id);
                }
            }
            // A whiff consumes the swing, not the flight or the rules' current hitter.
            if (!hit)
                player.Actor.MissContact();
            return hit;
        }
        public bool PredictContact(int end, bool requireBounce, out Vector3 point, out float delay, float height = .85f, bool beforeBounce = false)
        {
            var p = ballPosition;
            var v = ballVelocity;
            bool bounced = Rules.bounceCount > 0;
            if (beforeBounce && bounced) { point = Vector3.zero; delay = 0; return false; }
            for (int i = 1; i <= 300; i++)
            {
                BallFlight.Integrate(ref p, ref v, .01f);
                if (p.y <= Court.BallRadius)
                {
                    if (bounced || beforeBounce)
                        break;
                    bounced = true;
                    BallFlight.Rebound(ref p, ref v);
                }
                if (p.z * end > .5f && p.z * end < 9 && Mathf.Abs(p.y - height) < .1f && (!requireBounce || bounced))
                {
                    point = p;
                    delay = i * .01f;
                    return true;
                }
            }
            point = Vector3.zero;
            delay = 0;
            return false;
        }
    }
    // Both local and online adaptive bots execute the same Unity Behavior graph.
    public sealed class MultiplayerBot
    {
        readonly int seed;
        BhTreeBot brain;
        internal readonly BotContactChance Contact = new BotContactChance();
        internal readonly System.Random ContactRandom;
        public float AimError => brain ? brain.AimError : 0;
        public MultiplayerBot(int seed) { this.seed = seed; ContactRandom = new System.Random(seed); }
        public void Reset() { Contact.Reset(); if (brain) brain.ResetPoint(); }
        public void Step(NetworkMatch match, NetworkPlayer player, float dt, out Vector2 move, out bool run)
        {
            if (!brain)
            {
                brain = player.Actor.gameObject.AddComponent<BhTreeBot>();
                var policy = ProgressionRuntime.Instance?.Snapshot?.catalog?.botPolicy ?? new BotPolicy();
                brain.Initialize(new NetworkAdaptiveBotContext(match, player), match.Roster.Find(player.Identity.ToString()).elo, policy, seed);
            }
            brain.Step(dt, out move, out run);
        }
    }
    public sealed class NetworkAdaptiveBotContext : IAdaptiveBotContext
    {
        readonly NetworkMatch match;
        readonly NetworkPlayer player;
        public NetworkAdaptiveBotContext(NetworkMatch match, NetworkPlayer player) { this.match = match; this.player = player; }
        public MatchActor Actor => player.Actor;
        public Vector3 BallPosition => match.BallPosition;
        public Vector3 BallVelocity => match.BallVelocity;
        public Vector3 OpponentPosition => NetworkPlayer.All.Where(p => p && p.Team != player.Team).Select(p => p.transform.position).FirstOrDefault();
        public bool Active => !match.Paused && !match.Finished && !match.BetweenGames && !match.BetweenPoints && !match.TimedOut;
        public bool Incoming => Active && match.Rules.live && match.Rules.lastHitTeam != player.Team;
        public bool Serving => match.Rules.waitingServe;
        public bool IsServer => match.Rules.servingTeam == player.Team && match.Rules.serverIndex == player.Index;
        public int Stroke => match.Rules.strokeCount;
        public int Bounces => match.Rules.bounceCount;
        public int Server => IsServer ? Actor.playerId : -1;
        public int ExpectedHitter => Incoming ? Actor.playerId : -1;
        public string Phase => Serving ? "PreServe" : "Rally";
        public float ServeElapsed => match.ServeElapsed;
        public bool PredictContact(bool bounce, out Vector3 point, out float delay, float height, bool beforeBounce)
            => match.PredictContact(Actor.end, bounce, out point, out delay, height, beforeBounce);
        public bool RequestShot(PickleVerse.LocalMatch.ShotIntent shot)
        {
            if (Actor.Swinging) return false;
            match.TrySwing(player, BotMatchGameplayService.FromLocal(shot));
            return Actor.Swinging;
        }
    }
}
