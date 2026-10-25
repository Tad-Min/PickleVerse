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
        [Tooltip("Số lớn hơn là sản phẩm mới hơn trong Signature.")]
        public int releaseOrder;
        public int Price(ShopCurrency currency) => currency == ShopCurrency.Tokenpad ? tokenpadPrice : elitePrice;
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
        public const string DefaultPaddleId = "paddle.default";
        public PaddleOffer defaultPaddle;
        public PaddleOffer Find(string id) => id == DefaultPaddleId ? defaultPaddle : Array.Find(paddles ?? Array.Empty<PaddleOffer>(), x => x.id == id);
        public PaddleOffer[] CollectionItems => defaultPaddle == null ? paddles : new[] { defaultPaddle }.Concat(paddles ?? Array.Empty<PaddleOffer>()).ToArray();
        public Sprite Icon(ShopCurrency currency) => currency == ShopCurrency.Tokenpad ? tokenpadIcon : eliteIcon;
        public int[] NewestFirst() => Enumerable.Range(0, paddles.Length).OrderByDescending(i => paddles[i].releaseOrder).ThenBy(i => i).ToArray();
    }
}
