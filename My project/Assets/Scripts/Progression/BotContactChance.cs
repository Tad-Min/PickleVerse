using System;

namespace PickleVerse.Progression
{
    // One contact opportunity per incoming stroke, even across simulation ticks or swings.
    // Reset between rallies because their stroke numbers start over.
    public sealed class BotContactChance
    {
        bool decided, hit;
        int stroke;

        public bool HasDecision(int incomingStroke) => decided && stroke == incomingStroke;
        public bool Missed(int incomingStroke) => HasDecision(incomingStroke) && !hit;
        public bool Resolve(Random random, int elo, int incomingStroke)
        {
            if (!HasDecision(incomingStroke))
            {
                hit = EloPolicy.BotDecision(random, elo);
                stroke = incomingStroke;
                decided = true;
            }
            return hit;
        }
        public void Reset() { decided = false; }
    }
}
