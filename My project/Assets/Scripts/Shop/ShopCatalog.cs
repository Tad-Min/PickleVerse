using System;
using System.Linq;
using UnityEngine;

namespace PickleVerse.Shop
{
    [Serializable]
    public sealed class PaddleOffer
    {
        public string id, title, description, collection;
        public int tokenpadPrice, elitePrice;
        public GameObject previewPrefab;
        [Tooltip("Model fitted to the existing paddle socket and contact surface, in paddle-local units.")]
        public GameObject gameplayPrefab;
        public Sprite thumbnail;
        public bool gemstone;
        [Tooltip("Name of the skin's exclusive match effect, shown in the product details. Empty = no effect.")]
        public string effectTitle;
        [TextArea(3, 10), Tooltip("What the effect does, one line per point.")]
        public string effectDetails;
        [Tooltip("Số lớn hơn là sản phẩm mới hơn trong Signature.")]
        public int releaseOrder;
        public bool HasEffect => !string.IsNullOrEmpty(effectTitle) && !string.IsNullOrEmpty(effectDetails);
        public int Price(ShopCurrency currency) => currency == ShopCurrency.Tokenpad ? tokenpadPrice : elitePrice;
        /// <summary>False once the shop stops selling the skin; it stays in the catalog for the players who own it.</summary>
        public bool ForSale => tokenpadPrice > 0 || elitePrice > 0;
    }
    public sealed class ShopCatalog : ScriptableObject
    {
        public int initialTokenpad = 0;
        [HideInInspector]
        public int initialElite = 0;
        public int botWinReward = 100, botLossReward = 50, onlineWinReward = 150, onlineLossReward = 75;
        public Sprite tokenpadIcon, eliteIcon;
        public Material tokenpadSheen, eliteSheen;
        public PaddleOffer[] paddles;
        [NonSerialized] PaddleOffer[] modelled;
        /// <summary>
        /// Every skin this build has a model for, as shipped. <see cref="paddles"/> narrows to what the server sells; a skin
        /// an admin publishes later is found here, so it appears without restarting the game.
        /// </summary>
        public PaddleOffer[] Modelled => modelled ??= paddles ?? Array.Empty<PaddleOffer>();
        public const string DefaultPaddleId = "paddle.default";
        public PaddleOffer defaultPaddle;
        public PaddleOffer Find(string id) => id == DefaultPaddleId ? defaultPaddle : Array.Find(paddles ?? Array.Empty<PaddleOffer>(), x => x.id == id);
        public PaddleOffer[] CollectionItems => defaultPaddle == null ? paddles : new[] { defaultPaddle }.Concat(paddles ?? Array.Empty<PaddleOffer>()).ToArray();
        public Sprite Icon(ShopCurrency currency) => currency == ShopCurrency.Tokenpad ? tokenpadIcon : eliteIcon;
        public int[] NewestFirst() => Enumerable.Range(0, paddles.Length).OrderByDescending(i => paddles[i].ForSale).ThenByDescending(i => paddles[i].releaseOrder).ThenBy(i => i).ToArray();
    }
}
