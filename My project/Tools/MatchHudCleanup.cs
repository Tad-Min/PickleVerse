using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using TMPro;
using PickleVerse.Progression;
public static class MatchHudCleanup
{
    public static object Run()
    {
        if(Application.isPlaying)throw new Exception("Stop Play first");
        var fonts=new[]{"Assets/Fonts/Menu Vietnamese Bold.asset","Assets/Fonts/Menu Vietnamese Bold Italic.asset","Assets/LocalMatchContent/Match Vietnamese Font.asset"};
        for(int i=0;i<fonts.Length;i++)
        {
            var original=UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget("../UnityWork/MatchHud20261020/font-baseline-"+i+".asset");var source=original.OfType<TMP_FontAsset>().First();
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fonts[i]);font.ClearFontAssetData(true);var target=new SerializedObject(font);target.CopyFromSerializedProperty(new SerializedObject(source).FindProperty("m_FontFeatureTable"));target.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(font);AssetDatabase.SaveAssetIfDirty(font);
        }
        foreach(string file in new[]{"Assets/Resources/PerformanceTestRunInfo.json","Assets/Resources/PerformanceTestRunSettings.json"})if(File.Exists(file))AssetDatabase.DeleteAsset(file);
        string root=Path.Combine(Application.persistentDataPath,"LocalPlayers"),preferred=Path.Combine(root,"preferred.txt"),originalId=File.ReadAllText("../UnityWork/MatchHud20261020/original-profile.txt").Trim();
        if(!LocalPlayerStore.ValidId(originalId)||Regex.IsMatch(originalId,"^Hud(QA|UI)"))throw new Exception("Invalid original profile receipt");
        if(Regex.IsMatch(File.ReadAllText(preferred).Trim(),"^Hud(QA|UI)[0-9a-f]{8}$"))using(var store=new LocalPlayerStore(root)){store.Open();store.Switch(originalId);}
        int removed=0;foreach(string file in Directory.GetFiles(root))if(Regex.IsMatch(Path.GetFileName(file),"^Hud(QA|UI)[0-9a-f]{8}\\.(json(\\.bak)?|lock)$")){File.Delete(file);removed++;}
        return new{fonts=fonts.Length,removed,originalRestored=File.ReadAllText(preferred).Trim()==originalId};
    }
}
