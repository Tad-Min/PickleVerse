using PickleVerse.Replays;
using PickleVerse.Shop;
using UnityEngine;
namespace PickleVerse.LocalMatch
{
    public sealed partial class BotMatch
    {
        ReplayRecorder replay;
        void BeginReplay()
        {
            if (replay)
            {
                replay.Finish(false);
                Destroy(replay);
            }
            replay = gameObject.AddComponent<ReplayRecorder>();
            var opponent = progressionResult.opponents.Count > 0 ? progressionResult.opponents[0] : null;
            replay.Begin(new ReplayMetadata { playerId = progressionResult.playerId, matchId = progressionResult.id, startedUtcTicks = progressionResult.startedUtcTicks, night = BotMatchLaunchSettings.Night },
                new[] { ReplayActor(human, progressionResult.playerId, PickleVerse.Progression.ProgressionRuntime.Instance.Player.playerName), ReplayActor(bot, opponent?.id ?? "BOT", opponent?.name ?? "Bot") }, () => BallPosition,
                () => new ReplayFrame { scoreA = Rules.Points[0], scoreB = Rules.Points[1], gamesA = Rules.Games[0], gamesB = Rules.Games[1], status = Message }, visualBall: ball);
        }
        internal static ReplayActorSource ReplayActor(MatchActor a, string id, string name)
            => new ReplayActorSource { root = a.transform, paddle = a.paddle ? a.paddle.transform : null, id = id, name = name, team = a.playerId, visualId = a.IsFemale ? "Female" : "Male", paddleId = a.paddle ? a.paddle.GetComponent<PaddleAppearance>()?.CurrentId : EconomyIds.DefaultPaddleId };
    }
}
