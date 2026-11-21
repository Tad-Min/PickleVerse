using System;
using System.Collections.Generic;

namespace PickleVerse.Multiplayer
{
    // Request IDs survive a delayed echo and are unique across device takeovers.
    public sealed class ShotRequestHistory
    {
        const int Capacity = 64;
        readonly HashSet<string> seen = new HashSet<string>();
        readonly Queue<string> order = new Queue<string>();
        public string Current { get; private set; }
        public bool Contains(string id) => id != null && seen.Contains(id);
        public bool Add(string id)
        {
            if (!Guid.TryParseExact(id, "N", out _) || !seen.Add(id)) return false;
            order.Enqueue(id);
            if (order.Count > Capacity) seen.Remove(order.Dequeue());
            Current = id;
            return true;
        }
        public bool RejectCurrent(string id)
        {
            if (id == null || Current != id) return false;
            Current = null;
            return true;
        }
        public void Clear() { seen.Clear(); order.Clear(); Current = null; }
    }
}
