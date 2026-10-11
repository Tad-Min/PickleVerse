using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PickleVerse.Shop;
using UnityEngine;

public static class ShopVisualProbe
{
    static Color32[] Read(RenderTexture target)
    {
        var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();var pixels=image.GetPixels32();UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;return pixels;
    }
    static object Difference(Color32[] before,Color32[] after)
    {int count=0,max=0;for(int i=0;i<before.Length;i++){int d=Math.Abs(before[i].r-after[i].r)+Math.Abs(before[i].g-after[i].g)+Math.Abs(before[i].b-after[i].b);if(d>0)count++;max=Math.Max(max,d);}return new{changedPixels=count,maxRGBDelta=max};}
    public static async Task<object> Run()
    {
        var ui=UnityEngine.Object.FindAnyObjectByType<ShopScreen>();if(!ui||!Application.isPlaying)throw new Exception("Play Shop first.");
        ui.SelectGemstone();ui.preview.autoRotate=false;ui.preview.ResetView();ui.preview.autoRotate=false;
        var renderer=ui.preview.models[0].GetComponentInChildren<Renderer>();var original=renderer.sharedMaterial;var gem=new Material(original);renderer.sharedMaterial=gem;
        var icons=UnityEngine.Object.FindObjectsByType<WalletBar>().Single().GetComponentsInChildren<UnityEngine.UI.Image>().Where(i=>i.name=="CoinIcon").ToArray();var materials=icons.Select(i=>i.material).ToArray();var copies=materials.Select(m=>new Material(m)).ToArray();
        for(int i=0;i<icons.Length;i++)icons[i].material=copies[i];
        var camera=Camera.main;var old=camera.targetTexture;var target=new RenderTexture(640,360,24);target.Create();
        try
        {
            gem.SetFloat("_Glitter",0);ui.preview.previewCamera.Render();var noGlint=Read(ui.preview.previewCamera.targetTexture);
            gem.SetFloat("_Glitter",.24f);ui.preview.previewCamera.Render();var withGlint=Read(ui.preview.previewCamera.targetTexture);var gemstone=Difference(noGlint,withGlint);
            while(Time.time%1<.12f||Time.time%1>.24f)await Task.Delay(15);
            camera.targetTexture=target;
            foreach(var material in copies){var c=material.GetColor("_SweepColor");c.a=0;material.SetColor("_SweepColor",c);}camera.Render();var noSweep=Read(target);
            foreach(var material in copies){var c=material.GetColor("_SweepColor");c.a=.4f;material.SetColor("_SweepColor",c);}camera.Render();var withSweep=Read(target);var coin=Difference(noSweep,withSweep);
            var result=new{gemstone,coin};Directory.CreateDirectory("../UnityWork/Shop");File.WriteAllText("../UnityWork/Shop/visual-probe.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally{renderer.sharedMaterial=original;UnityEngine.Object.DestroyImmediate(gem);for(int i=0;i<icons.Length;i++){icons[i].material=materials[i];UnityEngine.Object.DestroyImmediate(copies[i]);}camera.targetTexture=old;target.Release();UnityEngine.Object.DestroyImmediate(target);ui.preview.previewCamera.Render();}
    }
}
