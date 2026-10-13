using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using PickleVerse.LocalMatch;

public static class StadiumRenderSetup
{
    const string Prefab="Assets/LocalMatchContent/StadiumEnhanced/PlayableStadium.prefab";
    public static void Configure(StadiumEnvironment env)
    {
        env.ConfigureFixtureGroups();
        env.roofNightIntensity=80;env.poleNightIntensity=45;env.standNightIntensity=8;
        foreach(var r in env.GetComponentsInChildren<Renderer>())
        {
            if(r.name.Contains("WarmLight")||new[]{"BlueSeats","PlantLeaves","Terracotta","SportsBanner","VietnamSatin"}.Any(n=>r.name.Contains(n)))r.shadowCastingMode=ShadowCastingMode.Off;
        }
        env.ApplyNight(0);EditorUtility.SetDirty(env);
    }
    public static object Materials()
    {
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(StadiumEnvironment.SourceAsset);
        var sources=model.GetComponentsInChildren<Renderer>().Select(r=>r.sharedMaterial).Distinct().ToArray();var changed=new System.Collections.Generic.List<string>();
        foreach(var source in sources)
        {
            if(!source.HasProperty("metallicFactor")||source.GetFloat("metallicFactor")>.05f||source.name.Contains("BlueSeats"))continue;
            string name=string.Concat(source.name.Select(c=>char.IsLetterOrDigit(c)?c:'_'));
            var target=AssetDatabase.LoadAssetAtPath<Material>("Assets/LocalMatchContent/StadiumEnhanced/Materials/"+name+".mat");if(!target)continue;
            var m=new Material(Shader.Find("Universal Render Pipeline/Simple Lit")){name=source.name,enableInstancing=true,doubleSidedGI=source.doubleSidedGI};
            m.SetColor("_BaseColor",source.GetColor("baseColorFactor"));m.SetTexture("_BaseMap",source.GetTexture("baseColorTexture"));
            var st=source.GetVector("baseColorTexture_ST");m.SetTextureScale("_BaseMap",new Vector2(st.x,st.y));m.SetTextureOffset("_BaseMap",new Vector2(st.z,st.w));
            m.SetFloat("_Cull",source.GetFloat("_Cull"));m.SetFloat("_Smoothness",1-source.GetFloat("roughnessFactor"));m.SetColor("_SpecColor",new Color(.08f,.08f,.08f,1-source.GetFloat("roughnessFactor")));m.EnableKeyword("_SPECULAR_COLOR");
            var normal=source.GetTexture("normalTexture");if(normal){m.SetTexture("_BumpMap",NormalMap(normal,name,source.GetFloat("normalTexture_scale")));m.EnableKeyword("_NORMALMAP");}
            if(source.GetFloat("_AlphaClip")>0){m.SetFloat("_AlphaClip",1);m.SetFloat("_Cutoff",source.GetFloat("alphaCutoff"));m.EnableKeyword("_ALPHATEST_ON");m.renderQueue=2450;}
            EditorUtility.CopySerialized(m,target);EditorUtility.SetDirty(target);AssetDatabase.SaveAssetIfDirty(target);UnityEngine.Object.DestroyImmediate(m);changed.Add(name);
        }
        return changed;
    }
    static Texture2D NormalMap(Texture source,string name,float strength)
    {
        // glTF normal textures contain raw RGB normals. Unity's built-in shader expects importer packing.
        string folder="Assets/LocalMatchContent/StadiumEnhanced/Textures";Directory.CreateDirectory(folder);
        string path=folder+"/"+name+"_Normal.png";var existing=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(existing)return existing;
        var rt=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        var previous=RenderTexture.active;var image=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false,true);
        try
        {
            Graphics.Blit(source,rt);RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
            var pixels=image.GetPixels();for(int i=0;i<pixels.Length;i++){var p=pixels[i];var n=new Vector3((p.r*2-1)*strength,(p.g*2-1)*strength,p.b*2-1).normalized;pixels[i]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1);}image.SetPixels(pixels);image.Apply();
            File.WriteAllBytes(path,image.EncodeToPNG());
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);}
        AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.NormalMap;importer.sRGBTexture=false;importer.mipmapEnabled=true;importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    public static object Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        var root=PrefabUtility.LoadPrefabContents(Prefab);
        try{Configure(root.GetComponent<StadiumEnvironment>());PrefabUtility.SaveAsPrefabAsset(root,Prefab);}
        finally{PrefabUtility.UnloadPrefabContents(root);}
        var rows=new System.Collections.Generic.List<object>();
        foreach(string name in new[]{"BotMatch","Training","Tutorial","MultiplayerCourt"})
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity");int overlays=0;
            foreach(var c in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>(true)))
            {
                if(c.renderMode!=RenderMode.ScreenSpaceCamera)continue;
                c.renderMode=RenderMode.ScreenSpaceOverlay;c.worldCamera=null;EditorUtility.SetDirty(c);overlays++;
            }
            foreach(var camera in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)))
            {
                var data=camera.GetUniversalAdditionalCameraData();data.requiresColorOption=CameraOverrideOption.Off;EditorUtility.SetDirty(data);
            }
            EditorSceneManager.SaveScene(scene);rows.Add(new{scene=name,overlays});
        }
        // SSAO retains contact shading; sampling at half resolution avoids a full-screen high-cost pass.
        var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
        foreach(var feature in renderer.rendererFeatures.Where(f=>f.GetType().Name=="ScreenSpaceAmbientOcclusion"))
        {
            var s=new SerializedObject(feature);s.FindProperty("m_Settings.Downsample").boolValue=true;s.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(feature);AssetDatabase.SaveAssetIfDirty(feature);
        }
        EditorSceneManager.OpenScene("Assets/Scenes/UI/Menu.unity");
        return new{scenes=rows,poleSources=8,roofSources=16,standSources=8,roofIntensity=80,ssaoHalfResolution=true};
    }
}
