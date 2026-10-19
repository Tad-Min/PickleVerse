using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PickleVerse.LocalMatch;
using PickleVerse;
using PickleVerse.FrontEnd;
using PickleVerse.Settings;
using TMPro;

public static class NewStadiumSetup
{
 public const string Report="../UnityWork/NewStadiums20261019";
 public const string Folder="Assets/LocalMatchContent/Stadiums";
 public static object Survey()
 {
  Directory.CreateDirectory(Report);var results=new List<object>();
  foreach(string name in new[]{"PickleballCourt_LowPoly","ProfessionalStadium"})
  {
   string path="Assets/Models/Stadium_Enhanced/"+name+".glb";var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!asset)throw new Exception("Not imported: "+path);
   var renderers=asset.GetComponentsInChildren<MeshRenderer>(true);
   var rows=renderers.Select(r=>new{r.name,parent=r.transform.parent.name,min=r.bounds.min.ToString("F4"),max=r.bounds.max.ToString("F4"),size=r.bounds.size.ToString("F4"),materials=r.sharedMaterials.Select(m=>m?m.name:"").ToArray(),vertices=r.GetComponent<MeshFilter>().sharedMesh.vertexCount,triangles=(long)r.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0)/3}).ToArray();
   File.WriteAllText(Report+"/"+name+"-survey.json",JsonConvert.SerializeObject(rows,Formatting.Indented));
   results.Add(new{name,bytes=new FileInfo(path).Length,renderers=rows.Length,vertices=rows.Sum(r=>r.vertices),triangles=rows.Sum(r=>r.triangles),relevant=rows.Where(r=>System.Text.RegularExpressions.Regex.IsMatch(r.name,"court|net|light|lamp|roof|kitchen|floor",System.Text.RegularExpressions.RegexOptions.IgnoreCase)).ToArray()});
  }
  return results;
 }
 public static object GeometryAudit()
 {
  return new[]{"Professional","LowPoly"}.Select(id=>
  {
   var env=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Courts/"+id+".prefab").GetComponent<StadiumEnvironment>();var f=env.competitionSurface;var verts=f.sharedMesh.vertices.Select(f.transform.TransformPoint).ToArray();var mats=f.GetComponent<Renderer>().sharedMaterials;
   return new{id,position=f.transform.position.ToString("F5"),scale=f.transform.lossyScale.ToString("F5"),submeshes=Enumerable.Range(0,f.sharedMesh.subMeshCount).Select(i=>new{mat=mats[i].name,y=f.sharedMesh.GetTriangles(i).Select(n=>verts[n].y).Distinct().OrderBy(v=>v).ToArray(),maxX=f.sharedMesh.GetTriangles(i).Select(n=>Mathf.Abs(verts[n].x)).Max(),maxZ=f.sharedMesh.GetTriangles(i).Select(n=>Mathf.Abs(verts[n].z)).Max()}).ToArray()};
  }).ToArray();
 }
 public static object Inspect()
 {
  string original=SceneManager.GetActiveScene().path;
  var mats=new List<object>();foreach(string name in new[]{"ProfessionalStadium","PickleballCourt_LowPoly"})
  {
   var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Stadium_Enhanced/"+name+".glb");
   mats.Add(new{name,materials=model.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Distinct().Select(m=>new{m.name,shader=m.shader.name,color=m.HasProperty("baseColorFactor")?m.GetColor("baseColorFactor").ToString():m.color.ToString(),alpha=m.HasProperty("_AlphaClip")?m.GetFloat("_AlphaClip"):0,texture=m.HasProperty("baseColorTexture")&&m.GetTexture("baseColorTexture")?m.GetTexture("baseColorTexture").name:null}).ToArray()});
  }
  var scene=EditorSceneManager.OpenScene("Assets/Scenes/UI/ModeSelection.unity");var ui=UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>();
  var rows=ui.botPage.GetComponentsInChildren<RectTransform>(true).Select(t=>new{t.name,parent=t.parent.name,min=t.anchorMin.ToString(),max=t.anchorMax.ToString(),label=t.GetComponent<TMP_Text>()?.text}).ToArray();
  EditorSceneManager.OpenScene(original);File.WriteAllText(Report+"/mode-before.json",JsonConvert.SerializeObject(rows,Formatting.Indented));return new{materials=mats,ui=rows};
 }

 static T Save<T>(T asset,string path)where T:UnityEngine.Object
 {
  var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old){EditorUtility.CopySerialized(asset,old);UnityEngine.Object.DestroyImmediate(asset);EditorUtility.SetDirty(old);AssetDatabase.SaveAssetIfDirty(old);return old;}
  AssetDatabase.CreateAsset(asset,path);return asset;
 }
 static Material Surface(string name,Color color)
 {var m=new Material(Shader.Find("Universal Render Pipeline/Simple Lit")){name=name,enableInstancing=true};m.color=color;m.SetFloat("_Smoothness",.2f);return Save(m,Folder+"/"+name+".mat");}
 static Material Convert(Material source,string prefix)
 {
  var m=new Material(Shader.Find("Universal Render Pipeline/Simple Lit")){name=prefix+"_"+source.name,enableInstancing=true};
  m.SetColor("_BaseColor",source.HasProperty("baseColorFactor")?source.GetColor("baseColorFactor"):source.color);
  if(source.HasProperty("baseColorTexture"))m.SetTexture("_BaseMap",source.GetTexture("baseColorTexture"));
  if(source.HasProperty("baseColorTexture_ST")){var st=source.GetVector("baseColorTexture_ST");m.SetTextureScale("_BaseMap",new Vector2(st.x,st.y));m.SetTextureOffset("_BaseMap",new Vector2(st.z,st.w));}
  m.SetFloat("_Cull",source.HasProperty("_Cull")?source.GetFloat("_Cull"):2);
  float smooth=source.HasProperty("roughnessFactor")?1-source.GetFloat("roughnessFactor"):.15f;m.SetFloat("_Smoothness",smooth);m.SetColor("_SpecColor",new Color(.08f,.08f,.08f,smooth));m.EnableKeyword("_SPECULAR_COLOR");
  if(source.HasProperty("_AlphaClip")&&source.GetFloat("_AlphaClip")>0){m.SetFloat("_AlphaClip",1);m.SetFloat("_Cutoff",source.GetFloat("alphaCutoff"));m.EnableKeyword("_ALPHATEST_ON");m.renderQueue=2450;}
  return Save(m,Folder+"/"+m.name+".mat");
 }
 static Transform Anchor(Transform parent,string name,Vector3 p){var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=p;return t;}
 static GameObject Cube(Transform parent,string name,Vector3 position,Vector3 scale,Material mat)
 {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localScale=scale;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=mat;return g;}
 public static object Build()
 {
  if(EditorApplication.isPlaying)throw new Exception("Stop Play first");Directory.CreateDirectory(Report);Directory.CreateDirectory(Folder);Directory.CreateDirectory("Assets/Resources/Courts");AssetDatabase.Refresh();var reports=new List<object>();
  var steel=Surface("Fixture_Steel",new Color(.10f,.14f,.19f));var ground=Surface("Surroundings",new Color(.23f,.29f,.31f));
  var on=Surface("LED_White",Color.white);on.EnableKeyword("_EMISSION");on.SetColor("_EmissionColor",Color.white*1.2f);EditorUtility.SetDirty(on);
  foreach(bool pro in new[]{true,false})
  {
   string id=pro?"Professional":"LowPoly",source=pro?"ProfessionalStadium":"PickleballCourt_LowPoly";
   var root=new GameObject(id+" Stadium");var env=root.AddComponent<StadiumEnvironment>();
   var visual=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Stadium_Enhanced/"+source+".glb"),root.transform);visual.name="Venue Visual";visual.transform.localPosition+=new Vector3(0,pro?-.45f:0,0);
   var converted=new Dictionary<Material,Material>();
   foreach(var r in visual.GetComponentsInChildren<Renderer>())
   {
    r.sharedMaterials=r.sharedMaterials.Select(m=>{if(!converted.TryGetValue(m,out var copy)){copy=Convert(m,id);converted[m]=copy;}return copy;}).ToArray();
    r.shadowCastingMode=r.name.Contains("Court_")||r.name.Contains("Seats")||r.name.Contains("Foliage")||r.name.Contains("Flowers")||r.name.Contains("Cutout")?ShadowCastingMode.Off:ShadowCastingMode.On;
   }
   foreach(var collider in visual.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(collider);
   var gate=visual.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name.StartsWith("Gate_Hinge"));if(gate)gate.Rotate(Vector3.up,105,Space.World);
   Cube(root.transform,"Surrounding Ground",new Vector3(0,pro?-.51f:-.18f,0),new Vector3(90,.1f,100),ground);
   var heads=new List<Renderer>();var lights=new List<Light>();float x=pro?5.85f:5.05f,z=pro?8.5f:7.5f,height=pro?7.8f:6.8f;
   for(int i=0;i<4;i++)
   {
    var pos=new Vector3((i%2==0?-1:1)*x,0,(i<2?-1:1)*z);
    Cube(root.transform,"Floodlight Pole "+i,pos+Vector3.up*(height*.5f),new Vector3(.10f,height,.10f),steel);
    var head=Cube(root.transform,"White LED Fixture "+i,pos+Vector3.up*height,new Vector3(.85f,.16f,.42f),on);head.transform.LookAt(new Vector3(0,.1f,Mathf.Sign(pos.z)*1.5f));heads.Add(head.GetComponent<Renderer>());
    var go=new GameObject("White Court Spotlight "+i,typeof(Light),typeof(UniversalAdditionalLightData));go.transform.SetParent(root.transform,false);go.transform.localPosition=pos+Vector3.up*(height-.14f);go.transform.LookAt(new Vector3(0,0,Mathf.Sign(pos.z)*1.5f));
    var l=go.GetComponent<Light>();l.type=LightType.Spot;l.color=Color.white;l.useColorTemperature=false;l.range=28;l.spotAngle=100;l.innerSpotAngle=78;l.shadows=i%3==0?LightShadows.Soft:LightShadows.None;l.shadowBias=.035f;l.shadowNormalBias=.15f;l.shadowNearPlane=.15f;
    var s=new SerializedObject(go.GetComponent<UniversalAdditionalLightData>());s.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue=1;s.ApplyModifiedPropertiesWithoutUndo();lights.Add(l);
   }
   env.poleLights=env.poleIllumination=lights.ToArray();env.roofLights=env.roofIllumination=env.standIllumination=Array.Empty<Light>();env.poleEmitters=heads.ToArray();env.roofEmitters=env.decorEmitters=Array.Empty<Renderer>();env.lightsOn=on;env.lightsOff=steel;
   env.poleNightIntensity=pro?155:125;env.roofNightIntensity=0;env.standNightIntensity=0;
   env.competitionSurface=visual.GetComponentsInChildren<MeshFilter>().Single(f=>f.name.StartsWith("Court_"));env.kitchenSurface=env.courtLines=env.competitionSurface;env.netStructure=visual.GetComponentsInChildren<MeshFilter>().First(f=>f.name=="PortableNet_White_Headband");
   env.nearEntrance=Anchor(root.transform,"Near Entrance",new Vector3(0,0,pro?-9.2f:-8.65f));env.farEntrance=Anchor(root.transform,"Far Entrance",new Vector3(0,0,pro?9.2f:8.65f));env.nearResult=Anchor(root.transform,"Near Result",new Vector3(0,0,-4.8f));env.farResult=Anchor(root.transform,"Far Result",new Vector3(0,0,4.8f));env.courtsideCameraLimits=new Vector2(pro?5.85f:4.35f,pro?10.4f:8.8f);
   env.ApplyNight(0);PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/Courts/"+id+".prefab");
   reports.Add(new{id,renderers=root.GetComponentsInChildren<Renderer>().Length,lights=lights.Count,shadowSources=lights.Count(l=>l.shadows!=LightShadows.None),materials=converted.Count});UnityEngine.Object.DestroyImmediate(root);
  }
  AssetDatabase.SaveAssets();File.WriteAllText(Report+"/build.json",JsonConvert.SerializeObject(reports,Formatting.Indented));return reports;
 }
 static void BindVenue(Scene scene,StadiumEnvironment env)
 {
  foreach(var root in scene.GetRootGameObjects())
  {
   foreach(var l in root.GetComponentsInChildren<MatchEnvironmentLighting>(true)){l.stadium=env;l.minimumCourtIllumination=.2f;l.Apply(l.selectedNight);EditorUtility.SetDirty(l);}
   foreach(var home in root.GetComponentsInChildren<HomeLobbyPresentation>(true)){home.stadium=env;home.floodlights=Array.Empty<Light>();home.lampFaces=Array.Empty<Renderer>();home.EvaluateCycle(0);EditorUtility.SetDirty(home);}
   foreach(var intro in root.GetComponentsInChildren<BotMatchIntro>(true)){intro.playerEntrance=env.nearEntrance;intro.opponentEntrance=env.farEntrance;EditorUtility.SetDirty(intro);}
   foreach(var result in root.GetComponentsInChildren<BotMatchVictory>(true)){result.nearPresentation=env.nearResult;result.farPresentation=env.farResult;EditorUtility.SetDirty(result);}
  }
 }
 static StadiumEnvironment ReplaceVenue(Scene scene,string id)
 {
  foreach(var env in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<StadiumEnvironment>(true)).ToArray())UnityEngine.Object.DestroyImmediate(env.gameObject);
  var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Courts/"+id+".prefab"),scene);var current=go.GetComponent<StadiumEnvironment>();BindVenue(scene,current);return current;
 }
 public static object Scenes()
 {
  if(EditorApplication.isPlaying||SceneManager.GetActiveScene().isDirty)throw new Exception("Clean Edit mode required");string original=SceneManager.GetActiveScene().path;Directory.CreateDirectory(Report+"/Scenes");
  try
  {
   foreach(string path in new[]{"Assets/Scenes/UI/Menu.unity","Assets/Scenes/BotMatch.unity","Assets/Scenes/MultiplayerCourt.unity"})
   {
    var scene=EditorSceneManager.OpenScene(path);if(!File.Exists(Report+"/Scenes/"+scene.name+"-before.unity"))EditorSceneManager.SaveScene(scene,Report+"/Scenes/"+scene.name+"-before.unity",true);
    var env=ReplaceVenue(scene,"Professional");var lighting=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MatchEnvironmentLighting>(true)).FirstOrDefault();
    if(lighting)
    {
     var choice=lighting.GetComponent<CourtVenueSelection>();if(!choice)choice=lighting.gameObject.AddComponent<CourtVenueSelection>();choice.current=env;choice.selected=CourtVenue.Professional;choice.useBotPreference=scene.name=="BotMatch";EditorUtility.SetDirty(choice);
     lighting.sun.color=new Color(1,.98f,.95f);lighting.sun.intensity=1.1f;EditorUtility.SetDirty(lighting.sun);
    }
    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   }
   return new{menu="Professional",bot="Professional or LowPoly",online="Host-selected Professional or LowPoly",scenes=3};
  }
  finally{EditorSceneManager.OpenScene(original);}
 }
 static void Place(RectTransform t,float x,float y,float right,float top){t.anchorMin=new Vector2(x,y);t.anchorMax=new Vector2(right,top);t.offsetMin=t.offsetMax=Vector2.zero;}
 static void Shot(Camera camera,string path)
 {
  var rt=RenderTexture.GetTemporary(640,360,24);var image=new Texture2D(640,360,TextureFormat.RGB24,false);var previous=camera.targetTexture;var active=RenderTexture.active;
  try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,640,360),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
  finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);}
 }
 public static object Previews()
 {
  if(EditorApplication.isPlaying)throw new Exception("Stop Play first");string original=SceneManager.GetActiveScene().path;Directory.CreateDirectory("Assets/FrontEndContent/Artwork/Courts");AssetDatabase.Refresh();
  try
  {
   foreach(string id in new[]{"Professional","LowPoly"})
   {
    var scene=EditorSceneManager.OpenScene("Assets/Scenes/BotMatch.unity");var env=ReplaceVenue(scene,id);var l=UnityEngine.Object.FindAnyObjectByType<MatchEnvironmentLighting>();var cam=l.matchCamera;
    cam.transform.SetPositionAndRotation(new Vector3(0,6.5f,-11.8f),Quaternion.Euler(34,0,0));cam.fieldOfView=57;
    foreach(bool night in new[]{false,true})
    {
     l.SelectForMatch(night);string path="Assets/FrontEndContent/Artwork/Courts/"+id+(night?"_Night":"_Day")+".png";Shot(cam,path);AssetDatabase.ImportAsset(path);
     var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.mipmapEnabled=false;importer.maxTextureSize=512;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
    }
   }
   return new{previews=4,resolution="512px import cap"};
  }
  finally{EditorSceneManager.OpenScene(original);}
 }
 static TMP_Text Label(Transform parent,string name,string value,float x,float y,float right,float top,int size)
 {
  var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);Place(rect,x,y,right,top);var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.font=ScreenUiTheme.Current.font;text.text=value;text.fontSize=size;text.enableAutoSizing=true;text.fontSizeMax=size;text.fontSizeMin=size*.75f;text.color=ScreenUiTheme.Text;text.raycastTarget=false;text.alignment=TextAlignmentOptions.MidlineLeft;return text;
 }
 public static object Mode()
 {
  if(EditorApplication.isPlaying)throw new Exception("Stop Play first");string original=SceneManager.GetActiveScene().path;
  try
  {
   var scene=EditorSceneManager.OpenScene("Assets/Scenes/UI/ModeSelection.unity");if(!File.Exists(Report+"/Scenes/ModeSelection-before.unity"))EditorSceneManager.SaveScene(scene,Report+"/Scenes/ModeSelection-before.unity",true);var ui=UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>();var p=ui.botPage.transform.Find("Setup");
   Place((RectTransform)p.Find("DifficultyChoices"),0,.79f,1,.92f);Place(ui.difficultyDescription.rectTransform,0,.705f,1,.78f);ui.difficultyDescription.enableAutoSizing=true;ui.difficultyDescription.fontSizeMax=14;ui.difficultyDescription.fontSizeMin=11;
   var heading=p.Find("EnvironmentHeading").GetComponent<TMP_Text>();heading.text="02   THỜI ĐIỂM";Place(heading.rectTransform,0,.625f,1,.695f);
   for(int i=0;i<2;i++)
   {
    var button=ui.environmentButtons[i];Place((RectTransform)button.transform,i==0?0:.515f,.50f,i==0?.485f:1,.615f);button.transform.Find("PreviewFrame").gameObject.SetActive(false);
    Place((RectTransform)button.transform.Find("Name"),.05f,.1f,.61f,.9f);Place(ui.environmentSelected[i].rectTransform,.60f,.1f,.96f,.9f);
   }
   var old=p.Find("VenueChoices");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);old=p.Find("VenueHeading");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
   var h=Label(p,"VenueHeading","03   CHỌN SÂN",0,.405f,1,.485f,18);h.color=ScreenUiTheme.Mint;
   var row=new GameObject("VenueChoices",typeof(RectTransform)).GetComponent<RectTransform>();row.SetParent(p,false);Place(row,0,.235f,1,.395f);
   ui.venueButtons=new UnityEngine.UI.Button[2];ui.venueSelected=new TMP_Text[2];ui.venueDayPreviews=new Texture2D[2];ui.venueNightPreviews=new Texture2D[2];
   for(int i=0;i<2;i++)
   {
    var rect=new GameObject(i==0?"Professional":"LowPoly",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button)).GetComponent<RectTransform>();rect.SetParent(row,false);Place(rect,i==0?0:.515f,0,i==0?.485f:1,1);var button=rect.GetComponent<UnityEngine.UI.Button>();ScreenUiTheme.ButtonStyle(button);ui.venueButtons[i]=button;
    var label=Label(rect,"Name",i==0?"CHUYÊN NGHIỆP":"NGOÀI TRỜI",.05f,.34f,.95f,.90f,17);label.alignment=TextAlignmentOptions.Center;
    ui.venueSelected[i]=Label(rect,"Selected","ĐÃ CHỌN",.05f,.05f,.95f,.35f,10);ui.venueSelected[i].alignment=TextAlignmentOptions.Center;ui.venueSelected[i].color=ScreenUiTheme.Gold;ui.venueSelected[i].gameObject.SetActive(i==0);
    string id=i==0?"Professional":"LowPoly";ui.venueDayPreviews[i]=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FrontEndContent/Artwork/Courts/"+id+"_Day.png");ui.venueNightPreviews[i]=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FrontEndContent/Artwork/Courts/"+id+"_Night.png");
    if(i==0)UnityEventTools.AddPersistentListener(button.onClick,ui.SelectProfessional);else UnityEventTools.AddPersistentListener(button.onClick,ui.SelectLowPoly);
   }
   Place(ui.summary.rectTransform,0,.14f,1,.225f);ui.summary.enableAutoSizing=true;ui.summary.fontSizeMax=12;ui.summary.fontSizeMin=9;p.Find("Format").gameObject.SetActive(false);Place((RectTransform)ui.startButton.transform,0,0,1,.13f);
   ui.dayPreview=ui.venueDayPreviews[0];ui.nightPreview=ui.venueNightPreviews[0];ui.courtPreview.texture=ui.dayPreview;
   var modePreview=ui.botButton.GetComponentInChildren<UnityEngine.UI.RawImage>(true);if(modePreview)modePreview.texture=ui.dayPreview;
   var hero=ui.botPage.transform.Find("CourtHero");var title=hero.Find("CourtTitle").GetComponent<TMP_Text>();title.text="Hai sân đấu. Một niềm đam mê.";title.enableAutoSizing=true;title.fontSizeMin=17;title.fontSizeMax=23;
   EditorUtility.SetDirty(ui);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);return new{venueChoices=2,previews=4,existingBindingsPreserved=true};
  }
  finally{EditorSceneManager.OpenScene(original);}
 }
}
