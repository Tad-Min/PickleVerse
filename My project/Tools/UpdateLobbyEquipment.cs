using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PickleVerse.FrontEnd;

/// <summary>Targeted authoring; does not rerun the full menu setup.</summary>
public static class UpdateLobbyEquipment
{
    public static object Apply()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Edit mode required.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var shell=UnityEngine.Object.FindAnyObjectByType<HomeMenuShell>();
        if(scene.path!="Assets/Scenes/UI/Menu.unity"||!shell)throw new InvalidOperationException("Open Menu first.");
        if(shell.play.GetComponentInParent<UnityEngine.UI.RectMask2D>()||shell.play.GetComponentInParent<UnityEngine.UI.Mask>())
            throw new InvalidOperationException("Unexpected clipping ancestor on the Play button.");
        Undo.RegisterFullObjectHierarchyUndo(shell.play.gameObject,"Use authored lobby equipment");
        var home=shell.nav.Find("BtnHome");
        if(home)Undo.DestroyObjectImmediate(home.gameObject);
        AssetDatabase.ImportAsset("Assets/FrontEndContent/LobbyRefresh/Lobby_UI_Atlas.png",ImportAssetOptions.ForceSynchronousImport);
        var paddle=shell.paddle.GetComponent<UnityEngine.UI.Image>();
        var ball=shell.ball.GetComponent<UnityEngine.UI.Image>();
        paddle.sprite=shell.theme.paddle;ball.sprite=shell.theme.ball;
        paddle.preserveAspect=ball.preserveAspect=true;
        paddle.raycastTarget=ball.raycastTarget=false;
        var text=shell.playTitle.rectTransform;
        text.anchorMin=new Vector2(.06f,.25f);text.anchorMax=new Vector2(.94f,.82f);
        text.offsetMin=text.offsetMax=Vector2.zero;
        typeof(HomeMenuShell).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(shell,null);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return new {scene=scene.path,paddleSize=shell.paddle.sizeDelta,buttonSize=shell.play.rect.size,
            texture=AssetDatabase.GetAssetPath(paddle.sprite),sourceBytes=new FileInfo("Assets/FrontEndContent/LobbyRefresh/Lobby_UI_Atlas.png").Length};
    }
}
