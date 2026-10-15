using UnityEditor;
using UnityEngine;
public static class ShotFeelSetup
{
    public static object Run()
    {
        const string path="Assets/Resources/PaddleImpact.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material)
        {
            var shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if(!shader)throw new System.Exception("URP particle shader missing");
            material=new Material(shader){name="PaddleImpact"};
            material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);
            material.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue=3000;
            AssetDatabase.CreateAsset(material,path);AssetDatabase.SaveAssets();
        }
        return new{path,shader=material.shader.name};
    }
}
