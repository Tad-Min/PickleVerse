using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using PickleVerse.Audio;

public static class RecordAlignmentValidation
{
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play first");
        UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");await Task.Delay(250);
        var music=MusicPlayer.Instance;var ui=UnityEngine.Object.FindAnyObjectByType<MusicUI>();ui.Open();await Task.Delay(100);
        var reports=new List<object>();int initial=music.CurrentIndex;bool paused=music.UserPaused;
        try
        {
            for(int i=0;i<music.Library.tracks.Length;i++)
            {
                music.Select(i);await Task.Delay(100);if(!music.UserPaused)music.TogglePause();
                Canvas.ForceUpdateCanvases();ui.FitRecord();
                var art=ui.discTransform;var holder=(RectTransform)art.parent;var frame=(RectTransform)holder.Find("Vinyl Orbit");var b=music.Track.discBounds;
                var localCenter=art.rect.min+Vector2.Scale(art.rect.size,b.center);float error=0;
                foreach(float angle in new[]{0,90,180,270})
                {art.localRotation=Quaternion.Euler(0,0,angle);error=Mathf.Max(error,Vector3.Distance(art.TransformPoint(localCenter),holder.TransformPoint(holder.rect.center)));}
                float radius=Mathf.Min(frame.rect.width,frame.rect.height)*.46f*.94f;
                float gapX=radius-art.rect.width*b.width*.5f,gapY=radius-art.rect.height*b.height*.5f;
                bool passed=error<.0001f&&gapX>0&&gapY>0&&Mathf.Abs(gapX-gapY)<2;
                reports.Add(new{track=music.Track.id,centerErrorWorld=error,gapX,gapY,passed});if(!passed)throw new Exception("Disc frame alignment failed: "+music.Track.id);
            }
            var result=new{passed=true,tracks=reports.Count,anglesPerTrack=4,reports};Directory.CreateDirectory("../UnityWork/RecordAlignment");File.WriteAllText("../UnityWork/RecordAlignment/validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally{music.Select(initial);await Task.Delay(100);if(paused&&!music.UserPaused)music.TogglePause();ui.Close();}
    }
}
