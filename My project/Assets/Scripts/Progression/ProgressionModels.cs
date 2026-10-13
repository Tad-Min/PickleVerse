using System;
using System.Collections.Generic;

namespace PickleVerse.Progression
{
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
