using System;
using System.IO;
using System.Linq;
using PickleVerse.CharacterMotion;
using PickleVerse.FrontEnd;
using PickleVerse.Settings;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HomeCharacterSelectionSetup
{
    static TMP_FontAsset font;
    static Material glass, tab, selected;
    static readonly Color Gold = new Color(.91f,.80f,.63f), Muted = new Color(.65f,.70f,.80f);

    public static object Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play before updating Menu.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/UI/Menu.unity") throw new InvalidOperationException("Open Menu first.");
        var lobby = UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();
        if (!lobby || !lobby.safeArea || !lobby.femaleVisual) throw new InvalidOperationException("Lobby references are missing.");
        var maleAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LocalMatchContent/Models/Male.glb");
        lobby.femaleBoom = Clip("female", "PKV_Dance_Boom");
        lobby.maleDance = Clip("male", "PKV_Dance_AllNight");
        lobby.femaleIdle = Clip("female", "PKV_Idle_Ready");
        lobby.maleIdle = Clip("male", "PKV_Idle_Ready");
        if (!maleAsset) throw new InvalidOperationException("Male model is missing.");
        Directory.CreateDirectory("../UnityWork/HomeCharacterSelection");
        string backup = "../UnityWork/HomeCharacterSelection/Menu.before.unity";
        if (!File.Exists(backup)) EditorSceneManager.SaveScene(scene, backup, true);

        var stage = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Home Male");
        if (!stage)
        {
            stage = new GameObject("Home Male");
            stage.transform.SetPositionAndRotation(new Vector3(-1.45f,0,5.7f), Quaternion.identity);
            stage.transform.localScale = lobby.femaleVisual.parent.localScale;
            var visual = UnityEngine.Object.Instantiate(maleAsset, stage.transform);
            visual.name = "Character";
            foreach (var a in visual.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            foreach (var a in visual.GetComponentsInChildren<Animation>(true)) a.enabled = false;
            foreach (var c in visual.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var c in visual.GetComponentsInChildren<Camera>(true)) UnityEngine.Object.DestroyImmediate(c);
            foreach (var l in visual.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(l);
            lobby.maleIdle.SampleAnimation(visual, 0);
            var mesh = new Mesh();
            float floor = float.PositiveInfinity;
            foreach (var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.BakeMesh(mesh);
                foreach (var vertex in mesh.vertices) floor = Mathf.Min(floor, skin.transform.TransformPoint(vertex).y);
            }
            UnityEngine.Object.DestroyImmediate(mesh);
            visual.transform.localPosition += Vector3.up * ((.018f - floor) / stage.transform.lossyScale.y);
            lobby.maleVisual = visual.transform;
            CharacterSecondaryMotion.Attach(visual, false);
        }
        else lobby.maleVisual = stage.transform.Find("Character");
        lobby.maleIdle.SampleAnimation(lobby.maleVisual.gameObject, 0);
        foreach (var skin in lobby.maleVisual.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = false;
        lobby.lobbyCamera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).Single();
        lobby.courtProps = scene.GetRootGameObjects().Single(g => g.name == "Home Court Props").transform;

        font = lobby.timeLabel.font;
        glass = AssetDatabase.LoadAssetAtPath<Material>("Assets/FrontEndContent/AnimeUI/Glass.mat");
        tab = AssetDatabase.LoadAssetAtPath<Material>("Assets/FrontEndContent/AnimeUI/Tab.mat");
        selected = AssetDatabase.LoadAssetAtPath<Material>("Assets/FrontEndContent/AnimeUI/Selected.mat");
        var panel = Rect("Character Selection", lobby.safeArea, 1,.5f,1,.5f);
        Background(panel, glass).enabled = false;
        lobby.danceLabel = null;
        lobby.femaleButton = Button("Select Female", panel, "NỮ", 0,.53f,1,1);
        lobby.maleButton = Button("Select Male", panel, "NAM", 0,0,1,.47f);
        lobby.femaleSelection = Selection(lobby.femaleButton);
        lobby.maleSelection = Selection(lobby.maleButton);
        lobby.maleSelection.gameObject.SetActive(false);
        Wire(lobby.femaleButton, lobby.SelectFemale);
        Wire(lobby.maleButton, lobby.SelectMale);
        ApplyCompactLayout(lobby);
        lobby.dance.SampleAnimation(lobby.femaleVisual.gameObject, 2.1f);
        EditorUtility.SetDirty(lobby);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return new { scene = scene.path, male = lobby.maleVisual.parent.position.ToString(), female = lobby.femaleVisual.parent.position.ToString(), dances = new[]{lobby.dance.name, lobby.femaleBoom.name, lobby.maleDance.name}, cameraCount = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).Count() };
    }

    // Targeted layout update: keep both button instances and their existing callbacks.
    public static object CompactUI()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play before updating Menu.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/UI/Menu.unity") throw new InvalidOperationException("Open Menu first.");
        var lobby = UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();
        if (!lobby || !lobby.femaleButton || !lobby.maleButton) throw new InvalidOperationException("Character selector is missing.");
        Directory.CreateDirectory("../UnityWork/HomeCharacterSelection");
        string backup = "../UnityWork/HomeCharacterSelection/Menu.before-compact-ui.unity";
        if (!File.Exists(backup)) EditorSceneManager.SaveScene(scene, backup, true);
        ApplyCompactLayout(lobby);
        EditorUtility.SetDirty(lobby);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return new { scene=scene.path, size="78 x 94", position="right center", labels=new[]{"NỮ","NAM"} };
    }

    static void ApplyCompactLayout(HomeLobbyPresentation lobby)
    {
        var panel = (RectTransform)lobby.femaleButton.transform.parent;
        panel.anchorMin = panel.anchorMax = new Vector2(1,.5f);
        panel.pivot = new Vector2(1,.5f);
        panel.sizeDelta = new Vector2(78,94);
        panel.anchoredPosition = new Vector2(-24,0);
        var backdrop = panel.GetComponent<UnityEngine.UI.Image>(); if (backdrop) backdrop.enabled = false;
        foreach (var name in new[]{"Heading","Dance Name"}) { var old=panel.Find(name); if(old)old.gameObject.SetActive(false); }
        lobby.danceLabel = null;
        PlaceButton(lobby.femaleButton,.53f,1);
        PlaceButton(lobby.maleButton,0,.47f);
        var nav = lobby.safeArea.Find("Nav/BtnSetting");
        var femaleNavigation = lobby.femaleButton.navigation; femaleNavigation.mode = UnityEngine.UI.Navigation.Mode.Explicit;
        femaleNavigation.selectOnLeft=lobby.playButton;femaleNavigation.selectOnRight=null;femaleNavigation.selectOnUp=null;femaleNavigation.selectOnDown=lobby.maleButton;
        lobby.femaleButton.navigation=femaleNavigation;
        var maleNavigation=femaleNavigation;maleNavigation.selectOnUp=lobby.femaleButton;maleNavigation.selectOnDown=nav?nav.GetComponent<UnityEngine.UI.Button>():null;
        lobby.maleButton.navigation=maleNavigation;
    }

    static void PlaceButton(UnityEngine.UI.Button button,float bottom,float top)
    {
        var r=(RectTransform)button.transform;r.anchorMin=new Vector2(0,bottom);r.anchorMax=new Vector2(1,top);
        r.offsetMin=r.offsetMax=Vector2.zero;
        button.GetComponentInChildren<TMP_Text>().fontSize=16;
    }

    static AnimationClip Clip(string gender, string name)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/LocalMatchContent/Clips/" + gender + "/" + name + ".anim");
        if (!clip) throw new InvalidOperationException("Missing animation: " + gender + "/" + name);
        return clip;
    }
    static void Wire(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction action)
    {
        for (int i = button.onClick.GetPersistentEventCount()-1; i >= 0; i--) UnityEventTools.RemovePersistentListener(button.onClick, i);
        UnityEventTools.AddPersistentListener(button.onClick, action);
    }
    static UnityEngine.UI.Image Selection(UnityEngine.UI.Button button)
    {
        var r = Rect("Selected", button.transform, 0,0,1,1);
        var image = Background(r, selected); r.SetAsFirstSibling();
        return image;
    }
    static UnityEngine.UI.Button Button(string name, Transform parent, string text, float x, float y, float right, float top)
    {
        var r = Rect(name,parent,x,y,right,top); var image = Background(r, tab); image.raycastTarget = true;
        var button = r.GetComponent<UnityEngine.UI.Button>() ?? r.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.12f,1.12f,1.12f);
        colors.pressedColor = new Color(.77f,.84f,.93f); colors.selectedColor = new Color(1.08f,1.08f,1.08f); colors.fadeDuration = .12f; button.colors = colors;
        var label = Label("Label", r,text,0,0,1,1,16,Muted,TextAlignmentOptions.Center);
        var focus = Background(Rect("Focus Edge",r,.13f,.035f,.87f,.07f),null); focus.color = new Color(Gold.r,Gold.g,Gold.b,0);
        var motion = r.GetComponent<UiButtonMotion>() ?? r.gameObject.AddComponent<UiButtonMotion>(); motion.focus = focus; motion.symbol = label.rectTransform; motion.keepFocus = name == "Select Female";
        if (name == "Select Female") label.color = Gold;
        return button;
    }
    static TMP_Text Label(string name, Transform parent, string text, float x, float y, float right, float top, float size, Color color, TextAlignmentOptions alignment)
    {
        var r = Rect(name,parent,x,y,right,top); var label = r.GetComponent<TextMeshProUGUI>() ?? r.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.text = text; label.color = color; label.fontSize = size; label.fontStyle = FontStyles.Bold;
        label.alignment = alignment; label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }
    static UnityEngine.UI.Image Background(RectTransform r, Material material)
    {
        var image = r.GetComponent<UnityEngine.UI.Image>() ?? r.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.material = material; image.color = Color.white; image.raycastTarget = false;
        return image;
    }
    static RectTransform Rect(string name, Transform parent, float x, float y, float right, float top)
    {
        var old = parent.Find(name); var r = old ? (RectTransform)old : new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        if (!old) r.SetParent(parent,false);
        r.gameObject.layer = 5; r.anchorMin = new Vector2(x,y); r.anchorMax = new Vector2(right,top);
        r.offsetMin = r.offsetMax = Vector2.zero; r.pivot = new Vector2(.5f,.5f);
        return r;
    }
}
