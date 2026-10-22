using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class StadiumMobileLOD
{
 const string Prefab="Assets/LocalMatchContent/StadiumEnhanced/PlayableStadium.prefab";
 const string Folder="Assets/LocalMatchContent/StadiumEnhanced/MobileMeshes";
 static readonly string Work=Path.GetFullPath(Path.Combine(Application.dataPath,"../../UnityWork/MobileStadiumLOD"));
 static bool Eligible(MeshFilter f)=>f.sharedMesh&&f.name.StartsWith("Batch ")&&f.sharedMesh.GetIndexCount(0)/3>6000&&!new[]{"Court","Net","Light","Lamp","Banner","Flag"}.Any(s=>f.name.Contains(s));
 public static object Export()
 {
  Directory.CreateDirectory(Work);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);var rows=prefab.GetComponentsInChildren<MeshFilter>(true).Where(Eligible).Select(f=>new{name=f.name,path=AssetDatabase.GetAssetPath(f.sharedMesh),vertices=f.sharedMesh.vertices.Select(v=>new[]{v.x,v.y,v.z}).ToArray(),uv=f.sharedMesh.uv.Select(v=>new[]{v.x,v.y}).ToArray(),triangles=f.sharedMesh.triangles}).ToArray();
  File.WriteAllText(Work+"/input.json",JsonConvert.SerializeObject(rows));return new{meshes=rows.Length,triangles=rows.Sum(r=>r.triangles.Length/3)};
 }
 public static object Import()
 {
  var rows=JArray.Parse(File.ReadAllText(Work+"/output.json"));Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
  var root=PrefabUtility.LoadPrefabContents(Prefab);long before=0,after=0;
  var priorSerialization=EditorSettings.serializationMode;
  try
  {
   EditorSettings.serializationMode=SerializationMode.ForceBinary;
   foreach(var row in rows)
   {
    string name=(string)row["name"];var filter=root.GetComponentsInChildren<MeshFilter>(true).First(f=>f.name==name);var original=filter.GetComponent<MeshRenderer>();
    var mesh=new Mesh {name=filter.sharedMesh.name+" Mobile LOD",indexFormat=row["vertices"].Count()<65536?IndexFormat.UInt16:IndexFormat.UInt32};
    mesh.vertices=row["vertices"].Select(v=>new Vector3((float)v[0],(float)v[1],(float)v[2])).ToArray();
    mesh.uv=row["uv"].Select(v=>new Vector2((float)v[0],(float)v[1])).ToArray();mesh.triangles=row["triangles"].Select(v=>(int)v).ToArray();
    mesh.RecalculateNormals();mesh.RecalculateBounds();MeshUtility.Optimize(mesh);MeshUtility.SetMeshCompression(mesh,ModelImporterMeshCompression.Medium);
    string path=Folder+"/"+name.Replace(' ','_')+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
    if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
    var child=filter.transform.Find("Mobile LOD");if(!child){child=new GameObject("Mobile LOD",typeof(MeshFilter),typeof(MeshRenderer)).transform;child.SetParent(filter.transform,false);}
    child.gameObject.layer=filter.gameObject.layer;GameObjectUtility.SetStaticEditorFlags(child.gameObject,GameObjectUtility.GetStaticEditorFlags(filter.gameObject));
    child.GetComponent<MeshFilter>().sharedMesh=mesh;var low=child.GetComponent<MeshRenderer>();low.sharedMaterials=original.sharedMaterials;low.shadowCastingMode=original.shadowCastingMode;low.receiveShadows=original.receiveShadows;low.lightProbeUsage=original.lightProbeUsage;low.reflectionProbeUsage=original.reflectionProbeUsage;
    var group=filter.GetComponent<LODGroup>();if(!group)group=filter.gameObject.AddComponent<LODGroup>();group.fadeMode=LODFadeMode.None;group.SetLODs(new[]{new LOD(.001f,new Renderer[]{original}),new LOD(.0001f,new Renderer[]{low})});group.RecalculateBounds();
    before+=(long)filter.sharedMesh.GetIndexCount(0)/3;after+=(long)mesh.GetIndexCount(0)/3;
   }
   foreach(string meshPath in Directory.GetFiles(Folder,"*.asset"))AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<Mesh>(meshPath));
   AssetDatabase.ForceReserializeAssets(Directory.GetFiles(Folder,"*.asset"),ForceReserializeAssetsOptions.ReserializeAssets);
   EditorSettings.serializationMode=priorSerialization;
   PrefabUtility.SaveAsPrefabAsset(root,Prefab);AssetDatabase.SaveAssets();var report=new{meshes=rows.Count,originalTriangles=before,mobileTriangles=after};File.WriteAllText(Work+"/summary.json",JsonConvert.SerializeObject(report,Formatting.Indented));return report;
  }
  finally{EditorSettings.serializationMode=priorSerialization;PrefabUtility.UnloadPrefabContents(root);}
 }
}
