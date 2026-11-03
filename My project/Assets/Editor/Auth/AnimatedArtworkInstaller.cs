using System;
using System.Linq;
using PickleVerse.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace PickleVerse.EditorTools
{
    public static class AnimatedArtworkInstaller
    {
        const string Login = "Assets/FrontEndContent/Login/Playback/Login";
        const string Loading = "Assets/FrontEndContent/Loading/Artwork/Playback/PKV_";
        public static readonly string[] Captions = {
            "01  /  BỨT PHÁ TRÊN SÂN", "02  /  ĐÓN BÓNG. TỎA SÁNG.",
            "03  /  SÂN ĐẤU CỦA BẠN", "04  /  SẴN SÀNG CHO CÚ ĐÁNH TIẾP THEO"
        };
        public static void ConfigureImports()
        {
            foreach (var prefix in new[] { Login }.Concat(Enumerable.Range(1, 4).Select(i => Loading + i)))
            {
                var image = (TextureImporter)AssetImporter.GetAtPath(prefix + ".jpg");
                image.textureType = TextureImporterType.Default;
                image.mipmapEnabled = image.isReadable = false;
                image.alphaSource = TextureImporterAlphaSource.None;
                image.maxTextureSize = 2048;
                image.wrapMode = TextureWrapMode.Clamp;
                image.textureCompression = TextureImporterCompression.CompressedHQ;
                image.SaveAndReimport();
                // Already encoded for native H.264 playback; avoid a second lossy transcode.
                var video = (VideoClipImporter)AssetImporter.GetAtPath(prefix + ".mp4");
                var settings = video.defaultTargetSettings;
                settings.enableTranscoding = false;
                video.defaultTargetSettings = settings;
                video.importAudio = false;
                video.SaveAndReimport();
            }
        }
        public static void ConfigureLogin(RawImage image)
        {
            var motion = image.GetComponent<CinematicArtwork>();
            if (!motion) motion = image.gameObject.AddComponent<CinematicArtwork>();
            motion.clip = Movie(Login); motion.poster = Poster(Login); motion.playOnEnable = true;
            image.texture = motion.poster;
        }
        static VideoClip Movie(string prefix) => AssetDatabase.LoadAssetAtPath<VideoClip>(prefix + ".mp4");
        static Texture2D Poster(string prefix) => AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + ".jpg");
        public static object Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play before installing artwork.");
            ConfigureImports();
            var path = "Assets/Resources/Loading/LoadingScreen.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = prefab.GetComponent<LoadingScreenView>();
                view.movies = Enumerable.Range(1, 4).Select(i => Movie(Loading + i)).ToArray();
                view.pictures = Enumerable.Range(1, 4).Select(i => Poster(Loading + i)).ToArray();
                view.pictureCaptions = Captions.ToArray();
                foreach (var image in new[] { view.artwork, view.nextArtwork })
                {
                    var motion = image.GetComponent<CinematicArtwork>();
                    if (!motion) motion = image.gameObject.AddComponent<CinematicArtwork>();
                    motion.playOnEnable = false;
                    image.texture = view.pictures[0];
                }
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var original = SceneManager.GetActiveScene();
            var login = SceneManager.GetSceneByPath(LoginSceneInstaller.ScenePath);
            bool opened = !login.IsValid() || !login.isLoaded;
            if (opened) login = EditorSceneManager.OpenScene(LoginSceneInstaller.ScenePath, OpenSceneMode.Additive);
            try
            {
                var image = login.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RawImage>(true)).Single();
                ConfigureLogin(image);
                EditorSceneManager.MarkSceneDirty(login);
                EditorSceneManager.SaveScene(login);
            }
            finally
            {
                SceneManager.SetActiveScene(original);
                if (opened) EditorSceneManager.CloseScene(login, true);
            }
            AssetDatabase.SaveAssets();
            return new { login = LoginSceneInstaller.ScenePath, loading = path, loadingMovies = 4 };
        }
    }
}
