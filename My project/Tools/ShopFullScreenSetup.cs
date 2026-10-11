using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using PickleVerse.Shop;

public static class ShopFullScreenSetup
{
    static void Place(Transform t,Vector2 min,Vector2 max){var r=(RectTransform)t;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
    public static object Apply()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play first.");
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Shop.unity");Directory.CreateDirectory("../UnityWork/ShopFullScreen");EditorSceneManager.SaveScene(scene,"../UnityWork/ShopFullScreen/Shop.before.unity",true);
        var ui=UnityEngine.Object.FindAnyObjectByType<ShopScreen>();var browser=ui.browser;
        if(browser.detailPresentation)throw new InvalidOperationException("Full-screen presentation already exists.");
        var input=ui.preview.gameObject;input.transform.SetParent(ui.transform,false);input.transform.SetAsFirstSibling();
        var fitter=input.GetComponent<AspectRatioFitter>();if(fitter)UnityEngine.Object.DestroyImmediate(fitter);
        Place(input.transform,Vector2.zero,Vector2.one);input.name="Full Screen 3D Input";
        var raw=input.GetComponent<RawImage>();raw.color=Color.clear;raw.texture=null;raw.raycastTarget=true;
        var presentation=browser.gameObject.AddComponent<ShopDetailPresentation>();presentation.screen=ui;presentation.uiCamera=ui.GetComponent<Canvas>().worldCamera;presentation.studioCamera=ui.preview.previewCamera;presentation.fullScreenInput=input;browser.detailPresentation=presentation;
        var detail=browser.detailRoot.transform;
        var controls=detail.Find("Preview Controls");Place(controls,new Vector2(.04f,.073f),new Vector2(.50f,.135f));
        Place(controls.Find("Zoom Out"),Vector2.zero,new Vector2(.12f,1));Place(controls.Find("Zoom In"),new Vector2(.14f,0),new Vector2(.26f,1));Place(controls.Find("Reset View"),new Vector2(.29f,0),new Vector2(.62f,1));controls.Find("Zoom Hint").gameObject.SetActive(false);
        var panel=detail.Find("PreviewPanel");var rotate=panel.Find("AutoRotate");rotate.SetParent(controls,false);Place(rotate,new Vector2(.65f,0),Vector2.one);panel.gameObject.SetActive(false);
        Place(detail.Find("Gestures"),new Vector2(.04f,.022f),new Vector2(.52f,.064f));
        Place(ui.collection.transform,new Vector2(.75f,.625f),new Vector2(.968f,.674f));
        Place(ui.title.transform,new Vector2(.75f,.526f),new Vector2(.968f,.628f));ui.title.fontSizeMax=40;ui.title.fontSizeMin=22;ui.title.enableAutoSizing=true;
        Place(ui.description.transform,new Vector2(.75f,.366f),new Vector2(.968f,.515f));ui.description.fontSize=19;
        Place(ui.ownership.transform,new Vector2(.745f,.155f),new Vector2(.968f,.203f));
        Place(ui.tokenpadBuy.transform.parent,new Vector2(.745f,.060f),new Vector2(.968f,.143f));
        Place(ui.status.transform,new Vector2(.745f,.011f),new Vector2(.968f,.049f));ui.status.fontSize=12;
        Place(browser.detailBack.transform,new Vector2(.04f,.805f),new Vector2(.315f,.871f));
        var lobby=ui.safeArea.GetComponentsInChildren<Button>(true).First(b=>b.name=="Back");for(int i=lobby.onClick.GetPersistentEventCount()-1;i>=0;i--)UnityEventTools.RemovePersistentListener(lobby.onClick,i);UnityEventTools.AddPersistentListener(lobby.onClick,ui.GoLobby);
        input.SetActive(false);EditorUtility.SetDirty(ui.preview);EditorUtility.SetDirty(browser);EditorUtility.SetDirty(presentation);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return new{scene=scene.path,fullBleed=true,direct3D=true};
    }
}
