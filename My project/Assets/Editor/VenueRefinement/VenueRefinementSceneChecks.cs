using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.LocalMatch;
using Newtonsoft.Json;

// All scene inspection edits are temporary and are discarded on restoring the original scene.
public static class VenueRefinementSceneChecks
{
    static readonly string Work=Path.GetFullPath("../BlenderWork/VenueRefinement20261025");
    static string Original()=>SceneManager.GetActiveScene().path;
    static void Restore(string path)
    {
        if(string.IsNullOrEmpty(path))EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        else EditorSceneManager.OpenScene(path);
    }
    public static object ValidateScenes()
    {
        if(EditorApplication.isPlaying||SceneManager.GetActiveScene().isDirty)throw new Exception("Clean Edit mode required");
        string previous=Original();var checks=new List<object>();
        try
        {
            foreach(string path in new[]{"Assets/Scenes/UI/Menu.unity","Assets/Scenes/BotMatch.unity","Assets/Scenes/MultiplayerCourt.unity","Assets/Scenes/Training.unity","Assets/Scenes/Tutorial.unity","Assets/Scenes/Replay.unity"})
            {
                var scene=EditorSceneManager.OpenScene(path);var roots=scene.GetRootGameObjects();
                var transforms=roots.SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
                var venues=roots.SelectMany(g=>g.GetComponentsInChildren<StadiumEnvironment>(true)).ToArray();
                checks.Add(new{name=scene.name+" no missing scripts",passed=transforms.All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0)});
                checks.Add(new{name=scene.name+" no missing visible meshes/materials",passed=transforms.Select(t=>t.GetComponent<MeshRenderer>()).Where(r=>r&&r.enabled&&r.gameObject.activeInHierarchy).All(r=>r.sharedMaterials.All(m=>m&&m.shader&&m.shader.isSupported)&&r.GetComponent<MeshFilter>()&&r.GetComponent<MeshFilter>().sharedMesh)});
                if(scene.name=="Menu"||scene.name=="BotMatch"||scene.name=="MultiplayerCourt")
                {
                    checks.Add(new{name=scene.name+" uses upgraded venue prefab",passed=venues.Length==1&&venues[0].GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Refined_Garden_Grass")});
                    if(venues.Length==1)
                    {
                        var e=venues[0];checks.Add(new{name=scene.name+" court grounded on rules plane",passed=Mathf.Abs(e.competitionSurface.GetComponent<Renderer>().bounds.max.y)<.001f});
                        var m=roots.SelectMany(g=>g.GetComponentsInChildren<BotMatchIntro>(true)).FirstOrDefault();
                        if(m)checks.Add(new{name=scene.name+" entry cinematic retains venue anchors",passed=m.playerEntrance==e.nearEntrance&&m.opponentEntrance==e.farEntrance});
                        var v=roots.SelectMany(g=>g.GetComponentsInChildren<BotMatchVictory>(true)).FirstOrDefault();
                        if(v)checks.Add(new{name=scene.name+" result camera retains venue anchors",passed=v.nearPresentation==e.nearResult&&v.farPresentation==e.farResult});
                    }
                }
            }
            var result=new{checks};File.WriteAllText(Work+"/scene-validation.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
        }
        finally{Restore(previous);}
    }
    static void Shot(Camera camera,string name,int width=1280,int height=720,string output=null)
    {
        var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);var old=camera.targetTexture;var active=RenderTexture.active;
        var image=new Texture2D(width,height,TextureFormat.RGB24,false);
        try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(output??(Work+"/"+name+".png"),image.EncodeToPNG());}
        finally{camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(image);}
    }
    public static object UpdateSelectionPreviews()
    {
        if(EditorApplication.isPlaying||SceneManager.GetActiveScene().isDirty)throw new Exception("Clean Edit mode required");
        string previous=Original();var updated=new List<string>();
        try
        {
            foreach(string id in new[]{"Professional","LowPoly"})
            {
                EditorSceneManager.OpenScene("Assets/Scenes/BotMatch.unity");var lighting=UnityEngine.Object.FindAnyObjectByType<MatchEnvironmentLighting>();
                lighting.stadium.gameObject.SetActive(false);lighting.stadium=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Courts/"+id+".prefab")).GetComponent<StadiumEnvironment>();
                var camera=lighting.matchCamera;camera.transform.position=new Vector3(0,6.5f,-12.5f);camera.transform.LookAt(new Vector3(0,0,.5f));camera.fieldOfView=54;
                foreach(bool night in new[]{false,true})
                {
                    lighting.SelectForMatch(night);string path="Assets/FrontEndContent/Artwork/Courts/"+id+(night?"_Night":"_Day")+".png";
                    Shot(camera,"preview",640,360,path);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.mipmapEnabled=false;importer.maxTextureSize=512;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();updated.Add(path);
                }
            }
            var dependencies=new HashSet<string>(AssetDatabase.GetDependencies("Assets/Scenes/UI/ModeSelection.unity",true));
            var checks=updated.Select(path=>{var t=AssetDatabase.LoadAssetAtPath<Texture2D>(path);return new{name="ModeSelection preview "+Path.GetFileName(path),passed=t&&t.width<=512&&t.height>0&&dependencies.Contains(path)};}).ToArray();
            File.WriteAllText(Work+"/preview-validation.json",JsonConvert.SerializeObject(new{checks},Formatting.Indented));
            return new{updated,checks};
        }
        finally{Restore(previous);}
    }
    public static object Pictures()
    {
        if(EditorApplication.isPlaying||SceneManager.GetActiveScene().isDirty)throw new Exception("Clean Edit mode required");
        string previous=Original();var paths=new List<string>();
        try
        {
            foreach(string id in new[]{"Professional","LowPoly"})
            {
                var scene=EditorSceneManager.OpenScene("Assets/Scenes/BotMatch.unity");
                var lighting=UnityEngine.Object.FindAnyObjectByType<MatchEnvironmentLighting>();
                var original=lighting.stadium;var venue=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Courts/"+id+".prefab")).GetComponent<StadiumEnvironment>();
                original.gameObject.SetActive(false);lighting.stadium=venue;
                var camera=lighting.matchCamera;camera.transform.position=new Vector3(0,6.5f,-12.5f);camera.transform.LookAt(new Vector3(0,0,.5f));camera.fieldOfView=54;
                foreach(bool night in new[]{false,true})
                {
                    lighting.SelectForMatch(night);string name="Unity-"+id+(night?"-Night":"-Day");Shot(camera,name);paths.Add(Work+"/"+name+".png");
                }
                camera.transform.position=new Vector3(14,11,-19);camera.transform.LookAt(new Vector3(0,0,0));camera.fieldOfView=58;lighting.SelectForMatch(false);
                Shot(camera,"Unity-"+id+"-Wide");paths.Add(Work+"/Unity-"+id+"-Wide.png");
            }
            var menu=EditorSceneManager.OpenScene("Assets/Scenes/UI/Menu.unity");var cam=Camera.main;
            if(cam){Shot(cam,"Unity-Menu");paths.Add(Work+"/Unity-Menu.png");}
            return new{paths};
        }
        finally{Restore(previous);}
    }
}
