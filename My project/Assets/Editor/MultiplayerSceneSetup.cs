using System;
using System.Linq;
using PickleVerse.Multiplayer.Services;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PickleVerse.EditorTools
{
    [InitializeOnLoad]
    public static class MultiplayerSceneSetup
    {
        static readonly string[] Required =
        {
            MultiplayerSceneConfiguration.RoomPath,
            MultiplayerSceneConfiguration.CourtPath,
            "Assets/Scenes/UI/ModeSelection.unity"
        };

        static MultiplayerSceneSetup()
        {
            ScheduleSharedScenes();
            EditorApplication.playModeStateChanged += BeforePlay;
        }

        internal static void ScheduleSharedScenes()
        {
            EditorApplication.delayCall -= EnsureSharedScenesWhenReady;
            EditorApplication.delayCall += EnsureSharedScenesWhenReady;
        }

        static void EnsureSharedScenesWhenReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                ScheduleSharedScenes();
                return;
            }
            if (Required.All(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path)))
                EnsureGlobalScenes();
        }

        static void BeforePlay(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode)
                ScheduleSharedScenes();
            if (change != PlayModeStateChange.ExitingEditMode) return;
            var profile = BuildProfile.GetActiveBuildProfile();
            bool usesMultiplayer = profile && profile.scenes.Any(scene => Required.Contains(scene.path));
            for (int i = 0; i < SceneManager.sceneCount && !usesMultiplayer; i++)
            {
                var path = SceneManager.GetSceneAt(i).path;
                usesMultiplayer = Required.Contains(path) || path == "Assets/Scenes/UI/Menu.unity";
            }
            if (usesMultiplayer) ConfigureScenes();
        }

        [MenuItem("PickleVerse/Multiplayer/Configure scenes for testing")]
        public static void ConfigureScenes()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Dừng Play trước khi cấu hình scene multiplayer.");
            foreach (var path in Required)
                if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(path))
                    throw new InvalidOperationException("Thiếu scene multiplayer: " + path + ". Hãy pull cả scene và file .meta.");
            EnsureGlobalScenes();
            var profile = BuildProfile.GetActiveBuildProfile();
            if (!profile || !profile.overrideGlobalScenes) return;
            var scenes = IncludeRequired(profile.scenes, out bool changed);
            if (!changed) return;
            profile.scenes = scenes;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
        }

        static void EnsureGlobalScenes()
        {
            // In Unity 6, 'scenes' can refer only to the active profile override.
            // Update the shared list explicitly so a fresh checkout also works.
            var scenes = IncludeRequired(EditorBuildSettings.globalScenes, out bool changed);
            if (changed) EditorBuildSettings.globalScenes = scenes;
        }

        static EditorBuildSettingsScene[] IncludeRequired(EditorBuildSettingsScene[] current, out bool changed)
        {
            var scenes = current.Select(scene => new EditorBuildSettingsScene(scene.path, scene.enabled)).ToList();
            changed = false;
            foreach (var path in Required)
            {
                var scene = scenes.FirstOrDefault(item => item.path == path);
                if (scene == null)
                {
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                    changed = true;
                }
                else if (!scene.enabled)
                {
                    scene.enabled = true;
                    changed = true;
                }
            }
            return scenes.ToArray();
        }
    }

    sealed class MultiplayerSceneImport : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom, bool didDomainReload)
        {
            // Scene assets may not be ready during InitializeOnLoad on a new pull.
            if (didDomainReload || imported.Contains(MultiplayerSceneConfiguration.RoomPath) || imported.Contains(MultiplayerSceneConfiguration.CourtPath))
                MultiplayerSceneSetup.ScheduleSharedScenes();
        }
    }
}
