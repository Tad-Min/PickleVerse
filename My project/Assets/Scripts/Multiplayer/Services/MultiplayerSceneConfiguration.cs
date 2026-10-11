using System;
using UnityEngine.SceneManagement;

namespace PickleVerse.Multiplayer.Services
{
    public static class MultiplayerSceneConfiguration
    {
        public const ushort ProtocolVersion = 6;
        public const string RoomPath = "Assets/Scenes/UI/PlayMultiplayer.unity";
        public const string CourtPath = "Assets/Scenes/MultiplayerCourt.unity";
        public static void Validate()
        {
            foreach (var path in new[] { RoomPath, CourtPath })
                if (SceneUtility.GetBuildIndexByScenePath(path) < 0)
                    throw new InvalidOperationException("Scene chưa được bật trong Build Profile: " + path + ". " + ConfigurationHelp);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && SceneUtility.GetBuildIndexByScenePath(scene.path) < 0)
                    throw new InvalidOperationException("Scene đang mở chưa có trong Build Profile: " + scene.path + ". Đóng scene phụ hoặc thêm vào build trước khi kết nối.");
            }
        }

        static string ConfigurationHelp =>
#if UNITY_EDITOR
            "Dừng Play, chọn PickleVerse > Multiplayer > Configure scenes for testing rồi chạy lại.";
#else
            "Build lại với profile MultiplayerWindows và dùng cùng bản build multiplayer mới trên mọi máy.";
#endif
    }
}
