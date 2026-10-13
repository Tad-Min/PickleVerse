using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using PickleVerse.LocalMatch;
using PickleVerse.FrontEnd;
using PickleVerse.Rules;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class StadiumSetup
{
    const string Folder="Assets/LocalMatchContent/StadiumEnhanced";
    const string Prefab=Folder+"/PlayableStadium.prefab";
    static T Save<T>(T asset,string path) where T:UnityEngine.Object
    {
        var old=AssetDatabase.LoadAssetAtPath<T>(path);
        if(old){EditorUtility.CopySerialized(asset,old);if(old is Mesh target&&asset is Mesh source){target.vertices=source.vertices;target.triangles=source.triangles;target.UploadMeshData(false);}UnityEngine.Object.DestroyImmediate(asset);EditorUtility.SetDirty(old);AssetDatabase.SaveAssetIfDirty(old);return old;}
        AssetDatabase.CreateAsset(asset,path);AssetDatabase.SaveAssetIfDirty(asset);return asset;
    }
    static void Folders(){foreach(string f in new[]{Folder,Folder+"/Meshes",Folder+"/Materials"})Directory.CreateDirectory(f);AssetDatabase.Refresh();}
    static string Name(string n)=>string.Concat(n.Select(c=>char.IsLetterOrDigit(c)?c:'_'));
    static Material Led(bool on)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=on?"Stadium LED On":"Stadium LED Off"};
        m.color=on?new Color(1,.89f,.68f):new Color(.14f,.15f,.17f);
        m.SetFloat("_Smoothness",.45f);if(on){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(1,.82f,.55f)*1.8f);}
        return Save(m,Folder+"/Materials/LED "+(on?"On":"Off")+".mat");
    }
    static List<Bounds> Islands(MeshFilter filter)
    {
        var v=filter.sharedMesh.vertices.Select(filter.transform.TransformPoint).ToArray();var t=filter.sharedMesh.triangles;var parent=Enumerable.Range(0,v.Length).ToArray();
        Func<int,int> root=null;root=i=>parent[i]==i?i:parent[i]=root(parent[i]);Action<int,int> join=(a,b)=>parent[root(a)]=root(b);
        var weld=new Dictionary<string,int>();for(int i=0;i<v.Length;i++){string key=v[i].ToString("F4");if(weld.TryGetValue(key,out int other))join(i,other);else weld[key]=i;}
        for(int i=0;i<t.Length;i+=3){join(t[i],t[i+1]);join(t[i],t[i+2]);}
        return Enumerable.Range(0,v.Length).GroupBy(root).Select(g=>{var b=new Bounds(v[g.First()],Vector3.zero);foreach(int i in g)b.Encapsulate(v[i]);return b;}).ToList();
    }
    static void Calibrate(MeshFilter filter)
    {
        var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);var vertices=mesh.vertices;float originalTop=filter.GetComponent<Renderer>().bounds.max.y;
        bool ground=filter.name.Contains("CourtBlue")||filter.name.Contains("CourtGreen")||filter.name.Contains("CourtKitchen");
        bool net=filter.name.Contains("White")||filter.name.Contains("Dark");
        for(int i=0;i<vertices.Length;i++)
        {
            Vector3 p=filter.transform.TransformPoint(vertices[i]);
            if(ground&&Mathf.Abs(p.y-originalTop)<.0001f)p.y=filter.name.Contains("CourtGreen")?-.008f:filter.name.Contains("CourtKitchen")?.001f:0;
            if(net&&p.y>.09f&&Mathf.Abs(p.z)<.06f&&Mathf.Abs(p.x)<=Court.HalfWidth+.003f)
            {
                float oldX=p.x,oldTop=.8636f+.0508f*Mathf.Pow(Mathf.Abs(oldX)/Court.HalfWidth,2);
                p.x*=Court.NetHalfSpan/Court.HalfWidth;
                p.y*=Court.NetHeight(p.x)/oldTop;
            }
            vertices[i]=filter.transform.InverseTransformPoint(p);
        }
        mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();filter.sharedMesh=Save(mesh,Folder+"/Meshes/"+Name(filter.name)+".asset");
    }
    static int Sector(Renderer r)
    {var p=r.bounds.center;if(Mathf.Abs(p.z)>16)return p.z<0?0:1;if(p.x<-7)return 2;if(p.x>7)return 3;return 4;}
    static void Batch(Transform root)
    {
        var filters=root.GetComponentsInChildren<MeshFilter>().Where(f=>!f.name.Contains("ChampionshipCourt")&&!f.name.Contains("WarmLight")).ToArray();
        var groups=filters.GroupBy(f=>Sector(f.GetComponent<Renderer>())+"_"+f.GetComponent<Renderer>().sharedMaterial.name).ToArray();
        var holder=new GameObject("Batched Stadium Geometry").transform;holder.SetParent(root,false);
        foreach(var group in groups)
        {
            var first=group.First();var mesh=new Mesh{name=group.Key,indexFormat=IndexFormat.UInt32};
            mesh.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,subMeshIndex=0,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true,false);
            mesh.RecalculateBounds();var asset=Save(mesh,Folder+"/Meshes/Batch_"+Name(group.Key)+".asset");
            var go=new GameObject("Batch "+group.Key,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(holder,false);
            go.GetComponent<MeshFilter>().sharedMesh=asset;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=first.GetComponent<Renderer>().sharedMaterial;
            r.shadowCastingMode=group.Key.Contains("PlantLeaves")?ShadowCastingMode.Off:ShadowCastingMode.On;
            foreach(var f in group){UnityEngine.Object.DestroyImmediate(f.GetComponent<Renderer>());UnityEngine.Object.DestroyImmediate(f);}
        }
    }
    static Transform Anchor(string name,Transform parent,Vector3 p)
    {var t=new GameObject(name).transform;t.SetParent(parent,false);t.position=p;return t;}
    static Light Spot(string name,Transform parent,Vector3 position,Vector3 target,float range,float cone,bool shadow)
    {
        var go=new GameObject(name,typeof(Light));go.transform.SetParent(parent,false);go.transform.position=position;go.transform.LookAt(target);
        var light=go.GetComponent<Light>();light.type=LightType.Spot;light.range=range;light.spotAngle=cone;light.innerSpotAngle=cone*.65f;light.color=new Color(1,.91f,.77f);light.intensity=5;
        light.shadows=shadow?LightShadows.Soft:LightShadows.None;light.shadowBias=.035f;light.shadowNormalBias=.15f;light.shadowNearPlane=.1f;
        var data=go.AddComponent<UniversalAdditionalLightData>();var serialized=new SerializedObject(data);serialized.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue=1;serialized.ApplyModifiedPropertiesWithoutUndo();
        return light;
    }
    public static object Build()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play before building.");Folders();
        var root=new GameObject("Stadium Enhanced");var visual=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(StadiumEnvironment.SourceAsset),root.transform);visual.name="Stadium Visual";visual.transform.localPosition=new Vector3(0,-.051f,0);
        var environment=root.AddComponent<StadiumEnvironment>();environment.lightsOn=Led(true);environment.lightsOff=Led(false);
        var materials=new Dictionary<Material,Material>();
        foreach(var r in visual.GetComponentsInChildren<Renderer>())
        {
            var source=r.sharedMaterial;if(!materials.TryGetValue(source,out var copy)){var existing=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/"+Name(source.name)+".mat");copy=existing&&existing.shader.name=="Universal Render Pipeline/Simple Lit"?existing:Save(new Material(source){name=source.name,enableInstancing=true},Folder+"/Materials/"+Name(source.name)+".mat");materials[source]=copy;}r.sharedMaterial=copy;
        }
        var court=visual.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.Contains("ChampionshipCourt")).ToArray();foreach(var f in court)Calibrate(f);
        environment.competitionSurface=court.Single(f=>f.name.Contains("CourtBlue"));environment.kitchenSurface=court.Single(f=>f.name.Contains("CourtKitchen"));environment.courtLines=court.Single(f=>f.name.Contains("White"));environment.netStructure=court.Single(f=>f.name.Contains("Dark"));
        var warm=visual.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.Contains("WarmLight")).ToArray();
        environment.poleEmitters=warm.Where(f=>f.name.Contains("LightPole")).Select(f=>f.GetComponent<Renderer>()).ToArray();environment.roofEmitters=warm.Where(f=>f.name.Contains("Dome")).Select(f=>f.GetComponent<Renderer>()).ToArray();environment.decorEmitters=warm.Where(f=>!f.name.Contains("LightPole")&&!f.name.Contains("Dome")).Select(f=>f.GetComponent<Renderer>()).ToArray();
        var lightRoot=new GameObject("Stadium Fixture Spotlights").transform;lightRoot.SetParent(root.transform,false);var poles=new List<Light>();var roofs=new List<Light>();var positions=new List<object>();
        foreach(var f in warm.Where(f=>f.name.Contains("LightPole")||f.name.Contains("Dome")))
        {
            bool pole=f.name.Contains("LightPole");var bulbs=Islands(f).Where(b=>!pole||b.center.y>5).OrderBy(b=>b.center.x).ThenBy(b=>b.center.z).ToArray();
            var closest=pole?bulbs.OrderBy(b=>b.center.sqrMagnitude).First().center:Vector3.zero;
            foreach(var b in bulbs)
            {
                Vector3 target=pole?new Vector3(Mathf.Sign(b.center.x)*1.4f,0,Mathf.Clamp(b.center.z*.5f,-5,5)):b.center+Vector3.down*10;
                bool shadow=pole&&Mathf.Abs(b.center.z)<10&&(b.center-closest).sqrMagnitude<.001f;
                var light=Spot((pole?"Y Pole":"Roof")+" Spotlight "+(pole?poles.Count:roofs.Count),lightRoot,b.center+Vector3.down*.012f,target,pole?23:13,pole?67:75,shadow);
                if(pole)poles.Add(light);else roofs.Add(light);positions.Add(new{fixture=f.name,position=light.transform.position.ToString("F4"),shadow});
            }
        }
        environment.poleLights=poles.ToArray();environment.roofLights=roofs.ToArray();
        environment.ConfigureFixtureGroups();
        environment.nearEntrance=Anchor("Player Entrance",root.transform,new Vector3(1.45f,0,-10.8f));environment.farEntrance=Anchor("Opponent Entrance",root.transform,new Vector3(-1.45f,0,10.8f));
        environment.nearResult=Anchor("Near Result Stage",root.transform,new Vector3(0,0,-4.8f));environment.farResult=Anchor("Far Result Stage",root.transform,new Vector3(0,0,4.8f));
        Batch(visual.transform);foreach(var r in root.GetComponentsInChildren<Renderer>())if(r.name.Contains("WarmLight")||new[]{"BlueSeats","PlantLeaves","Terracotta","SportsBanner","VietnamSatin"}.Any(n=>r.name.Contains(n)))r.shadowCastingMode=ShadowCastingMode.Off;
        foreach(var c in root.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
        environment.ApplyNight(0);PrefabUtility.SaveAsPrefabAsset(root,Prefab);int renders=root.GetComponentsInChildren<Renderer>().Length;UnityEngine.Object.DestroyImmediate(root);
        Directory.CreateDirectory("../UnityWork/Stadium");File.WriteAllText("../UnityWork/Stadium/light-placements.json",Newtonsoft.Json.JsonConvert.SerializeObject(positions,Newtonsoft.Json.Formatting.Indented));
        return new{prefab=Prefab,poleLights=poles.Count,roofLights=roofs.Count,renderers=renders};
    }
    public static object Scenes()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play before scene authoring.");var results=new List<object>();
        foreach(var path in new[]{"Assets/Scenes/UI/Menu.unity","Assets/Scenes/BotMatch.unity","Assets/Scenes/MultiplayerCourt.unity","Assets/Scenes/Training.unity","Assets/Scenes/Tutorial.unity"})
        {
            var scene=EditorSceneManager.OpenScene(path);string backup="../UnityWork/Stadium/"+scene.name+".before.unity";if(!File.Exists(backup))EditorSceneManager.SaveScene(scene,backup,true);
            var home=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();var lighting=UnityEngine.Object.FindAnyObjectByType<MatchEnvironmentLighting>();
            var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).First(c=>c.enabled);
            foreach(var g in scene.GetRootGameObjects().Where(g=>g.name=="Arena"||g.name=="Home Arena"||g.name=="PickleballCourt"||g.name=="NetVisual"||g.name=="Stadium Enhanced"))UnityEngine.Object.DestroyImmediate(g);
            if(scene.name=="MultiplayerCourt")
            {
                var legacy=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="BotMatchArena");
                if(legacy)foreach(Transform child in legacy.transform.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            var stadium=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));stadium.name="Stadium Enhanced";var environment=stadium.GetComponent<StadiumEnvironment>();
            if(home)
            {
                home.stadium=environment;home.floodlights=Array.Empty<Light>();home.lampFaces=Array.Empty<Renderer>();home.femaleVisual.parent.position=new Vector3(1.45f,0,-5.7f);home.maleVisual.parent.position=new Vector3(-1.45f,0,5.7f);
                camera.transform.SetPositionAndRotation(new Vector3(0,2.35f,-10.4f),Quaternion.Euler(7.3f,0,0));home.EvaluateCycle(0);EditorUtility.SetDirty(home);
            }
            else
            {
                if(!lighting){var owner=new GameObject("Stadium Lighting");lighting=owner.AddComponent<MatchEnvironmentLighting>();}
                if(!lighting.daySky||!lighting.nightSky)
                {
                    var bot=EditorSceneManager.OpenScene("Assets/Scenes/BotMatch.unity",OpenSceneMode.Additive);var reference=bot.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MatchEnvironmentLighting>()).First();lighting.daySky=reference.daySky;lighting.nightSky=reference.nightSky;EditorSceneManager.CloseScene(bot,true);
                }
                lighting.matchCamera=camera;lighting.sun=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>(true)).First(l=>l.type==LightType.Directional);lighting.stadium=environment;lighting.floodlights=Array.Empty<Light>();lighting.lampFaces=Array.Empty<Renderer>();
                lighting.sun.transform.rotation=Quaternion.Euler(55,-35,0);
                lighting.sun.intensity=1.1f;lighting.sun.color=new Color(1,.93f,.82f);lighting.sun.shadows=LightShadows.Soft;
                lighting.Apply(lighting.selectedNight);EditorUtility.SetDirty(lighting);
            }
            var intro=UnityEngine.Object.FindAnyObjectByType<BotMatchIntro>();if(intro&&intro.gameObject.scene==scene){intro.playerEntrance=environment.nearEntrance;intro.opponentEntrance=environment.farEntrance;EditorUtility.SetDirty(intro);}
            var victory=UnityEngine.Object.FindAnyObjectByType<BotMatchVictory>();if(victory&&victory.gameObject.scene==scene){victory.nearPresentation=environment.nearResult;victory.farPresentation=environment.farResult;EditorUtility.SetDirty(victory);}
            if(scene.name=="MultiplayerCourt")PhysicsCourt(scene);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);results.Add(new{scene=scene.name,environment.poleLights.Length,roof=environment.roofLights.Length});
        }
        return results;
    }
    static void PhysicsCourt(UnityEngine.SceneManagement.Scene scene)
    {
        var ground=scene.GetRootGameObjects().First(g=>g.name=="PhysicsGround");var renderer=ground.GetComponent<Renderer>();if(renderer)UnityEngine.Object.DestroyImmediate(renderer);var mesh=ground.GetComponent<MeshFilter>();if(mesh)UnityEngine.Object.DestroyImmediate(mesh);
        ground.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);ground.transform.localScale=Vector3.one;var box=ground.GetComponent<BoxCollider>();box.center=new Vector3(0,-.05f,0);box.size=new Vector3(60,.1f,80);
        var segments=scene.GetRootGameObjects().Where(g=>g.name.StartsWith("PhysicsNet")).OrderBy(g=>int.Parse(g.name.Substring(10))).ToArray();
        for(int i=0;i<segments.Length;i++){float width=Court.NetHalfSpan*2/segments.Length,x=-Court.NetHalfSpan+(i+.5f)*width,height=Court.NetHeight(x);var g=segments[i];g.transform.SetPositionAndRotation(new Vector3(x,height*.5f,0),Quaternion.identity);g.transform.localScale=Vector3.one;var c=g.GetComponent<BoxCollider>();c.center=Vector3.zero;c.size=new Vector3(width,height,.024f);}
    }
}
