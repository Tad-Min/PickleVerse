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
        readonly System.Random botAccuracy = new System.Random();
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
            bool hit = chance.Resolve(botAccuracy, Roster.Find(id).elo, Rules.strokeCount);
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
        public bool PredictContact(int end, bool requireBounce, out Vector3 point, out float delay, float height = .85f)
        {
            var p = ballPosition;
            var v = ballVelocity;
            bool bounced = Rules.bounceCount > 0;
            for (int i = 1; i <= 300; i++)
            {
                BallFlight.Integrate(ref p, ref v, .01f);
                if (p.y <= Court.BallRadius)
                {
                    if (bounced)
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
    // Server-owned movement and swings use the same actor, contact checks and rules as human inputs.
    public sealed class MultiplayerBot
    {
        readonly System.Random random;
        float reaction, cooldown;
        int seenStroke = -1;
        internal readonly BotContactChance Contact = new BotContactChance();
        public MultiplayerBot(int seed)
        {
            random = new System.Random(seed);
        }
        public void Reset()
        {
            seenStroke = -1;
            reaction = cooldown = 0;
            Contact.Reset();
        }
        public void Step(NetworkMatch match, NetworkPlayer player, float dt, out Vector2 move, out bool run)
        {
            move = Vector2.zero;
            run = false;
            cooldown = Mathf.Max(0, cooldown - dt);
            if (match.Paused || match.Finished || match.BetweenGames || match.BetweenPoints || match.TimedOut)
                return;
            var rules = match.Rules;
            var actor = player.Actor;
            if (rules.waitingServe)
            {
                if (!Contact.Missed(rules.strokeCount) && rules.servingTeam == player.Team && rules.serverIndex == player.Index && match.ServeElapsed > 1.1f && !actor.Swinging)
                    match.TrySwing(player, new ShotIntent { kind = ShotKind.Groundstroke, crossCourt = true });
                return;
            }
            if (!rules.live)
                return;
            bool incoming = rules.lastHitTeam != player.Team;
            if (seenStroke != rules.strokeCount)
            {
                seenStroke = rules.strokeCount;
                reaction = .12f;
            }
            reaction -= dt;
            var target = new Vector3(0, 0, actor.end * (rules.strokeCount <= 2 && rules.servingTeam == player.Team ? 6.4f : 3.05f));
            if (incoming)
            {
                bool volley = rules.strokeCount > 2 && Mathf.Abs(actor.transform.position.z) < 3.7f && match.BallPosition.y > .6f;
                if (match.PredictContact(actor.end, !volley, out var point, out float delay))
                {
                    var kind = volley ? PickleVerse.LocalMatch.ShotKind.Volley : rules.strokeCount == 2 ? PickleVerse.LocalMatch.ShotKind.Drop : Mathf.Abs(point.z) < 3.5f ? PickleVerse.LocalMatch.ShotKind.Dink : PickleVerse.LocalMatch.ShotKind.Drive;
                    var intent = new PickleVerse.LocalMatch.ShotIntent(kind, false, random.NextDouble() < .55);
                    var offset = actor.ContactOffset(intent, kind == PickleVerse.LocalMatch.ShotKind.Drop);
                    if (kind == PickleVerse.LocalMatch.ShotKind.Drop && offset.x > -.18f)
                        offset.x = -.22f;
                    if (match.PredictContact(actor.end, !volley, out var corrected, out var correctedDelay, offset.y))
                    {
                        point = corrected;
                        delay = correctedDelay;
                    }
                    target = point - actor.transform.TransformDirection(offset);
                    target.y = 0;
                    if (volley)
                        target.z = actor.end * Mathf.Max(Mathf.Abs(target.z), 2.85f);
                    var delta = target - actor.transform.position;
                    delta.y = 0;
                    float arrival = delta.magnitude / (actor.runSpeed * (actor.Swinging ? .6f : 1));
                    if (!Contact.Missed(rules.strokeCount) && reaction <= 0 && cooldown <= 0 && !actor.Swinging && delay <= intent.ContactDelay + .025f && arrival < intent.ContactDelay + .1f)
                    {
                        match.TrySwing(player, BotMatchGameplayService.FromLocal(intent));
                        if (actor.Swinging)
                            cooldown = intent.Duration;
                    }
                }
            }
            var direction = target - actor.transform.position;
            direction.y = 0;
            if (direction.magnitude > .08f && reaction <= 0)
            {
                direction = Vector3.ClampMagnitude(direction * 3, 1);
                move = new Vector2(direction.x, direction.z);
                run = incoming && Vector3.Distance(target, actor.transform.position) > .55f;
            }
        }
    }
}
