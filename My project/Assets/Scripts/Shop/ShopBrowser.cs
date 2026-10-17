using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PickleVerse.Shop
{
    public sealed class ShopBrowser : MonoBehaviour
    {
        public ShopScreen screen;
        public GameObject browseRoot,detailRoot,signaturePage,allPage;
        public Button signatureTab,allTab,detailBack,heroButton;
        public Image signatureLine,allLine,heroImage;
        public TMP_Text heroTitle,heroCollection,heroDescription,heroPrice,totalCount,detailBreadcrumb,emptyLabel;
        public TMP_InputField search;
        public RectTransform signatureContent,allContent;
        public GridLayoutGroup signatureGrid,allGrid;
        public ScrollRect allScroll;
        public ShopProductCard cardPrefab;
        public ShopDetailPresentation detailPresentation;
        readonly List<ShopProductCard> featuredCards=new List<ShopProductCard>(),allCards=new List<ShopProductCard>();
        PlayerEconomyState wallet;
        int heroIndex;
        float lastWidth=-1;
        public int Tab {get;private set;}
        public bool Inspecting=>detailRoot.activeSelf;
        public int[] VisibleProducts {get;private set;}=Array.Empty<int>();
        void Awake()
        {
            search.onValueChanged.AddListener(_=>RebuildAll());heroButton.onClick.AddListener(()=>screen.OpenProduct(heroIndex));
            if(!screen.collectionMode)BuildFeatured();RebuildAll();if(screen.collectionMode)ShowAll();else ShowSignature();
        }
        void Update(){float width=allScroll.viewport.rect.width;if(Mathf.Abs(width-lastWidth)>1){lastWidth=width;LayoutCards();}}
        public void ShowSignature(){Show(0);}public void ShowAll(){Show(1);}
        void Show(int tab)
        {
            if(screen.confirmation.activeSelf)return;if(screen.collectionMode)tab=1;Tab=tab;detailRoot.SetActive(false);browseRoot.SetActive(true);if(detailPresentation)detailPresentation.Hide();else screen.preview.previewCamera.enabled=false;
            signaturePage.SetActive(tab==0);allPage.SetActive(tab==1);signatureLine.enabled=tab==0;allLine.enabled=tab==1;
            Canvas.ForceUpdateCanvases();LayoutCards();
            PickleVerse.Settings.ScreenUiTheme.Selection(signatureTab,tab==0);
            PickleVerse.Settings.ScreenUiTheme.Selection(allTab,tab==1);
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject((tab==0?signatureTab:allTab).gameObject);
        }
        public void OpenDetails()
        {
            browseRoot.SetActive(false);detailRoot.SetActive(true);if(detailPresentation)detailPresentation.Show();else screen.preview.previewCamera.enabled=true;
            detailBreadcrumb.text=screen.collectionMode?"‹  QUAY LẠI BỘ SƯU TẬP":Tab==0?"‹  QUAY LẠI SIGNATURE":"‹  QUAY LẠI TẤT CẢ SẢN PHẨM";FocusDetail();
        }
        public void CloseDetails(){Show(Tab);}
        public void FocusDetail(){if(EventSystem.current)EventSystem.current.SetSelectedGameObject(detailBack.gameObject);}
        public void FocusTab(){if(EventSystem.current)EventSystem.current.SetSelectedGameObject((Tab==0?signatureTab:allTab).gameObject);}
        void BuildFeatured()
        {
            Clear(featuredCards);var order=PlayerEconomy.Catalog.NewestFirst();if(order.Length==0){heroButton.gameObject.SetActive(false);return;}
            heroIndex=order[0];var offer=PlayerEconomy.Catalog.paddles[heroIndex];heroTitle.text=offer.title;heroCollection.text=offer.collection;heroDescription.text=offer.description;heroPrice.text=ShopProductCard.Price(offer);heroImage.sprite=offer.thumbnail;
            foreach(int index in order.Skip(1).Take(3))featuredCards.Add(Create(index,signatureContent));
        }
        public void RebuildAll()
        {
            Clear(allCards);string term=search.text.Trim();var items=screen.Items;
            var order=screen.collectionMode?Enumerable.Range(0,items.Length).ToArray():PlayerEconomy.Catalog.NewestFirst();
            VisibleProducts=order.Where(i=>Matches(items[i],term)).ToArray();
            foreach(int index in VisibleProducts)allCards.Add(Create(index,allContent));
            totalCount.text=VisibleProducts.Length+(screen.collectionMode?" SKIN VỢT":" SẢN PHẨM");emptyLabel.gameObject.SetActive(VisibleProducts.Length==0);allScroll.verticalNormalizedPosition=1;LayoutCards();
        }
        static bool Matches(PaddleOffer offer,string term)=>string.IsNullOrEmpty(term)||(offer.title??"").IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0||(offer.collection??"").IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0;
        ShopProductCard Create(int index,Transform parent){var card=Instantiate(cardPrefab,parent);card.gameObject.SetActive(true);card.Bind(screen,index,wallet?.Owns(screen.Items[index].id)??(screen.Items[index].id==ShopCatalog.DefaultPaddleId));return card;}
        static void Clear(List<ShopProductCard> cards){foreach(var card in cards)if(card){card.gameObject.SetActive(false);Destroy(card.gameObject);}cards.Clear();}
        void LayoutCards()
        {
            float width=Mathf.Max(1,allScroll.viewport.rect.width);int columns=width>=960?4:width>=680?3:2;allGrid.constraintCount=columns;
            float cell=(width-allGrid.padding.horizontal-allGrid.spacing.x*(columns-1))/columns;allGrid.cellSize=new Vector2(Mathf.Max(100,cell),Mathf.Max(200,cell*.95f));
            float featured=Mathf.Max(1,signatureContent.rect.width);signatureGrid.constraintCount=3;signatureGrid.cellSize=new Vector2((featured-signatureGrid.spacing.x*2)/3,signatureContent.rect.height);
            if(allPage.activeInHierarchy)LayoutRebuilder.ForceRebuildLayoutImmediate(allContent);
        }
        public void RefreshWallet(PlayerEconomyState state)
        {wallet=state;foreach(var card in featuredCards.Concat(allCards))if(card)card.SetState(state.Owns(screen.Items[card.Index].id),state.equippedPaddle==screen.Items[card.Index].id);}
    }
}
