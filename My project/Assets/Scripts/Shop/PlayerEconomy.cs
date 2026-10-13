using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace PickleVerse.Shop
{
    public static class EconomyModelExtensions { public static PlayerEconomyState Copy(this PlayerEconomyState state) => JsonUtility.FromJson<PlayerEconomyState>(JsonUtility.ToJson(state)); }
    public interface IEconomyStorage {string Read();void Write(string json);}
    public sealed class LocalEconomyStorage : IEconomyStorage
    {
        public const string Key="PKV.Economy.Demo.v1";
        public string Read()=>PlayerPrefs.GetString(Key,"");
        public void Write(string json){PlayerPrefs.SetString(Key,json);PlayerPrefs.Save();}
    }
    public interface IPlayerEconomy
    {
        event Action<PlayerEconomyState> Changed;
        Task<PlayerEconomyState> ReadAsync();
        Task<PurchaseResult> PurchaseAsync(string itemId,ShopCurrency currency,string requestId);
        Task<EquipStatus> EquipPaddleAsync(string itemId);
        Task<bool> GrantMatchRewardAsync(string matchId,bool online,bool won);
    }
    /// <summary>Local prototype adapter. Replace it with a server-backed adapter without changing UI.</summary>
    public sealed class LocalPlayerEconomy : IPlayerEconomy
    {
        readonly ShopCatalog catalog;readonly IEconomyStorage storage;PlayerEconomyState state;
        public event Action<PlayerEconomyState> Changed;
        public LocalPlayerEconomy(ShopCatalog catalog,IEconomyStorage storage)
        {
            this.catalog=catalog;this.storage=storage;
            string saved=storage.Read();
            try{state=JsonUtility.FromJson<PlayerEconomyState>(saved);}catch{state=null;}
            if(state==null){state=new PlayerEconomyState{tokenpad=catalog.initialTokenpad,elite=0};Save();}
            state.tokenpad=Math.Max(0,state.tokenpad);state.elite=Math.Max(0,state.elite);
            state.owned=state.owned??new List<string>();state.rewardedMatches=state.rewardedMatches??new List<string>();state.purchases=state.purchases??new List<PurchaseRecord>();
            // Old wallets retain every balance and receipt; only an invalid selection falls back.
            if(string.IsNullOrEmpty(state.equippedPaddle)||!state.Owns(state.equippedPaddle)||
                (state.equippedPaddle!=ShopCatalog.DefaultPaddleId&&catalog.Find(state.equippedPaddle)==null))
                state.equippedPaddle=ShopCatalog.DefaultPaddleId;
        }
        public Task<PlayerEconomyState> ReadAsync()=>Task.FromResult(state.Copy());
        public Task<EquipStatus> EquipPaddleAsync(string itemId)
        {
            if(itemId!=ShopCatalog.DefaultPaddleId&&catalog.Find(itemId)==null)return Task.FromResult(EquipStatus.InvalidItem);
            if(!state.Owns(itemId))return Task.FromResult(EquipStatus.NotOwned);
            if(state.equippedPaddle==itemId)return Task.FromResult(EquipStatus.AlreadyEquipped);
            var next=state.Copy();next.equippedPaddle=itemId;Commit(next);return Task.FromResult(EquipStatus.Success);
        }
        public Task<PurchaseResult> PurchaseAsync(string itemId,ShopCurrency currency,string requestId)
        {
            var offer=catalog.Find(itemId);var previous=state.purchases.Find(p=>p.requestId==requestId);
            PurchaseStatus status;
            if(string.IsNullOrWhiteSpace(requestId)||!Enum.IsDefined(typeof(ShopCurrency),currency))status=PurchaseStatus.InvalidRequest;
            else if(previous!=null)status=previous.itemId==itemId&&previous.currency==currency?PurchaseStatus.Success:PurchaseStatus.InvalidRequest;
            else if(offer==null)status=PurchaseStatus.InvalidItem;
            else if(state.Owns(itemId))status=PurchaseStatus.AlreadyOwned;
            else if(offer.Price(currency)<=0)status=PurchaseStatus.NotAllowed;
            else if(state.Balance(currency)<offer.Price(currency))status=PurchaseStatus.InsufficientFunds;
            else
            {
                var next=state.Copy();if(currency==ShopCurrency.Tokenpad)next.tokenpad-=offer.tokenpadPrice;else next.elite-=offer.elitePrice;
                next.owned.Add(itemId);next.purchases.Add(new PurchaseRecord{requestId=requestId,itemId=itemId,currency=currency});Commit(next);status=PurchaseStatus.Success;
            }
            return Task.FromResult(new PurchaseResult{status=status,wallet=state.Copy()});
        }
        public Task<bool> GrantMatchRewardAsync(string matchId,bool online,bool won)
        {
            if(string.IsNullOrWhiteSpace(matchId)||state.rewardedMatches.Contains(matchId))return Task.FromResult(false);
            int amount=online?(won?catalog.onlineWinReward:catalog.onlineLossReward):(won?catalog.botWinReward:catalog.botLossReward);
            if(amount<=0)return Task.FromResult(false);
            var next=state.Copy();next.tokenpad=checked(next.tokenpad+amount);next.rewardedMatches.Add(matchId);Commit(next);return Task.FromResult(true);
        }
        void Save()=>storage.Write(JsonUtility.ToJson(state));
        void Commit(PlayerEconomyState next)
        {storage.Write(JsonUtility.ToJson(next));state=next;Changed?.Invoke(state.Copy());}
    }
    public static class PlayerEconomy
    {
        static IPlayerEconomy service;static ShopCatalog catalog;
        public static ShopCatalog Catalog=>catalog?catalog:catalog=Resources.Load<ShopCatalog>("Shop/Shop Catalog");
        public static IPlayerEconomy Service=>service??(service=new ProfilePlayerEconomy(Catalog,PickleVerse.Progression.ProgressionRuntime.Instance));
        public static void Configure(IPlayerEconomy provider){service=provider;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset(){(service as IDisposable)?.Dispose();service=null;catalog=null;}
        public static async void Reward(string matchId,bool online,bool won)
        {try{await Service.GrantMatchRewardAsync(matchId,online,won);}catch(Exception e){Debug.LogWarning("Match reward pending: "+e.Message);}}
    }
}
