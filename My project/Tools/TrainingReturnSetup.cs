using System;
using System.IO;
using System.Linq;
using PickleVerse.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TrainingReturnSetup
{
    public static object Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play before authoring Menu.");
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/UI/Menu.unity");
        var p=UnityEngine.Object.FindAnyObjectByType<PlayerProgressionMenu>();
        var home=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();
        var button=p.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name=="BtnProgression");
        Directory.CreateDirectory("../UnityWork/TrainingReturn");EditorSceneManager.SaveScene(scene,"../UnityWork/TrainingReturn/Menu.before.unity",true);
        p.progressionButton=button;button.gameObject.SetActive(true);button.interactable=true;
        button.targetGraphic.raycastTarget=true;
        foreach(var text in button.GetComponentsInChildren<TMPro.TMP_Text>())text.color=new Color(.94f,.97f,.96f);
        var icon=button.transform.Find("Nav Icon");if(icon&&icon.GetComponent<UnityEngine.UI.Graphic>())icon.GetComponent<UnityEngine.UI.Graphic>().color=new Color(.94f,.97f,.96f);
        var canvas=p.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;
        if(!home.GetComponent<PickleVerse.Settings.LobbyRenderBudget>())home.gameObject.AddComponent<PickleVerse.Settings.LobbyRenderBudget>();
        EditorUtility.SetDirty(p);EditorUtility.SetDirty(button);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return new{p.progressionButton.name,button.interactable,scene=scene.path};
    }
}
