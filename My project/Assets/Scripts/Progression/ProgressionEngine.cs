using System;
using System.Linq;

namespace PickleVerse.Progression
{
    public sealed class ProgressionEngine
    {
        public ProgressionConfig Config { get; }
        public ProgressionEngine(ProgressionConfig config)
        {
            Config = config;
        }
        public static string DayKey(DateTime utc) => utc.Date.ToString("yyyyMMdd");
        public static DateTime WeekStart(DateTime utc) => utc.Date.AddDays(-((int)utc.DayOfWeek + 6) % 7);
        public static string WeekKey(DateTime utc) => WeekStart(utc).ToString("yyyyMMdd");
        public static DateTime NextReset(ProgressScope scope, DateTime utc) => scope == ProgressScope.Daily ? utc.Date.AddDays(1) : WeekStart(utc).AddDays(7);
        public bool RefreshPeriods(PlayerProfile p, DateTime utc)
        {
            bool changed = RefreshPeriod(p, ProgressScope.Daily, DayKey(utc));
            return RefreshPeriod(p, ProgressScope.Weekly, WeekKey(utc)) || changed;
        }
        bool RefreshPeriod(PlayerProfile p, ProgressScope scope, string key)
        {
            var old = Period(p, scope);
            if (old.key == key || string.CompareOrdinal(key, old.key) < 0)
                return false;
            if (old.key.Length > 0)
            {
                foreach (var def in Config.definitions.Where(d => d.scope == scope))
                    ClaimDefinition(p, def);
                ClaimBonus(p, scope);
                var archive = scope == ProgressScope.Daily ? p.previousDays : p.previousWeeks;
                if (!archive.Any(period => period.key == old.key))
                    archive.Add(old);
            }
            var next = new MissionPeriod { key = key };
            if (scope == ProgressScope.Daily)
                p.daily = next;
            else p.weekly = next;
            return true;
        }
        public long Progress(PlayerProfile p, ProgressDefinition d) => Counters(p, d.scope).Get(d.metric);
        public bool Claimed(PlayerProfile p, ProgressDefinition d) => d.scope == ProgressScope.Achievement ? p.achievementClaims.Contains(d.id) : Period(p, d.scope).claimed.Contains(d.id);
        public int Completed(PlayerProfile p, ProgressScope scope) => Config.definitions.Count(d => d.scope == scope && Progress(p, d) >= d.target);
        public int Count(ProgressScope scope) => Config.definitions.Count(d => d.scope == scope);
        public bool Claim(PlayerProfile p, string id, DateTime utc)
        {
            RefreshPeriods(p, utc);
            if (id == "DailyBonus")
                return ClaimBonus(p, ProgressScope.Daily);
            if (id == "WeeklyBonus")
                return ClaimBonus(p, ProgressScope.Weekly);
            var definition = Config.definitions.Find(d => d.id == id);
            return definition != null && ClaimDefinition(p, definition);
        }
        bool ClaimDefinition(PlayerProfile p, ProgressDefinition d)
        {
            if (Claimed(p, d) || Progress(p, d) < d.target)
                return false;
            Grant(p, d.reward);
            if (d.scope == ProgressScope.Achievement)
                p.achievementClaims.Add(d.id);
            else Period(p, d.scope).claimed.Add(d.id);
            return true;
        }
        public bool ClaimBonus(PlayerProfile p, ProgressScope scope)
        {
            if (scope == ProgressScope.Achievement)
                return false;
            var period = Period(p, scope);
            if (period.bonusClaimed || Count(scope) == 0 || Completed(p, scope) != Count(scope))
                return false;
            Grant(p, scope == ProgressScope.Daily ? Config.dailyBonus : Config.weeklyBonus);
            period.bonusClaimed = true;
            return true;
        }
        public static void Grant(PlayerProfile p, RewardBundle reward)
        {
            p.coins += Math.Max(0, reward.coins);
            // ELITE TOKEN is reserved for verified top-ups, including legacy reward configurations.
            long xp = Math.Max(0, reward.xp);
            p.totalXp += xp;
            long available = p.levelXp + xp;
            while (available >= p.NextLevelXp && p.level < int.MaxValue)
            {
                available -= p.NextLevelXp;
                p.level++;
            }
            p.levelXp = (int)Math.Min(int.MaxValue, available);
        }
        public bool Apply(PlayerProfile p, MatchResult result)
        {
            if (result == null || string.IsNullOrEmpty(result.id) || result.playerId != p.id || p.processedMatches.Contains(result.id))
                return false;
            RefreshPeriods(p, new DateTime(result.endedUtcTicks, DateTimeKind.Utc));
            p.processedMatches.Add(result.id);
            DailyStreakEngine.Apply(p, result);
            if (p.pendingMatch?.id == result.id)
                p.pendingMatch = null;
            bool won = result.outcome == MatchOutcome.Won;
            if (result.outcome != MatchOutcome.Interrupted && result.Eligible)
            {
                if (won)
                    p.wins++;
                else p.losses++;
                p.lifetime.streak = won ? p.lifetime.streak + 1 : 0;
                p.lifetime.bestStreak = Math.Max(p.lifetime.bestStreak, p.lifetime.streak);
                if (result.Ranked)
                {
                    result.eloDelta = EloPolicy.VersionedDelta(result.ratingPolicyVersion, result.ownElo, result.opponentElo, won, p.rankWinStreak, p.rankLossStreak);
                    p.elo = result.ownElo + result.eloDelta;
                    p.rankWinStreak = won ? p.rankWinStreak + 1 : 0;
                    p.rankLossStreak = won ? 0 : p.rankLossStreak + 1;
                }
                if (!result.localForfeit)
                {
                    UpdateCounters(p.lifetime, result, false);
                    Grant(p, new RewardBundle(0, 0, won ? Config.winXp : Config.lossXp));
                }
                UpdateMissionCounters(p, result, ProgressScope.Daily);
                UpdateMissionCounters(p, result, ProgressScope.Weekly);
            }
            p.history.Insert(0, result);
            p.history.Sort((a, b) => b.endedUtcTicks.CompareTo(a.endedUtcTicks));
            if (p.history.Count > 20)
                p.history.RemoveRange(20, p.history.Count - 20);
            return true;
        }
        void UpdateMissionCounters(PlayerProfile p, MatchResult r, ProgressScope scope)
        {
            var utc = new DateTime(r.endedUtcTicks, DateTimeKind.Utc);
            string key = scope == ProgressScope.Daily ? DayKey(utc) : WeekKey(utc);
            var current = Period(p, scope);
            if (current.key == key)
            {
                if (r.localForfeit)
                    current.counters.streak = 0;
                else UpdateCounters(current.counters, r, true);
                return;
            }
            var archive = scope == ProgressScope.Daily ? p.previousDays : p.previousWeeks;
            var endedPeriod = archive.Find(period => period.key == key);
            if (endedPeriod == null)
            {
                endedPeriod = new MissionPeriod { key = key };
                archive.Add(endedPeriod);
            }
            if (r.localForfeit)
            {
                endedPeriod.counters.streak = 0;
                return;
            }
            UpdateCounters(endedPeriod.counters, r, true);
            // A confirmed result can arrive after reset during reconnect. Award the expired period atomically.
            if (scope == ProgressScope.Daily)
                p.daily = endedPeriod;
            else p.weekly = endedPeriod;
            foreach (var d in Config.definitions.Where(d => d.scope == scope))
                ClaimDefinition(p, d);
            ClaimBonus(p, scope);
            if (scope == ProgressScope.Daily)
                p.daily = current;
            else p.weekly = current;
        }
        static void UpdateCounters(ProgressCounters c, MatchResult r, bool trackStreak)
        {
            bool won = r.outcome == MatchOutcome.Won;
            c.matches++;
            if (won)
                c.wins++;
            c.points += Math.Max(0, r.points);
            c.returns += Math.Max(0, r.validReturns);
            c.serves += Math.Max(0, r.validServes);
            if (r.Bot)
                c.botMatches++;
            else
            {
                c.humanMatches++;
                if (won)
                    c.humanWins++;
            }
            if (won && r.Ranked)
            {
                c.rankedWins++;
                if (r.opponentElo > r.ownElo)
                    c.higherRatedWins++;
            }
            if (won && r.kind == MatchKind.MatchedDoubles)
                c.doublesWins++;
            if (trackStreak)
            {
                c.streak = won ? c.streak + 1 : 0;
                c.bestStreak = Math.Max(c.bestStreak, c.streak);
            }
        }
        public static MissionPeriod Period(PlayerProfile p, ProgressScope scope) => scope == ProgressScope.Daily ? p.daily : p.weekly;
        public static ProgressCounters Counters(PlayerProfile p, ProgressScope scope) => scope == ProgressScope.Achievement ? p.lifetime : Period(p, scope).counters;
    }
}
