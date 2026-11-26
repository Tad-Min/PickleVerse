using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PickleVerse.Progression;

namespace PickleVerse.Shop
{
    public sealed class OnlinePlayerEconomy : IPlayerEconomy, IDisposable
    {
        public event Action<PlayerEconomyState> Changed;
        ProgressionRuntime Runtime => ProgressionRuntime.Instance;
        public OnlinePlayerEconomy()
        {
            ProgressionRuntime.Changed += Refresh;
            if (Runtime && Runtime.Ready)
                ApplyCatalog();
        }
        PlayerEconomyState Current()
        {
            if (!Runtime.Ready)
                throw new InvalidOperationException("Dữ liệu cửa hàng chưa được tải từ máy chủ.");
            var inventory = Runtime.Snapshot.inventory;
            return new PlayerEconomyState
            {
                tokenpad = inventory.tokenpad,
                elite = inventory.elite,
                owned = inventory.owned.ToList(),
                equippedPaddle = inventory.equippedPaddle
            };
        }
        void Refresh()
        {
            if (!Runtime)
                return;
            if (!Runtime.Ready)
            {
                Changed?.Invoke(new PlayerEconomyState());
                return;
            }
            ApplyCatalog();
            Changed?.Invoke(Current());
        }
        void ApplyCatalog() => Merge(PlayerEconomy.Catalog, Runtime.Snapshot.catalog.cosmetics);

        /// <summary>
        /// Takes titles, descriptions and prices from the server. An item that this build has no model for (added in
        /// the CMS, or sold only in a newer build) is skipped: an exception here would leave through
        /// ProgressionRuntime.Changed and stop every profile load and command of the player.
        /// </summary>
        public static void Merge(ShopCatalog catalog, IReadOnlyCollection<OnlineCosmeticOffer> offers)
        {
            var modelled = catalog.Modelled;   // the full list, kept across merges: a skin published later comes back
            foreach (var remote in offers)
            {
                var item = string.IsNullOrEmpty(remote.id) ? null
                    : remote.id == ShopCatalog.DefaultPaddleId ? catalog.defaultPaddle : Array.Find(modelled, p => p.id == remote.id);
                if (item == null)
                    continue;
                item.title = remote.title;
                item.description = remote.description;
                item.collection = remote.collection;
                item.tokenpadPrice = remote.tokenpadPrice;
                item.elitePrice = remote.elitePrice;
                item.gemstone = remote.gemstone;
                item.releaseOrder = remote.releaseOrder;
            }
            catalog.paddles = modelled.Where(p => offers.Any(c => c.id == p.id)).ToArray();
        }
        public async Task<PlayerEconomyState> ReadAsync()
        {
            await Runtime.EnsureReadyAsync();
            ApplyCatalog();
            return Current();
        }
        public async Task<PurchaseResult> PurchaseAsync(string itemId, ShopCurrency currency, string requestId)
        {
            await Runtime.CommandAsync("purchase", new { RequestId = requestId, Code = itemId, Currency = currency == ShopCurrency.Tokenpad ? "Tokenpad" : "EliteToken" });
            return new PurchaseResult { status = PurchaseStatus.Success, wallet = Current() };
        }
        public async Task<EquipStatus> EquipPaddleAsync(string itemId)
        {
            await Runtime.CommandAsync("equip", new { RequestId = Guid.NewGuid().ToString("N"), Code = itemId });
            return EquipStatus.Success;
        }
        // Match finalization awards the server wallet atomically; the UI cannot grant extra money.
        public Task<bool> GrantMatchRewardAsync(string matchId, bool online, bool won) => Task.FromResult(false);
        public void Dispose()
        {
            ProgressionRuntime.Changed -= Refresh;
        }
    }
}
