using System;
using System.IO;
using System.Linq;
using UnityEngine;
using PickleVerse.Audio;

public static class RecordBoundsAnalysis
{
    public static object Run()
    {
        var library=Resources.Load<MusicLibrary>("Audio/Front End Music");
        var result=library.tracks.Select(track=>
        {
            var sprite=track.disc;var source=sprite.texture;
            var target=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            var previous=RenderTexture.active;Texture2D readable=null;
            try
            {
                Graphics.Blit(source,target);RenderTexture.active=target;
                readable=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false,true);readable.ReadPixels(new Rect(0,0,source.width,source.height),0,0);readable.Apply();
                var pixels=readable.GetPixels32();var area=sprite.rect;int left=(int)area.xMax,right=(int)area.xMin,bottom=(int)area.yMax,top=(int)area.yMin;
                for(int y=(int)area.yMin;y<area.yMax;y++)for(int x=(int)area.xMin;x<area.xMax;x++)if(pixels[y*source.width+x].a>=180){left=Math.Min(left,x);right=Math.Max(right,x);bottom=Math.Min(bottom,y);top=Math.Max(top,y);}
                return new{id=track.id,sprite=sprite.name,width=area.width,height=area.height,bounds=new[]{(left-area.xMin)/area.width,(bottom-area.yMin)/area.height,(right-left+1)/area.width,(top-bottom+1)/area.height},center=new[]{((left+right+1)*.5f-area.xMin)/area.width,((bottom+top+1)*.5f-area.yMin)/area.height}};
            }
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);if(readable)UnityEngine.Object.DestroyImmediate(readable);}
        }).ToArray();
        Directory.CreateDirectory("../UnityWork/RecordAlignment");File.WriteAllText("../UnityWork/RecordAlignment/bounds.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
    }
}
