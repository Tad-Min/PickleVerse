using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using PickleVerse.Multiplayer;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace PickleVerse.EditorTools
{
    public static class MultiplayerSetup
    {
        static TMP_FontAsset font;
        static Color panelColor=new Color(.065f,.105f,.18f),accent=new Color(.09f,.58f,.82f);
        const string PrefabFolder="Assets/Prefabs/Multiplayer";
        [MenuItem("PickleVerse/Multiplayer/Generate Test Scenes")]
        public static void Generate()
        {
            Directory.CreateDirectory(PrefabFolder); Directory.CreateDirectory("Assets/Resources"); Directory.CreateDirectory("Assets/Settings/Multiplayer"); Directory.CreateDirectory("Assets/Settings/BuildProfiles");
            AssetDatabase.Refresh();
            font=PrepareFont();
            var settings=AssetDatabase.LoadAssetAtPath<MultiplayerSettings>("Assets/Resources/MultiplayerSettings.asset");
            if(settings==null) { settings=ScriptableObject.CreateInstance<MultiplayerSettings>(); AssetDatabase.CreateAsset(settings,"Assets/Resources/MultiplayerSettings.asset"); }
            settings.maleModel=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Characters/Male.glb");
            settings.femaleModel=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Characters/Female.glb");
            settings.maleAnimator=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animation/Characters/MalePlayerAnimator.controller");
            settings.femaleAnimator=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animation/Characters/FemalePlayerAnimator.controller");
            settings.paddlePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/PickleballPaddle.prefab");
            settings.ballMaterial=MaterialAsset("Ball",new Color(.95f,1,.12f),false);
            settings.trailMaterial=MaterialAsset("Trail",new Color(.85f,1,.25f),true);
            settings.teamAMaterial=MaterialAsset("TeamA",new Color(.13f,.65f,1),true);
            settings.teamBMaterial=MaterialAsset("TeamB",new Color(1,.45f,.15f),true);
            var go=new GameObject("NetworkPlayer",typeof(NetworkObject),typeof(NetworkPlayer));
            var collider=go.AddComponent<CapsuleCollider>(); collider.height=1.8f; collider.radius=.23f; collider.center=new Vector3(0,.9f,0);
            settings.playerPrefab=PrefabUtility.SaveAsPrefabAsset(go,PrefabFolder+"/NetworkPlayer.prefab"); UnityEngine.Object.DestroyImmediate(go);
            go=new GameObject("NetworkMatch",typeof(NetworkObject),typeof(NetworkMatch));
            settings.matchPrefab=PrefabUtility.SaveAsPrefabAsset(go,PrefabFolder+"/NetworkMatch.prefab"); UnityEngine.Object.DestroyImmediate(go);
            settings.animations=CalibrateAnimations(settings);
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
            var original=SceneManager.GetActiveScene();
            if(!File.Exists("Assets/Scenes/MultiplayerTest.unity")) GenerateTestScene();
            if(!File.Exists("Assets/Scenes/MultiplayerCourt.unity")) GenerateCourt();
            WireMenu();
            var profile=AssetDatabase.LoadAssetAtPath<BuildProfile>("Assets/Settings/BuildProfiles/MultiplayerWindows.asset");
            if(profile==null)
            {
                profile=BuildProfile.GetAllBuildProfiles().FirstOrDefault(p=>p.name=="AssetsSettingsBuildProfilesMultiplayerWindows.asset");
                if(profile==null) profile=BuildProfile.CreateBuildProfile(new UnityEngine.GUID("4e3c793746204150860bf175a9a41a05"),"MultiplayerWindows",null);
                var createdPath=AssetDatabase.GetAssetPath(profile);
                AssetDatabase.MoveAsset(createdPath,"Assets/Settings/BuildProfiles/MultiplayerWindows.asset"); profile.name="MultiplayerWindows";
            }
            profile.overrideGlobalScenes=true;
            profile.scenes=new[] { new EditorBuildSettingsScene("Assets/Scenes/MultiplayerTest.unity",true),new EditorBuildSettingsScene("Assets/Scenes/MultiplayerCourt.unity",true),new EditorBuildSettingsScene("Assets/Scenes/HomeScene/Menu.unity",true) };
            EditorUtility.SetDirty(profile); PlayerSettings.runInBackground=true;
            AssetDatabase.SaveAssets(); if(original.IsValid()&&original.isLoaded) SceneManager.SetActiveScene(original);
            Debug.Log("PickleVerse multiplayer scenes, prefabs, animation markers and Windows profile generated.");
        }
        static TMP_FontAsset PrepareFont()
        {
            var existing=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Settings/Multiplayer/VietnameseFont.asset"); if(existing!=null) return existing;
            string path="Assets/Settings/Multiplayer/Arial.ttf";
            if(!File.Exists(path)) File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"arial.ttf"),path);
            AssetDatabase.ImportAsset(path);
            var source=AssetDatabase.LoadAssetAtPath<Font>(path);
            var asset=TMP_FontAsset.CreateFontAsset(source,64,9,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic);
            asset.name="VietnameseFont"; AssetDatabase.CreateAsset(asset,"Assets/Settings/Multiplayer/VietnameseFont.asset");
            foreach(var texture in asset.atlasTextures) AssetDatabase.AddObjectToAsset(texture,asset);
            AssetDatabase.AddObjectToAsset(asset.material,asset);
            asset.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚĂĐĨŨƠƯẠẢẤẦẨẪẬẮẰẲẴẶẸẺẼỀẾỂỄỆỈỊỌỎỐỒỔỖỘỚỜỞỠỢỤỦỨỪỬỮỰỲỴỶỸàáâãèéêìíòóôõùúăđĩũơưạảấầẩẫậắằẳẵặẹẻẽềếểễệỉịọỏốồổỗộớờởỡợụủứừửữựỳỵỷỹ—–…:./()•+",out _);
            EditorUtility.SetDirty(asset); return asset;
        }
        static Material MaterialAsset(string name,Color color,bool unlit)
        {
            string path="Assets/Settings/Multiplayer/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) { material=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,path); }
            material.color=color; EditorUtility.SetDirty(material); return material;
        }
        static ShotAnimationLibrary CalibrateAnimations(MultiplayerSettings settings)
        {
            var asset=AssetDatabase.LoadAssetAtPath<ShotAnimationLibrary>("Assets/Settings/Multiplayer/ShotAnimations.asset");
            if(asset==null) { asset=ScriptableObject.CreateInstance<ShotAnimationLibrary>(); AssetDatabase.CreateAsset(asset,"Assets/Settings/Multiplayer/ShotAnimations.asset"); }
            var entries=new List<ShotAnimationLibrary.Entry>(); var report=new List<object>();
            foreach(string gender in new[]{"Male","Female"})
            {
                var model=UnityEngine.Object.Instantiate(gender=="Male"?settings.maleModel:settings.femaleModel);
                model.hideFlags=HideFlags.HideAndDontSave;
                var hand=model.GetComponentsInChildren<Transform>().First(t=>t.name.EndsWith("RightHand"));
                var hip=model.GetComponentsInChildren<Transform>().First(t=>t.name.EndsWith("Hips"));
                foreach(string guid in AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets/Animation/Characters/"+gender}))
                {
                    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                    bool shot=clip.name.StartsWith("Groundstroke")||clip.name.StartsWith("Dink")||clip.name.StartsWith("Volley")||clip.name.StartsWith("ThirdShotDrop")||clip.name=="Overhead_Smash"||clip.name=="Serve_FH_Underhand";
                    var clipSettings=AnimationUtility.GetAnimationClipSettings(clip);
                    bool loop=clip.name=="Idle_Ready"||clip.name.Contains("Walk")||clip.name.Contains("Run")||clip.name.StartsWith("Shuffle")||clip.name=="Backpedal";
                    if(clipSettings.loopTime!=loop) { clipSettings.loopTime=loop; AnimationUtility.SetAnimationClipSettings(clip,clipSettings); EditorUtility.SetDirty(clip); }
                    if(!shot) continue;
                    int samples=Mathf.CeilToInt(clip.length*clip.frameRate); var positions=new Vector3[samples+1]; var heights=new float[samples+1];
                    for(int i=0;i<=samples;i++) { clip.SampleAnimation(model,i/clip.frameRate); positions[i]=model.transform.InverseTransformPoint(hand.position); heights[i]=hip.position.y-model.transform.position.y; }
                    int best=Mathf.Clamp(samples/2,1,samples); float bestSpeed=-1;
                    for(int i=Mathf.Max(1,samples/6);i<samples*3/4;i++)
                    {
                        var delta=positions[i]-positions[i-1];
                        if(clip.name=="Serve_FH_Underhand" && (positions[i].y>heights[i]+.08f||delta.y<=0)) continue;
                        float speed=delta.sqrMagnitude;
                        if(speed>bestSpeed) { bestSpeed=speed; best=i; }
                    }
                    float normalized=best/(float)samples;
                    entries.Add(new ShotAnimationLibrary.Entry { state=clip.name,gender=gender,contactNormalized=normalized,releaseNormalized=Mathf.Max(.05f,normalized-.12f),duration=clip.name=="Serve_FH_Underhand"?1.3f:clip.name.StartsWith("Volley")?.55f:.8f,contactLocal=positions[best] });
                    report.Add(new {gender,clip=clip.name,clip.length,clip.frameRate,contactFrame=best,contactNormalized=normalized,contact=new { x=positions[best].x,y=positions[best].y,z=positions[best].z },validServeSample=clip.name!="Serve_FH_Underhand"||bestSpeed>=0});
                }
                UnityEngine.Object.DestroyImmediate(model);
            }
            asset.entries=entries.ToArray(); EditorUtility.SetDirty(asset);
            Directory.CreateDirectory("Docs"); File.WriteAllText("Docs/MultiplayerAnimationCalibration.json",JsonConvert.SerializeObject(report,Formatting.Indented));
            return asset;
        }
        static GameObject Rect(string name,Transform parent,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax)
        {
            var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>(); rect.anchorMin=min; rect.anchorMax=max; rect.offsetMin=offsetMin; rect.offsetMax=offsetMax; return go;
        }
        static GameObject Panel(string name,Transform parent,Vector2 min,Vector2 max,Color color)
        {
            var go=Rect(name,parent,min,max,Vector2.zero,Vector2.zero); var image=go.AddComponent<UnityEngine.UI.Image>(); image.color=color; image.raycastTarget=false; return go;
        }
        static TMP_Text Text(string name,Transform parent,string value,Vector2 min,Vector2 max,int size=22)
        {
            var go=Rect(name,parent,min,max,new Vector2(8,4),new Vector2(-8,-4)); var text=go.AddComponent<TextMeshProUGUI>(); text.font=font; text.text=value; text.fontSize=size; text.color=new Color(.91f,.95f,1); text.alignment=TextAlignmentOptions.MidlineLeft; text.raycastTarget=false; return text;
        }
        static UnityEngine.UI.Button Button(string name,Transform parent,string label,Vector2 min,Vector2 max,Color? color=null)
        {
            var go=Rect(name,parent,min,max,new Vector2(4,4),new Vector2(-4,-4)); var image=go.AddComponent<UnityEngine.UI.Image>(); image.color=color??accent;
            var button=go.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic=image;
            var colors=button.colors; colors.disabledColor=new Color(.35f,.42f,.48f,.6f); colors.highlightedColor=new Color(1.15f,1.15f,1.15f); button.colors=colors;
            var text=Text("Label",go.transform,label,Vector2.zero,Vector2.one,20); text.alignment=TextAlignmentOptions.Center; return button;
        }
        static TMP_InputField Input(string name,Transform parent,string placeholder,Vector2 min,Vector2 max)
        {
            var go=Rect(name,parent,min,max,new Vector2(4,4),new Vector2(-4,-4)); go.AddComponent<UnityEngine.UI.Image>().color=new Color(.12f,.18f,.26f);
            var input=go.AddComponent<TMP_InputField>(); var viewport=Rect("TextArea",go.transform,Vector2.zero,Vector2.one,new Vector2(12,4),new Vector2(-12,-4)); viewport.AddComponent<UnityEngine.UI.RectMask2D>();
            input.textViewport=viewport.GetComponent<RectTransform>(); input.textComponent=(TextMeshProUGUI)Text("Text",viewport.transform,"",Vector2.zero,Vector2.one,21);
            var hint=Text("Placeholder",viewport.transform,placeholder,Vector2.zero,Vector2.one,20); hint.color=new Color(.6f,.68f,.77f); input.placeholder=hint; input.characterLimit=24; return input;
        }
        static GameObject Canvas()
        {
            var canvas=new GameObject("Canvas",typeof(RectTransform),typeof(UnityEngine.Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            canvas.GetComponent<UnityEngine.Canvas>().renderMode=RenderMode.ScreenSpaceCamera;
            canvas.GetComponent<UnityEngine.Canvas>().worldCamera=Camera.main;
            canvas.GetComponent<UnityEngine.Canvas>().planeDistance=1;
            var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1280,720); scaler.matchWidthOrHeight=.5f;
            new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule)); return canvas;
        }
        static void GenerateTestScene()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
            var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)); camera.tag="MainCamera"; camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor; camera.GetComponent<Camera>().backgroundColor=new Color(.025f,.045f,.08f);
            var canvas=Canvas(); var ui=canvas.AddComponent<MultiplayerTestUI>();
            Text("Brand",canvas.transform,"PICKLEVERSE  /  MULTIPLAYER TEST",new Vector2(.045f,.89f),new Vector2(.94f,.98f),30);
            ui.statusText=Text("Status",canvas.transform,"",new Vector2(.045f,.025f),new Vector2(.96f,.13f),20);
            ui.menuPanel=Panel("PlayPanel",canvas.transform,new Vector2(.26f,.2f),new Vector2(.74f,.83f),panelColor);
            Text("Title",ui.menuPanel.transform,"BẮT ĐẦU CHƠI",new Vector2(.08f,.78f),new Vector2(.92f,.95f),32);
            ui.profileInput=Input("TestProfile",ui.menuPanel.transform,"Hồ sơ test: Player1",new Vector2(.08f,.6f),new Vector2(.92f,.72f));
            ui.nameInput=Input("DisplayName",ui.menuPanel.transform,"Tên người chơi",new Vector2(.08f,.44f),new Vector2(.92f,.56f));
            ui.gender=Button("Gender",ui.menuPanel.transform,"",new Vector2(.08f,.29f),new Vector2(.92f,.41f),new Color(.15f,.25f,.35f)); ui.genderText=ui.gender.GetComponentInChildren<TMP_Text>();
            ui.play=Button("Play",ui.menuPanel.transform,"PLAY",new Vector2(.08f,.1f),new Vector2(.92f,.24f));
            ui.roomPanel=Panel("WaitingRoom",canvas.transform,new Vector2(.04f,.16f),new Vector2(.96f,.87f),panelColor);
            ui.roomTitle=Text("Title",ui.roomPanel.transform,"PHÒNG 1vs1",new Vector2(.03f,.83f),new Vector2(.6f,.98f),32);
            ui.singles=Button("Mode1v1",ui.roomPanel.transform,"1vs1",new Vector2(.62f,.86f),new Vector2(.79f,.98f));
            ui.doubles=Button("Mode2v2",ui.roomPanel.transform,"2vs2",new Vector2(.8f,.86f),new Vector2(.97f,.98f));
            ui.roomCode=Text("RoomCode",ui.roomPanel.transform,"",new Vector2(.03f,.72f),new Vector2(.44f,.83f),23);
            ui.copy=Button("CopyCode",ui.roomPanel.transform,"Sao chép mã",new Vector2(.44f,.72f),new Vector2(.6f,.83f),new Color(.16f,.25f,.35f));
            ui.memberText=Text("Players",ui.roomPanel.transform,"",new Vector2(.03f,.08f),new Vector2(.38f,.7f),21); ui.memberText.alignment=TextAlignmentOptions.TopLeft;
            ui.timerText=Text("Elapsed",ui.roomPanel.transform,"00:00",new Vector2(.66f,.65f),new Vector2(.96f,.8f),42); ui.timerText.alignment=TextAlignmentOptions.Center;
            ui.find=Button("FindMatch",ui.roomPanel.transform,"TÌM TRẬN",new Vector2(.64f,.5f),new Vector2(.97f,.64f));
            ui.cancel=Button("CancelMatch",ui.roomPanel.transform,"HỦY",new Vector2(.64f,.37f),new Vector2(.97f,.49f),new Color(.7f,.24f,.2f));
            ui.codeInput=Input("JoinCode",ui.roomPanel.transform,"Nhập mã phòng",new Vector2(.64f,.24f),new Vector2(.85f,.35f));
            ui.join=Button("Join",ui.roomPanel.transform,"Vào",new Vector2(.85f,.24f),new Vector2(.97f,.35f));
            ui.customSingles=Button("Custom1v1",ui.roomPanel.transform,"Custom 1vs1",new Vector2(.64f,.12f),new Vector2(.8f,.23f),new Color(.16f,.25f,.35f));
            ui.customDoubles=Button("Custom2v2",ui.roomPanel.transform,"Custom 2vs2",new Vector2(.81f,.12f),new Vector2(.97f,.23f),new Color(.16f,.25f,.35f));
            ui.leave=Button("LeaveGroup",ui.roomPanel.transform,"Rời nhóm / phòng",new Vector2(.64f,.01f),new Vector2(.97f,.12f),new Color(.2f,.24f,.3f));
            ui.ready=Button("Ready",ui.roomPanel.transform,"Sẵn sàng",new Vector2(.64f,.5f),new Vector2(.8f,.64f));
            ui.start=Button("StartCustom",ui.roomPanel.transform,"Bắt đầu",new Vector2(.81f,.5f),new Vector2(.97f,.64f));
            ui.teamA=Button("TeamA",ui.roomPanel.transform,"Đội A",new Vector2(.64f,.37f),new Vector2(.8f,.49f));
            ui.teamB=Button("TeamB",ui.roomPanel.transform,"Đội B",new Vector2(.81f,.37f),new Vector2(.97f,.49f),new Color(.85f,.38f,.15f));
            ui.memberActions=new GameObject[4]; ui.assignA=new UnityEngine.UI.Button[4]; ui.assignB=new UnityEngine.UI.Button[4]; ui.kick=new UnityEngine.UI.Button[4];
            for(int i=0;i<4;i++)
            {
                var row=Rect("MemberActions"+i,ui.roomPanel.transform,new Vector2(.38f,.565f-i*.156f),new Vector2(.6f,.695f-i*.156f),Vector2.zero,Vector2.zero); ui.memberActions[i]=row;
                ui.assignA[i]=Button("A",row.transform,"A",Vector2.zero,new Vector2(.32f,1)); ui.assignB[i]=Button("B",row.transform,"B",new Vector2(.33f,0),new Vector2(.65f,1)); ui.kick[i]=Button("Kick",row.transform,"Mời ra",new Vector2(.66f,0),Vector2.one,new Color(.6f,.23f,.2f));
            }
            var toggle=Rect("HoldModifiers",canvas.transform,new Vector2(.64f,.9f),new Vector2(.95f,.95f),Vector2.zero,Vector2.zero);
            ui.holdModifiers=toggle.AddComponent<UnityEngine.UI.Toggle>();
            var box=Panel("Box",toggle.transform,Vector2.zero,new Vector2(.07f,1),new Color(.16f,.25f,.35f)); box.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            var check=Text("Check",box.transform,"X",Vector2.zero,Vector2.one,19); ui.holdModifiers.targetGraphic=box.GetComponent<UnityEngine.UI.Image>(); ui.holdModifiers.graphic=check;
            Text("Label",toggle.transform,"Giữ U/I làm modifier",new Vector2(.09f,0),Vector2.one,18);
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/MultiplayerTest.unity"); EditorSceneManager.CloseScene(scene,true);
        }
        static void GenerateCourt()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
            var court=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/PickleballCourt.prefab")); court.name="PickleballCourt";
            foreach(var collider in court.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name="PhysicsGround"; floor.transform.position=new Vector3(0,-.11f,0); floor.transform.localScale=new Vector3(30,.2f,30);
            floor.AddComponent<CourtSurface>().kind=CourtSurface.Kind.Ground;
            var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Multiplayer/Surround.mat");
            if(mat==null) { mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.color=new Color(.07f,.14f,.13f); AssetDatabase.CreateAsset(mat,"Assets/Settings/Multiplayer/Surround.mat"); }
            floor.GetComponent<Renderer>().sharedMaterial=mat;
            for(int i=0;i<16;i++)
            {
                float x=-3.1f+(i+.5f)*6.2f/16; float height=.8636f+Mathf.Abs(x)/3.1f*(.9144f-.8636f);
                var net=new GameObject("PhysicsNet"+i); net.transform.position=new Vector3(x,height*.5f,0); var box=net.AddComponent<BoxCollider>(); box.size=new Vector3(6.2f/16,height,.022f); net.AddComponent<CourtSurface>().kind=CourtSurface.Kind.Net;
            }
            var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)); camera.tag="MainCamera"; camera.transform.position=new Vector3(0,9,-12); camera.transform.LookAt(Vector3.zero); camera.GetComponent<Camera>().fieldOfView=58;
            var light=new GameObject("Directional Light",typeof(Light)); light.transform.rotation=Quaternion.Euler(50,-25,0); light.GetComponent<Light>().type=LightType.Directional; light.GetComponent<Light>().intensity=1.2f;
            RenderSettings.ambientLight=new Color(.5f,.55f,.65f);
            new GameObject("CourtBootstrap",typeof(CourtBootstrap)); var canvas=Canvas(); var ui=canvas.AddComponent<MultiplayerCourtUI>();
            Panel("HUDBackground",canvas.transform,new Vector2(.15f,.83f),new Vector2(.85f,.99f),panelColor);
            ui.score=Text("Score",canvas.transform,"ĐỘI A  0 : 0  ĐỘI B",new Vector2(.27f,.91f),new Vector2(.8f,.98f),30);
            ui.server=Text("Serve",canvas.transform,"",new Vector2(.25f,.85f),new Vector2(.83f,.92f),20);
            ui.status=Text("Status",canvas.transform,"",new Vector2(.15f,.13f),new Vector2(.85f,.24f),23); ui.status.alignment=TextAlignmentOptions.Center;
            ui.leave=Button("Leave",canvas.transform,"Rời trận",new Vector2(.87f,.91f),new Vector2(.99f,.98f),new Color(.65f,.23f,.2f));
            ui.returnToRoom=Button("Return",canvas.transform,"Về phòng",new Vector2(.4f,.29f),new Vector2(.6f,.38f)); ui.returnToRoom.gameObject.SetActive(false);
            Text("Controls",canvas.transform,"WASD di chuyển • Shift/Space chạy • J Groundstroke / giao bóng • K Dink • L Volley\nJ+K Drop • J+L Smash • K+L Reset (thử nghiệm) • U tấn công • I chéo sân",new Vector2(.1f,.01f),new Vector2(.9f,.12f),19).alignment=TextAlignmentOptions.Center;
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/MultiplayerCourt.unity"); EditorSceneManager.CloseScene(scene,true);
        }
        static void WireMenu()
        {
            string path="Assets/Scenes/HomeScene/Menu.unity"; var existing=SceneManager.GetSceneByPath(path); bool wasLoaded=existing.IsValid()&&existing.isLoaded;
            var scene=wasLoaded?existing:EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            var button=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UnityEngine.UI.Button>(true)).First(b=>b.name=="BtnPlay");
            var handler=button.GetComponent<MenuPlayMultiplayer>()??button.gameObject.AddComponent<MenuPlayMultiplayer>();
            bool wired=Enumerable.Range(0,button.onClick.GetPersistentEventCount()).Any(i=>button.onClick.GetPersistentTarget(i)==handler&&button.onClick.GetPersistentMethodName(i)=="Play");
            if(!wired) UnityEventTools.AddPersistentListener(button.onClick,handler.Play);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); if(!wasLoaded) EditorSceneManager.CloseScene(scene,true);
        }
    }
}
