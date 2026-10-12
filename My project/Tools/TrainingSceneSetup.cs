using System;
using System.IO;
using System.Linq;
using PickleVerse.LocalMatch;
using PickleVerse.FrontEnd;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TrainingSceneSetup
{
    static readonly Color Mint=new Color(.35f,1,.83f),Gold=new Color(1,.83f,.35f);
    public static object Run()
    {
        if(Application.isPlaying)throw new Exception("Stop Play first.");
        Directory.CreateDirectory("../UnityWork/Training");
        var source=EditorSceneManager.OpenScene("Assets/Scenes/BotMatch.unity",OpenSceneMode.Single);
        var match=UnityEngine.Object.FindAnyObjectByType<BotMatch>();var sourceLight=match.GetComponent<MatchEnvironmentLighting>();
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Training.unity",OpenSceneMode.Additive);
        string backup="../UnityWork/Training/Training.before.unity";if(!File.Exists(backup))EditorSceneManager.SaveScene(scene,backup,true);
        foreach(var old in scene.GetRootGameObjects())UnityEngine.Object.DestroyImmediate(old);
        SceneManager.SetActiveScene(scene);
        var arena=Clone(source.GetRootGameObjects().Single(g=>g.name=="Arena"),scene,"Arena");
        var sun=Clone(sourceLight.sun.gameObject,scene,"Training Sun").GetComponent<Light>();
        var camera=Clone(sourceLight.matchCamera.gameObject,scene,"Training Camera").GetComponent<Camera>();
        var follow=camera.GetComponent<MatchCamera>();if(follow)UnityEngine.Object.DestroyImmediate(follow);
        var events=Clone(source.GetRootGameObjects().Single(g=>g.GetComponent<UnityEngine.EventSystems.EventSystem>()),scene,"Training EventSystem");
        var root=new GameObject("Training Academy");var director=root.AddComponent<TrainingDirector>();
        director.player=Clone(match.human.gameObject,scene,"Player").GetComponent<MatchActor>();
        director.coach=Clone(match.bot.gameObject,scene,"Coach").GetComponent<MatchActor>();
        director.partner=Clone(match.bot.gameObject,scene,"AI Partner").GetComponent<MatchActor>();
        director.opponent=Clone(match.bot.gameObject,scene,"Opponent Two").GetComponent<MatchActor>();
        director.player.playerId=0;director.coach.playerId=director.partner.playerId=director.opponent.playerId=1;
        director.ball=Clone(match.ball.gameObject,scene,"Training Ball").transform;director.trail=director.ball.GetComponentInChildren<TrailRenderer>();director.view=camera;
        var lighting=root.AddComponent<MatchEnvironmentLighting>();lighting.daySky=sourceLight.daySky;lighting.nightSky=sourceLight.nightSky;lighting.sun=sun;lighting.matchCamera=camera;lighting.lampsOff=sourceLight.lampsOff;lighting.lampsOn=sourceLight.lampsOn;
        lighting.floodlights=arena.GetComponentsInChildren<Light>(true).Where(l=>l.type==LightType.Spot).ToArray();lighting.lampFaces=arena.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="LampHousing").Select(t=>t.GetComponent<Renderer>()).ToArray();lighting.useLocalLaunchSettings=false;lighting.selectedNight=sourceLight.selectedNight;lighting.Apply(lighting.selectedNight);
        if(!AssetDatabase.IsValidFolder("Assets/LocalMatchContent/Training"))AssetDatabase.CreateFolder("Assets/LocalMatchContent","Training");
        const string matPath="Assets/LocalMatchContent/Training/Goal Lines.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if(!material){material=new Material(Shader.Find("Sprites/Default")){name="Training Goal Lines"};AssetDatabase.CreateAsset(material,matPath);}
        director.targetRing=Line("Target Goal",root.transform,material,Gold,.045f);director.receiveRing=Line("Receive Goal",root.transform,material,Mint,.045f);director.recoveryRing=Line("Recovery Goal",root.transform,material,new Color(.4f,.7f,1),.04f);
        director.kitchenNear=Line("Near Kitchen",root.transform,material,Mint,.025f);director.kitchenFar=Line("Far Kitchen",root.transform,material,Mint,.025f);
        Rectangle(director.kitchenNear,-PickleVerse.Rules.Court.Kitchen,0);Rectangle(director.kitchenFar,0,PickleVerse.Rules.Court.Kitchen);
        director.teamLink=Line("Team Spacing",root.transform,material,new Color(.45f,.8f,1),.035f);director.teamLink.positionCount=2;
        var canvasRoot=new GameObject("Training HUD",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
        var canvas=canvasRoot.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        var scaler=canvasRoot.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
        var hud=canvasRoot.AddComponent<TrainingHud>();director.hud=hud;
        hud.Build(director,AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/FrontEndContent/Mode Vietnamese Font.asset"),AssetDatabase.LoadAssetAtPath<Material>("Assets/FrontEndContent/AnimeUI/Glass.mat"),AssetDatabase.LoadAssetAtPath<Material>("Assets/FrontEndContent/AnimeUI/Tab.mat"),AssetDatabase.LoadAssetAtPath<Material>("Assets/FrontEndContent/AnimeUI/Gold Action.mat"));
        var controls=UnityEngine.Object.Instantiate(match.hud.touchControls,canvasRoot.transform);controls.name="TouchControls";director.touchRoot=controls;director.touchControls=controls.GetComponentsInChildren<TouchControl>(true);foreach(var c in director.touchControls)c.match=null;
        controls.SetActive(false);director.partner.gameObject.SetActive(false);director.opponent.gameObject.SetActive(false);
        director.ball.gameObject.SetActive(false);camera.transform.position=new Vector3(-6,11,-12);camera.transform.LookAt(new Vector3(0,.2f,0));
        hud.catalogue.gameObject.SetActive(true);hud.briefing.gameObject.SetActive(false);hud.practice.gameObject.SetActive(false);hud.results.gameObject.SetActive(false);hud.pausePanel.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);EditorSceneManager.CloseScene(source,true);
        var result=new{scene=scene.path,arena=arena.GetComponentsInChildren<Renderer>(true).Length,lights=lighting.floodlights.Length,cameraCount=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).Count(),missing=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))};
        ConfigureEntryPoints();EditorSceneManager.OpenScene("Assets/Scenes/Training.unity");return result;
    }
    static GameObject Clone(GameObject original,Scene scene,string name){var clone=UnityEngine.Object.Instantiate(original);clone.name=name;SceneManager.MoveGameObjectToScene(clone,scene);return clone;}
    static LineRenderer Line(string name,Transform parent,Material material,Color color,float width)
    {var root=new GameObject(name);root.transform.SetParent(parent,false);var line=root.AddComponent<LineRenderer>();line.sharedMaterial=material;line.startColor=line.endColor=color;line.widthMultiplier=width;line.useWorldSpace=true;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;return line;}
    static void Rectangle(LineRenderer line,float from,float to){float w=PickleVerse.Rules.Court.HalfWidth;line.positionCount=5;line.SetPositions(new[]{new Vector3(-w,.031f,from),new Vector3(w,.031f,from),new Vector3(w,.031f,to),new Vector3(-w,.031f,to),new Vector3(-w,.031f,from)});}
    public static object ConfigureEntryPoints()
    {
        var mode=EditorSceneManager.OpenScene("Assets/Scenes/UI/ModeSelection.unity");Backup(mode);
        var menu=UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>();var button=menu.modePage.transform.Find("TrainingMode").GetComponent<UnityEngine.UI.Button>();button.interactable=true;
        for(int i=button.onClick.GetPersistentEventCount()-1;i>=0;i--)UnityEventTools.RemovePersistentListener(button.onClick,i);UnityEventTools.AddPersistentListener(button.onClick,menu.OpenTraining);
        foreach(var t in button.GetComponentsInChildren<TMP_Text>(true)){if(t.text.Contains("SẮP")||t.text.Contains("CHƯA"))t.text="CHƠI NGAY";else if(t.text.Contains("sau")||t.text.Contains("đang"))t.text="Bài kỹ thuật, combo và phối hợp đánh đôi.\nChấm sao • thưởng Tokenpad.";}
        EditorSceneManager.MarkSceneDirty(mode);EditorSceneManager.SaveScene(mode);
        var home=EditorSceneManager.OpenScene("Assets/Scenes/UI/Menu.unity");Backup(home);
        var progress=UnityEngine.Object.FindAnyObjectByType<PlayerProgressionMenu>();if(!progress)throw new Exception("Existing player progression UI not found.");progress.ConfigureTrainingUI();EditorUtility.SetDirty(progress);EditorSceneManager.MarkSceneDirty(home);EditorSceneManager.SaveScene(home);
        return new{trainingButton=true,progressionLinked=true};
    }
    static void Backup(Scene scene){string path="../UnityWork/Training/"+scene.name+".before.unity";if(!File.Exists(path))EditorSceneManager.SaveScene(scene,path,true);}
}
