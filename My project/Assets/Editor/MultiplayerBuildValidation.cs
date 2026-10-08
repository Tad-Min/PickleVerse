using System.Linq;
using PickleVerse.Multiplayer;
using PickleVerse.Multiplayer.Services;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace PickleVerse.EditorTools
{
    public sealed class MultiplayerBuildValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (!scenes.Contains(MultiplayerSceneConfiguration.RoomPath) && !scenes.Contains(MultiplayerSceneConfiguration.CourtPath)) return;
            foreach (var path in new[] { MultiplayerSceneConfiguration.RoomPath, MultiplayerSceneConfiguration.CourtPath })
                if (!scenes.Contains(path)) throw new BuildFailedException("Multiplayer scene missing from active build profile: " + path);
            var settings = UnityEngine.Resources.Load<MultiplayerSettings>("MultiplayerSettings");
            if (!settings || !settings.maleActorPrefab || !settings.femaleActorPrefab || !settings.ballVisualPrefab)
                throw new BuildFailedException("Run PlayMultiplayer integration to assign BotMatch actor and ball prefabs.");
        }
    }
}
