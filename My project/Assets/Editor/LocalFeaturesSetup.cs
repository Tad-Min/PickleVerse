using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using PickleVerse.FrontEnd;
using PickleVerse.LocalMatch;
using PickleVerse.Multiplayer.UI;
using PickleVerse.Replays;
using PickleVerse.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PickleVerse.EditorTools
{
    public static class LocalFeaturesSetup
    {
        const string GraphPath="Assets/Resources/BhTree/Bot Behavior.asset";
        public const string ReplayPath="Assets/Scenes/Replay.unity";
        const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        [MenuItem("PickleVerse/Features/Setup")]
        public static void Setup()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Dừng Play Mode trước khi setup.");
            BuildGraph();
            Edit("Assets/Scenes/UI/PlayMultiplayer.unity",ConfigureRoom);
            Edit("Assets/Scenes/UI/ModeSelection.unity",ConfigureModes);
            Edit("Assets/Scenes/BotMatch.unity",BuildReplayAssets);
            foreach(var path in new[]{ReplayPath})
                if(!EditorBuildSettings.globalScenes.Any(s=>s.path==path))EditorBuildSettings.globalScenes=EditorBuildSettings.globalScenes.Concat(new[]{new EditorBuildSettingsScene(path,true)}).ToArray();
            var profile=UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile();
            if(profile&&profile.overrideGlobalScenes&&!profile.scenes.Any(s=>s.path==ReplayPath))
            {profile.scenes=profile.scenes.Concat(new[]{new EditorBuildSettingsScene(ReplayPath,true)}).ToArray();EditorUtility.SetDirty(profile);}
            AssetDatabase.SaveAssets();
            Debug.Log("Local features setup: BhTree graph, replay visuals, Replay scene, multiplayer back and fourth bot choice.");
        }
        static void Edit(string path,Action<Scene> edit)
        {
            var previous=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            // Preserve the user's unsaved target scene before this authorized edit.
            if(scene.isDirty){Directory.CreateDirectory("Tools/SceneBackups");EditorSceneManager.SaveScene(scene,"Tools/SceneBackups/"+scene.name+"_"+DateTime.Now.ToString("yyyyMMddHHmmss")+".unity",true);}
            try{SceneManager.SetActiveScene(scene);edit(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Không lưu được "+path);}
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);if(previous.isLoaded)SceneManager.SetActiveScene(previous);}
        }
        static T Component<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).Single();
        static void Clear(Button button){while(button.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(button.onClick,0);}
        public static void ConfigureRoom(Scene scene)
        {
            var p=Component<PlayMultiplayerPresenter>(scene);var canvas=p.GetComponentInParent<Canvas>()??Component<Canvas>(scene);
            var old=canvas.transform.Find("TxtPlayerName");if(old)Object.DestroyImmediate(old.gameObject);
            var existing=canvas.transform.Find("BtnBack");var back=existing?existing.GetComponent<Button>():Object.Instantiate(p.join,canvas.transform);
            back.name="BtnBack";Clear(back);
            var rect=(RectTransform)back.transform;rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(36,-26);rect.sizeDelta=new Vector2(200,54);
            back.GetComponentInChildren<TMP_Text>(true).text="QUAY LẠI";p.back=back;
            var error=canvas.transform.Find("NavigationError");if(!error)error=new GameObject("NavigationError",typeof(RectTransform),typeof(TextMeshProUGUI)).transform;
            error.SetParent(canvas.transform,false);var er=(RectTransform)error;er.anchorMin=er.anchorMax=new Vector2(.5f,1);er.anchoredPosition=new Vector2(0,-100);er.sizeDelta=new Vector2(900,52);
            var text=error.GetComponent<TMP_Text>();text.font=p.roomType.font;text.fontSize=22;text.alignment=TextAlignmentOptions.Center;text.color=new Color(1,.62f,.48f);text.text="";text.raycastTarget=false;p.navigationError=text;
            EditorUtility.SetDirty(p);
        }
        static void ConfigureModes(Scene scene)
        {
            var m=Component<ModeSelectionMenu>(scene);if(m.difficultyButtons.Length==4)return;
            var button=Object.Instantiate(m.difficultyButtons[2],m.difficultyButtons[2].transform.parent);button.name="Difficulty3";Clear(button);UnityEventTools.AddIntPersistentListener(button.onClick,m.SetDifficulty,3);
            button.transform.Find("Name").GetComponent<TMP_Text>().text="Tự động";
            m.difficultyButtons=m.difficultyButtons.Concat(new[]{button}).ToArray();m.difficultySelected=m.difficultySelected.Concat(new[]{button.transform.Find("Selected").GetComponent<TMP_Text>()}).ToArray();
            foreach(var b in m.difficultyButtons){var layout=b.GetComponent<LayoutElement>();if(layout){layout.minWidth=0;layout.preferredWidth=-1;layout.flexibleWidth=1;}}
            EditorUtility.SetDirty(m);
        }
        static void Strip(GameObject go)
        {
            foreach(var c in go.GetComponentsInChildren<Component>(true))
                if(c&&(c is MonoBehaviour||c is Collider||c is Rigidbody||c is Animator||c is Animation||c is AudioSource||c is AudioListener||c is TrailRenderer))Object.DestroyImmediate(c);
        }
        public static void BuildReplayAssets(Scene source)
        {
            Directory.CreateDirectory("Assets/Resources/ReplayActors");Directory.CreateDirectory("Assets/Resources/ReplayEquipment");AssetDatabase.Refresh();
            var match=Component<BotMatch>(source);
            if(match.hud&&match.hud.difficultyLabel){match.hud.difficultyLabel.rectTransform.sizeDelta=new Vector2(390,78);match.hud.difficultyLabel.alignment=TextAlignmentOptions.TopLeft;}
            foreach(var actor in new[]{match.human,match.bot})
            {
                var clone=Object.Instantiate(actor);var go=clone.gameObject;go.name=actor.playerId==0?"Female":"Male";clone.Initialize();Strip(go);
                PrefabUtility.SaveAsPrefabAsset(go,"Assets/Resources/ReplayActors/"+go.name+".prefab");Object.DestroyImmediate(go);
            }
            var catalog=Resources.Load<ShopCatalog>("Shop Catalog");if(!catalog)catalog=AssetDatabase.FindAssets("t:ShopCatalog").Select(g=>AssetDatabase.LoadAssetAtPath<ShopCatalog>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
            if(catalog)foreach(var offer in catalog.CollectionItems.Where(p=>p!=null&&p.gameplayPrefab))
            {var clone=Object.Instantiate(offer.gameplayPrefab);Strip(clone);PrefabUtility.SaveAsPrefabAsset(clone,"Assets/Resources/ReplayEquipment/"+offer.id+".prefab");Object.DestroyImmediate(clone);}
            var previous=SceneManager.GetActiveScene();var replay=SceneManager.GetSceneByPath(ReplayPath);bool loaded=replay.isLoaded;
            if(!loaded)replay=AssetDatabase.LoadAssetAtPath<SceneAsset>(ReplayPath)?EditorSceneManager.OpenScene(ReplayPath,OpenSceneMode.Additive):EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(replay);foreach(var root in replay.GetRootGameObjects())Object.DestroyImmediate(root);
                var sourceLighting=Component<MatchEnvironmentLighting>(source);
                StadiumEnvironment replayStadium=null;Light replaySun=null;
                foreach(var root in source.GetRootGameObjects().Where(g=>g.name=="Stadium Enhanced"||g.name=="Arena"||g.name=="Match Sun"))
                {var clone=Object.Instantiate(root);clone.name=root.name;if(clone.GetComponent<StadiumEnvironment>())replayStadium=clone.GetComponent<StadiumEnvironment>();if(clone.GetComponent<Light>())replaySun=clone.GetComponent<Light>();}
                var camera=new GameObject("Replay Camera",typeof(Camera));camera.tag="MainCamera";camera.transform.position=new Vector3(0,15,-20);camera.transform.LookAt(Vector3.zero);camera.GetComponent<Camera>().fieldOfView=60;camera.GetComponent<Camera>().farClipPlane=150;
                var view=new GameObject("Replay Viewer").AddComponent<ReplayView>();var ball=Object.Instantiate(match.ball.gameObject,view.transform);ball.name="Ball";Strip(ball);
                var environment=view.gameObject.AddComponent<ReplayEnvironment>();environment.daySky=sourceLighting.daySky;environment.nightSky=sourceLighting.nightSky;environment.sun=replaySun;environment.viewCamera=camera.GetComponent<Camera>();
                if(replayStadium){environment.poles=replayStadium.poleIllumination;environment.roofs=replayStadium.roofIllumination;environment.poleEmitters=replayStadium.poleEmitters;environment.roofEmitters=replayStadium.roofEmitters.Concat(replayStadium.decorEmitters).ToArray();environment.lampsOn=replayStadium.lightsOn;environment.lampsOff=replayStadium.lightsOff;}
                foreach(var root in replay.GetRootGameObjects().Where(g=>g!=view.gameObject))Strip(root);
                RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.63f,.75f,.85f);RenderSettings.ambientEquatorColor=new Color(.48f,.58f,.58f);RenderSettings.ambientGroundColor=new Color(.23f,.29f,.26f);
                if(!EditorSceneManager.SaveScene(replay,ReplayPath))throw new IOException("Không lưu được Replay scene.");
            }
            finally{if(!loaded)EditorSceneManager.CloseScene(replay,true);SceneManager.SetActiveScene(previous);}
        }
        // Unity Behavior 1.0.16 authoring models are internal. Keep all version-specific
        // authoring access here; runtime and game rules use the public package API.
        static Type TypeOf(string name)=>TypeCache.GetTypesDerivedFrom<object>().First(t=>t.FullName==name);
        static object Get(object obj,string name)=>obj.GetType().GetProperty(name,Flags)?.GetValue(obj)??obj.GetType().GetField(name,Flags)?.GetValue(obj);
        public static void BuildGraph()
        {
            var existing=AssetDatabase.LoadMainAssetAtPath(GraphPath);
            if(existing){RebuildBlackboard(Get(existing,"Blackboard"));existing.GetType().GetMethod("BuildRuntimeGraph",Flags).Invoke(existing,new object[]{true});AssetDatabase.SaveAssets();return;}
            var package=UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Unity.Behavior.BehaviorGraph).Assembly);
            if(package.version!="1.0.16")throw new InvalidOperationException("Graph setup đã kiểm chứng cho Unity Behavior 1.0.16; bản hiện tại: "+package.version);
            Directory.CreateDirectory("Assets/Resources/BhTree");AssetDatabase.Refresh();
            var author=ScriptableObject.CreateInstance(TypeOf("Unity.Behavior.BehaviorAuthoringGraph"));author.name="Bot Behavior";AssetDatabase.CreateAsset(author,GraphPath);
            author.GetType().GetMethod("EnsureAssetHasBlackboard",Flags).Invoke(author,null);
            var registry=TypeOf("Unity.Behavior.NodeRegistry");var info=registry.GetMethod("GetInfo",Flags);
            var create=author.GetType().GetMethod("CreateNode",Flags);
            object Node(string runtime,string model,Vector2 position,object port=null)
            {var nodeInfo=info.Invoke(null,new object[]{TypeOf(runtime)});if(nodeInfo==null)throw new InvalidOperationException("Node chưa đăng ký: "+runtime);return create.Invoke(author,new object[]{TypeOf(model),position,port,new[]{nodeInfo}});}
            object Output(object model)=>((IEnumerable)Get(model,"OutputPortModels")).Cast<object>().First();
            var start=Node("Unity.Behavior.Start","Unity.Behavior.StartNodeModel",new Vector2(700,0));start.GetType().GetField("Repeat",Flags).SetValue(start,false);
            var selector=Node("Unity.Behavior.SelectorComposite","Unity.Behavior.CompositeNodeModel",new Vector2(700,140),Output(start));
            string[] actions={"BhWait","BhServe","BhSmash","BhReset","BhDrop","BhDink","BhVolley","BhDrive","BhRecover"};
            for(int i=0;i<actions.Length;i++)Node("PickleVerse.LocalMatch."+actions[i],"Unity.Behavior.ActionNodeModel",new Vector2(i*250,380),Output(selector));
            var blackboard=Get(author,"Blackboard");var variables=(IList)Get(blackboard,"Variables");
            void Variable(string name,Type type){var value=Activator.CreateInstance(TypeOf("Unity.Behavior.GraphFramework.TypedVariableModel`1").MakeGenericType(type));value.GetType().GetField("Name",Flags).SetValue(value,name);variables.Add(value);}
            foreach(string name in new[]{"BallPosition","BallVelocity","BotPosition","HumanPosition","ContactPoint"})Variable(name,typeof(Vector3));
            foreach(string name in new[]{"Elo","Stroke","Bounces","Server","ExpectedHitter"})Variable(name,typeof(int));
            foreach(string name in new[]{"Swinging","InKitchen","CanAct"})Variable(name,typeof(bool));
            foreach(string name in new[]{"Phase","Branch"})Variable(name,typeof(string));
            foreach(string name in new[]{"ContactDelay","Cooldown","SimulationTime"})Variable(name,typeof(float));
            EditorUtility.SetDirty(author);RebuildBlackboard(blackboard);author.GetType().GetMethod("BuildRuntimeGraph",Flags).Invoke(author,new object[]{true});AssetDatabase.SaveAssets();
        }
        static void RebuildBlackboard(object blackboard){blackboard.GetType().GetMethod("SetAssetDirty",Flags).Invoke(blackboard,null);}
    }
}
