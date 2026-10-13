using System;
using System.Globalization;
using System.Linq;
namespace PickleVerse.Progression
{
    public interface IUtcClock { DateTime UtcNow { get; } }
    public sealed class SystemUtcClock : IUtcClock { public DateTime UtcNow => DateTime.UtcNow; }
    public static class DailyStreakEngine
    {
        public static bool Ensure(PlayerProfile p, DateTime utc)
        {
            bool changed = p.dailyStreak == null || p.dailyStreak.enabledUtcTicks == 0;
            if (changed) p.dailyStreak = new DailyStreakData { enabledUtcTicks = utc.Ticks };
            if (p.dailyStreak.days == null) p.dailyStreak.days = new System.Collections.Generic.List<string>();
            p.version = Math.Max(2, p.version);
            return changed;
        }
        public static void Apply(PlayerProfile p, MatchResult r)
        {
            if (p.dailyStreak == null || r.endedUtcTicks < p.dailyStreak.enabledUtcTicks || r.localForfeit || r.outcome == MatchOutcome.Interrupted) return;
            string day = ProgressionEngine.DayKey(new DateTime(r.endedUtcTicks, DateTimeKind.Utc));
            if (!p.dailyStreak.days.Contains(day)) p.dailyStreak.days.Add(day);
            Recalculate(p.dailyStreak, new DateTime(r.endedUtcTicks, DateTimeKind.Utc));
        }
        public static bool Refresh(PlayerProfile p, DateTime utc)
        {
            if (p.dailyStreak == null) return false;
            int before = p.dailyStreak.current; Recalculate(p.dailyStreak, utc); return before != p.dailyStreak.current;
        }
        static void Recalculate(DailyStreakData d, DateTime utc)
        {
            d.days = d.days.Distinct().OrderBy(x => x).ToList();
            int count = 0, best = 0; DateTime prior = DateTime.MinValue;
            foreach (var day in d.days)
            {
                if (!DateTime.TryParseExact(day,"yyyyMMdd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)) continue;
                count = date == prior.AddDays(1) ? count + 1 : 1;
                best = Math.Max(best,count); prior = date;
            }
            d.best = best; d.lastDay = prior == DateTime.MinValue ? "" : prior.ToString("yyyyMMdd");
            d.current = prior >= utc.Date.AddDays(-1) ? count : 0;
        }
    }
}
