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
        bool? compact;
        bool stateOwned,stateEquipped;
        void Awake()
        {
            PickleVerse.Settings.LobbyPanelStyle.SurfaceStyle((RectTransform)transform,true,true);
            PickleVerse.Settings.LobbyPanelStyle.Button(button);
        }
        void LateUpdate()
        {
            bool narrow=((RectTransform)transform).rect.height<230;
            if(compact==narrow)return;compact=narrow;
            Place(thumbnail.rectTransform,narrow?.025f:.05f,narrow?.06f:.41f,narrow?.33f:.95f,narrow?.96f:.98f);
            Place(collection.rectTransform,narrow?.37f:.06f,narrow?.70f:.33f,.94f,narrow?.91f:.40f);
            Place(title.rectTransform,narrow?.37f:.06f,narrow?.43f:.22f,.94f,narrow?.72f:.34f);
            Place(price.rectTransform,narrow?.37f:.06f,narrow?.24f:.12f,.94f,narrow?.44f:.22f);
            Place(action.rectTransform,narrow?.37f:.06f,narrow?.065f:.025f,.94f,narrow?.25f:.10f);
            title.fontSize=narrow?23:26;collection.fontSize=11;price.fontSize=13;action.fontSize=12;
            title.enableAutoSizing=collection.enableAutoSizing=price.enableAutoSizing=action.enableAutoSizing=false;
            title.color=PickleVerse.Settings.ScreenUiTheme.Text;collection.color=PickleVerse.Settings.ScreenUiTheme.Gold;
            price.color=PickleVerse.Settings.ScreenUiTheme.Muted;action.color=PickleVerse.Settings.ScreenUiTheme.Mint;
            if(collectionMode)SetState(stateOwned,stateEquipped);
        }
        static void Place(RectTransform r,float x,float y,float right,float top){r.anchorMin=new Vector2(x,y);r.anchorMax=new Vector2(right,top);r.offsetMin=r.offsetMax=Vector2.zero;}
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
            stateOwned=owned;stateEquipped=equipped;
            action.text=collectionMode?(equipped?"ĐANG SỬ DỤNG  •  XEM 3D":owned?"ĐÃ SỞ HỮU  •  XEM 3D":"CHƯA SỞ HỮU  •  XEM 3D"):owned?"ĐÃ SỞ HỮU  •  XEM 3D":"XEM CHI TIẾT  →";
            if(!collectionMode)return;
            thumbnail.color=owned?Color.white:new Color(.28f,.30f,.33f,1);
            title.color=owned?Color.white:new Color(.48f,.52f,.56f,1);
            accent.color=owned?accentColor:new Color(.15f,.20f,.24f,1);
            price.text=equipped?"VỢT ĐÃ CHỌN":owned?"SẴN SÀNG THI ĐẤU":"XEM TRƯỚC SKIN";
        }
        public static string Price(PaddleOffer item)=>(item.tokenpadPrice>0?item.tokenpadPrice.ToString("N0")+" TOKENPAD  /  ":"")+item.elitePrice.ToString("N0")+" ELITE TOKEN";
    }
}
