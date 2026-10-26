using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using PickleVerse.LocalMatch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Explicit Editor commands only: no runtime bootstrap, network access or backend changes.
public static class VenueRefinementInstaller
{
    static readonly string Work=Path.GetFullPath("../BlenderWork/VenueRefinement20261025");
    const string Folder="Assets/LocalMatchContent/Stadiums";
    const string Textures=Folder+"/RefinedTextures";
    static string[] Keys={"Professional","LowPoly"};
    static string FileName(string id)=>id=="Professional"?"ProfessionalStadium":"PickleballCourt_LowPoly";
    static object Geometry(StadiumEnvironment e)
    {
        return new { surfaces=new[]{e.competitionSurface,e.kitchenSurface,e.courtLines,e.netStructure}.Select(f=>new{
            f.name,triangles=f.sharedMesh.triangles.Length/3,vertices=f.sharedMesh.vertices.Select(f.transform.TransformPoint).Select(v=>new[]{Mathf.Round(v.x*100000),Mathf.Round(v.y*100000),Mathf.Round(v.z*100000)}).GroupBy(v=>string.Join(",",v)).Select(g=>g.First()).OrderBy(v=>v[0]).ThenBy(v=>v[1]).ThenBy(v=>v[2]).ToArray()
        }).ToArray(), anchors=new[]{e.nearEntrance,e.farEntrance,e.nearResult,e.farResult}.Select(t=>new{t.name,position=t.localPosition.ToString("F5")}).ToArray(),limits=e.courtsideCameraLimits.ToString("F5"),
            lights=e.poleIllumination.Select(l=>new{position=l.transform.localPosition.ToString("F5"),rotation=l.transform.localRotation.ToString("F5"),l.range,l.spotAngle,l.intensity,color=l.color.ToString("F5")}).ToArray()};
    }
    public static object CaptureOriginal()
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
        var records=Keys.ToDictionary(id=>id,id=>Geometry(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Courts/"+id+".prefab").GetComponent<StadiumEnvironment>()));
        File.WriteAllText(Work+"/geometry-before.json",JsonConvert.SerializeObject(records,Formatting.Indented));return new{captured=records.Count};
    }
    static void ConfigureTextures()
    {
        foreach(var path in Directory.GetFiles(Textures,"*.png"))
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var t=(TextureImporter)AssetImporter.GetAtPath(path);t.textureType=TextureImporterType.Default;t.sRGBTexture=true;t.mipmapEnabled=true;t.isReadable=false;
            t.maxTextureSize=path.Contains("Signage")?1024:256;t.wrapMode=path.Contains("Signage")?TextureWrapMode.Clamp:TextureWrapMode.Repeat;t.filterMode=FilterMode.Trilinear;t.anisoLevel=2;
            t.textureCompression=TextureImporterCompression.CompressedHQ;t.crunchedCompression=false;
            foreach(string platform in new[]{"Android","iPhone"}){var p=t.GetPlatformTextureSettings(platform);p.name=platform;p.overridden=true;p.maxTextureSize=t.maxTextureSize;p.format=TextureImporterFormat.ASTC_6x6;p.compressionQuality=75;t.SetPlatformTextureSettings(p);}
            t.SaveAndReimport();
        }
    }
    static Material Convert(Material source,string key,JObject textureMap)
    {
        bool shared=source.name.StartsWith("Garden_")||source.name.StartsWith("Venue_");
        string name=(shared?"Refined":key)+"_"+source.name;
        string path=Folder+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Simple Lit")){name=name};AssetDatabase.CreateAsset(m,path);}
        m.shader=Shader.Find("Universal Render Pipeline/Simple Lit");m.enableInstancing=true;
        var color=source.HasProperty("baseColorFactor")?source.GetColor("baseColorFactor"):source.color;
        m.SetColor("_BaseColor",color);
        string file=(string)textureMap[source.name];
        var texture=!string.IsNullOrEmpty(file)?AssetDatabase.LoadAssetAtPath<Texture2D>(Textures+"/"+file):source.HasProperty("baseColorTexture")?source.GetTexture("baseColorTexture"):null;
        m.SetTexture("_BaseMap",texture);m.SetTextureScale("_BaseMap",Vector2.one);m.SetTextureOffset("_BaseMap",Vector2.zero);
        bool cutout=source.name=="Net_Weave"||source.name=="ChainLink";
        m.SetFloat("_Surface",0);m.SetFloat("_AlphaClip",cutout?1:0);m.SetFloat("_Cutoff",.5f);
        if(cutout){m.EnableKeyword("_ALPHATEST_ON");m.renderQueue=2450;}else{m.DisableKeyword("_ALPHATEST_ON");m.renderQueue=2000;}
        m.SetFloat("_Cull",cutout||source.name=="Venue_Signage"?0:2);
        m.SetFloat("_Smoothness",.12f);m.SetColor("_SpecColor",new Color(.035f,.035f,.035f,.12f));
        m.DisableKeyword("_SPECULAR_COLOR");m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");m.DisableKeyword("_NORMALMAP");
        m.SetTexture("_BumpMap",null);m.DisableKeyword("_EMISSION");m.SetColor("_EmissionColor",Color.black);
        EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);return m;
    }
    static object Apply(string key)
    {
        string path="Assets/Resources/Courts/"+key+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var e=root.GetComponent<StadiumEnvironment>();var old=root.transform.Find("Venue Visual");if(!old)throw new Exception("Visual root missing");
            var localPosition=old.localPosition;var localRotation=old.localRotation;var localScale=old.localScale;
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Stadium_Enhanced/"+FileName(key)+".glb");if(!source)throw new Exception("Source not imported");
            var visual=UnityEngine.Object.Instantiate(source,root.transform);visual.name="Venue Visual";
            visual.transform.localPosition=localPosition;visual.transform.localRotation=localRotation;visual.transform.localScale=localScale;
            var textures=(JObject)JObject.Parse(File.ReadAllText(Work+"/"+key+"-manifest.json"))["textures"];
            var lookup=new Dictionary<Material,Material>();
            foreach(var renderer in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>{if(!lookup.TryGetValue(m,out var converted)){converted=Convert(m,key,textures);lookup.Add(m,converted);}return converted;}).ToArray();
                bool ground=renderer.name.Contains("Paving")||renderer.name.Contains("Grass")||renderer.name.StartsWith("Court_");
                bool flat=ground||renderer.name.Contains("Signage")||renderer.name.Contains("Cutout")||renderer.name.Contains("Soil");
                renderer.shadowCastingMode=flat?ShadowCastingMode.Off:ShadowCastingMode.On;renderer.receiveShadows=true;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.BatchingStatic);
            }
            foreach(var c in visual.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
            var filters=visual.GetComponentsInChildren<MeshFilter>(true);
            e.competitionSurface=filters.Single(f=>f.name.StartsWith("Court_"));e.kitchenSurface=e.courtLines=e.competitionSurface;
            e.netStructure=filters.Single(f=>f.name=="PortableNet_White_Headband");
            // The GLB keeps the authored slab elevation. Prefabs are calibrated
            // to the shared y=0 rules plane, independent of importer root folding.
            float plane=e.competitionSurface.sharedMesh.vertices.Select(e.competitionSurface.transform.TransformPoint).Max(v=>v.y);
            visual.transform.position-=Vector3.up*plane;
            var gate=visual.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Gate_Hinge_OPEN_ROTATE_LOCAL_Z");if(gate)gate.Rotate(Vector3.up,105,Space.World);
            UnityEngine.Object.DestroyImmediate(old.gameObject);
            var groundRoot=root.transform.Find("Surrounding Ground");if(groundRoot)groundRoot.gameObject.SetActive(false);
            e.poleNightIntensity=key=="Professional"?115:95;
            // Four white beams share one soft shadow source on desktop. The
            // other beams provide fill light without repeating all actor draws.
            for(int i=0;i<e.poleIllumination.Length;i++)e.poleIllumination[i].shadows=i==0?LightShadows.Soft:LightShadows.None;
            e.ApplyNight(0,true);
            var result=new{key,geometry=Geometry(e),renderers=filters.Length,triangles=filters.Sum(f=>f.sharedMesh.triangles.Length/3),drawMaterials=visual.GetComponentsInChildren<Renderer>().Sum(r=>r.sharedMaterials.Length)};
            PrefabUtility.SaveAsPrefabAsset(root,path);return result;
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    public static object Install()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling)throw new Exception("Clean Edit mode required");
        ConfigureTextures();
        return InstallFrom(Path.Combine(Work,"Exports"));
    }
    public static object InstallFrom(string exportFolder)
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling)throw new Exception("Clean Edit mode required");
        foreach(var key in Keys){string file=FileName(key)+".glb";File.Copy(Path.Combine(exportFolder,file),"Assets/Models/Stadium_Enhanced/"+file,true);AssetDatabase.ImportAsset("Assets/Models/Stadium_Enhanced/"+file,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);}
        var rows=Keys.Select(Apply).ToArray();
        File.WriteAllText(Work+"/installation.json",JsonConvert.SerializeObject(rows,Formatting.Indented));return rows;
    }
    public static object Validate()
    {
        var before=JObject.Parse(File.ReadAllText(Work+"/geometry-before.json"));var checks=new List<object>();
        foreach(string key in Keys)
        {
            var e=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Courts/"+key+".prefab").GetComponent<StadiumEnvironment>();
            var current=JToken.FromObject(Geometry(e));
            foreach(string field in new[]{"surfaces","anchors","limits","lights"})
            {
                bool passed=JToken.DeepEquals(before[key][field],current[field]);checks.Add(new{name=key+" unchanged "+(field=="lights"?"lamp anchors/cones/day level":field),passed});
            }
            checks.Add(new{name=key+" no missing materials or meshes",passed=e.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m&&m.shader&&m.shader.isSupported))&&e.GetComponentsInChildren<MeshFilter>().All(f=>f.sharedMesh)});
            checks.Add(new{name=key+" exactly four original spotlights",passed=e.GetComponentsInChildren<Light>().Length==4&&e.poleIllumination.All(l=>l.type==LightType.Spot&&l.color==Color.white)});
            checks.Add(new{name=key+" no decorative physics collisions",passed=e.transform.Find("Venue Visual").GetComponentsInChildren<Collider>(true).Length==0});
            checks.Add(new{name=key+" sparse garden and brand present",passed=e.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Refined_Garden_Grass")&&e.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Refined_Venue_Signage")});
        }
        var result=new{checks};File.WriteAllText(Work+"/geometry-validation.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
    }
    public static object CreateIsolatedBaseline()
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
        const string destination="Assets/Editor/VenueRefinement/Baseline";
        Directory.CreateDirectory(destination);AssetDatabase.Refresh();var report=new List<object>();
        foreach(string key in Keys)
        {
            string modelPath=destination+"/"+FileName(key)+".glb";
            File.Copy(Work+"/Original/"+FileName(key)+".glb",modelPath,true);AssetDatabase.ImportAsset(modelPath,ImportAssetOptions.ForceSynchronousImport);
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Courts/"+key+".prefab"));root.name="Baseline "+key;
            try
            {
                var env=root.GetComponent<StadiumEnvironment>();var old=root.transform.Find("Venue Visual");
                var visual=UnityEngine.Object.Instantiate(model,root.transform);visual.name="Venue Visual";
                var materialMap=new Dictionary<Material,Material>();
                foreach(var r in visual.GetComponentsInChildren<MeshRenderer>(true))
                {
                    r.sharedMaterials=r.sharedMaterials.Select(source=>
                    {
                        if(materialMap.TryGetValue(source,out var found))return found;
                        string name=key+"_"+source.name;string staged=Work+"/BaselineRuntimeAssets/My project/Assets/LocalMatchContent/Stadiums/"+name+".mat";
                        string target=destination+"/"+name+".mat";
                        if(!File.Exists(staged))throw new Exception("Original baseline material is missing: "+name);
                        File.Copy(staged,target,true);AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceSynchronousImport);
                        var m=AssetDatabase.LoadAssetAtPath<Material>(target);
                        m.SetTexture("_BaseMap",source.HasProperty("baseColorTexture")?source.GetTexture("baseColorTexture"):null);
                        EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);materialMap.Add(source,m);return m;
                    }).ToArray();
                    r.shadowCastingMode=r.name.Contains("Court_")||r.name.Contains("Seats")||r.name.Contains("Foliage")||r.name.Contains("Flowers")||r.name.Contains("Cutout")?ShadowCastingMode.Off:ShadowCastingMode.On;
                }
                UnityEngine.Object.DestroyImmediate(old.gameObject);
                env.competitionSurface=visual.GetComponentsInChildren<MeshFilter>().Single(f=>f.name.StartsWith("Court_"));env.kitchenSurface=env.courtLines=env.competitionSurface;
                env.netStructure=visual.GetComponentsInChildren<MeshFilter>().Single(f=>f.name=="PortableNet_White_Headband");
                float plane=env.competitionSurface.sharedMesh.vertices.Select(env.competitionSurface.transform.TransformPoint).Max(v=>v.y);visual.transform.position-=Vector3.up*plane;
                var gate=visual.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Gate_Hinge_OPEN_ROTATE_LOCAL_Z");if(gate)gate.Rotate(Vector3.up,105,Space.World);
                root.transform.Find("Surrounding Ground").gameObject.SetActive(true);env.poleNightIntensity=key=="Professional"?155:125;
                for(int i=0;i<env.poleIllumination.Length;i++)env.poleIllumination[i].shadows=i%3==0?LightShadows.Soft:LightShadows.None;
                env.ApplyNight(0,true);PrefabUtility.SaveAsPrefabAsset(root,destination+"/"+key+".prefab");
                report.Add(new{key,renderers=root.GetComponentsInChildren<MeshRenderer>().Length,triangles=root.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3),materialPasses=root.GetComponentsInChildren<Renderer>().Sum(r=>r.sharedMaterials.Length)});
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        return report;
    }
}
