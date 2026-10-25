using System.Collections.Generic;
using System.Linq;

namespace PickleVerse.Multiplayer
{
    public enum ReadyDecision { Pending, Accepted, Declined }
    public enum ReadyOutcome { Waiting, Approved, Rejected }

    // A missing human is never treated as a bot or an implicit acceptance.
    public static class MatchReadyPolicy
    {
        public const int Seconds = 20;
        public static ReadyOutcome Evaluate(MatchRoster roster, IReadOnlyDictionary<string, ReadyDecision> decisions,
            bool expired, bool allConnected)
        {
            if (roster == null || !roster.IsValid())
                return ReadyOutcome.Rejected;
            var humans = roster.members.Where(p => !p.bot).ToArray();
            if (humans.Any(p => decisions.TryGetValue(p.id, out var d) && d == ReadyDecision.Declined))
                return ReadyOutcome.Rejected;
            if (expired)
                return ReadyOutcome.Rejected;
            return allConnected && humans.All(p => decisions.TryGetValue(p.id, out var d) && d == ReadyDecision.Accepted)
                ? ReadyOutcome.Approved : ReadyOutcome.Waiting;
        }
        public static bool ResumeSearch(ReadyDecision localDecision) => localDecision == ReadyDecision.Accepted;
    }
}
