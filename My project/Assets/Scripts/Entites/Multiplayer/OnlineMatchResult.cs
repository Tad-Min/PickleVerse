using System;
using PickleVerse.Progression;

namespace PickleVerse.Multiplayer
{
    [Serializable]
    public sealed class OnlineMatchResult
    {
        public string onlineId;
        public MatchResult result;
    }

}
