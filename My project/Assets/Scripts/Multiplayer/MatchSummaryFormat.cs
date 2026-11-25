using System.Linq;
using PickleVerse.Progression;

namespace PickleVerse.Multiplayer
{
    /// <summary>Wording and numbers of the online match summary (pure, so the screen and its tests agree).</summary>
    public static class MatchSummaryFormat
    {
        /// <summary>Lowest Elo of each rank, in the order of <see cref="EloPolicy.Rank"/>.</summary>
        public static readonly int[] RankFloors = { EloPolicy.Minimum, 500, 1000, 2000, 3500, 5000, 7000 };

        public static string Title(MatchResult r) => r.outcome == MatchOutcome.Won ? "CHIẾN THẮNG"
            : r.outcome == MatchOutcome.Lost ? "THẤT BẠI" : "TRẬN ĐẤU GIÁN ĐOẠN";

        public static int CompletedGames(MatchResult r) => r.scores?.Count(g => g != null && g.completed) ?? 0;

        /// <summary>Online matches are best of three; the server only accepts a played-out result with two games won.</summary>
        public const int GamesToWin = 2;

        /// <summary>The match was decided before either side won two games (someone left, gave up or lost connection).</summary>
        public static bool EndedEarly(MatchResult r) => r.outcome != MatchOutcome.Interrupted &&
            (r.localForfeit || System.Math.Max(r.gamesWon, r.gamesLost) < GamesToWin);

        /// <summary>One line under the title that explains how the match was decided.</summary>
        public static string Context(MatchResult r)
        {
            if (r.outcome == MatchOutcome.Interrupted)
                return "Trận đã dừng trước khi có kết quả";
            if (r.Surrendered)
                return r.outcome == MatchOutcome.Won ? "Đối thủ đã đầu hàng · bạn được xử thắng"
                    : (r.opponents != null && r.opponents.Count > 1 ? "Đội bạn" : "Bạn") + " đã đầu hàng · trận được xử thua";
            if (r.localForfeit)
                return "Bạn đã rời trận · trận được xử thua";
            if (EndedEarly(r))
                return r.outcome == MatchOutcome.Won
                    ? "Đối thủ rời trận hoặc mất kết nối · bạn được xử thắng"
                    : "Trận kết thúc sớm do rời trận hoặc mất kết nối";
            int games = CompletedGames(r);
            return (r.outcome == MatchOutcome.Won ? "Thắng " : "Thua ") + r.gamesWon + "–" + r.gamesLost + " sau " + games + " ván";
        }

        public static string Signed(int value) => value > 0 ? "+" + value : value.ToString();

        public static string EloLine(MatchResult r, int currentElo) => r.Ranked
            ? $"{r.ownElo:N0} → {r.ownElo + r.eloDelta:N0} Elo"
            : r.kind == MatchKind.Custom ? "Phòng riêng không tính Elo" : $"{currentElo:N0} Elo · chế độ này không đổi Elo";

        public static int RankTier(int elo)
        {
            int tier = 0;
            for (int i = 1; i < RankFloors.Length; i++)
                if (elo >= RankFloors[i])
                    tier = i;
            return tier;
        }

        /// <summary>Elo needed for the next rank, or null at the top rank.</summary>
        public static int? NextRankElo(int elo)
        {
            int tier = RankTier(elo);
            return tier + 1 < RankFloors.Length ? RankFloors[tier + 1] : (int?)null;
        }

        /// <summary>How far through the current rank (0 at its floor, 1 at the next rank).</summary>
        public static float RankProgress(int elo)
        {
            int tier = RankTier(elo);
            if (tier + 1 >= RankFloors.Length)
                return 1;
            float low = RankFloors[tier], high = RankFloors[tier + 1];
            return elo <= low ? 0 : elo >= high ? 1 : (elo - low) / (high - low);
        }

        public static string RankNote(int elo)
        {
            var next = NextRankElo(elo);
            return next.HasValue
                ? $"{elo:N0} Elo · còn {next.Value - elo:N0} lên {EloPolicy.Rank(next.Value).ToUpperInvariant()}"
                : $"{elo:N0} Elo · hạng cao nhất";
        }

        /// <summary>+1 promoted, -1 demoted, 0 same rank (ranked matches only).</summary>
        public static int RankShift(MatchResult r) => r.Ranked
            ? System.Math.Sign(RankTier(r.ownElo + r.eloDelta) - RankTier(r.ownElo)) : 0;

        public static string GameChip(GameScore g) => g.own + "–" + g.opponent + (g.completed ? "" : " · dở");

        public static float Ratio(long value, long total) => total <= 0 ? 0 : value <= 0 ? 0 : value >= total ? 1 : (float)value / total;
    }
}
