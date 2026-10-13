using System;
using System.Collections.Generic;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    /// <summary>Uses a monotonic clock. Search starts at 6.000 seconds, never at 5.</summary>
    public sealed class SearchClock
    {
        public const double DelaySeconds = 6, SearchSeconds = 120;
        public bool Running { get; private set; }
        double began;
        public void Start(double now, double alreadyElapsed = 0) { began = now - Math.Max(0, alreadyElapsed); Running = true; }
        public void Stop() { Running = false; }
        public double Elapsed(double now) => Running ? Math.Max(0, now - began) : 0;
        public bool CanSubmit(double now) => Running && Elapsed(now) >= DelaySeconds;
        public bool TimedOut(double now) => Running && Elapsed(now) >= DelaySeconds + SearchSeconds;
        public string Display(double now) { var seconds = (int)Elapsed(now); return $"{seconds / 60:00}:{seconds % 60:00}"; }
    }

    public struct ShotIntent
    {
        public ShotKind kind;
        public bool attack, crossCourt;
    }

}
