using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using PickleVerse.Settings;
using PickleVerse.Progression;
using PickleVerse.LocalMatch;

public static class StadiumRenderValidation
{
    static void Loaded(Scene scene,LoadSceneMode mode)
    {if(scene.name=="MultiplayerCourt"){var b=UnityEngine.Object.FindAnyObjectByType<PickleVerse.Multiplayer.CourtBootstrap>();if(b)b.enabled=false;}}
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play first");
        var rows=new List<object>();Action<string,bool> check=(n,ok)=>{rows.Add(new{name=n,passed=ok});if(!ok)throw new Exception(n);};
        var runtime=ProgressionRuntime.Instance;string original=runtime.Player.id,id="StadUI"+Guid.NewGuid().ToString("N").Substring(0,10);string settings=JsonUtility.ToJson(GameSettings.Current);
        SceneManager.sceneLoaded+=Loaded;
        try
        {
            var viewType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEditor.GameView")).First(t=>t!=null);var window=UnityEditor.EditorWindow.GetWindow(viewType);window.Show();window.Focus();
            runtime.SwitchPlayer(id,"UI QA");
            foreach(string name in new[]{"BotMatch","Training","Tutorial","MultiplayerCourt"})
            {
                SceneManager.LoadScene(name);await Task.Delay(600);
                if(name=="BotMatch"){var m=UnityEngine.Object.FindAnyObjectByType<BotMatch>();m.opening?.Skip();m.Continue();}
                window.Repaint();await Task.Delay(600);
                var budget=UnityEngine.Object.FindAnyObjectByType<MatchRenderBudget>();var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                check(name+" has exactly one scene render budget",budget&&UnityEngine.Object.FindObjectsByType<MatchRenderBudget>().Length==1);
                check(name+" caps MSAA without modifying saved graphics",pipeline.msaaSampleCount<=2&&JsonUtility.ToJson(GameSettings.Current)==settings);
                var canvases=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>(true)).ToArray();
                check(name+" screen HUDs retain native display resolution",canvases.All(c=>c.renderMode!=RenderMode.ScreenSpaceCamera));
                Canvas.ForceUpdateCanvases();int clicks=0;
                foreach(var button in canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Where(b=>b.interactable&&b.gameObject.activeInHierarchy))
                {
                    var canvas=button.GetComponentInParent<Canvas>();var ray=canvas.GetComponent<GraphicRaycaster>();if(!ray)continue;
                    var r=(RectTransform)button.transform;var pos=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center));
                    var hits=new List<RaycastResult>();ray.Raycast(new PointerEventData(EventSystem.current){position=pos},hits);
                    if(hits.Any(h=>h.gameObject==button.gameObject||h.gameObject.transform.IsChildOf(button.transform)))clicks++;
                }
                if(name!="MultiplayerCourt")check(name+" visible buttons receive pointer raycasts",clicks>0);
                if(name=="BotMatch")
                {
                    var match=UnityEngine.Object.FindAnyObjectByType<BotMatch>();SettingsOverlay.Open(match);while(SettingsOverlay.Loading)await Task.Delay(30);
                    check("Settings pauses match and covers HUD",match.Paused&&canvases.All(c=>!c.enabled));
                    SettingsOverlay.Close(true);while(SettingsOverlay.Loading)await Task.Delay(30);
                    check("Closing Settings resumes match and restores HUD",!match.Paused&&canvases.All(c=>c.enabled));
                }
            }
            SceneManager.LoadScene("Menu");await Task.Delay(600);var p=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            check("Returning to Menu releases the match budget",UnityEngine.Object.FindObjectsByType<MatchRenderBudget>().Length==0&&p.msaaSampleCount==GameSettings.Current.msaa);
            check("Saved graphics remain unchanged",JsonUtility.ToJson(GameSettings.Current)==settings);
            var result=new{passed=true,count=rows.Count,checks=rows};File.WriteAllText("../UnityWork/StadiumPerformance/ui-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally
        {
            SceneManager.sceneLoaded-=Loaded;SceneManager.LoadScene("Menu");await Task.Delay(200);runtime.SwitchPlayer(original,null);
            foreach(var suffix in new[]{".json",".json.bak",".lock"}){string file=Path.Combine(Application.persistentDataPath,"LocalPlayers",id+suffix);if(File.Exists(file))File.Delete(file);}
        }
    }
}
