using System;
using System.Collections.Generic;
using System.Linq;

namespace PickleVerse.Shop
{
    public static class EconomyIds { public const string DefaultPaddleId = "paddle.default"; }
    [Serializable]
    public sealed class PurchaseRecord { public string requestId, itemId; public ShopCurrency currency; }
    [Serializable]
    public sealed class PlayerEconomyState
    {
        public int version = 1;
        public long tokenpad, elite;
        public string equippedPaddle = EconomyIds.DefaultPaddleId;
        public List<string> owned = new List<string>(), rewardedMatches = new List<string>();
        public List<PurchaseRecord> purchases = new List<PurchaseRecord>();
        public long Balance(ShopCurrency currency) => currency == ShopCurrency.Tokenpad ? tokenpad : elite;
        public bool Owns(string itemId) => itemId == EconomyIds.DefaultPaddleId || owned.Contains(itemId);
    }
    public enum PurchaseStatus { Success, AlreadyOwned, InsufficientFunds, NotAllowed, InvalidItem, InvalidRequest }
    public enum EquipStatus { Success, AlreadyEquipped, NotOwned, InvalidItem }
    public sealed class PurchaseResult
    {
        public PurchaseStatus status; public PlayerEconomyState wallet;
        public bool Success => status == PurchaseStatus.Success;
    }

}
