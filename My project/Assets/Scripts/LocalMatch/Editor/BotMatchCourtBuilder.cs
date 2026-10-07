using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PickleVerse.Rules;

namespace PickleVerse.LocalMatch.Editor
{
    public static class BotMatchCourtBuilder
    {
        const string Source = "Assets/Models/court/Court.glb";
        const string Folder = "Assets/LocalMatchContent/Court";
        const string Prefab = Folder + "/PlayableCourt.prefab";
        const string ScenePath = "Assets/Scenes/BotMatch.unity";
        const float CenterHeight = .8636f, EdgeHeight = .9144f;

        public static GameObject Create(Transform parent)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (!source) throw new FileNotFoundException("Court model not found", Source);
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var court = UnityEngine.Object.Instantiate(source); court.name = "Court";
            // Source is already in metres. The model rounds 20ft to 6.10m and 44ft
            // to 13.41m; adjust the playing surface and line edges to the rules engine.
            var surface = Part(court, "Court_Surface");
            var bounds = surface.sharedMesh.bounds;
            court.transform.position = new Vector3(0, -bounds.max.y, 0);
            surface.transform.localScale = new Vector3(Court.HalfWidth / bounds.extents.x, 1, Court.HalfLength / bounds.extents.z);
            var lines = Part(court, "Court_Lines");
            lines.transform.localPosition = new Vector3(0, -court.transform.position.y, 0);
            lines.sharedMesh = SaveMesh(Lines(), "RegulationLines.asset");
            var net = Part(court, "Net_Mesh");
            net.transform.localPosition = new Vector3(0, -court.transform.position.y, 0);
            net.sharedMesh = SaveMesh(Net(net.sharedMesh), "RegulationNet.asset");
            var tape = new GameObject("Net_TopTape", typeof(MeshFilter), typeof(MeshRenderer));
            tape.transform.SetParent(net.transform, false);
            tape.GetComponent<MeshFilter>().sharedMesh = SaveMesh(Tape(), "NetTopTape.asset");
            tape.GetComponent<MeshRenderer>().sharedMaterial = lines.GetComponent<Renderer>().sharedMaterial;
            foreach (string name in new[] { "Net_Post_L", "Net_Post_R" })
            {
                var post = Part(court, name); var b = post.sharedMesh.bounds;
                float radius = b.extents.x;
                post.transform.localPosition = new Vector3((name.EndsWith("L") ? -1 : 1) * (Court.NetHalfSpan + radius),
                    EdgeHeight * .5f - court.transform.position.y, 0);
                post.transform.localScale = new Vector3(1, EdgeHeight / b.size.y, 1);
            }
            foreach (var camera in court.GetComponentsInChildren<Camera>(true)) UnityEngine.Object.DestroyImmediate(camera.gameObject);
            foreach (var light in court.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(light.gameObject);
            foreach (var collider in court.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            // Ball/court collision uses the analytical rules engine. A second PhysX
            // collider would disagree with the swept net and bounce calculations.
            PrefabUtility.SaveAsPrefabAsset(court, Prefab);
            UnityEngine.Object.DestroyImmediate(court);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), parent);
            instance.name = "Court";
            return instance;
        }
        static MeshFilter Part(GameObject court, string name) => court.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == name);
        static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = Folder + "/" + name;
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old)
            {
                EditorUtility.CopySerialized(mesh, old);
                // CopySerialized preserves the asset GUID but can leave the Editor's
                // existing GPU buffers stale when topology changes during regeneration.
                old.vertices = mesh.vertices; old.triangles = mesh.triangles; old.UploadMeshData(false);
                UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(old); AssetDatabase.SaveAssetIfDirty(old); return old;
            }
            AssetDatabase.CreateAsset(mesh, path); AssetDatabase.SaveAssetIfDirty(mesh); return mesh;
        }
        static Mesh Lines()
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            float w = Court.LineWidth, x = Court.HalfWidth, z = Court.HalfLength, kitchen = Court.Kitchen;
            for (int side = -1; side <= 1; side += 2)
            {
                Quad(vertices, triangles, side * (x - w / 2), 0, w, 2 * z);
                Quad(vertices, triangles, 0, side * (z - w / 2), 2 * x, w);
                Quad(vertices, triangles, 0, side * (kitchen - w / 2), 2 * x, w);
                Quad(vertices, triangles, 0, side * (z + kitchen) / 2, w, z - kitchen);
            }
            var mesh = new Mesh { name = "Regulation pickleball lines" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static void Quad(List<Vector3> v, List<int> t, float x, float z, float width, float length)
        {
            int i = v.Count; float a = width / 2, b = length / 2;
            v.Add(new Vector3(x-a,.002f,z-b)); v.Add(new Vector3(x-a,.002f,z+b));
            v.Add(new Vector3(x+a,.002f,z+b)); v.Add(new Vector3(x+a,.002f,z-b));
            t.AddRange(new[] { i, i+1, i+2, i, i+2, i+3 });
        }
        // Keep the source grid intact. Its highest wire meets the bottom of the
        // white tape at the sidelines; the tape defines the regulation sag profile.
        static Mesh Net(Mesh source)
        {
            var mesh = UnityEngine.Object.Instantiate(source);
            mesh.name = "Regulation pickleball net";
            var vertices = mesh.vertices;
            float span = source.bounds.extents.x, height = source.bounds.max.y;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i].x *= Court.NetHalfSpan / span;
                vertices[i].y *= CenterHeight / height;
            }
            mesh.vertices = vertices; mesh.RecalculateBounds(); return mesh;
        }
        static Mesh Tape()
        {
            float[] x = { -Court.NetHalfSpan, -Court.HalfWidth, 0, Court.HalfWidth, Court.NetHalfSpan };
            var v = new List<Vector3>(); var t = new List<int>();
            foreach (float a in x)
            {
                float h = Court.NetHeight(a);
                v.Add(new Vector3(a,h,.011f)); v.Add(new Vector3(a,h-Court.LineWidth,.011f));
                v.Add(new Vector3(a,h,-.011f)); v.Add(new Vector3(a,h-Court.LineWidth,-.011f));
            }
            for (int i=0;i<x.Length-1;i++)
            {
                int a=i*4,b=a+4;
                t.AddRange(new[]{a,a+1,b+1,a,b+1,b,a+2,b+3,a+3,a+2,b+2,b+3,a,b+2,a+2,a,b,b+2});
            }
            var mesh=new Mesh{name="Net white top tape"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        [MenuItem("PickleVerse/Restore court in BotMatch")]
        public static void Restore()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before restoring court.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!=ScenePath) throw new InvalidOperationException("Open BotMatch scene first.");
            Directory.CreateDirectory("../UnityWork/CourtRestore");
            EditorSceneManager.SaveScene(scene,"../UnityWork/CourtRestore/BotMatch_before_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
            var arena=scene.GetRootGameObjects().Single(g=>g.name=="Arena").transform;
            var old=arena.Find("Court");var court=Create(arena);
            Undo.RegisterCreatedObjectUndo(court,"Restore pickleball court");
            if(old)Undo.DestroyObjectImmediate(old.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject=court;
            if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.Frame(new Bounds(Vector3.zero,new Vector3(12,2,20)),false);
            Debug.Log("BotMatch court restored from "+Source+" at regulation dimensions.");
        }
    }
}
