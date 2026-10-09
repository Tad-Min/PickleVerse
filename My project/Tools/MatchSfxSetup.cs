using System;
using System.IO;
using System.Linq;
using PickleVerse.Audio;
using UnityEditor;
using UnityEngine;

public static class MatchSfxSetup
{
    public static object Run()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play before setting up imported audio.");
        string[] files={"smash-normal.wav","smash-serve.wav","smash-strong.wav","Ma_Sat_Giay.wav","foot-step.wav","Countdown.wav","Coi.wav","Coi_Large.wav"};
        foreach(var file in files)
        {
            string path="Assets/Audio/SFX/"+file;
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono=true;importer.loadInBackground=false;
            var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;settings.preloadAudioData=true;
            importer.defaultSampleSettings=settings;importer.SaveAndReimport();
        }
        Directory.CreateDirectory("Assets/Resources/Audio");AssetDatabase.Refresh();
        string libraryPath="Assets/Resources/Audio/Match SFX.asset";
        var library=AssetDatabase.LoadAssetAtPath<SfxLibrary>(libraryPath);
        if(!library){library=ScriptableObject.CreateInstance<SfxLibrary>();AssetDatabase.CreateAsset(library,libraryPath);}
        library.clips=files.Select(f=>AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/"+f)).ToArray();EditorUtility.SetDirty(library);AssetDatabase.SaveAssetIfDirty(library);
        var manager=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset")[0];var audio=new SerializedObject(manager);
        audio.FindProperty("m_DSPBufferSize").intValue=256;audio.FindProperty("m_RequestedDSPBufferSize").intValue=256;audio.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(manager);AssetDatabase.SaveAssets();
        var configuration=AudioSettings.GetConfiguration();configuration.dspBufferSize=256;AudioSettings.Reset(configuration);
        var settingsScene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/UI/Settings.unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
        var note=settingsScene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TMPro.TMP_Text>(true)).First(t=>t.name=="Note"&&t.text.Contains("âm lượng"));
        note.text="Hiệu ứng trận đấu đã hoạt động. Nhạc nền sẽ được bổ sung sau.";
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(settingsScene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(settingsScene);UnityEditor.SceneManagement.EditorSceneManager.CloseScene(settingsScene,true);
        AudioSettings.GetDSPBufferSize(out var size,out var buffers);
        var clips=files.Select(f=>{
            var path="Assets/Audio/SFX/"+f;var c=AssetDatabase.LoadAssetAtPath<AudioClip>(path);var i=(AudioImporter)AssetImporter.GetAtPath(path);
            return new {file=f,seconds=c.length,channels=c.channels,load=c.loadType.ToString(),compression=i.defaultSampleSettings.compressionFormat.ToString(),preload=i.defaultSampleSettings.preloadAudioData,background=i.loadInBackground};
        }).ToArray();
        return new{size,buffers,rate=AudioSettings.outputSampleRate,clips};
    }
}
