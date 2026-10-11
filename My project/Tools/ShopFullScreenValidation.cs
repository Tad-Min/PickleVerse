using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PickleVerse.Shop;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class ShopFullScreenValidation
{
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool ok){checks.Add(new{name,passed=ok});if(!ok)throw new Exception(name);}
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play first.");checks.Clear();var stored=PlayerPrefs.GetString(LocalEconomyStorage.Key,"");
        var ui=UnityEngine.Object.FindAnyObjectByType<ShopScreen>();var browser=ui.browser;var p=browser.detailPresentation;var camera=p.uiCamera;
        browser.ShowAll();Canvas.ForceUpdateCanvases();var original=camera.transform.position;
        Check("Browse camera is restored",camera.cullingMask==(1<<5)&&!p.Showing);
        ui.OpenProduct(1);ui.preview.autoRotate=false;await Task.Delay(100);Canvas.ForceUpdateCanvases();
        Check("Details render the studio directly",p.Showing&&ui.preview.previewCamera==camera&&camera.targetTexture==null&&(camera.cullingMask&(1<<30))!=0);
        var rect=(RectTransform)p.fullScreenInput.transform;
        Check("3D input covers full Canvas",rect.anchorMin==Vector2.zero&&rect.anchorMax==Vector2.one&&rect.parent==ui.transform);
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=new Vector2(Screen.width*.5f,Screen.height*.5f)},hits);
        Check("Center still accepts rotation gestures",hits.Any(h=>h.gameObject==p.fullScreenInput));
        Check("One active camera and listener",UnityEngine.Object.FindObjectsByType<Camera>().Count(c=>c.enabled)==1&&UnityEngine.Object.FindObjectsByType<AudioListener>().Count(l=>l.enabled)==1);
        var title=ui.title.rectTransform;var buy=(RectTransform)ui.tokenpadBuy.transform.parent;
        Check("Name and description are on outer right",title.anchorMin.x>=.74f&&ui.description.rectTransform.anchorMin.x>=.74f);
        Check("Buy action is bottom right",buy.anchorMin.x>=.74f&&buy.anchorMax.y<.15f);
        var rotation=ui.preview.pivot.localRotation;ui.preview.Rotate(360,0);Check("360 rotation works",Quaternion.Angle(rotation,ui.preview.pivot.localRotation)<.02f);
        float distance=camera.transform.localPosition.magnitude;ui.preview.ZoomIn();Check("Zoom closer changes distance",camera.transform.localPosition.magnitude<distance);
        ui.Back();Check("Back retains All tab",!p.Showing&&browser.Tab==1&&camera.transform.position==original&&camera.cullingMask==(1<<5));
        ui.OpenProduct(1);ui.preview.autoRotate=false;Check("Reopening resets default zoom without drift",Mathf.Abs(camera.transform.localPosition.magnitude-distance)<.001f);
        ui.BuyElite();Check("Purchase confirmation remains available",ui.confirmation.activeSelf);ui.CancelPurchase();
        Check("No real wallet transaction occurred",PlayerPrefs.GetString(LocalEconomyStorage.Key,"")==stored);
        var lobby=ui.safeArea.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b=>b.name=="Back");lobby.onClick.Invoke();await Task.Delay(250);
        Check("Top-left lobby button goes directly to Menu",SceneManager.GetActiveScene().name=="Menu");
        var result=new{passed=true,count=checks.Count,checks};Directory.CreateDirectory("../UnityWork/ShopFullScreen");File.WriteAllText("../UnityWork/ShopFullScreen/validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
    }
}
