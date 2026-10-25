using System;
using PickleVerse.Progression;
using PickleVerse.Rules;
using Unity.Behavior;
using UnityEngine;
namespace PickleVerse.LocalMatch
{
    public sealed class BhTreeBot : MonoBehaviour
    {
        public BehaviorGraph graph;
        BehaviorGraphAgent agent;
        public BotMatch Match { get; private set; }
        public MatchActor Actor => Match.bot;
        public int Elo { get; private set; }
        public int Tier => Elo < 500 ? 0 : Elo < 2000 ? 1 : 2;
        public float ReactionSeconds => Tier == 0 ? .28f : Tier == 1 ? .15f : .08f;
        public float AimError => Tier == 0 ? .30f : Tier == 1 ? .16f : .08f;
        public string Branch { get; private set; } = "Chờ";
        public Vector2 Move { get; private set; }
        public bool Run { get; private set; }
        public bool Incoming => Active && Match.Phase == MatchPhase.Rally && Match.Rules.Live && Match.Rules.ExpectedHitter == Actor.playerId;
        public bool Active => Match && !Match.Paused && (Match.Phase == MatchPhase.PreServe || Match.Phase == MatchPhase.Rally);
        float reaction, cooldown;
        int stroke = -1;
        float simulationTime;
        public void Initialize(BotMatch match, int elo)
        {
            Match = match;
            Elo = elo;
            reaction = cooldown = simulationTime = 0;
            stroke = -1;
            if (!graph)
                graph = Array.Find(Resources.LoadAll<BehaviorGraph>("BhTree/Bot Behavior"), g => g != null);
            if (!graph)
                throw new InvalidOperationException("Thiếu graph BhTree. Chạy PickleVerse/Features/Setup.");
            agent = GetComponent<BehaviorGraphAgent>() ?? gameObject.AddComponent<BehaviorGraphAgent>();
            agent.enabled = false;
            agent.Graph = graph;
            agent.Init();
        }
        public void Step(float dt, out Vector2 move, out bool run)
        {
            Move = Vector2.zero;
            Run = false;
            cooldown = Mathf.Max(0, cooldown - dt);
            simulationTime += dt;
            if (stroke != Match.Rules.Stroke)
            {
                stroke = Match.Rules.Stroke;
                reaction = ReactionSeconds;
            }
            reaction -= dt;
            agent.SetVariableValue("BallPosition", Match.BallPosition);
            agent.SetVariableValue("BallVelocity", Match.BallVelocity);
            agent.SetVariableValue("BotPosition", Actor.transform.position);
            agent.SetVariableValue("HumanPosition", Match.human.transform.position);
            agent.SetVariableValue("Elo", Elo);
            agent.SetVariableValue("Stroke", stroke);
            agent.SetVariableValue("Bounces", Match.Rules.Bounces);
            agent.SetVariableValue("Phase", Match.Phase.ToString());
            agent.SetVariableValue("Swinging", Actor.Swinging);
            agent.SetVariableValue("InKitchen", Actor.InKitchen);
            agent.SetVariableValue("Server", Match.Rules.Server);
            agent.SetVariableValue("ExpectedHitter", Match.Rules.ExpectedHitter);
            agent.SetVariableValue("CanAct", Active);
            agent.SetVariableValue("Cooldown", cooldown);
            agent.SetVariableValue("SimulationTime", simulationTime);
            // Nodes are instantaneous decisions. Drain the ordered selectors within this simulation tick.
            agent.Graph.Restart();
            for (int i = 0; i < 24 && agent.Graph.IsRunning; i++)
                agent.Graph.Tick();
            agent.SetVariableValue("Branch", Branch);
            move = Move;
            run = Run;
        }
        public bool Wait()
        {
            if (Active)
                return false;
            Branch = "Chờ";
            return true;
        }
        public bool Serve()
        {
            if (Match.Phase != MatchPhase.PreServe)
                return false;
            Branch = "Giao / chờ giao";
            if (Match.Rules.Server == Actor.playerId && Match.ServeElapsed > 1.1f && !Actor.Swinging)
                Match.RequestShot(Actor.playerId, new ShotIntent(ShotKind.Serve, Tier == 2, true));
            return true;
        }
        public bool Shot(ShotKind kind)
        {
            if (!Incoming)
                return false;
            bool mandatory = Match.Rules.Stroke <= 2;
            if ((kind == ShotKind.Drop || kind == ShotKind.Dink || kind == ShotKind.Reset) && Tier == 0)
                return false;
            if (kind == ShotKind.Smash && Tier < 2)
                return false;
            bool air = kind == ShotKind.Volley || kind == ShotKind.Reset || kind == ShotKind.Smash;
            if (air && (mandatory || Actor.InKitchen))
                return false;
            if (kind == ShotKind.Reset && Match.BallVelocity.magnitude < 8)
                return false;
            if (kind == ShotKind.Drop && Match.Rules.Stroke != 2)
                return false;
            if (kind == ShotKind.Volley && (Mathf.Abs(Actor.transform.position.z) > 3.7f || Match.BallPosition.y < .6f))
                return false;
            float height = kind == ShotKind.Smash ? 1.9f : .85f;
            bool beforeBounce = kind == ShotKind.Reset || kind == ShotKind.Volley;
            if (!Match.PredictContact(Actor.end, !air, out var point, out float delay, height, beforeBounce))
                return false;
            if (kind == ShotKind.Dink && Mathf.Abs(point.z) >= 3.5f)
                return false;
            var intent = new ShotIntent(kind, Tier == 2 && kind == ShotKind.Drive, false);
            if (Tier == 2)
            {
                var straight = ShotTrajectory.Target(Actor, intent, point, 0);
                var cross = ShotTrajectory.Target(Actor, new ShotIntent(kind, intent.power, true), point, 0);
                intent.cross = Vector3.Distance(cross, Match.human.transform.position) > Vector3.Distance(straight, Match.human.transform.position);
            }
            bool backhand = kind == ShotKind.Drop;
            var offset = Actor.ContactOffset(intent, backhand);
            if (backhand && offset.x > -.18f)
                offset.x = -.22f;
            if (!Match.PredictContact(Actor.end, !air, out point, out delay, offset.y, beforeBounce))
                return false;
            agent.SetVariableValue("ContactPoint", point);
            agent.SetVariableValue("ContactDelay", delay);
            var target = point - Actor.transform.TransformDirection(offset);
            target.y = 0;
            if (air)
                target.z = Actor.end * Mathf.Max(Mathf.Abs(target.z), 2.85f);
            Branch = kind.ToString();
            MoveTo(target, true);
            float arrival = Vector3.Distance(target, Actor.transform.position) / (Actor.runSpeed * (Actor.Swinging ? .6f : 1));
            if (reaction <= 0 && cooldown <= 0 && !Actor.Swinging && delay <= intent.ContactDelay + .025f && arrival < intent.ContactDelay + .10f)
                if (Match.RequestShot(Actor.playerId, intent))
                    cooldown = intent.Duration;
            return true;
        }
        public bool Recover()
        {
            Branch = "Hồi vị";
            MoveTo(new Vector3(0, 0, Actor.end * (Match.Rules.Stroke <= 2 && Match.Rules.Server == Actor.playerId ? 6.4f : 3.05f)), false);
            return true;
        }
        void MoveTo(Vector3 target, bool incoming)
        {
            var d = target - Actor.transform.position;
            d.y = 0;
            if (d.magnitude <= .08f || reaction > 0)
                return;
            Run = incoming && d.magnitude > .55f;
            d = Vector3.ClampMagnitude(d * 3, 1);
            Move = new Vector2(d.x, d.z);
        }
        public void ResetPoint()
        {
            reaction = cooldown = 0;
            stroke = -1;
            Move = Vector2.zero;
            Run = false;
            agent?.Graph?.End();
        }
        void OnDestroy()
        {
            agent?.Graph?.End();
        }
    }
}
