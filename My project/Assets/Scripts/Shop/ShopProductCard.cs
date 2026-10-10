using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.Shop
{
    public sealed class ShopProductCard : MonoBehaviour
    {
        public Image thumbnail,accent;
        public TMP_Text title,collection,price,action;
        public Button button;
        public int Index {get;private set;}
        bool collectionMode;
        Color accentColor;
        public void Bind(ShopScreen screen,int index,bool owned)
        {
            Index=index;collectionMode=screen.collectionMode;var offer=screen.Items[index];thumbnail.sprite=offer.thumbnail;
            title.text=offer.title;collection.text=offer.collection;price.text=collectionMode?"":Price(offer);
            accentColor=offer.gemstone?new Color(.85f,.72f,.43f):new Color(.43f,.86f,.77f);accent.color=accentColor;
            SetOwned(owned);button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>screen.OpenProduct(Index));
        }
        public void SetOwned(bool owned){SetState(owned,false);}
        public void SetState(bool owned,bool equipped)
        {
            action.text=collectionMode?(equipped?"ĐANG SỬ DỤNG  •  XEM 3D":owned?"ĐÃ SỞ HỮU  •  XEM 3D":"CHƯA SỞ HỮU  •  XEM 3D"):owned?"ĐÃ SỞ HỮU  •  XEM 3D":"XEM CHI TIẾT  →";
            if(!collectionMode)return;
            thumbnail.color=owned?Color.white:new Color(.28f,.30f,.33f,1);
            title.color=owned?Color.white:new Color(.48f,.52f,.56f,1);
            accent.color=owned?accentColor:new Color(.15f,.20f,.24f,1);
            price.text=equipped?"VỢT ĐÃ CHỌN":owned?"SẴN SÀNG THI ĐẤU":"XEM TRƯỚC SKIN";
        }
        public static string Price(PaddleOffer item)=>(item.tokenpadPrice>0?item.tokenpadPrice.ToString("N0")+" Tokenpad  /  ":"")+item.elitePrice.ToString("N0")+" Elite";
    }
}
