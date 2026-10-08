using UnityEngine;
using PickleVerse.LocalMatch;
using PickleVerse.Rules;
using LocalIntent = PickleVerse.LocalMatch.ShotIntent;
using LocalKind = PickleVerse.LocalMatch.ShotKind;

namespace PickleVerse.Multiplayer
{
    public interface IGameplayShotService
    {
        Vector3 Target(MatchActor actor, LocalIntent shot, Vector3 ball, float serverX);
        Vector3 Launch(MatchActor actor, LocalIntent shot, Vector3 ball, float serverX);
        bool CanContact(MatchActor actor, Vector3 previous, Vector3 ball, int bounces, float radius);
    }
    /// <summary>BotMatch shot selection, reach checks and ballistic trajectory adapted for two teams.</summary>
    public sealed class BotMatchGameplayService : IGameplayShotService
    {
        public static LocalIntent ToLocal(ShotIntent shot)
        {
            var kind = shot.kind == ShotKind.Serve ? LocalKind.Serve : shot.kind == ShotKind.Smash ? LocalKind.Smash :
                shot.kind == ShotKind.Drop ? LocalKind.Drop : shot.kind == ShotKind.Dink ? LocalKind.Dink :
                shot.kind == ShotKind.ResetVolley ? LocalKind.Reset : shot.kind == ShotKind.Volley || shot.kind == ShotKind.Punch ? LocalKind.Volley : LocalKind.Drive;
            return new LocalIntent(kind, shot.attack || shot.kind == ShotKind.Punch || shot.kind == ShotKind.SpeedUp, shot.crossCourt);
        }
        public static ShotIntent FromLocal(LocalIntent shot) => new ShotIntent
        {
            kind = shot.kind == LocalKind.Serve || shot.kind == LocalKind.Drive ? ShotKind.Groundstroke :
                shot.kind == LocalKind.Dink ? ShotKind.Dink : shot.kind == LocalKind.Volley ? ShotKind.Volley :
                shot.kind == LocalKind.Drop ? ShotKind.Drop : shot.kind == LocalKind.Smash ? ShotKind.Smash : ShotKind.ResetVolley,
            attack = shot.power, crossCourt = shot.cross
        };
        public bool CanContact(MatchActor actor, Vector3 previous, Vector3 ball, int bounces, float radius)
        {
            if (!actor.ContactActive || ball.z * actor.end <= .05f) return false;
            var shot = actor.Intent;
            if (shot.AfterBounce && bounces == 0 || shot.BeforeBounce && bounces != 0) return false;
            if (shot.kind == LocalKind.Smash && ball.y < 1.65f) return false;
            return SegmentDistance(previous, ball, actor.paddleFace.position) <= radius &&
                Vector3.Distance(ball, actor.transform.position + Vector3.up * .9f) <= 1.55f;
        }
        public Vector3 Target(MatchActor actor, LocalIntent shot, Vector3 ball, float serverX) => ShotTrajectory.Target(actor, shot, ball, serverX);
        public Vector3 Launch(MatchActor actor, LocalIntent shot, Vector3 ball, float serverX) => ShotTrajectory.Launch(shot, ball, Target(actor, shot, ball, serverX));
        public static float SegmentDistance(Vector3 a, Vector3 b, Vector3 point)
        { var d = b - a; return Vector3.Distance(point, a + d * Mathf.Clamp01(Vector3.Dot(point - a, d) / Mathf.Max(.000001f, d.sqrMagnitude))); }
    }
}
