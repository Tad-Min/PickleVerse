using System;
using System.IO;
using System.Linq;
using PickleVerse.LocalMatch;
using PickleVerse.Settings;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BotMatchVictorySetup
{
    const string Content="Assets/LocalMatchContent/Victory";
    static readonly Color Gold=new Color(.95f,.81f,.49f),Navy=new Color(.055f,.08f,.12f);
    static TMP_FontAsset font;
    public static object Run()
    {
        if(Application.isPlaying)throw new Exception("Stop Play before editing BotMatch.");
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/BotMatch.unity")throw new Exception("Open BotMatch first.");
        var match=UnityEngine.Object.FindAnyObjectByType<BotMatch>();var hud=match.hud;
        if(!hud.resultActions)throw new Exception("Match result buttons are missing.");
        Directory.CreateDirectory("../UnityWork/BotMatchVictory");
        string backup="../UnityWork/BotMatchVictory/BotMatch.before.unity";
        if(!File.Exists(backup))EditorSceneManager.SaveScene(scene,backup,true);
        if(!AssetDatabase.IsValidFolder(Content))AssetDatabase.CreateFolder("Assets/LocalMatchContent","Victory");
        var victory=match.GetComponent<BotMatchVictory>();if(!victory)victory=match.gameObject.AddComponent<BotMatchVictory>();match.victory=victory;
        victory.match=match;victory.followCamera=hud.GetComponent<Canvas>().worldCamera.GetComponent<MatchCamera>();
        victory.femaleWin=Clip("female","PKV_Dance_Win");victory.maleSuperSaiyan=Clip("male","PKV_Dance_SuperSaiyan");victory.maleClap=Clip("male","PKV_Dance_Clap");
        victory.gameplayHud=hud.transform.Cast<Transform>().Where(t=>new[]{"Scoreboard","RefereeNotice","ServeClock","TechniqueBar","Settings","Help","Touch","Timeout","Difficulty","TouchControls","ControlsHelp"}.Contains(t.name)).Select(t=>t.gameObject).ToArray();
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/FrontEndContent/Mode Vietnamese Font.asset");
        var root=(RectTransform)hud.resultActions.transform;
        root.anchorMin=root.anchorMax=root.pivot=new Vector2(.5f,0);root.anchoredPosition=new Vector2(0,20);root.sizeDelta=new Vector2(620,240);
        victory.resultOpacity=root.GetComponent<CanvasGroup>();if(!victory.resultOpacity)victory.resultOpacity=root.gameObject.AddComponent<CanvasGroup>();
        victory.resultOpacity.alpha=0;victory.resultOpacity.blocksRaycasts=false;victory.resultOpacity.interactable=false;
        var shade=Rect("Victory Shade",root);shade.anchorMin=shade.anchorMax=new Vector2(.5f,0);shade.pivot=new Vector2(.5f,0);shade.anchoredPosition=new Vector2(0,-20);shade.sizeDelta=new Vector2(4096,290);shade.SetAsFirstSibling();
        var shadeImage=shade.GetComponent<UnityEngine.UI.Image>();if(!shadeImage)shadeImage=shade.gameObject.AddComponent<UnityEngine.UI.Image>();shadeImage.raycastTarget=false;
        var shadeMaterial=AssetDatabase.LoadAssetAtPath<Material>(Content+"/Victory Shade.mat");
        if(!shadeMaterial){shadeMaterial=new Material(Shader.Find("PickleVerse/UI/Arena Glass")){name="Victory Shade"};AssetDatabase.CreateAsset(shadeMaterial,Content+"/Victory Shade.mat");}
        shadeMaterial.SetColor("_Top",new Color(.02f,.035f,.06f,0));shadeMaterial.SetColor("_Bottom",new Color(.015f,.025f,.045f,.92f));shadeMaterial.SetColor("_Border",Color.clear);shadeMaterial.SetFloat("_Cut",0);EditorUtility.SetDirty(shadeMaterial);AssetDatabase.SaveAssetIfDirty(shadeMaterial);shadeImage.material=shadeMaterial;
        victory.winnerTitle=Label("Winner",root,"Thắng",new Vector2(0,.52f),Vector2.one,76,Gold);
        victory.winnerTitle.fontStyle=FontStyles.Bold|FontStyles.Italic;victory.winnerTitle.characterSpacing=10;
        victory.winnerTitle.enableVertexGradient=true;
        victory.winnerTitle.color=Color.white;victory.winnerTitle.colorGradient=new VertexGradient(new Color(1,.97f,.82f),new Color(1,.97f,.82f),new Color(.87f,.59f,.23f),new Color(.87f,.59f,.23f));
        var goldMaterial=AssetDatabase.LoadAssetAtPath<Material>(Content+"/Winner Gold.mat");
        if(!goldMaterial){goldMaterial=new Material(font.material){name="Winner Gold"};AssetDatabase.CreateAsset(goldMaterial,Content+"/Winner Gold.mat");}
        goldMaterial.SetColor("_FaceColor",Color.white);goldMaterial.SetColor("_OutlineColor",new Color(.31f,.17f,.045f,1));goldMaterial.SetFloat("_OutlineWidth",.10f);
        goldMaterial.SetColor("_UnderlayColor",new Color(.02f,.035f,.055f,.70f));goldMaterial.SetFloat("_UnderlayOffsetY",-.32f);goldMaterial.SetFloat("_UnderlaySoftness",.26f);goldMaterial.EnableKeyword("UNDERLAY_ON");
        EditorUtility.SetDirty(goldMaterial);AssetDatabase.SaveAssetIfDirty(goldMaterial);victory.winnerTitle.fontSharedMaterial=goldMaterial;
        var mask=Rect("Sheen Window",victory.winnerTitle.transform);mask.anchorMin=mask.anchorMax=mask.pivot=new Vector2(.5f,.5f);mask.sizeDelta=new Vector2(80,116);
        var clipping=mask.GetComponent<UnityEngine.UI.RectMask2D>();if(!clipping)clipping=mask.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();clipping.softness=new Vector2Int(14,0);
        var sheen=Label("Winner Sheen",mask,"Thắng",Vector2.zero,Vector2.one,76,new Color(1,1,.94f,.92f));
        sheen.fontStyle=victory.winnerTitle.fontStyle;sheen.characterSpacing=victory.winnerTitle.characterSpacing;
        sheen.rectTransform.anchorMin=sheen.rectTransform.anchorMax=sheen.rectTransform.pivot=new Vector2(.5f,.5f);sheen.rectTransform.sizeDelta=new Vector2(620,115.2f);
        victory.sheenMask=mask;victory.sheenText=sheen.rectTransform;
        hud.resultSummary.font=font;Place(hud.resultSummary.rectTransform,new Vector2(0,.31f),new Vector2(1,.49f));hud.resultSummary.fontSize=18;hud.resultSummary.color=new Color(.86f,.9f,.96f);hud.resultSummary.fontStyle=FontStyles.Bold;
        var replay=root.Find("Replay").GetComponent<UnityEngine.UI.Button>();var mode=root.Find("ChooseMode").GetComponent<UnityEngine.UI.Button>();
        StyleButton(replay,new Vector2(.055f,.01f),new Vector2(.48f,.25f),"CHƠI LẠI",true);
        StyleButton(mode,new Vector2(.52f,.01f),new Vector2(.945f,.25f),"CHỌN CHẾ ĐỘ",false);
        var leftNav=replay.navigation;leftNav.mode=UnityEngine.UI.Navigation.Mode.Explicit;leftNav.selectOnRight=mode;leftNav.selectOnUp=leftNav.selectOnDown=leftNav.selectOnLeft=null;replay.navigation=leftNav;
        var rightNav=leftNav;rightNav.selectOnRight=null;rightNav.selectOnLeft=replay;mode.navigation=rightNav;
        var key=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Victory Portrait Light")??new GameObject("Victory Portrait Light");
        var light=key.GetComponent<Light>();if(!light)light=key.AddComponent<Light>();light.type=LightType.Spot;light.range=9;light.spotAngle=65;light.innerSpotAngle=45;light.intensity=3.5f;light.color=new Color(.98f,.95f,.9f);light.shadows=LightShadows.None;light.enabled=false;victory.portraitLight=light;
        ConfigureResultStyles(victory,root);
        root.gameObject.SetActive(false);EditorUtility.SetDirty(match);EditorUtility.SetDirty(victory);EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return new{scene=scene.path,clips=new[]{victory.femaleWin.name,victory.maleSuperSaiyan.name,victory.maleClap.name},buttons=new[]{replay.onClick.GetPersistentMethodName(0),mode.onClick.GetPersistentMethodName(0)},hiddenHud= victory.gameplayHud.Length};
    }

    public static object PlayerResults()
    {
        if(Application.isPlaying)throw new Exception("Stop Play before editing the result UI.");
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/BotMatch.unity")throw new Exception("Open BotMatch first.");
        var match=UnityEngine.Object.FindAnyObjectByType<BotMatch>();var victory=match.victory;
        if(!victory || !victory.winnerTitle)throw new Exception("Result presentation is missing.");
        Directory.CreateDirectory("../UnityWork/BotMatchVictory");
        string backup="../UnityWork/BotMatchVictory/BotMatch.before-player-pov.unity";
        if(!File.Exists(backup))EditorSceneManager.SaveScene(scene,backup,true);
        font=victory.winnerTitle.font;
        ConfigureResultStyles(victory,(RectTransform)match.hud.resultActions.transform);
        EditorUtility.SetDirty(victory);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return new{scene=scene.path,win="Thắng",loss="Thua",alwaysLocalPlayer=true};
    }

    static void ConfigureResultStyles(BotMatchVictory victory,RectTransform root)
    {
        victory.winTitleMaterial=AssetDatabase.LoadAssetAtPath<Material>(Content+"/Winner Gold.mat");
        var gray=AssetDatabase.LoadAssetAtPath<Material>(Content+"/Result Gray.mat");
        if(!gray){gray=new Material(font.material){name="Result Gray"};AssetDatabase.CreateAsset(gray,Content+"/Result Gray.mat");}
        gray.SetColor("_FaceColor",Color.white);gray.SetColor("_OutlineColor",new Color(.11f,.12f,.14f));gray.SetFloat("_OutlineWidth",.10f);
        gray.SetColor("_UnderlayColor",new Color(.015f,.02f,.03f,.85f));gray.SetFloat("_UnderlayOffsetY",-.32f);gray.SetFloat("_UnderlaySoftness",.35f);gray.EnableKeyword("UNDERLAY_ON");
        EditorUtility.SetDirty(gray);AssetDatabase.SaveAssetIfDirty(gray);victory.lossTitleMaterial=gray;
        var dimmer=Rect("Defeat Shade",root);dimmer.anchorMin=dimmer.anchorMax=new Vector2(.5f,0);dimmer.pivot=new Vector2(.5f,0);
        dimmer.anchoredPosition=new Vector2(0,-20);dimmer.sizeDelta=new Vector2(4096,4096);dimmer.SetAsFirstSibling();
        var image=dimmer.GetComponent<UnityEngine.UI.Image>();if(!image)image=dimmer.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color=new Color(.018f,.024f,.036f,.24f);image.raycastTarget=false;victory.defeatShade=image;dimmer.gameObject.SetActive(false);
        victory.winnerTitle.text="Thắng";victory.winnerTitle.fontSharedMaterial=victory.winTitleMaterial;
        if(victory.sheenText)victory.sheenText.GetComponent<TMP_Text>().text="Thắng";
    }
    static AnimationClip Clip(string gender,string name){var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/LocalMatchContent/Clips/"+gender+"/"+name+".anim");if(!clip)throw new Exception("Missing "+name);return clip;}
    static void StyleButton(UnityEngine.UI.Button button,Vector2 min,Vector2 max,string value,bool primary)
    {
        Place((RectTransform)button.transform,min,max);var image=button.GetComponent<UnityEngine.UI.Image>();image.color=Color.white;image.raycastTarget=true;
        image.material=AssetDatabase.LoadAssetAtPath<Material>("Assets/FrontEndContent/AnimeUI/"+(primary?"Gold Action":"Tab")+".mat");
        var label=button.GetComponentInChildren<TMP_Text>();label.font=font;label.text=value;label.fontSize=18;label.color=primary?Navy:new Color(.95f,.96f,.98f);label.fontStyle=FontStyles.Bold;label.raycastTarget=false;
        var focus=Rect("Focus Edge",button.transform);Place(focus,new Vector2(.1f,.015f),new Vector2(.9f,.04f));var edge=focus.GetComponent<UnityEngine.UI.Image>();if(!edge)edge=focus.gameObject.AddComponent<UnityEngine.UI.Image>();edge.color=new Color(Gold.r,Gold.g,Gold.b,0);edge.raycastTarget=false;
        var motion=button.GetComponent<UiButtonMotion>();if(!motion)motion=button.gameObject.AddComponent<UiButtonMotion>();motion.focus=edge;
    }
    static TMP_Text Label(string name,Transform parent,string text,Vector2 min,Vector2 max,float size,Color color)
    {
        var rect=Rect(name,parent);Place(rect,min,max);var label=rect.GetComponent<TextMeshProUGUI>();if(!label)label=rect.gameObject.AddComponent<TextMeshProUGUI>();label.font=font;label.text=text;label.fontSize=size;label.color=color;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;return label;
    }
    static RectTransform Rect(string name,Transform parent){var old=parent.Find(name);var rect=old?(RectTransform)old:new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();if(!old)rect.SetParent(parent,false);rect.gameObject.layer=5;return rect;}
    static void Place(RectTransform rect,Vector2 min,Vector2 max){rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;}
}
