using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class OfflineCoachSetup
{
    public const string Folder = "Assets/OfflineCoach";
    [MenuItem("PickleVerse/Coach/Install or refresh in Training")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before installing.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Training.unity") throw new InvalidOperationException("Open Training first.");
        var root = GameObject.Find("PlayerRoot");
        if (!root || !root.GetComponent<SentisPoseTracker>()) throw new InvalidOperationException("PlayerRoot tracker is missing.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "OfflineCoach");
        var settings = AssetDatabase.LoadAssetAtPath<CoachSettings>(Folder + "/CoachSettings.asset");
        if (!settings) { settings = ScriptableObject.CreateInstance<CoachSettings>(); AssetDatabase.CreateAsset(settings, Folder + "/CoachSettings.asset"); }
        var coach = root.GetComponent<OfflineCoach>();
        if (!coach) coach = Undo.AddComponent<OfflineCoach>(root);
        var serialized = new SerializedObject(coach);
        serialized.FindProperty("settings").objectReferenceValue = settings; serialized.ApplyModifiedProperties();
        var voice = root.GetComponent<CoachVoicePlayer>();
        Undo.RecordObject(voice, "Assign offline coach clips");
        voice.InsufficientData = Clip("InsufficientData"); voice.StraightKnees = Clip("StraightKnees");
        voice.ShortFinish = Clip("ShortFinish"); voice.Recorded = Clip("Recorded");
        var source = voice.GetComponent<AudioSource>();
        Undo.RecordObject(source, "Configure 2D coach voice");
        source.playOnAwake = false; source.loop = false; source.spatialBlend = 0;
        source.volume = .8f;
        Font();
        TrainingUiSceneSetup.Install();
        EditorUtility.SetDirty(voice); EditorUtility.SetDirty(source);
        EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
        Debug.Log("Offline Coach installed. Vietnamese clips: " + (voice.HasAllClips ? "complete" : "MISSING; text-only until generated."));
    }
    private static AudioClip Clip(string name) =>
        AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/Voice/" + name + ".mp3") ??
        AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/Voice/" + name + ".wav");

    private static TMP_FontAsset Font()
    {
        string path = Folder + "/CoachVietnamese.asset";
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font) return font;
        var source = AssetDatabase.LoadAssetAtPath<UnityEngine.Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
        if (!source) throw new InvalidOperationException("LiberationSans source font missing.");
        font = TMP_FontAsset.CreateFontAsset(source, 80, 8, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDF16, 1024, 1024);
        font.name = "CoachVietnamese";
        string characters = new string(Enumerable.Range(32, 95).Select(i => (char)i).ToArray()) +
            "Coach đã tắt Đang phân tích cú vung Nghỉ giữa hai cú Chuẩn bị forehand tay phải " +
            "Giữ vai, hông và tay trong khung hình. Giọng Việt offline Thiếu clip giọng Việt — chỉ hiện chữ " +
            "Coach: Bật Tắt Giọng Âm lượng";
        foreach (CoachCode code in Enum.GetValues(typeof(CoachCode))) characters += CoachFeedback.Message(code);
        if (!font.TryAddCharacters(characters, out string missing))
            throw new InvalidOperationException("Coach font missing characters: " + missing);
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(font, path);
        AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
        EditorUtility.SetDirty(font); AssetDatabase.SaveAssets();
        return font;
    }
}
