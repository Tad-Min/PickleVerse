using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace PickleVerse.Shop
{
    public sealed class ShopScreen : MonoBehaviour
    {
        public RectTransform safeArea;
        public bool collectionMode;
        public ShopBrowser browser;
        public PaddlePreview preview;
        public TMP_Text title,collection,description,status,ownership,tokenpadPrice,elitePrice,confirmTitle,confirmBody;
        public Button tokenpadBuy,eliteBuy,confirmBuy,cancelBuy;
        public GameObject confirmation;
        public Image[] cardBorders;
        public TMP_Text[] cardOwnership,cardPrices;
        public Image confirmIcon;
        PlayerEconomyState wallet;IPlayerEconomy economy;
        ShopCurrency pendingCurrency;string requestId;bool busy;
        Rect lastSafeArea;Vector2 lastResolution;
        public int Selected {get;private set;}
        public PaddleOffer[] Items=>collectionMode?PlayerEconomy.Catalog.CollectionItems:PlayerEconomy.Catalog.paddles;
        public PaddleOffer Offer=>Items[Selected];
        async void Awake()
        {ShopPanelPresentation.Install(this);economy=PlayerEconomy.Service;economy.Changed+=Refresh;confirmation.SetActive(false);if(Items.Length>0)Select(0);wallet=await economy.ReadAsync();if(this)Refresh(wallet);}
        void Start(){if(browser)browser.FocusTab();else if(EventSystem.current)EventSystem.current.SetSelectedGameObject(cardBorders[0].gameObject);}
        void Update()
        {
            var a=Screen.safeArea;var size=new Vector2(Screen.width,Screen.height);
            if(size.x>0&&size.y>0&&(a!=lastSafeArea||size!=lastResolution)){lastSafeArea=a;lastResolution=size;safeArea.anchorMin=new Vector2(a.xMin/size.x,a.yMin/size.y);safeArea.anchorMax=new Vector2(a.xMax/size.x,a.yMax/size.y);safeArea.offsetMin=safeArea.offsetMax=Vector2.zero;}
            if(!PickleVerse.Settings.FrontEndModalInput.IsOpen&&Keyboard.current?.escapeKey.wasPressedThisFrame==true){if(confirmation.activeSelf)CancelPurchase();else Back();}
        }
        public void Select(int item)
        {
            if(busy||confirmation.activeSelf||item<0||item>=Items.Length)return;Selected=item;preview.SelectOffer(Offer,item,Items.Length);
            title.text=Offer.title;collection.text=Offer.collection;description.text=Offer.description;
            status.text=collectionMode?"Skin chỉ thay đổi diện mạo • Không thay đổi sức mạnh":"Tokenpad nhận từ trận đấu • Skin chỉ thay đổi diện mạo";
            tokenpadPrice.text="MUA • "+Offer.tokenpadPrice.ToString("N0")+" TOKENPAD";elitePrice.text="MUA • "+Offer.elitePrice.ToString("N0")+" ELITE TOKEN";
            tokenpadBuy.gameObject.SetActive(Offer.tokenpadPrice>0);
            var eliteRect=(RectTransform)eliteBuy.transform;eliteRect.anchorMin=new Vector2(Offer.tokenpadPrice>0?.52f:0,0);eliteRect.anchorMax=Vector2.one;eliteRect.offsetMin=eliteRect.offsetMax=Vector2.zero;
            for(int i=0;i<cardBorders.Length;i++)cardBorders[i].color=i==item?new Color(.78f,.66f,.38f):new Color(.14f,.22f,.28f);
            if(wallet!=null)Refresh(wallet);
        }
        void Refresh(PlayerEconomyState state)
        {
            if(browser)browser.RefreshWallet(state);
            if(Items.Length==0){wallet=state;return;}
            wallet=state;bool owned=wallet.Owns(Offer.id);
            if(collectionMode)
            {
                bool equipped=wallet.equippedPaddle==Offer.id;
                ownership.text=equipped?"VỢT ĐANG SỬ DỤNG":owned?"ĐÃ SỞ HỮU":"CHƯA SỞ HỮU";
                tokenpadBuy.gameObject.SetActive(false);
                var equipRect=(RectTransform)eliteBuy.transform;equipRect.anchorMin=Vector2.zero;equipRect.anchorMax=Vector2.one;equipRect.offsetMin=equipRect.offsetMax=Vector2.zero;
                elitePrice.text=equipped?"ĐANG SỬ DỤNG":"SỬ DỤNG";
                eliteBuy.interactable=!busy&&owned&&!equipped;
                status.text=owned?"Áp dụng cho vợt ở đại sảnh và khi thi đấu.":"Sở hữu skin tại cửa hàng để sử dụng.";
                return;
            }
            ownership.text=owned?"ĐÃ SỞ HỮU":"SKIN VỢT • DIỆN MẠO RIÊNG CỦA BẠN";
            tokenpadBuy.gameObject.SetActive(!owned&&Offer.tokenpadPrice>0);
            var eliteRect=(RectTransform)eliteBuy.transform;eliteRect.anchorMin=new Vector2(!owned&&Offer.tokenpadPrice>0?.52f:0,0);eliteRect.anchorMax=Vector2.one;eliteRect.offsetMin=eliteRect.offsetMax=Vector2.zero;
            if(owned)elitePrice.text="ĐÃ SỞ HỮU";
            tokenpadBuy.interactable=!busy&&!owned&&wallet.tokenpad>=Offer.tokenpadPrice;
            eliteBuy.interactable=!busy&&!owned&&wallet.elite>=Offer.elitePrice;
            for(int i=0;i<cardOwnership.Length&&i<PlayerEconomy.Catalog.paddles.Length;i++)
            {
                var item=PlayerEconomy.Catalog.paddles[i];cardOwnership[i].text=wallet.owned.Contains(item.id)?"ĐÃ SỞ HỮU":"XEM 3D  →";
                if(cardPrices!=null&&i<cardPrices.Length&&cardPrices[i])cardPrices[i].text=(item.tokenpadPrice>0?item.tokenpadPrice.ToString("N0")+" TOKENPAD  /  ":"")+item.elitePrice.ToString("N0")+" ELITE TOKEN";
            }
            if(!owned&&Offer.tokenpadPrice>0&&wallet.tokenpad<Offer.tokenpadPrice&&wallet.elite<Offer.elitePrice)status.text="Chưa đủ tiền cho skin này. Hoàn thành trận để nhận Tokenpad.";
            if(!owned&&Offer.tokenpadPrice==0&&wallet.elite<Offer.elitePrice)status.text="Chưa đủ ELITE TOKEN để sở hữu skin này. ELITE TOKEN chỉ nhận qua nạp.";
        }
        public void BuyTokenpad(){RequestPurchase(ShopCurrency.Tokenpad);}
        public void BuyElite(){if(collectionMode)EquipSelected();else RequestPurchase(ShopCurrency.Elite);}
        public async void EquipSelected()
        {
            if(!collectionMode||busy||wallet==null||!wallet.Owns(Offer.id))return;
            busy=true;eliteBuy.interactable=false;
            try{var result=await economy.EquipPaddleAsync(Offer.id);if(this){wallet=await economy.ReadAsync();status.text=result==EquipStatus.Success?"Đã chọn "+Offer.title:"Không thể sử dụng skin này.";}}
            catch(Exception e){if(this)status.text="Chưa lưu lựa chọn: "+e.Message;}
            finally{busy=false;if(this&&wallet!=null)Refresh(wallet);}
        }
        public void RequestPurchase(ShopCurrency currency)
        {
            if(collectionMode||busy||confirmation.activeSelf||wallet==null||wallet.Owns(Offer.id)||Offer.Price(currency)<=0)return;
            pendingCurrency=currency;requestId=Guid.NewGuid().ToString("N");
            confirmTitle.text="SỞ HỮU "+Offer.title;confirmIcon.sprite=PlayerEconomy.Catalog.Icon(currency);
            confirmBody.text="Giá: "+Offer.Price(currency).ToString("N0")+" "+(currency==ShopCurrency.Tokenpad?"Tokenpad":"Elite Token")+"\nSố dư sau mua: "+(wallet.Balance(currency)-Offer.Price(currency)).ToString("N0")+"\nSkin sẽ được lưu vào bộ sưu tập của bạn.";
            confirmBuy.interactable=wallet.Balance(currency)>=Offer.Price(currency);confirmation.SetActive(true);
            if(EventSystem.current&&cancelBuy)EventSystem.current.SetSelectedGameObject(cancelBuy.gameObject);
        }
        public async void ConfirmPurchase()
        {
            if(busy||!confirmation.activeSelf)return;busy=true;confirmBuy.interactable=false;status.text="Đang xác nhận giao dịch…";
            try
            {
                var result=await economy.PurchaseAsync(Offer.id,pendingCurrency,requestId);
                if(!this)return;confirmation.SetActive(false);wallet=result.wallet;
                status.text=result.Success?"Đã sở hữu "+Offer.title+". Skin đã được lưu.":result.status==PurchaseStatus.InsufficientFunds?"Chưa đủ số dư. Không có tiền nào bị trừ.":result.status==PurchaseStatus.AlreadyOwned?"Bạn đã sở hữu skin này.":"Không thể hoàn tất giao dịch. Hãy thử lại.";
            }
            catch(Exception e){if(this){confirmation.SetActive(false);status.text="Chưa hoàn tất mua: "+e.Message;}}
            finally{busy=false;if(this&&wallet!=null){Refresh(wallet);FocusProduct();}}
        }
        void FocusProduct(){if(browser)browser.FocusDetail();else if(EventSystem.current)EventSystem.current.SetSelectedGameObject(cardBorders[Selected].gameObject);}
        public void OpenProduct(int index){if(busy||confirmation.activeSelf||index<0||index>=Items.Length)return;Select(index);if(browser)browser.OpenDetails();}
        public void CancelPurchase(){if(!busy){confirmation.SetActive(false);FocusProduct();}}
        public void Back(){if(confirmation.activeSelf){CancelPurchase();return;}if(busy)return;if(browser&&browser.Inspecting){browser.CloseDetails();return;}LobbyNavigation.ReturnToLobby();}
        public void GoLobby(){if(!busy)LobbyNavigation.ReturnToLobby();}
        public void SelectGemstone(){OpenProduct(0);}public void SelectThot(){OpenProduct(1);}
        void OnDestroy(){if(economy!=null)economy.Changed-=Refresh;}
    }
}
