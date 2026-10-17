using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
public static class UiUpgradeAudit
{
 public static object Run()
 {
  if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
  string original=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
  string folder=Path.GetFullPath("../UnityWork/AnimeScreens20261016");Directory.CreateDirectory(folder);
  string[] paths={"Assets/Scenes/UI/ModeSelection.unity","Assets/Scenes/UI/Settings.unity","Assets/Scenes/UI/PlayMultiplayer.unity","Assets/Scenes/Collection.unity","Assets/Scenes/Shop.unity","Assets/Scenes/Tutorial.unity","Assets/Scenes/Replay.unity"};
  foreach(string path in paths){var scene=EditorSceneManager.OpenScene(path);var sb=new StringBuilder();foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<RectTransform>(true)){var names=new System.Collections.Generic.List<string>();for(Transform p=t;p;p=p.parent)names.Insert(0,p.name);var text=t.GetComponent<TMP_Text>();sb.AppendLine(string.Join("/",names)+" | "+t.anchorMin+".."+t.anchorMax+" offset "+t.offsetMin+".."+t.offsetMax+" | "+string.Join(",",t.GetComponents<Component>().Where(c=>c).Select(c=>c.GetType().Name))+" | "+(text?text.text.Replace('\n',' '):""));}File.WriteAllText(Path.Combine(folder,scene.name+"-before.txt"),sb.ToString());}
  EditorSceneManager.OpenScene(original);return new{folder,scenes=paths.Length};
 }
}
