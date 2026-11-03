using System;
using System.Linq;
using Fusion;
using Fusion.Editor;
using Fusion.Photon.Realtime;
using UnityEditor;
using UnityEngine;

namespace PickleVerse.Editor
{
    [InitializeOnLoad]
    public static class PhotonProjectSetup
    {
        static PhotonProjectSetup(){EditorApplication.delayCall+=Configure;}
        [MenuItem("PickleVerse/Online/Configure Photon")]
        public static void Configure()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)
            {EditorApplication.delayCall+=Configure;return;}
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            FusionGlobalScriptableObjectUtils.EnsureAssetExists<NetworkProjectConfigAsset>();
            FusionGlobalScriptableObjectUtils.EnsureAssetExists<PhotonAppSettings>();
            var settings=PhotonAppSettings.Global;
            if(string.IsNullOrEmpty(settings.AppSettings.AppIdFusion))
            {
                settings.AppSettings.AppIdFusion="bde901c3-d763-47d2-b00a-ce444fe38ae9";
                settings.AppSettings.AppVersion="pickleverse-dev-1";
                EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
            }
            var config=NetworkProjectConfig.Global;
            bool changed=false;
            if(config.HubMode==NetworkProjectConfig.FusionHubMode.None){config.HubMode=NetworkProjectConfig.FusionHubMode.Advanced;changed=true;}
            if(config.PeerMode!=NetworkProjectConfig.PeerModes.Multiple){config.PeerMode=NetworkProjectConfig.PeerModes.Multiple;changed=true;}
            if(!config.AssembliesToWeave.Contains("PickleVerse.Multiplayer"))
            {config.AssembliesToWeave=config.AssembliesToWeave.Concat(new[]{"PickleVerse.Multiplayer"}).ToArray();changed=true;}
            if(changed)NetworkProjectConfigUtilities.SaveGlobalConfig(config);
            CreateLobbyScene();
        }
        [MenuItem("PickleVerse/Online/Create Photon Session Prefab")]
        public static GameObject CreateSessionPrefab()
        {
            const string folder="Assets/Resources/Photon",path=folder+"/Session State.prefab";
            if(AssetDatabase.LoadAssetAtPath<GameObject>(path))return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/Resources","Photon");
            var root=new GameObject("Photon Session State");
            try
            {
                var network=root.AddComponent<NetworkObject>();
                network.Flags=(network.Flags|NetworkObjectFlags.MasterClientObject)&~NetworkObjectFlags.DestroyWhenStateAuthorityLeaves;
                root.AddComponent<PickleVerse.Multiplayer.PhotonRoomState>();
                new NetworkObjectBakerEditTime().Bake(root);
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);
                AssetDatabase.SaveAssets();NetworkProjectConfigUtilities.RebuildPrefabTable();return prefab;
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        public static void CreateLobbyScene()
        {
            const string path="Assets/Scenes/PhotonLobby.unity";
            if(!AssetDatabase.LoadAssetAtPath<SceneAsset>(path))
            {
                var scene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Additive);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,path);
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
            }
            if(!EditorBuildSettings.globalScenes.Any(s=>s.path==path))
                EditorBuildSettings.globalScenes=EditorBuildSettings.globalScenes.Concat(new[]{new EditorBuildSettingsScene(path,true)}).ToArray();
            var profile=UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile();
            if(profile&&profile.overrideGlobalScenes)
            {
                var required=new[]{"Assets/Scenes/UI/Login.unity","Assets/Scenes/UI/Menu.unity","Assets/Scenes/UI/ModeSelection.unity","Assets/Scenes/BotMatch.unity","Assets/Scenes/Training.unity","Assets/Scenes/UI/PlayMultiplayer.unity","Assets/Scenes/MultiplayerCourt.unity","Assets/Scenes/Shop.unity","Assets/Scenes/Collection.unity","Assets/Scenes/Tutorial.unity","Assets/Scenes/Replay.unity","Assets/Scenes/UI/Loading.unity",path};
                var scenes=required.Concat(profile.scenes.Where(s=>s.enabled).Select(s=>s.path)).Distinct().Where(p=>AssetDatabase.LoadAssetAtPath<SceneAsset>(p)).Select(p=>new EditorBuildSettingsScene(p,true)).ToArray();
                if(!profile.scenes.Select(s=>s.path).SequenceEqual(scenes.Select(s=>s.path)))
                {profile.scenes=scenes;EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();}
            }
        }
        public static void CreateCommandPrefab()
        {
            const string path="Assets/Resources/Photon/Command Channel.prefab";
            if(AssetDatabase.LoadAssetAtPath<GameObject>(path))return;
            var root=new GameObject("Photon Command Channel");
            try
            {
                var network=root.AddComponent<NetworkObject>();
                network.Flags=(network.Flags|NetworkObjectFlags.DestroyWhenStateAuthorityLeaves)&~NetworkObjectFlags.MasterClientObject;
                root.AddComponent<PickleVerse.Multiplayer.PhotonCommandChannel>();new NetworkObjectBakerEditTime().Bake(root);
                PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();NetworkProjectConfigUtilities.RebuildPrefabTable();
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        public static object InspectGameplayPrefabs()
        {
            var settings=Resources.Load<PickleVerse.Multiplayer.MultiplayerSettings>("MultiplayerSettings");
            return new[]{settings.playerPrefab,settings.matchPrefab}.Select(p=>new{path=AssetDatabase.GetAssetPath(p),components=p.GetComponents<Component>().Select(c=>c?c.GetType().FullName:"Missing").ToArray()}).ToArray();
        }
        public static void MigrateGameplayPrefabs()
        {
            var settings=Resources.Load<PickleVerse.Multiplayer.MultiplayerSettings>("MultiplayerSettings");
            foreach(var prefab in new[]{settings.playerPrefab,settings.matchPrefab})
            {
                string path=AssetDatabase.GetAssetPath(prefab);var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach(var old in root.GetComponentsInChildren<Component>(true).Where(c=>c&&c.GetType().FullName=="Unity.Netcode.NetworkObject"))UnityEngine.Object.DestroyImmediate(old);
                    var network=root.GetComponent<NetworkObject>()??root.AddComponent<NetworkObject>();
                    network.Flags=(network.Flags|NetworkObjectFlags.MasterClientObject)&~NetworkObjectFlags.DestroyWhenStateAuthorityLeaves;
                    // Persist the newly added object first so Fusion can derive its stable GlobalObjectId.
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();NetworkProjectConfigUtilities.RebuildPrefabTable();
        }
    }
}
