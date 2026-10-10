using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using PickleVerse.LocalMatch;
using PickleVerse.Rules;

public static class TutorialSetup
{
    const string Path="Assets/Scenes/Tutorial.unity";
    static TMP_FontAsset font;
    static Material glow;
    static readonly Color Ink=new Color(.024f,.074f,.095f,.96f),Mint=new Color(.45f,.93f,.80f),Muted=new Color(.62f,.75f,.77f);
    public static object Run()
    {
        if(Application.isPlaying)throw new Exception("Stop Play first.");if(AssetDatabase.LoadAssetAtPath<SceneAsset>(Path))throw new Exception("Tutorial already exists.");
        Directory.CreateDirectory("Assets/LocalMatchContent/Tutorial");Directory.CreateDirectory("../UnityWork/Tutorial");AssetDatabase.Refresh();
        var source=EditorSceneManager.OpenScene("Assets/Scenes/BotMatch.unity");EditorSceneManager.SaveScene(source,Path,true);var scene=EditorSceneManager.OpenScene(Path);
        var old=UnityEngine.Object.FindAnyObjectByType<BotMatch>();var root=old.gameObject;root.name="Basic Tutorial";
        var player=old.human;var coach=old.bot;var ball=old.ball;var trail=old.ballTrail;var camera=UnityEngine.Object.FindAnyObjectByType<MatchCamera>();
        foreach(var t in new[]{old.ballShadow,old.landingMarker,old.aimMarker})if(t)UnityEngine.Object.DestroyImmediate(t.gameObject);
        UnityEngine.Object.DestroyImmediate(old.hud.gameObject);UnityEngine.Object.DestroyImmediate(root.GetComponent<BotMatchIntro>());UnityEngine.Object.DestroyImmediate(old);UnityEngine.Object.DestroyImmediate(camera);
        var tutorial=root.AddComponent<TutorialDirector>();tutorial.player=player;tutorial.coach=coach;tutorial.ball=ball;tutorial.trail=trail;tutorial.view=UnityEngine.Object.FindAnyObjectByType<Camera>();
        player.name="Learner / Female";coach.name="Coach / Male";
        var lighting=root.GetComponent<MatchEnvironmentLighting>();lighting.useLocalLaunchSettings=false;lighting.selectedNight=false;lighting.Apply(false);
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/LocalMatchContent/Match Vietnamese Font.asset");
        glow=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")){name="Tutorial Glow"};glow.SetColor("_BaseColor",Color.white);glow.SetFloat("_Surface",1);glow.SetFloat("_ZWrite",0);glow.SetFloat("_SrcBlend",5);glow.SetFloat("_DstBlend",1);glow.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glow.renderQueue=3000;
        AssetDatabase.CreateAsset(glow,"Assets/LocalMatchContent/Tutorial/Glow.mat");
        var highlights=new GameObject("Court Teaching Lines");
        tutorial.courtLine=Rectangle("Court Perimeter",highlights.transform,-Court.HalfWidth,Court.HalfWidth,-Court.HalfLength,Court.HalfLength);
        tutorial.kitchenNear=Rectangle("Near Kitchen",highlights.transform,-Court.HalfWidth,Court.HalfWidth,-Court.Kitchen,0);
        tutorial.kitchenFar=Rectangle("Far Kitchen",highlights.transform,-Court.HalfWidth,Court.HalfWidth,0,Court.Kitchen);
        tutorial.dimensions=new GameObject("Court Measurements");tutorial.dimensions.transform.SetParent(highlights.transform,false);
        WorldText("Length",tutorial.dimensions.transform,"13,41 m",new Vector3(-3.70f,.2f,0),tutorial.view);
        WorldText("Width",tutorial.dimensions.transform,"6,10 m",new Vector3(0,.2f,-7.15f),tutorial.view);
        tutorial.depthLabel=new GameObject("Kitchen Measurement");tutorial.depthLabel.transform.SetParent(highlights.transform,false);
        WorldText("Depth",tutorial.depthLabel.transform,"2,13 m  /  MỖI BÊN",new Vector3(0,.15f,-1.2f),tutorial.view);
        var ring=new GameObject("Incoming Landing Ring",typeof(LineRenderer));ring.transform.SetParent(highlights.transform,false);tutorial.landingRing=ring.transform;var line=ring.GetComponent<LineRenderer>();line.sharedMaterial=glow;line.useWorldSpace=false;line.widthMultiplier=.028f;line.positionCount=49;line.startColor=line.endColor=Mint;
        for(int i=0;i<49;i++){float angle=i*Mathf.PI*2/48;line.SetPosition(i,new Vector3(Mathf.Cos(angle)*.26f,0,Mathf.Sin(angle)*.26f));}
        tutorial.view.transform.SetPositionAndRotation(new Vector3(-7.1f,13,-11.4f),Quaternion.LookRotation(new Vector3(7.1f,-12.8f,11.4f)));tutorial.view.fieldOfView=52;
        BuildHud(tutorial);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        AddReplayButton();RegisterScene();AssetDatabase.SaveAssetIfDirty(glow);EditorSceneManager.OpenScene(Path);
        return new{scene=Path,lessons=TutorialCurriculum.Lessons.Length,shots=TutorialCurriculum.ShotCount,courtWidth=Court.HalfWidth*2,courtLength=Court.HalfLength*2,kitchenDepth=Court.Kitchen};
    }
    static void BuildHud(TutorialDirector tutorial)
    {
        var root=Rect("Tutorial UI",null,Vector2.zero,Vector2.one);var canvas=root.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=tutorial.view;canvas.planeDistance=.5f;
        var scaler=root.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;root.gameObject.AddComponent<GraphicRaycaster>();
        var hud=root.gameObject.AddComponent<TutorialHud>();tutorial.hud=hud;hud.safeArea=Rect("Safe Area",root,Vector2.zero,Vector2.one);
        var header=Block("Header",hud.safeArea,new Vector2(.035f,.89f),new Vector2(.68f,.974f),Ink);
        hud.chapter=Text("Chapter",header,"CƠ BẢN / 01 - 17",new Vector2(.035f,.15f),new Vector2(.53f,.85f),19,Mint,true);
        Text("Brand",header,"PICKLEVERSE  /  TUTORIAL",new Vector2(.56f,.15f),new Vector2(.96f,.85f),13,Muted);
        hud.skip=Button("Skip",hud.safeArea,"ĐỂ SAU",new Vector2(.84f,.91f),new Vector2(.965f,.974f),Ink,Muted);
        var panel=Block("Explanation",hud.safeArea,new Vector2(.735f,.345f),new Vector2(.965f,.86f),Ink);
        Block("Accent",panel,Vector2.zero,new Vector2(.012f,1),Mint);
        hud.title=Text("Lesson Title",panel,"Làm quen sân đấu",new Vector2(.065f,.73f),new Vector2(.94f,.94f),27,Color.white,true);hud.title.enableAutoSizing=true;hud.title.fontSizeMin=20;hud.title.fontSizeMax=27;
        hud.body=Text("Explanation Text",panel,"",new Vector2(.065f,.16f),new Vector2(.94f,.72f),19,Muted);
        hud.progress=Text("Progress",panel,"Khoảng 2–3 phút",new Vector2(.065f,.03f),new Vector2(.94f,.14f),12,Mint);
        hud.next=Button("Continue",hud.safeArea,"TIẾP TỤC →",new Vector2(.735f,.26f),new Vector2(.965f,.329f),Mint,new Color(.03f,.10f,.13f));hud.nextLabel=hud.next.GetComponentInChildren<TMP_Text>();
        hud.replay=Button("Replay",hud.safeArea,"XEM LẠI",new Vector2(.735f,.20f),new Vector2(.965f,.25f),Ink,Muted,13);
        hud.touchToggle=Button("Touch Toggle",hud.safeArea,"CẢM ỨNG",new Vector2(.70f,.91f),new Vector2(.82f,.974f),Ink,Muted,13);
        var key=Block("Input Prompt",hud.safeArea,new Vector2(.04f,.07f),new Vector2(.40f,.205f),Ink);hud.keyPanel=key.gameObject;
        hud.keys=Text("Keys",key,"J",new Vector2(.05f,.42f),new Vector2(.95f,.96f),34,Mint,true,TextAlignmentOptions.Center);
        hud.instruction=Text("Instruction",key,"",new Vector2(.06f,.07f),new Vector2(.94f,.45f),14,Color.white,false,TextAlignmentOptions.Center);
        hud.feedback=Text("Feedback",hud.safeArea,"",new Vector2(.06f,.215f),new Vector2(.69f,.29f),20,Mint,true,TextAlignmentOptions.Center);
        Text("Settings Hint",hud.safeArea,"Esc / Cài đặt • Enter / Tiếp tục",new Vector2(.04f,.018f),new Vector2(.45f,.048f),12,Muted);
        hud.touchRoot=Rect("Basic Touch Controls",hud.safeArea,Vector2.zero,Vector2.one).gameObject;
        var joystick=Block("Move",hud.touchRoot.transform,new Vector2(0,0),new Vector2(0,0),Ink,true);joystick.sizeDelta=new Vector2(136,136);var touch=joystick.gameObject.AddComponent<TouchControl>();touch.joystick=true;
        var knob=Block("Knob",joystick,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Mint);knob.sizeDelta=new Vector2(40,40);touch.knob=knob;
        var controls=new System.Collections.Generic.List<TouchControl>{touch};int[] masks={1,2,4,8,16};
        foreach(int mask in masks)
        {var button=Block("Touch "+mask,hud.touchRoot.transform,Vector2.zero,Vector2.zero,Ink,true);button.sizeDelta=new Vector2(91,48);var control=button.gameObject.AddComponent<TouchControl>();control.shotMask=mask;control.modifier=mask>=8;Text("Label",button,ShotInput.Keys(mask),Vector2.zero,Vector2.one,16,Color.white,true,TextAlignmentOptions.Center);controls.Add(control);}
        tutorial.touchControls=controls.ToArray();hud.touchRoot.SetActive(false);
        var modal=Block("Skip Confirmation",hud.safeArea,Vector2.zero,Vector2.one,new Color(0,0,0,.78f),true);hud.skipDialog=modal.gameObject;
        var card=Block("Panel",modal,new Vector2(.25f,.28f),new Vector2(.75f,.72f),Ink,true);
        Text("Title",card,"Để phần hướng dẫn này sau?",new Vector2(.07f,.63f),new Vector2(.94f,.91f),27,Color.white,true);
        Text("Body",card,"Bạn có thể mở lại Hướng dẫn cơ bản ở màn chọn chế độ.",new Vector2(.07f,.36f),new Vector2(.94f,.66f),19,Muted);
        hud.stay=Button("Stay",card,"TIẾP TỤC HỌC",new Vector2(.07f,.10f),new Vector2(.48f,.28f),Mint,new Color(.03f,.1f,.13f));
        hud.confirmSkip=Button("Defer",card,"ĐỂ SAU",new Vector2(.53f,.10f),new Vector2(.94f,.28f),new Color(.08f,.20f,.24f),Color.white);modal.gameObject.SetActive(false);
    }
    static LineRenderer Rectangle(string name,Transform parent,float left,float right,float near,float far)
    {
        var root=new GameObject(name,typeof(LineRenderer));root.transform.SetParent(parent,false);var line=root.GetComponent<LineRenderer>();line.sharedMaterial=glow;line.widthMultiplier=.055f;line.startColor=line.endColor=Mint;line.positionCount=5;
        line.SetPositions(new[]{new Vector3(left,.025f,near),new Vector3(right,.025f,near),new Vector3(right,.025f,far),new Vector3(left,.025f,far),new Vector3(left,.025f,near)});return line;
    }
    static void WorldText(string name,Transform parent,string value,Vector3 position,Camera camera)
    {
        var root=new GameObject(name,typeof(TextMeshPro),typeof(TutorialBillboard));root.transform.SetParent(parent,false);root.transform.position=position;
        var text=root.GetComponent<TextMeshPro>();text.font=font;text.text=value;text.fontSize=3;text.color=Mint;text.alignment=TextAlignmentOptions.Center;root.GetComponent<RectTransform>().sizeDelta=new Vector2(4,1);root.GetComponent<TutorialBillboard>().view=camera;
    }
    static void AddReplayButton()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/UI/ModeSelection.unity");EditorSceneManager.SaveScene(scene,"../UnityWork/Tutorial/ModeSelection.before.unity",true);
        var ui=UnityEngine.Object.FindAnyObjectByType<PickleVerse.FrontEnd.ModeSelectionMenu>();
        var button=Button("Replay Basic Tutorial",ui.safeArea,"HƯỚNG DẪN CƠ BẢN",new Vector2(.06f,.105f),new Vector2(.30f,.16f),Ink,Mint,13);
        var launcher=button.gameObject.AddComponent<TutorialLauncher>();UnityEventTools.AddPersistentListener(button.onClick,launcher.Replay);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
    static void RegisterScene()
    {
        foreach(var guid in AssetDatabase.FindAssets("t:BuildProfile"))
        {
            var profile=AssetDatabase.LoadAssetAtPath<UnityEditor.Build.Profile.BuildProfile>(AssetDatabase.GUIDToAssetPath(guid));if(profile.overrideGlobalScenes&&!profile.scenes.Any(s=>s.path==Path)){profile.scenes=profile.scenes.Concat(new[]{new EditorBuildSettingsScene(Path,true)}).ToArray();EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);}
        }
        if(!EditorBuildSettings.scenes.Any(s=>s.path==Path))EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(Path,true)}).ToArray();
    }
    static Button Button(string name,Transform parent,string value,Vector2 min,Vector2 max,Color bg,Color fg,int size=16)
    {var r=Block(name,parent,min,max,bg,true);var button=r.gameObject.AddComponent<Button>();button.targetGraphic=r.GetComponent<Image>();Text("Label",r,value,new Vector2(.04f,0),new Vector2(.96f,1),size,fg,true,TextAlignmentOptions.Center);return button;}
    static TMP_Text Text(string name,Transform parent,string value,Vector2 min,Vector2 max,int size,Color color,bool bold=false,TextAlignmentOptions align=TextAlignmentOptions.Left)
    {var r=Rect(name,parent,min,max);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=size;t.color=color;t.alignment=align;t.raycastTarget=false;if(bold)t.fontStyle=FontStyles.Bold;return t;}
    static RectTransform Block(string name,Transform parent,Vector2 min,Vector2 max,Color color,bool raycast=false)
    {var r=Rect(name,parent,min,max);var image=r.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=raycast;return r;}
    static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
    {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.gameObject.layer=5;if(parent)r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
}
