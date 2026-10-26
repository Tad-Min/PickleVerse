using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using PickleVerse.LocalMatch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Explicit authoring/QA commands. No runtime Update, service or backend calls.
public static class NaturalParkInstaller
{
    const string Folder="Assets/EnvironmentContent/NaturalPark";
    static readonly string Work=Path.GetFullPath("../BlenderWork/NaturalPark20261026");
    static readonly string[] Keys={"Professional","LowPoly"};
    static Material[] materials;
    static GameObject source;
    static Material Surface(string name,string texture,bool grass=false)
    {
        string path=Folder+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("PickleVerse/Environment/Natural Foliage")){name=name};AssetDatabase.CreateAsset(m,path);}
        m.shader=Shader.Find("PickleVerse/Environment/Natural Foliage");m.enableInstancing=true;
        m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Textures/"+texture+".png"));
        m.SetColor("_BaseColor",Color.white);m.SetFloat("_WindStrength",grass?.055f:name=="Bark"?.035f:.075f);
        m.SetFloat("_WindSpeed",.8f);m.SetFloat("_Grass",grass?1:0);
        m.SetFloat("_Translucency",name=="Bark"?0:.38f);m.SetFloat("_Cull",name=="Bark"?2:0);
        EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);return m;
    }
    static Vector3 Position(JToken t)=>new Vector3((float)t[0],(float)t[1],(float)t[2]);
    static void Plant(Transform parent,string type,JToken row,int index)
    {
        var root=new GameObject(type+" "+index);root.transform.SetParent(parent,false);
        root.transform.localPosition=Position(row["position"]);root.transform.localRotation=Quaternion.Euler(0,(float)row["angle"],0);
        root.transform.localScale=Vector3.one*(row["scale"]!=null?(float)row["scale"]:1);
        bool grass=type=="GrassPatch";var lods=new LOD[3];
        for(int l=0;l<3;l++)
        {
            string id=type+"_LOD"+l;
            var template=source.GetComponentsInChildren<Transform>(true).Single(t=>t.name==id);
            var go=UnityEngine.Object.Instantiate(template.gameObject,root.transform,false);go.name=id;
            var renderer=go.GetComponent<MeshRenderer>();
            renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>materials[m.name.StartsWith("Bark")?0:m.name.StartsWith("Leaf")?1:2]).ToArray();
            renderer.shadowCastingMode=grass||l==2?ShadowCastingMode.Off:ShadowCastingMode.On;
            renderer.receiveShadows=true;renderer.lightProbeUsage=LightProbeUsage.Off;
            renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
            // Do not static-batch: instances share meshes, shaders move vertices.
            GameObjectUtility.SetStaticEditorFlags(go,0);
            // A grass patch is four metres wide. These screen-height thresholds
            // retain full blades within ~9m, thin at ~27m and cull near 39m.
            float height=grass?new[]{.45f,.16f,.11f}[l]:new[]{.24f,.065f,.012f}[l];
            lods[l]=new LOD(height,new Renderer[]{renderer});
        }
        var group=root.AddComponent<LODGroup>();group.SetLODs(lods);group.RecalculateBounds();
        group.size*=1.08f;group.fadeMode=LODFadeMode.None;group.animateCrossFading=false;
    }
    public static object Install()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling)throw new Exception("Edit mode required");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(string path in Directory.GetFiles(Folder+"/Textures","*.png"))
        {
            var t=(TextureImporter)AssetImporter.GetAtPath(path);t.mipmapEnabled=true;t.isReadable=false;t.sRGBTexture=true;
            t.maxTextureSize=path.Contains("Bark")||path.Contains("Leaf")?512:256;
            t.wrapMode=TextureWrapMode.Repeat;t.filterMode=FilterMode.Trilinear;t.anisoLevel=2;
            t.textureCompression=TextureImporterCompression.CompressedHQ;
            foreach(string platform in new[]{"Android","iPhone"}){var p=t.GetPlatformTextureSettings(platform);p.name=platform;p.overridden=true;p.maxTextureSize=t.maxTextureSize;p.format=TextureImporterFormat.ASTC_6x6;p.compressionQuality=75;t.SetPlatformTextureSettings(p);}
            t.SaveAndReimport();
        }
        materials=new[]{Surface("Bark","Bark_Fibres"),Surface("Leaf","Leaf_Venation"),Surface("Grass","Grass_Fibres",true)};
        source=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/NaturalVegetation.glb");
        if(!source)throw new Exception("Shared prototypes not imported");
        VenueRefinementInstaller.InstallFrom(Work+"/Exports");
        var manifest=JObject.Parse(File.ReadAllText(Work+"/natural-park-manifest.json"));
        foreach(var venue in manifest["venues"])
        {
            string key=(string)venue["key"],path="Assets/Resources/Courts/"+key+".prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var old=root.transform.Find("Natural Park");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
                var park=new GameObject("Natural Park");park.transform.SetParent(root.transform,false);
                int index=0;foreach(var row in venue["trees"])Plant(park.transform,(int)row["species"]==0?"Broadleaf_A":"Broadleaf_B",row,index++);
                index=0;foreach(var row in venue["grass"])Plant(park.transform,"GrassPatch",row,index++);
                index=0;foreach(var row in venue["shrubs"])Plant(park.transform,"Shrub",row,index++);
                var ground=root.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name=="Refined_Garden_Grass").sharedMaterial;
                ground.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Textures/Ground_Organic.png"));
                ground.SetColor("_BaseColor",Color.white);EditorUtility.SetDirty(ground);AssetDatabase.SaveAssetIfDirty(ground);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        return Validate();
    }
    public static object Validate()
    {
        var checks=new List<object>();
        foreach(var key in Keys)
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Courts/"+key+".prefab");
            var park=root.transform.Find("Natural Park");var lods=park.GetComponentsInChildren<LODGroup>(true);
            checks.Add(new{name=key+" all plants have three LODs",passed=lods.Length>50&&lods.All(l=>l.GetLODs().Length==3)});
            checks.Add(new{name=key+" shared wind materials, no per-plant behaviours/colliders",passed=park.GetComponentsInChildren<MonoBehaviour>().Length==0&&park.GetComponentsInChildren<Collider>().Length==0&&park.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m&&m.shader.name=="PickleVerse/Environment/Natural Foliage"&&m.enableInstancing))});
            checks.Add(new{name=key+" vertex wind/AO masks present",passed=park.GetComponentsInChildren<MeshFilter>().All(f=>f.sharedMesh.colors.Length==f.sharedMesh.vertexCount)});
            checks.Add(new{name=key+" blob canopy removed",passed=!root.GetComponentsInChildren<Transform>(true).Any(t=>t.name.StartsWith("Refined_Garden_Leaf_")||t.name=="Refined_Garden_Bark")});
            checks.Add(new{name=key+" grass has no shadow draw, bounded density",passed=park.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("GrassPatch")).All(r=>r.shadowCastingMode==ShadowCastingMode.Off)&&park.GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="GrassPatch_LOD0").All(f=>f.sharedMesh.triangles.Length/3<=5600)});
        }
        var shader=Shader.Find("PickleVerse/Environment/Natural Foliage");
        checks.Add(new{name="Wind shader supported and no compiler errors",passed=shader&&shader.isSupported&&!ShaderUtil.ShaderHasError(shader)});
        var result=new{checks,shaderMessages=ShaderUtil.GetShaderMessages(shader).Select(m=>new{m.message,m.severity,m.platform})};
        File.WriteAllText(Work+"/plant-validation.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
    }
    public static void Shot(Camera c,string name,int width=1280,int height=720)
    {
        var descriptor=new RenderTextureDescriptor(width,height,RenderTextureFormat.ARGB32,24){msaaSamples=4,sRGB=QualitySettings.activeColorSpace==ColorSpace.Linear};
        var target=RenderTexture.GetTemporary(descriptor);var old=c.targetTexture;var active=RenderTexture.active;
        var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
        try{c.targetTexture=target;c.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(Work+"/"+name+".png",texture.EncodeToPNG());}
        finally{c.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(texture);}
    }
    public static object Pictures()
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
        string previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        try
        {
            foreach(string key in Keys)
            {
                EditorSceneManager.OpenScene("Assets/Scenes/BotMatch.unity");var lighting=UnityEngine.Object.FindAnyObjectByType<MatchEnvironmentLighting>();
                lighting.stadium.gameObject.SetActive(false);
                lighting.stadium=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Courts/"+key+".prefab")).GetComponent<StadiumEnvironment>();
                var c=lighting.matchCamera;
                foreach(bool night in new[]{false,true})
                {
                    lighting.SelectForMatch(night);c.transform.position=new Vector3(24,8,-24);c.transform.LookAt(new Vector3(5,1,-3));c.fieldOfView=57;
                    Shot(c,key+(night?"-Night":"-Day"));
                }
                lighting.SelectForMatch(false);var tree=lighting.stadium.transform.Find("Natural Park").GetChild(0);
                c.transform.position=tree.position+new Vector3(4.6f,3.2f,-5.8f);c.transform.LookAt(tree.position+Vector3.up*2.5f);c.fieldOfView=54;
                Shot(c,key+"-Tree");
                c.transform.position=tree.position+new Vector3(1,.4f,-1.9f);c.transform.LookAt(tree.position+new Vector3(.5f,.06f,-.6f));c.fieldOfView=52;c.nearClipPlane=.03f;
                Shot(c,key+"-Grass");
            }
            return new{folder=Work};
        }
        finally{if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);}
    }
}
