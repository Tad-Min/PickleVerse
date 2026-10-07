using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PickleVerse.LocalMatch.Editor
{
    public static class BotMatchIntroSetup
    {
        public static void Configure(BotMatch match)
        {
            var intro=match.GetComponent<BotMatchIntro>();if(!intro)intro=match.gameObject.AddComponent<BotMatchIntro>();
            match.opening=intro;intro.match=match;intro.followCamera=UnityEngine.Object.FindAnyObjectByType<MatchCamera>();
            intro.femaleWalk=Clip("female");intro.maleWalk=Clip("male");
            var root=match.hud.transform.Find("CinematicIntro");
            if(!root)
            {
                root=new GameObject("CinematicIntro",typeof(RectTransform)).transform;root.SetParent(match.hud.transform,false);
                var rect=(RectTransform)root;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
                Bar("LetterboxTop",root,true);Bar("LetterboxBottom",root,false);
                var caption=Text("EntranceCaption",root,match.hud.selection.font,23);
                caption.rectTransform.anchorMin=caption.rectTransform.anchorMax=caption.rectTransform.pivot=new Vector2(0,0);
                caption.rectTransform.anchoredPosition=new Vector2(32,68);caption.rectTransform.sizeDelta=new Vector2(300,40);caption.alignment=TextAlignmentOptions.Left;
                var label=Text("MatchAnnouncement",root,match.hud.selection.font,48);
                label.text="Trận đấu bắt đầu";label.fontStyle=FontStyles.Bold;label.color=new Color(.83f,1f,.94f);
                label.rectTransform.anchorMin=label.rectTransform.anchorMax=label.rectTransform.pivot=new Vector2(.5f,.5f);
                label.rectTransform.sizeDelta=new Vector2(840,90);label.alignment=TextAlignmentOptions.Center;
                var skip=new GameObject("SkipIntro",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));skip.transform.SetParent(root,false);
                var buttonRect=(RectTransform)skip.transform;buttonRect.anchorMin=buttonRect.anchorMax=buttonRect.pivot=new Vector2(1,0);
                buttonRect.anchoredPosition=new Vector2(-24,6);buttonRect.sizeDelta=new Vector2(170,34);
                skip.GetComponent<UnityEngine.UI.Image>().color=new Color(.07f,.12f,.14f,.8f);
                var button=skip.GetComponent<UnityEngine.UI.Button>();button.targetGraphic=skip.GetComponent<UnityEngine.UI.Image>();UnityEventTools.AddPersistentListener(button.onClick,intro.Skip);
                var skipText=Text("Label",skip.transform,match.hud.selection.font,16);skipText.text="Bỏ qua / Enter";
                skipText.rectTransform.anchorMin=Vector2.zero;skipText.rectTransform.anchorMax=Vector2.one;skipText.rectTransform.offsetMin=skipText.rectTransform.offsetMax=Vector2.zero;skipText.alignment=TextAlignmentOptions.Center;
            }
            intro.overlay=root.gameObject;intro.announcement=(RectTransform)root.Find("MatchAnnouncement");intro.caption=root.Find("EntranceCaption").GetComponent<TMP_Text>();
            intro.topBar=root.Find("LetterboxTop").GetComponent<UnityEngine.UI.Image>();intro.bottomBar=root.Find("LetterboxBottom").GetComponent<UnityEngine.UI.Image>();
            root.gameObject.SetActive(false);EditorUtility.SetDirty(intro);EditorUtility.SetDirty(match);
        }
        static AnimationClip Clip(string gender)
        {
            string output="Assets/LocalMatchContent/Clips/"+gender+"/Entrance_Walk.anim";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(output);
            if(!clip)clip=AnimationData.Create(gender,"Entrance_Walk",output);
            clip.wrapMode=WrapMode.Loop;
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;settings.startTime=0;settings.stopTime=clip.length;
            AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);AssetDatabase.SaveAssetIfDirty(clip);
            return clip;
        }
        static TMP_Text Text(string name,Transform parent,TMP_FontAsset font,float size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
            var text=go.GetComponent<TextMeshProUGUI>();text.font=font;text.fontSize=size;text.color=Color.white;text.raycastTarget=false;return text;
        }
        static void Bar(string name,Transform parent,bool top)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=new Vector2(0,top?1:0);rect.anchorMax=new Vector2(1,top?1:0);rect.pivot=new Vector2(.5f,top?1:0);
            rect.sizeDelta=new Vector2(0,48);rect.anchoredPosition=Vector2.zero;
            var image=go.GetComponent<UnityEngine.UI.Image>();image.color=new Color(0,0,0,.9f);image.raycastTarget=false;
        }
        [MenuItem("PickleVerse/Add opening cinematic to BotMatch")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/BotMatch.unity")throw new InvalidOperationException("Open BotMatch first.");
            Directory.CreateDirectory("../UnityWork/CinematicIntro");
            if(!File.Exists("../UnityWork/CinematicIntro/Scene_before.unity"))EditorSceneManager.SaveScene(scene,"../UnityWork/CinematicIntro/Scene_before.unity",true);
            Configure(UnityEngine.Object.FindAnyObjectByType<BotMatch>());EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
    }
}
