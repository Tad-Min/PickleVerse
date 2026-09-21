using UnityEngine;
using UnityEditor;

public class ForceUpdateScene : EditorWindow
{
    [MenuItem("Tools/Force Update Scene Prefabs")]
    public static void ForceUpdate()
    {
        var court = GameObject.Find("PickleballCourt");
        if (court != null) DestroyImmediate(court);

        var paddle = GameObject.Find("PickleballPaddle");
        if (paddle != null) DestroyImmediate(paddle);

        SetupAICoachScene.SetupScene();
        Debug.Log("Scene forced update complete!");
    }
}
