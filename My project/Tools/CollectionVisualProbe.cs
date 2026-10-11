using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.Shop;
using PickleVerse.LocalMatch;

public static class CollectionVisualProbe
{
    sealed class Memory:IEconomyStorage{public string json;public string Read()=>json;public void Write(string value){json=value;}}
    static void Capture(Camera camera,string name,int width=1280,int height=720)
    {
        var old=camera.targetTexture;float aspect=camera.aspect;var active=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);camera.targetTexture=rt;camera.aspect=(float)width/height;
        Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
        var tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();
        Directory.CreateDirectory("../UnityWork/Collection/Images");File.WriteAllBytes("../UnityWork/Collection/Images/"+name+".png",tex.EncodeToPNG());
        camera.targetTexture=old;camera.aspect=aspect;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.Destroy(tex);
    }
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play first.");var real=PlayerEconomy.Service;
        var state=new PlayerEconomyState{equippedPaddle="paddle.gemstone",tokenpad=1000,elite=350};state.owned.Add("paddle.gemstone");
        PlayerEconomy.Configure(new LocalPlayerEconomy(PlayerEconomy.Catalog,new Memory{json=JsonUtility.ToJson(state)}));
        try
        {
            SceneManager.LoadScene("Menu");await Task.Delay(250);var home=UnityEngine.Object.FindAnyObjectByType<PickleVerse.FrontEnd.HomeLobbyPresentation>();home.animateAtmosphere=false;
            home.ApplyAtmosphere(0);home.SampleDance(.7f);await Task.Delay(250);Capture(Camera.main,"Menu-Gemstone-Day");home.ApplyAtmosphere(1);await Task.Delay(250);Capture(Camera.main,"Menu-Gemstone-Night");
            SceneManager.LoadScene("BotMatch");await Task.Delay(250);var match=UnityEngine.Object.FindAnyObjectByType<BotMatch>();match.Restart();match.SetPaused(true);match.human.RefreshPose();match.bot.RefreshPose();
            var camera=new GameObject("Grip QA Camera",typeof(Camera)).GetComponent<Camera>();camera.nearClipPlane=.01f;camera.fieldOfView=40;camera.clearFlags=CameraClearFlags.Skybox;
            var hand=match.human.hand;var face=match.human.paddleFace;var target=(hand.position+face.position)*.5f;
            camera.transform.position=target+new Vector3(-.65f,.24f,.65f);camera.transform.LookAt(target);Capture(camera,"Female-Gemstone-Grip");
            var male=PaddleAppearance.Attach(match.bot.paddle);male.Apply("paddle.thot");hand=match.bot.hand;face=match.bot.paddleFace;target=(hand.position+face.position)*.5f;
            camera.transform.position=target+new Vector3(.65f,.24f,-.65f);camera.transform.LookAt(target);Capture(camera,"Male-THOT-Grip");UnityEngine.Object.Destroy(camera.gameObject);
            return new{images="UnityWork/Collection/Images",femaleHandLocal=match.human.paddle.transform.InverseTransformPoint(match.human.hand.position).ToString(),maleHandLocal=match.bot.paddle.transform.InverseTransformPoint(match.bot.hand.position).ToString()};
        }
        finally{PlayerEconomy.Configure(real);SceneManager.LoadScene("Collection");}
    }
}
