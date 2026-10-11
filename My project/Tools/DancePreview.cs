using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DancePreview
{
    public static object Run()
    {
        if(Application.isPlaying)throw new Exception("Edit Mode preview only");
        var scene=EditorSceneManager.NewPreviewScene();Camera camera=null;RenderTexture target=null;Texture2D sheet=null;
        try
        {
            var cam=new GameObject("Dance QA Camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cam,scene);camera=cam.GetComponent<Camera>();camera.scene=scene;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.17f,.23f);camera.fieldOfView=36;
            camera.transform.SetPositionAndRotation(new Vector3(1.4f,1.35f,3.1f),Quaternion.LookRotation(new Vector3(-1.4f,-.5f,-3.1f)));
            var light=new GameObject("Dance QA Key",typeof(Light));SceneManager.MoveGameObjectToScene(light,scene);light.GetComponent<Light>().type=LightType.Directional;light.GetComponent<Light>().intensity=2;light.GetComponent<Light>().cullingMask=1<<31;light.transform.rotation=Quaternion.Euler(35,-30,0);
            const int width=300,height=400;target=new RenderTexture(width,height,24);camera.targetTexture=target;sheet=new Texture2D(width*4,height*6,TextureFormat.RGB24,false);
            var rows=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("../UnityWork/DanceImport/extraction.json"))["rows"];
            for(int row=0;row<6;row++)
            {
                var entry=rows[row];string gender=(string)entry["gender"];string model="Assets/LocalMatchContent/Models/"+(gender=="female"?"Female":"Male")+".glb";string path=(string)entry["outputs"][0];var obj=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(model));SceneManager.MoveGameObjectToScene(obj,scene);
                foreach(var t in obj.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
                foreach(var a in obj.GetComponentsInChildren<Animator>(true))a.enabled=false;foreach(var a in obj.GetComponentsInChildren<Animation>(true))a.enabled=false;
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                for(int column=0;column<4;column++)
                {
                    clip.SampleAnimation(obj,clip.length*column/4);camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;sheet.ReadPixels(new Rect(0,0,width,height),column*width,(5-row)*height);RenderTexture.active=previous;
                }
                UnityEngine.Object.DestroyImmediate(obj);
            }
            sheet.Apply();Directory.CreateDirectory("../UnityWork/DanceImport/Images");File.WriteAllBytes("../UnityWork/DanceImport/Images/contact-sheet.png",sheet.EncodeToPNG());return new{path="UnityWork/DanceImport/Images/contact-sheet.png",rows="Male Clap, Male SuperSaiyan, Male AllNight, Female Clap, Female Boom, Female AllNight",columns="0%, 25%, 50%, 75%"};
        }
        finally{if(camera)camera.targetTexture=null;if(target)UnityEngine.Object.DestroyImmediate(target);if(sheet)UnityEngine.Object.DestroyImmediate(sheet);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
