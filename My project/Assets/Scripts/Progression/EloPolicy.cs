using System;
using System.Collections.Generic;

namespace PickleVerse.Progression
{
    public static class EloPolicy
    {
        public const int Minimum = 100, Window = 120;
        public static int Delta(int own, int opponent, bool won, int priorWins, int priorLosses)
        {
            double magnitude = opponent > own ? (opponent - (double)own) / 2 : opponent == own ? 20 : won ? (own - (double)opponent) / 4 : (own - (double)opponent) * 4;
            if (won)
            {
                magnitude *= 1 + Math.Min(.5, .1 * Math.Max(0, priorWins + 1 - 2));
                magnitude *= Math.Max(.5, 1 - .05 * priorLosses);
            }
            else magnitude *= 1 + Math.Min(.5, .1 * Math.Max(0, priorLosses));
            long rounded = Math.Max(1, (long)Math.Round(magnitude, MidpointRounding.AwayFromZero));
            long next = won ? Math.Min(int.MaxValue, own + rounded) : Math.Max(Minimum, own - rounded);
            return (int)(next - own);
        }
        public static string Rank(int elo) => elo < 500 ? "Đồng" : elo < 1000 ? "Bạc" : elo < 2000 ? "Vàng" : elo < 3500 ? "Bạch Kim" : elo < 5000 ? "Kim Cương" : elo < 7000 ? "Cao Thủ" : "Huyền Thoại";
        public static int BotElo(int own, int losses) => losses >= 5 ? own - 100 : (int)Math.Min(int.MaxValue, (long)own + 120);
        public static int BotAccuracy(int elo) => Math.Min(100, 25 + Math.Max(0, elo - 100) / 120);
        public static bool BotDecision(Random random,int elo) => random.NextDouble()<BotAccuracy(elo)/100d;
        public static List<string> PairingKeys(int elo, int losses, bool expanded)
        {
            var keys = new List<string>();
            long first = Math.Max(Minimum, (long)elo - (expanded || losses >= 5 ? Window : 0));
            long last = Math.Min(int.MaxValue, (long)elo + (expanded || losses < 5 ? Window : 0));
            for (long candidate = first; candidate <= last; candidate++) keys.Add(Math.Min(elo, candidate) + ":" + Math.Max(elo, candidate));
            return keys;
        }
        // Unity permits at most 100 items in a CustomData array. Every canonical pair belongs
        // to one of three shared UTC phases; the full ±120 window is covered every 30 seconds.
        public static int TicketPhase(DateTime utc) => (int)(utc.Ticks/TimeSpan.TicksPerSecond/10%3);
        public static double TicketPhaseRemaining(DateTime utc) => 10-(utc.Ticks/(double)TimeSpan.TicksPerSecond%10);
        public static List<string> TicketKeys(int elo,int losses,bool expanded,int phase)
        {
            var selected=new List<string>();
            foreach(var key in PairingKeys(elo,losses,expanded))
            {
                var pair=key.Split(':');
                if((long.Parse(pair[0])+long.Parse(pair[1]))%3==phase) selected.Add(key);
            }
            return selected;
        }
    }
    public enum SearchDecision { Pending, Human, Bot, Cancelled }
    // All claim paths share this gate. Once claimed, cancellation or a late response cannot start a second match.
    public sealed class RankedSearchGate
    {
        public const double PrioritySeconds = 30, DeadlineSeconds = 360;
        double began;
        public SearchDecision Decision { get; private set; } = SearchDecision.Cancelled;
        public void Start(double now) { began = now; Decision = SearchDecision.Pending; }
        public double Elapsed(double now) => Math.Max(0, now - began);
        public bool Expanded(double now) => Elapsed(now) >= PrioritySeconds;
        public bool ClaimHuman(double now)
        {
            if (Decision != SearchDecision.Pending || Elapsed(now) >= DeadlineSeconds) return false;
            Decision = SearchDecision.Human; return true;
        }
        public bool ClaimBot(double now)
        {
            if (Decision != SearchDecision.Pending || Elapsed(now) < DeadlineSeconds) return false;
            Decision = SearchDecision.Bot; return true;
        }
        public void Cancel() { if (Decision == SearchDecision.Pending) Decision = SearchDecision.Cancelled; }
        public void RetryHuman() { if (Decision == SearchDecision.Human) Decision = SearchDecision.Pending; }
    }
}
