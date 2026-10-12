using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PickleVerse.LocalMatch;
using PickleVerse.Progression;
using PickleVerse.FrontEnd;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TrainingVisuals
{
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play first.");
        var runtime=ProgressionRuntime.Instance;string original=runtime.Player.id;string qa="TrainingQA"+Guid.NewGuid().ToString("N").Substring(0,10);bool pause=UnityEditor.EditorApplication.isPaused;
        try
        {
            runtime.SwitchPlayer(qa,"Training QA");runtime.UpdateWallet(p=>{foreach(var lesson in TrainingCurriculum.Lessons)TrainingEngine.Record(TrainingEngine.Ensure(p),lesson.id).bestStars=1;});
            SceneManager.LoadScene("Training");await Task.Delay(300);var d=UnityEngine.Object.FindAnyObjectByType<TrainingDirector>();d.SetPaused(false);
            d.Select("drop");d.Advance(2.5f);Canvas.ForceUpdateCanvases();await Task.Delay(100);Capture(d.view,"briefing");
            d.Select("doubles-cover");d.StartLesson();d.SetPaused(false);UnityEditor.EditorApplication.isPaused=true;
            for(int i=0;i<70;i++)d.Advance(1f/60);
            d.hud.Refresh();Canvas.ForceUpdateCanvases();Capture(d.view,"doubles-practice");
            d.Catalogue();runtime.UpdateWallet(p=>{
                var a=TrainingEngine.Start(p,"drive",true);a.trials=8;a.successes=6;a.contacts=6;a.timingTotal=.8;a.seconds=45;a.Error(TrainingError.Late);a.Error(TrainingError.WrongInput);TrainingEngine.Complete(p,a);
            });
            UnityEditor.EditorApplication.isPaused=false;SceneManager.LoadScene("Menu");await Task.Delay(300);
            var menu=UnityEngine.Object.FindAnyObjectByType<PlayerProgressionMenu>();menu.OpenTrainingProgress();await Task.Delay(150);Canvas.ForceUpdateCanvases();Capture(Camera.main,"progression");menu.Close();
            return new{images=new[]{"UnityWork/Training/briefing.png","UnityWork/Training/doubles-practice.png","UnityWork/Training/progression.png"},profile="QA only"};
        }
        finally
        {
            var d=UnityEngine.Object.FindAnyObjectByType<TrainingDirector>();if(d)d.Catalogue();runtime.SwitchPlayer(original,null);UnityEditor.EditorApplication.isPaused=pause;
            string root=Path.Combine(Application.persistentDataPath,"LocalPlayers");foreach(var suffix in new[]{".json",".json.bak",".lock"}){string path=Path.Combine(root,qa+suffix);if(File.Exists(path))File.Delete(path);}
        }
    }
    static void Capture(Camera camera,string name)
    {
        var target=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;
        try{camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();Directory.CreateDirectory("../UnityWork/Training");File.WriteAllBytes("../UnityWork/Training/"+name+".png",image.EncodeToPNG());}
        finally{camera.targetTexture=previousTarget;RenderTexture.active=previousActive;UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);}
    }
}
