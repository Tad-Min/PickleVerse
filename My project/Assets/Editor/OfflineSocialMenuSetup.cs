using System;
using PickleVerse.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PickleVerse.EditorTools
{
    public static class OfflineSocialMenuSetup
    {
        [MenuItem("PickleVerse/Social/Configure Offline Menu")]
        public static void Configure()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Dừng Play trước khi cấu hình Menu.");
            var scene=SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/UI/Menu.unity") throw new InvalidOperationException("Mở Scene Menu trước khi cấu hình.");
            var ui=UnityEngine.Object.FindAnyObjectByType<PlayerProgressionMenu>();
            if(!ui) throw new InvalidOperationException("Menu cần PlayerProgressionMenu đã cấu hình.");
            var social=ui.GetComponent<OfflineSocialMenu>();
            if(!social) social=ui.gameObject.AddComponent<OfflineSocialMenu>();
            social.ui=ui; social.ConfigureWelcomeCard();
            if(!social.modal) social.BuildShell();
            social.ConfigureOverlay();
            EditorUtility.SetDirty(social.modal.GetComponent<Canvas>());
            EditorUtility.SetDirty(social); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}
