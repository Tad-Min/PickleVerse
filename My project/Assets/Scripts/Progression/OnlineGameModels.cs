using System;
using System.Collections.Generic;

namespace PickleVerse.Progression
{
    [Serializable]
    public sealed class OnlineCosmeticOffer
    {
        public string id, title, description, collection;
        public int tokenpadPrice, elitePrice, releaseOrder;
        public bool gemstone;
    }
    [Serializable]
    public sealed class OnlineGameCatalog
    {
        public BotPolicy botPolicy = new BotPolicy();
        public bool hybridTelemetryEnabled, hybridCoachingEnabled;
        public string version;
        public RuntimeMatchmakingSettings matchmaking;
        public RuntimeOnlineMatchSettings onlineMatch;
        public int winXp, lossXp;
        public RewardBundle dailyBonus, weeklyBonus;
        public List<ProgressDefinition> definitions;
        public TrainingDrill[] lessons;
        public OnlineCosmeticOffer[] cosmetics;
    }
    [Serializable]
    public sealed class OnlineInventory
    {
        public long tokenpad, elite;
        public List<string> owned;
        public string equippedPaddle;
    }
    [Serializable]
    public sealed class OnlineTutorialState { public bool completed, skipped; }
    [Serializable]
    public sealed class OnlineGameSnapshot
    {
        public long revision;
        public bool isMale;
        public PlayerProfile player;
        public OnlineInventory inventory;
        public OnlineGameCatalog catalog;
        public OnlineTutorialState tutorial;
    }
}
