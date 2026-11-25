namespace PickleVerse.Shop
{
    /// <summary>Which buy buttons the product details show. The Elite button also reports an owned or a discontinued skin.</summary>
    public readonly struct ShopBuyButtons
    {
        public const string NotSold = "NGỪNG BÁN";
        public readonly bool tokenpad, elite;
        public readonly string tokenpadLabel, eliteLabel;
        public ShopBuyButtons(PaddleOffer offer, bool owned)
        {
            tokenpad = !owned && offer.tokenpadPrice > 0;
            elite = !tokenpad || offer.elitePrice > 0;
            tokenpadLabel = "MUA • " + offer.tokenpadPrice.ToString("N0") + " TOKENPAD";
            eliteLabel = owned ? "ĐÃ SỞ HỮU" : offer.ForSale ? "MUA • " + offer.elitePrice.ToString("N0") + " ELITE TOKEN" : NotSold;
        }
        public static bool CanBuy(PaddleOffer offer, PlayerEconomyState wallet, ShopCurrency currency) =>
            !wallet.Owns(offer.id) && offer.Price(currency) > 0 && wallet.Balance(currency) >= offer.Price(currency);

        /// <summary>Why the player cannot buy the skin now, or null when it is owned or affordable.</summary>
        public static string Problem(PaddleOffer offer, PlayerEconomyState wallet)
        {
            if (wallet.Owns(offer.id) || CanBuy(offer, wallet, ShopCurrency.Tokenpad) || CanBuy(offer, wallet, ShopCurrency.Elite))
                return null;
            if (!offer.ForSale)
                return "Cửa hàng đã ngừng bán skin này.";
            return offer.tokenpadPrice > 0 ? "Chưa đủ tiền cho skin này. Hoàn thành trận để nhận Tokenpad."
                : "Chưa đủ ELITE TOKEN để sở hữu skin này. ELITE TOKEN chỉ nhận qua nạp.";
        }
    }
}
