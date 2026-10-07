using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class MultiplayerCourtVisualFix
{
    public static string Run()
    {
        const string scenePath="Assets/Scenes/MultiplayerCourt.unity";
        var loaded=SceneManager.GetSceneByPath(scenePath); bool wasLoaded=loaded.IsValid()&&loaded.isLoaded;
        var scene=wasLoaded?loaded:EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
        var roots=scene.GetRootGameObjects();
        var canvas=roots.SelectMany(g=>g.GetComponentsInChildren<Canvas>(true)).First();
        var camera=roots.SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).First();
        canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1;
        foreach(var text in roots.SelectMany(g=>g.GetComponentsInChildren<TMPro.TMP_Text>(true))) if(text.name=="Controls") text.text=text.text.Replace("J Drive","J Groundstroke / giao bóng");
        var existing=roots.FirstOrDefault(g=>g.name=="NetVisual");
        if(existing==null) { existing=new GameObject("NetVisual"); SceneManager.MoveGameObjectToScene(existing,scene); }
        var vertices=new List<Vector3>(); var wires=new List<int>(); var band=new List<int>();
        void Quad(float x0,float x1,float y0,float y1,List<int> triangles)
        {
            int n=vertices.Count; vertices.Add(new Vector3(x0,y0,0)); vertices.Add(new Vector3(x1,y0,0)); vertices.Add(new Vector3(x1,y1,0)); vertices.Add(new Vector3(x0,y1,0));
            triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3,n+2,n+1,n,n+3,n+2,n});
        }
        float Height(float x)=>.8636f+Mathf.Abs(x)/3.1f*(.9144f-.8636f);
        for(float x=-3.1f;x<=3.1f;x+=.1f) Quad(x-.004f,x+.004f,.02f,Height(x),wires);
        for(float y=.025f;y<.86f;y+=.08f) Quad(-3.1f,3.1f,y-.004f,y+.004f,wires);
        for(int i=0;i<62;i++) { float x=-3.1f+i*.1f; Quad(x,x+.1f,Height(x)-.026f,Height(x),band); }
        const string meshPath="Assets/Settings/Multiplayer/NetGrid.asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(mesh==null) { mesh=new Mesh(); AssetDatabase.CreateAsset(mesh,meshPath); } else mesh.Clear();
        mesh.name="PickleballNetGrid"; mesh.SetVertices(vertices); mesh.subMeshCount=2; mesh.SetTriangles(wires,0); mesh.SetTriangles(band,1); mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
        Material Material(string name,Color color)
        {
            string path="Assets/Settings/Multiplayer/"+name+".mat"; var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null) { mat=new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(mat,path); }
            mat.color=color; EditorUtility.SetDirty(mat); return mat;
        }
        var filter=existing.GetComponent<MeshFilter>(); if(filter==null) filter=existing.AddComponent<MeshFilter>(); filter.sharedMesh=mesh;
        var renderer=existing.GetComponent<MeshRenderer>(); if(renderer==null) renderer=existing.AddComponent<MeshRenderer>();
        renderer.sharedMaterials=new[]{Material("NetWire",new Color(.06f,.08f,.09f)),Material("NetBand",new Color(.94f,.96f,.96f))};
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        if(!wasLoaded) EditorSceneManager.CloseScene(scene,true);
        return "Saved court HUD camera and visible net mesh";
    }
}
