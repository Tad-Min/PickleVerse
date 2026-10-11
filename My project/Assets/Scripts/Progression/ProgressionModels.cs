using System;
using System.Collections.Generic;

namespace PickleVerse.Progression
{
    public enum ProgressScope { Achievement, Daily, Weekly }
    public enum ProgressMetric { Matches, Wins, Points, Returns, Serves, BotMatches, HumanMatches, HumanWins, RankedWins, DoublesWins, BestStreak, HigherRatedWins }
    public enum MatchKind { BotPractice, RankedSingles, RankedBot, MatchedDoubles, Custom }
    public enum MatchOutcome { Won, Lost, Interrupted }

    [Serializable] public struct RewardBundle
    {
        public int coins, gems, xp;
        public RewardBundle(int coins, int gems, int xp) { this.coins = coins; this.gems = gems; this.xp = xp; }
        public override string ToString() => $"{coins} TOKENPAD  ·  {xp} XP";
    }
    [Serializable] public sealed class ProgressDefinition
    {
        public string id, title;
        public ProgressScope scope;
        public ProgressMetric metric;
        public int target;
        public RewardBundle reward;
        public ProgressDefinition(string id, string title, ProgressScope scope, ProgressMetric metric, int target, RewardBundle reward)
        { this.id = id; this.title = title; this.scope = scope; this.metric = metric; this.target = target; this.reward = reward; }
    }
    [Serializable] public sealed class ProgressCounters
    {
        public long matches, wins, points, returns, serves, botMatches, humanMatches, humanWins, rankedWins, doublesWins, higherRatedWins;
        public int streak, bestStreak;
        public long Get(ProgressMetric metric)
        {
            switch (metric)
            {
                case ProgressMetric.Matches: return matches;
                case ProgressMetric.Wins: return wins;
                case ProgressMetric.Points: return points;
                case ProgressMetric.Returns: return returns;
                case ProgressMetric.Serves: return serves;
                case ProgressMetric.BotMatches: return botMatches;
                case ProgressMetric.HumanMatches: return humanMatches;
                case ProgressMetric.HumanWins: return humanWins;
                case ProgressMetric.RankedWins: return rankedWins;
                case ProgressMetric.DoublesWins: return doublesWins;
                case ProgressMetric.HigherRatedWins: return higherRatedWins;
                default: return bestStreak;
            }
        }
    }
    [Serializable] public sealed class MissionPeriod
    {
        public string key = "";
        public ProgressCounters counters = new ProgressCounters();
        public List<string> claimed = new List<string>();
        public bool bonusClaimed;
    }
    [Serializable] public sealed class OpponentInfo
    {
        public string id, name;
        public int elo;
    }
    [Serializable] public sealed class GameScore
    {
        public int own, opponent;
        public bool completed;
        public GameScore() { }
        public GameScore(int own, int opponent, bool completed) { this.own = own; this.opponent = opponent; this.completed = completed; }
    }
    [Serializable] public sealed class MatchResult
    {
        public string id, playerId, reason = "";
        public long startedUtcTicks, endedUtcTicks;
        public MatchKind kind;
        public MatchOutcome outcome;
        public bool localForfeit;
        public int ownElo = 100, opponentElo = 100, eloDelta, gamesWon, gamesLost, points, validReturns, validServes;
        public List<OpponentInfo> opponents = new List<OpponentInfo>();
        public List<GameScore> scores = new List<GameScore>();
        public bool Ranked => kind == MatchKind.RankedSingles || kind == MatchKind.RankedBot;
        public bool Bot => kind == MatchKind.BotPractice || kind == MatchKind.RankedBot;
        public bool Eligible => kind != MatchKind.Custom;
        public string KindLabel => kind == MatchKind.BotPractice ? "Đấu bot" : kind == MatchKind.RankedBot ? "Rank · Bot" : kind == MatchKind.RankedSingles ? "Rank · 1v1" : kind == MatchKind.MatchedDoubles ? "Tìm trận · 2v2" : "Phòng riêng";
        public string OutcomeLabel => outcome == MatchOutcome.Interrupted ? "Gián đoạn" : localForfeit ? "Thua · Bỏ trận" : outcome == MatchOutcome.Won ? "Thắng" : "Thua";
    }
    [Serializable] public sealed class PlayerProfile
    {
        public int version = 1;
        public string id, playerName, avatarId = "initial";
        public long createdUtcTicks, totalXp, coins, gems, wins, losses;
        // Keep the legacy JSON balance names; coins = TOKENPAD, gems = paid ELITE TOKEN.
        public int currencyVersion;
        public string shopData = "";
        public int elo = 100, level = 1, levelXp, rankWinStreak, rankLossStreak;
        public double activeSeconds;
        public ProgressCounters lifetime = new ProgressCounters();
        public MissionPeriod daily = new MissionPeriod(), weekly = new MissionPeriod();
        public List<MissionPeriod> previousDays = new List<MissionPeriod>(), previousWeeks = new List<MissionPeriod>();
        public List<string> achievementClaims = new List<string>(), processedMatches = new List<string>();
        public List<MatchResult> history = new List<MatchResult>();
        public MatchResult pendingMatch;
        public long NextLevelXp => (long)level * 10;
        public double WinRate => wins + losses == 0 ? 0 : 100d * wins / (wins + losses);
    }
    public static class CurrencyPolicy
    {
        public static bool Migrate(PlayerProfile player)
        {
            if (player.currencyVersion >= 1) return false;
            // Version 0 had only gameplay gems. No payment or verified top-up existed.
            player.gems = 0;
            player.currencyVersion = 1;
            return true;
        }
    }
    // Captures gameplay facts, once per stroke, only after the rules accept its landing/return.
    public sealed class StrokeProgressTracker
    {
        int hitter = -1, stroke, acceptedStroke = -1;
        bool serve;
        readonly Dictionary<int, int> returns = new Dictionary<int, int>(), serves = new Dictionary<int, int>();
        public void Launch(int player, bool isServe, int sequence)
        { hitter = player; serve = isServe; stroke = sequence; }
        public void AcceptFlight()
        {
            if (hitter < 0 || acceptedStroke == stroke) return;
            acceptedStroke = stroke;
            var counts = serve ? serves : returns;
            counts[hitter] = counts.TryGetValue(hitter, out var value) ? value + 1 : 1;
        }
        public void NewRally() { hitter = -1; acceptedStroke = -1; }
        public int Returns(int player) => returns.TryGetValue(player, out var count) ? count : 0;
        public int Serves(int player) => serves.TryGetValue(player, out var count) ? count : 0;
    }
}
