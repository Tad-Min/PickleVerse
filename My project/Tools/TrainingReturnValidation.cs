using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using PickleVerse.FrontEnd;
using PickleVerse.LocalMatch;
using PickleVerse.Progression;
using PickleVerse.Shop;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class TrainingReturnValidation
{
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool ok){checks.Add(new{name,passed=ok});if(!ok)throw new Exception(name);}
    static async Task Ready(string scene)
    {
        float limit=Time.realtimeSinceStartup+20;
        while(SceneManager.GetActiveScene().name!=scene||UnityEngine.Object.FindAnyObjectByType<LobbyNavigation>()?.Busy==true)
        {if(Time.realtimeSinceStartup>limit)throw new Exception("Navigation timeout "+scene);await Task.Delay(30);}
        await Task.Delay(400);
    }
    static void Click(Button b)
    {ExecuteEvents.Execute(b.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);}
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play first.");checks.Clear();
        var runtime=ProgressionRuntime.Instance;string original=runtime.Player.id,qa="TrainingQA"+Guid.NewGuid().ToString("N").Substring(0,10);
        var settings=PlayerPrefs.GetString(PickleVerse.Settings.GameSettings.PreferenceKey,"");
        try
        {
            runtime.SwitchPlayer(qa,"Training QA");runtime.UpdateWallet(p=>{var a=TrainingEngine.Start(p,"serve");a.trials=a.successes=a.contacts=8;a.timingTotal=.5;a.Error(TrainingError.Late);TrainingEngine.Complete(p,a);});
            SceneManager.LoadScene("Menu");await Ready("Menu");var p=UnityEngine.Object.FindAnyObjectByType<PlayerProgressionMenu>();var home=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();var lobby=home.gameObject.scene;
            Check("Bottom progression nav is visible and interactable",p.progressionButton&&p.progressionButton.gameObject.activeInHierarchy&&p.progressionButton.IsInteractable());
            Check("Lobby UI stays at native display resolution",p.GetComponent<Canvas>().renderMode==RenderMode.ScreenSpaceOverlay);
            Canvas.ForceUpdateCanvases();
            var canvas=p.progressionButton.GetComponentInParent<Canvas>();var r=(RectTransform)p.progressionButton.transform;var hits=new List<RaycastResult>();
            if(canvas.worldCamera)canvas.worldCamera.Render();Canvas.ForceUpdateCanvases();
            canvas.GetComponent<GraphicRaycaster>().Raycast(new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,r.TransformPoint(r.rect.center))},hits);
            Directory.CreateDirectory("../UnityWork/TrainingReturn");
            File.WriteAllText("../UnityWork/TrainingReturn/raycast.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{frame=Time.frameCount,paused=UnityEditor.EditorApplication.isPaused,canvas=canvas.name,canvas.enabled,camera=canvas.worldCamera?canvas.worldCamera.name:"",cameraPosition=canvas.worldCamera?canvas.worldCamera.transform.position.ToString():"",screen=new{Screen.width,Screen.height},point=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,r.TransformPoint(r.rect.center)).ToString(),size=r.rect.size.ToString(),depth=p.progressionButton.targetGraphic.depth,cull=p.progressionButton.targetGraphic.canvasRenderer.cull,raycast=p.progressionButton.targetGraphic.raycastTarget,hits=hits.Select(h=>h.gameObject.name).ToArray(),groups=p.progressionButton.GetComponentsInParent<CanvasGroup>().Select(g=>new{g.name,g.blocksRaycasts,g.interactable}).ToArray()},Newtonsoft.Json.Formatting.Indented));
            Check("Progression nav has a reachable pointer hit",hits.Any(h=>h.gameObject==p.progressionButton.gameObject||h.gameObject.transform.IsChildOf(p.progressionButton.transform)));
            Click(p.progressionButton);await Task.Delay(80);
            Check("Click opens training progression",p.modal.gameObject.activeSelf&&p.title.text=="TIẾN TRÌNH LUYỆN TẬP");
            Check("Saved training stars and mistakes are visible",p.content.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text.Contains("3/3 sao"))&&p.content.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text.Contains("Đánh hụt")));
            p.Close();Click(p.missionsButton);Check("Missions remains its own daily rewards view",p.title.text=="NHIỆM VỤ");p.Close();
            for(int pass=0;pass<3;pass++)
            {
                float dance=home.DanceTime;home.playButton.onClick.Invoke();await Ready("ModeSelection");UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>().OpenTraining();await Ready("Training");
                var d=UnityEngine.Object.FindAnyObjectByType<TrainingDirector>();
                Check("Training retains the original suspended lobby "+pass,lobby.isLoaded&&SceneManager.sceneCount==2&&!home.enabled&&Mathf.Abs(home.DanceTime-dance)<.1f);
                Check("Only Training camera, listener and input render "+pass,UnityEngine.Object.FindObjectsByType<Camera>().Count(c=>c.enabled)==1&&UnityEngine.Object.FindObjectsByType<AudioListener>().Count(c=>c.enabled)==1&&UnityEngine.Object.FindObjectsByType<EventSystem>().Count(c=>c.enabled)==1&&lobby.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).All(x=>!x.enabled));
                Check("Training restores user render scale "+pass,Mathf.Abs(((UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)QualitySettings.renderPipeline).renderScale-PickleVerse.Settings.GameSettings.Current.renderScale)<.001f);
                if(pass==0){d.SetPaused(false);d.Select("drive");d.StartLesson();d.Session.trials=2;d.Session.successes=1;d.SaveActive();}
                d.BackToModes();await Ready("ModeSelection");UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>().Back();await Ready("Menu");
                Check("Returning reuses exact original Menu "+pass,UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>()==home&&home.enabled&&SceneManager.sceneCount==1);
                Check("No Training actor or duplicate camera remains "+pass,UnityEngine.Object.FindObjectsByType<TrainingDirector>().Length==0&&UnityEngine.Object.FindObjectsByType<Camera>().Count(c=>c.enabled)==1&&UnityEngine.Object.FindObjectsByType<AudioListener>().Count(c=>c.enabled)==1);
                Click(p.progressionButton);Check("Progression remains clickable after Training "+pass,p.modal.gameObject.activeSelf&&p.title.text.Contains("LUYỆN TẬP"));p.Close();
            }
            Check("Practice checkpoint survives navigation",runtime.Player.training.active.lessonId=="drive"&&runtime.Player.training.active.trials==2&&runtime.Player.training.active.successes==1);
            Click(p.progressionButton);p.bonusButton.onClick.Invoke();await Ready("Training");var resumed=UnityEngine.Object.FindAnyObjectByType<TrainingDirector>();
            Check("Continue from progression opens the saved lesson",resumed.Phase==TrainingPhase.Briefing&&resumed.Lesson.id=="drive");resumed.SetPaused(false);resumed.StartLesson();
            Check("Continue restores saved attempt count",resumed.Session.trials==2&&resumed.Session.successes==1);
            resumed.LeaveToMenu();await Ready("Menu");Check("Direct Training exit reuses original Menu",UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>()==home&&SceneManager.sceneCount==1);
            Check("Original graphics preference is preserved",PlayerPrefs.GetString(PickleVerse.Settings.GameSettings.PreferenceKey,"")==settings);
            var result=new{passed=true,count=checks.Count,checks};Directory.CreateDirectory("../UnityWork/TrainingReturn");File.WriteAllText("../UnityWork/TrainingReturn/navigation-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally
        {
            var d=UnityEngine.Object.FindAnyObjectByType<TrainingDirector>();if(d)d.Catalogue();runtime.SwitchPlayer(original,null);
            string root=Path.Combine(Application.persistentDataPath,"LocalPlayers");foreach(var suffix in new[]{".json",".json.bak",".lock"}){string path=Path.Combine(root,qa+suffix);if(File.Exists(path))File.Delete(path);}
        }
    }
}
