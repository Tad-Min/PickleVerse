using PickleVerse.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.Shop
{
    /// <summary>Native-resolution shop chrome; the studio renders only while inspecting a paddle.</summary>
    public sealed class ShopPanelPresentation : MonoBehaviour
    {
        ShopScreen screen;GameObject backdrop;Camera sceneCamera;bool cameraEnabled;bool? inspecting;
        public static ShopPanelPresentation Install(ShopScreen owner)
        {
            var presentation=owner.GetComponent<ShopPanelPresentation>()??owner.gameObject.AddComponent<ShopPanelPresentation>();
            presentation.screen=owner;presentation.Apply();return presentation;
        }
        public void Apply()
        {
            if(!screen||!screen.browser)return;var browser=screen.browser;var root=screen.safeArea;
            var canvas=root.GetComponentInParent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var old=canvas.transform.Find("Shop Full Bleed Background");
            if(!old)
            {
                var r=new GameObject("Shop Full Bleed Background",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();r.SetParent(canvas.transform,false);r.SetAsFirstSibling();
                r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
                r.GetComponent<Image>().color=LobbyPanelStyle.Background;r.GetComponent<Image>().raycastTarget=false;old=r;
            }
            backdrop=old.gameObject;
            var bg=browser.browseRoot.transform.Find("Browse Backdrop") as RectTransform;if(bg)LobbyEtching.AddTo(bg);
            LobbyPanelStyle.SurfaceStyle((RectTransform)browser.heroButton.transform,true);
            LobbyPanelStyle.SurfaceStyle(browser.detailRoot.transform.Find("Details Glass") as RectTransform,true,true);
            LobbyPanelStyle.SurfaceStyle(screen.confirmation.transform.Find("Panel") as RectTransform,true);
            foreach(var b in root.GetComponentsInChildren<Button>(true))LobbyPanelStyle.Button(b,b==screen.confirmBuy||b==screen.tokenpadBuy||b==screen.eliteBuy);
            LobbyPanelStyle.Text(root.Find("Brand")?.GetComponent<TMP_Text>(),36,ScreenUiTheme.Text);
            LobbyPanelStyle.Text(browser.heroTitle,42,ScreenUiTheme.Text);LobbyPanelStyle.Text(browser.heroCollection,12,ScreenUiTheme.Gold);
            LobbyPanelStyle.Text(browser.heroDescription,18,ScreenUiTheme.Muted,true);LobbyPanelStyle.Text(browser.heroPrice,18,ScreenUiTheme.Gold,true);
            browser.heroCollection.characterSpacing=2;
            foreach(var item in new[]{browser.signatureTab,browser.allTab})LobbyPanelStyle.Text(item.GetComponentInChildren<TMP_Text>(),15,ScreenUiTheme.Text);
            var mask=browser.allScroll.viewport.GetComponent<Mask>();if(mask)mask.enabled=false;
            if(!browser.allScroll.viewport.GetComponent<RectMask2D>())browser.allScroll.viewport.gameObject.AddComponent<RectMask2D>();
            var image=browser.allScroll.viewport.GetComponent<Image>();if(image){image.color=Color.clear;image.raycastTarget=true;}
            browser.allScroll.decelerationRate=.08f;
            LobbyPanelStyle.Text(screen.status,12,ScreenUiTheme.Muted,true);
            if(!sceneCamera&&browser.detailPresentation){sceneCamera=browser.detailPresentation.uiCamera;if(sceneCamera)cameraEnabled=sceneCamera.enabled;}
            inspecting=null;RefreshCamera();
        }
        void Update(){RefreshCamera();}
        void RefreshCamera()
        {
            if(!screen||!screen.browser)return;bool detail=screen.browser.Inspecting;
            // ShowAll/ShowSignature also call Hide(), which can enable the camera again.
            if(sceneCamera&&sceneCamera.enabled!=detail)sceneCamera.enabled=detail;
            if(screen.preview&&screen.preview.enabled!=detail)screen.preview.enabled=detail;
            if(inspecting==detail)return;inspecting=detail;
            if(backdrop)backdrop.SetActive(!detail);
        }
        void OnDestroy(){if(sceneCamera)sceneCamera.enabled=cameraEnabled;}
    }
}
