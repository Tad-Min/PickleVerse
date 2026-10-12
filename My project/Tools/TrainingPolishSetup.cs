using System;
using System.IO;
using PickleVerse.LocalMatch;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TrainingPolishSetup
{
    public static object Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play mode before authoring.");
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Training.unity");
        var d=UnityEngine.Object.FindAnyObjectByType<TrainingDirector>();
        if(!d||!d.hud||!d.hud.safeArea)throw new Exception("Training HUD is missing.");
        Directory.CreateDirectory("../UnityWork/TrainingPolish");
        EditorSceneManager.SaveScene(scene,"../UnityWork/TrainingPolish/Training.before.unity",true);
        d.hud.EnsureAnnouncements();
        var a=d.hud.announcements;
        var background=a.panel.GetComponent<UnityEngine.UI.Image>();background.material=null;background.color=new Color(.025f,.06f,.09f,.96f);
        EditorUtility.SetDirty(d.hud);EditorUtility.SetDirty(a);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return new {panel=a.panel.name,a.externallyDriven,raycasts=a.opacity.blocksRaycasts,font=a.headline.font.name,scene=scene.path};
    }
    public static object Font()
    {
        var font=AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/FrontEndContent/Mode Vietnamese Font.asset");
        string missing;
        try
        {
            font.atlasPopulationMode=TMPro.AtlasPopulationMode.Dynamic;
            if(!font.TryAddCharacters("Ũũ",out missing))throw new Exception("Missing glyphs: "+missing);
            foreach(var texture in font.atlasTextures)if(!AssetDatabase.Contains(texture))AssetDatabase.AddObjectToAsset(texture,font);
        }
        finally{font.atlasPopulationMode=TMPro.AtlasPopulationMode.Static;}
        EditorUtility.SetDirty(font);foreach(var texture in font.atlasTextures)EditorUtility.SetDirty(texture);AssetDatabase.SaveAssetIfDirty(font);
        return new {font=font.name,has=font.HasCharacter(0x169),mode=font.atlasPopulationMode.ToString(),atlases=font.atlasTextures.Length};
    }
}
