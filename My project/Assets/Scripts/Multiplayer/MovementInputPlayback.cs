using System;
using Fusion;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    public struct MovementInputFrame : INetworkStruct
    {
        public uint sequence;
        public Vector2 axes;
        public NetworkBool running;
    }

    // Each input represents one network tick, even when movement runs at 120 Hz.
    public sealed class MovementInputPlayback
    {
        public uint Acknowledged { get; private set; }
        float remaining;
        MovementInputFrame active;
        public void Reset(uint sequence) { Acknowledged = sequence; remaining = 0; }
        public bool Take(float budget, float duration, Func<uint, MovementInputFrame?> read,
            out MovementInputFrame input, out float seconds)
        {
            if (remaining <= .000001f)
            {
                var next = read(Acknowledged + 1);
                if (!next.HasValue) { input = default; seconds = budget; return false; }
                active = next.Value; remaining = duration;
            }
            input = active;
            seconds = Mathf.Min(budget, remaining);
            remaining -= seconds;
            if (remaining <= .000001f) Acknowledged = active.sequence;
            return true;
        }
    }
}
