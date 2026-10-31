#nullable disable
using System;
using System.Collections.Generic;
namespace PickleVerse.Progression
{
    [Serializable]
    public sealed class DailyStreakData
    {
        public int version = 1, current, best;
        public long enabledUtcTicks;
        public string lastDay = "";
        public List<string> days = new List<string>();
    }
}
