using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StadiumSurvey
{
    public static object Run()
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Stadium_Enhanced/Stadium_Enhanced.glb");
        var renderers=asset.GetComponentsInChildren<Renderer>(true);
        var rows=renderers.Select(r=>new{name=r.name,parent=r.transform.parent.name,position=r.transform.position.ToString("F4"),minimum=r.bounds.min.ToString("F4"),maximum=r.bounds.max.ToString("F4"),size=r.bounds.size.ToString("F4"),materials=r.sharedMaterials.Select(m=>m?m.name:"").ToArray(),vertices=r.GetComponent<MeshFilter>()?.sharedMesh.vertexCount??0}).ToArray();
        Directory.CreateDirectory("../UnityWork/Stadium");File.WriteAllText("../UnityWork/Stadium/model-survey.json",Newtonsoft.Json.JsonConvert.SerializeObject(rows,Newtonsoft.Json.Formatting.Indented));
        return new{renderers=rows.Length,vertices=rows.Sum(r=>r.vertices),lights=asset.GetComponentsInChildren<Light>(true).Length,relevant=rows.Where(r=>System.Text.RegularExpressions.Regex.IsMatch(r.name,"Court|Net|Light|Lamp|Roof|Flood|Flash|Kitchen|Y_",System.Text.RegularExpressions.RegexOptions.IgnoreCase)).ToArray()};
    }
    public static object Scenes()
    {
        var rows=new System.Collections.Generic.List<object>();
        foreach(string path in new[]{"Assets/Scenes/UI/Menu.unity","Assets/Scenes/BotMatch.unity","Assets/Scenes/MultiplayerCourt.unity","Assets/Scenes/Training.unity","Assets/Scenes/Tutorial.unity"})
        {
            var s=EditorSceneManager.OpenScene(path);
            rows.Add(new{scene=s.name,roots=s.GetRootGameObjects().Select(g=>new{g.name,position=g.transform.position.ToString(),children=g.transform.childCount,components=g.GetComponents<Component>().Where(c=>c).Select(c=>c.GetType().Name).ToArray()}).ToArray(),colliders=s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Collider>(true)).Select(c=>new{c.name,type=c.GetType().Name,c.isTrigger,position=c.transform.position.ToString(),size=c.bounds.size.ToString()}).ToArray()});
        }
        File.WriteAllText("../UnityWork/Stadium/scenes-before.json",Newtonsoft.Json.JsonConvert.SerializeObject(rows,Newtonsoft.Json.Formatting.Indented));
        return rows.Select(r=>r).ToArray();
    }
    public static object Fixtures()
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Stadium_Enhanced/Stadium_Enhanced.glb");var rows=new System.Collections.Generic.List<object>();
        foreach(var f in asset.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.Contains("WarmLight")&&(f.name.Contains("LightPole")||f.name.Contains("Dome"))))
        {
            var v=f.sharedMesh.vertices.Select(f.transform.TransformPoint).ToArray();var triangles=f.sharedMesh.triangles;var parent=Enumerable.Range(0,v.Length).ToArray();
            Func<int,int> root=null;root=i=>parent[i]==i?i:parent[i]=root(parent[i]);Action<int,int> join=(a,b)=>parent[root(a)]=root(b);
            var weld=new System.Collections.Generic.Dictionary<string,int>();for(int i=0;i<v.Length;i++){string key=v[i].ToString("F4");if(weld.TryGetValue(key,out int other))join(i,other);else weld[key]=i;}
            for(int i=0;i<triangles.Length;i+=3){join(triangles[i],triangles[i+1]);join(triangles[i],triangles[i+2]);}
            var components=Enumerable.Range(0,v.Length).GroupBy(root).Select(g=>{var b=new Bounds(v[g.First()],Vector3.zero);foreach(int i in g)b.Encapsulate(v[i]);return new{center=b.center.ToString("F4"),size=b.size.ToString("F4"),maxY=b.max.y,vertices=g.Count()};}).ToArray();
            rows.Add(new{name=f.name,components});
        }
        var white=asset.GetComponentsInChildren<MeshFilter>().Single(f=>f.name.Contains("ChampionshipCourt")&&f.name.Contains("White"));
        var net=white.sharedMesh.vertices.Select(white.transform.TransformPoint).Where(v=>v.y>.1f&&Mathf.Abs(v.z)<.05f).GroupBy(v=>Mathf.Round(v.x*1000)).Select(g=>new{x=g.Key/1000,y=g.Max(v=>v.y)}).OrderBy(v=>v.x).ToArray();
        var result=new{fixtures=rows,net};File.WriteAllText("../UnityWork/Stadium/fixtures.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
    }
}
