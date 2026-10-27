using UnityEngine;

namespace PickleVerse.LocalMatch
{
    public interface IAdaptiveBotContext
    {
        MatchActor Actor { get; }
        Vector3 BallPosition { get; }
        Vector3 BallVelocity { get; }
        Vector3 OpponentPosition { get; }
        bool Active { get; }
        bool Incoming { get; }
        bool Serving { get; }
        bool IsServer { get; }
        int Stroke { get; }
        int Bounces { get; }
        int Server { get; }
        int ExpectedHitter { get; }
        string Phase { get; }
        float ServeElapsed { get; }
        bool PredictContact(bool bounce, out Vector3 point, out float delay, float height, bool beforeBounce);
        bool RequestShot(ShotIntent shot);
    }
    public sealed class LocalAdaptiveBotContext : IAdaptiveBotContext
    {
        readonly BotMatch match;
        public LocalAdaptiveBotContext(BotMatch match) { this.match = match; }
        public MatchActor Actor => match.bot;
        public Vector3 BallPosition => match.BallPosition;
        public Vector3 BallVelocity => match.BallVelocity;
        public Vector3 OpponentPosition => match.human.transform.position;
        public bool Active => match && !match.Paused && (match.Phase == MatchPhase.PreServe || match.Phase == MatchPhase.Rally);
        public bool Incoming => Active && match.Phase == MatchPhase.Rally && match.Rules.Live && match.Rules.ExpectedHitter == Actor.playerId;
        public bool Serving => match.Phase == MatchPhase.PreServe;
        public bool IsServer => match.Rules.Server == Actor.playerId;
        public int Stroke => match.Rules.Stroke;
        public int Bounces => match.Rules.Bounces;
        public int Server => match.Rules.Server;
        public int ExpectedHitter => match.Rules.ExpectedHitter;
        public string Phase => match.Phase.ToString();
        public float ServeElapsed => match.ServeElapsed;
        public bool PredictContact(bool bounce, out Vector3 point, out float delay, float height, bool beforeBounce)
            => match.PredictContact(Actor.end, bounce, out point, out delay, height, beforeBounce);
        public bool RequestShot(ShotIntent shot) => match.RequestShot(Actor.playerId, shot);
    }
}
