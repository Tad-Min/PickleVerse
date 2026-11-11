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
        public IAdaptiveBotContext Context { get; private set; }
        public MatchActor Actor => Context.Actor;
        BotTierProfile profile;
        System.Random random;
        bool attack;
        public int Elo { get; private set; }
        public int Tier => Elo < 500 ? 0 : Elo < 2000 ? 1 : 2;
        public float ReactionSeconds => profile.reactionSeconds;
        public float AimError => profile.aimError;
        public string Branch { get; private set; } = "Chờ";
        public Vector2 Move { get; private set; }
        public bool Run { get; private set; }
        public bool Incoming => Context.Incoming;
        public bool Active => Context != null && Context.Active;
        float reaction, cooldown;
        int stroke = -1;
        float simulationTime;
        public void Initialize(BotMatch match, int elo)
        {
            Match = match;
            Initialize(new LocalAdaptiveBotContext(match), elo, ProgressionRuntime.Instance?.Snapshot?.catalog?.botPolicy ?? new BotPolicy(), 731);
        }
        public void Initialize(IAdaptiveBotContext context, int elo, BotPolicy policy, int seed)
        {
            Context = context;
            Elo = elo;
            profile = policy.Profile(elo);
            random = new System.Random(seed);
            reaction = cooldown = simulationTime = 0;
            stroke = -1;
#if UNITY_SERVER
            // The Linux Dedicated Player loses child nodes when Unity Behavior clones the asset.
            agent = null;
#else
            if (!graph)
                graph = Array.Find(Resources.LoadAll<BehaviorGraph>("BhTree/Bot Behavior"), g => g != null);
            if (!graph)
                throw new InvalidOperationException("Thiếu graph BhTree. Chạy PickleVerse/Features/Setup.");
            agent = GetComponent<BehaviorGraphAgent>() ?? gameObject.AddComponent<BehaviorGraphAgent>();
            agent.enabled = false;
            agent.Graph = graph;
            agent.Init();
#endif
        }
        public void Step(float dt, out Vector2 move, out bool run)
        {
            Move = Vector2.zero;
            Run = false;
            predictions.Clear();
            cooldown = Mathf.Max(0, cooldown - dt);
            simulationTime += dt;
            if (stroke != Context.Stroke)
            {
                stroke = Context.Stroke;
                reaction = ReactionSeconds;
                attack = random.NextDouble() < profile.aggressiveness;
            }
            reaction -= dt;
            if (agent)
                StepGraph();
            else
                StepHeadless();
            move = Move;
            run = Run;
        }
        void StepHeadless()
        {
            // Match the priority in the Bot Behavior selector, using the same actions.
            if (Wait() || Serve() || Shot(ShotKind.Smash) || Shot(ShotKind.Reset) ||
                Shot(ShotKind.Drop) || Shot(ShotKind.Dink) || Shot(ShotKind.Volley) || Shot(ShotKind.Drive))
                return;
            Recover();
        }
        void StepGraph()
        {
            agent.SetVariableValue("BallPosition", Context.BallPosition);
            agent.SetVariableValue("BallVelocity", Context.BallVelocity);
            agent.SetVariableValue("BotPosition", Actor.transform.position);
            agent.SetVariableValue("HumanPosition", Context.OpponentPosition);
            agent.SetVariableValue("Elo", Elo);
            agent.SetVariableValue("Stroke", stroke);
            agent.SetVariableValue("Bounces", Context.Bounces);
            agent.SetVariableValue("Phase", Context.Phase);
            agent.SetVariableValue("Swinging", Actor.Swinging);
            agent.SetVariableValue("InKitchen", Actor.InKitchen);
            agent.SetVariableValue("Server", Context.Server);
            agent.SetVariableValue("ExpectedHitter", Context.ExpectedHitter);
            agent.SetVariableValue("CanAct", Active);
            agent.SetVariableValue("Cooldown", cooldown);
            agent.SetVariableValue("SimulationTime", simulationTime);
            // Nodes are instantaneous decisions. Drain the ordered selectors within this simulation tick.
            agent.Graph.Restart();
            for (int i = 0; i < 24 && agent.Graph.IsRunning; i++)
                agent.Graph.Tick();
            agent.SetVariableValue("Branch", Branch);
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
            if (!Context.Serving)
                return false;
            Branch = "Giao / chờ giao";
            if (Context.IsServer && Context.ServeElapsed > 1.1f && !Actor.Swinging)
                Context.RequestShot(new ShotIntent(ShotKind.Serve, Tier == 2, false));
            return true;
        }
        public bool Shot(ShotKind kind)
        {
            if (!Incoming)
                return false;
            bool mandatory = Context.Stroke <= 2;
            if ((kind == ShotKind.Drop || kind == ShotKind.Dink || kind == ShotKind.Reset) && Tier == 0)
                return false;
            if (kind == ShotKind.Smash && (Tier < 2 || !attack))
                return false;
            bool air = kind == ShotKind.Volley || kind == ShotKind.Reset || kind == ShotKind.Smash;
            if (air && (mandatory || Actor.InKitchen))
                return false;
            if (kind == ShotKind.Reset && Context.BallVelocity.magnitude < 8)
                return false;
            if (kind == ShotKind.Drop && Context.Stroke != 2)
                return false;
            if (kind == ShotKind.Volley && (Mathf.Abs(Actor.transform.position.z) > 3.7f || Context.BallPosition.y < .6f))
                return false;
            float height = kind == ShotKind.Smash ? 1.9f : .85f;
            bool beforeBounce = kind == ShotKind.Reset || kind == ShotKind.Volley;
            if (!PredictContact(!air, out var point, out float delay, height, beforeBounce))
                return false;
            if (kind == ShotKind.Dink && Mathf.Abs(point.z) >= 3.5f)
                return false;
            var intent = new ShotIntent(kind, attack && kind == ShotKind.Drive, false);
            if (Tier == 2 && !intent.power)
            {
                var straight = ShotTrajectory.Target(Actor, intent, point, 0);
                var cross = ShotTrajectory.Target(Actor, new ShotIntent(kind, intent.power, true), point, 0);
                intent.cross = Vector3.Distance(cross, Context.OpponentPosition) > Vector3.Distance(straight, Context.OpponentPosition);
            }
            bool backhand = kind == ShotKind.Drop;
            var offset = Actor.ContactOffset(intent, backhand);
            if (backhand && offset.x > -.18f)
                offset.x = -.22f;
            if (!PredictContact(!air, out point, out delay, offset.y, beforeBounce))
                return false;
            if (agent)
            {
                agent.SetVariableValue("ContactPoint", point);
                agent.SetVariableValue("ContactDelay", delay);
            }
            var target = point - Actor.transform.TransformDirection(offset);
            target.y = 0;
            if (air)
                target.z = Actor.end * Mathf.Max(Mathf.Abs(target.z), 2.85f);
            Branch = kind.ToString();
            MoveTo(target, true);
            float arrival = Vector3.Distance(target, Actor.transform.position) / (Actor.runSpeed * (Actor.Swinging ? .6f : 1));
            if (reaction <= 0 && cooldown <= 0 && !Actor.Swinging && delay <= intent.ContactDelay + .025f && arrival < intent.ContactDelay + .10f)
                if (Context.RequestShot(intent))
                    cooldown = intent.Duration;
            return true;
        }
        public bool Recover()
        {
            Branch = "Hồi vị";
            MoveTo(new Vector3(0, 0, Actor.end * (Context.Stroke <= 2 && Context.Server == Actor.playerId ? 6.4f : 3.05f)), false);
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
        readonly System.Collections.Generic.Dictionary<string, Prediction> predictions = new System.Collections.Generic.Dictionary<string, Prediction>();
        struct Prediction { public bool valid; public Vector3 point; public float delay; }
        bool PredictContact(bool bounce, out Vector3 point, out float delay, float height, bool beforeBounce)
        {
            string key = (bounce ? "b" : "a") + (beforeBounce ? "v" : "n") + height.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            if (!predictions.TryGetValue(key, out var prediction))
            {
                prediction.valid = Context.PredictContact(bounce, out prediction.point, out prediction.delay, height, beforeBounce);
                predictions[key] = prediction;
            }
            point = prediction.point; delay = prediction.delay;
            return prediction.valid;
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
