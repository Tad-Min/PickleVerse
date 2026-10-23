using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.FrontEnd;
using PickleVerse.Shop;

public static class RemainingUiSetup
{
    public static object Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play before authoring");
        if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new Exception("An open scene has unsaved edits");
        string original=SceneManager.GetActiveScene().path;
        string folder=Path.GetFullPath("../UnityWork/RemainingUi20261022/Before");Directory.CreateDirectory(folder);
        string[] paths={"Assets/Scenes/UI/Menu.unity","Assets/Scenes/Shop.unity","Assets/Scenes/Collection.unity"};
        try
        {
            foreach(string path in paths)
            {
                string copy=Path.Combine(folder,Path.GetFileName(path));if(!File.Exists(copy))File.Copy(path,copy);
                var scene=EditorSceneManager.OpenScene(path);
                if(scene.name=="Menu")
                {
                    var ui=UnityEngine.Object.FindAnyObjectByType<PlayerProgressionMenu>();ui.ApplyPresentation();
                    UnityEngine.Object.FindAnyObjectByType<OfflineSocialMenu>().ApplyPresentation();
                    ui.modal.gameObject.SetActive(false);ui.GetComponent<OfflineSocialMenu>().modal.gameObject.SetActive(false);
                }
                else ShopPanelPresentation.Install(UnityEngine.Object.FindAnyObjectByType<ShopScreen>());
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
        }
        finally{if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original);}
        AssetDatabase.SaveAssets();return new{scenes=paths,newTextures=0,newFonts=0};
    }
}
