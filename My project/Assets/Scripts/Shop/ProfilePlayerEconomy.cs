using System;
using System.IO;
using System.Threading.Tasks;
using PickleVerse.Progression;
using UnityEngine;

namespace PickleVerse.Shop
{
    // Both progression rewards and shop purchases use the same atomic, leased player file.
    public sealed class ProfileEconomyStorage : IEconomyStorage
    {
        readonly Func<PlayerProfile> player;
        readonly Action<Action<PlayerProfile>> transaction;
        public ProfileEconomyStorage(Func<PlayerProfile> player, Action<Action<PlayerProfile>> transaction)
        { this.player=player; this.transaction=transaction; }
        public string Read()
        {
            var p=player();
            var state=string.IsNullOrEmpty(p.shopData)?new PlayerEconomyState():JsonUtility.FromJson<PlayerEconomyState>(p.shopData);
            if(state==null)throw new InvalidDataException("Không đọc được bộ sưu tập của player.");
            state.tokenpad=p.coins; state.elite=p.gems;
            return JsonUtility.ToJson(state);
        }
        public void Write(string json)
        {
            var next=JsonUtility.FromJson<PlayerEconomyState>(json);
            transaction(p=>{p.coins=next.tokenpad;p.gems=next.elite;p.shopData=json;});
        }
        public static bool ImportLegacy(PlayerProfile player, PlayerEconomyState legacy)
        {
            if(legacy==null||!string.IsNullOrEmpty(player.shopData))return false;
            var imported=legacy.Copy();
            player.coins=checked(player.coins+Math.Max(0,imported.tokenpad));
            // The old global demo wallet had no verified payment source.
            imported.elite=player.gems; imported.tokenpad=player.coins;
            player.shopData=JsonUtility.ToJson(imported);
            return true;
        }
    }
    public sealed class ProfilePlayerEconomy : IPlayerEconomy, IDisposable
    {
        readonly ShopCatalog catalog;
        readonly ProgressionRuntime runtime;
        readonly ProfileEconomyStorage storage;
        public event Action<PlayerEconomyState> Changed;
        public ProfilePlayerEconomy(ShopCatalog catalog,ProgressionRuntime runtime)
        {
            this.catalog=catalog;this.runtime=runtime;
            if(!runtime)throw new InvalidOperationException("Player chưa được tải.");
            ImportLegacyWallet();
            storage=new ProfileEconomyStorage(()=>runtime.Player,runtime.UpdateWallet);
            ProgressionRuntime.Changed+=ProfileChanged;
        }
        void ImportLegacyWallet()
        {
            if(!string.IsNullOrEmpty(runtime.Player.shopData))return;
            string json=PlayerPrefs.GetString(LocalEconomyStorage.Key,"");
            if(string.IsNullOrEmpty(json))return;
            var legacy=JsonUtility.FromJson<PlayerEconomyState>(json);
            if(legacy==null)return;
            string root=Path.Combine(Application.persistentDataPath,"LocalPlayers");
            string owner=Path.Combine(root,"legacy-shop-owner.txt");
            // A global prototype wallet is imported by exactly one profile, across processes.
            using(var guard=new FileStream(Path.Combine(root,"legacy-shop-import.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
            {
                if(!File.Exists(owner))LocalPlayerStore.WriteAtomic(owner,runtime.Player.id);
                if(File.ReadAllText(owner).Trim()!=runtime.Player.id)return;
                runtime.UpdateWallet(p=>ProfileEconomyStorage.ImportLegacy(p,legacy));
            }
        }
        LocalPlayerEconomy Current()=>new LocalPlayerEconomy(catalog,storage);
        public Task<PlayerEconomyState> ReadAsync()=>Current().ReadAsync();
        public Task<PurchaseResult> PurchaseAsync(string itemId,ShopCurrency currency,string requestId)=>Current().PurchaseAsync(itemId,currency,requestId);
        public Task<EquipStatus> EquipPaddleAsync(string itemId)=>Current().EquipPaddleAsync(itemId);
        public Task<bool> GrantMatchRewardAsync(string matchId,bool online,bool won)=>Current().GrantMatchRewardAsync(matchId,online,won);
        void ProfileChanged(){if(runtime)Changed?.Invoke(Current().ReadAsync().Result);}
        public void Dispose(){ProgressionRuntime.Changed-=ProfileChanged;}
    }
}
