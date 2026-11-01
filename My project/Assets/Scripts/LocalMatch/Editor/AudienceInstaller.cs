using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace PickleVerse.LocalMatch.Editor
{
    /// <summary>Explicit, repeatable authoring command. Finds the real blue chair surfaces.</summary>
    public static class AudienceInstaller
    {
        const string Folder = "Assets/LocalMatchContent/Audience";
        const string Output = "Assets/Resources/Audience";
        static readonly string[] Models = { "PlayBoy", "T1-girl", "Wibu", "YellowHairGirl" };
        static readonly string[] AnimationNames = { "dân-chơi", "nữ-T1", "nam-đeo-kính", "nữ-tóc-vàng" };

        public static void ImportOptimizedModels(string exportFolder)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            Directory.CreateDirectory(Folder + "/Models");
            foreach (string model in Models)
            {
                string target = Folder + "/Models/" + model + "_Crowd.glb";
                File.Copy(Path.Combine(exportFolder, model + "_Crowd.glb"), target, true);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
        }

        [MenuItem("PickleVerse/Audience/Install seated match audience")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var open = SceneManager.GetSceneAt(i);
                if (open.isDirty && (open.path == "Assets/Scenes/BotMatch.unity" || open.path == "Assets/Scenes/MultiplayerCourt.unity"))
                    throw new InvalidOperationException("Scene has unsaved changes: " + open.path);
            }
            Directory.CreateDirectory(Folder + "/Prefabs"); Directory.CreateDirectory(Output);
            var prefabs = Enumerable.Range(0, 4).Select(BuildSpectator).ToArray();
            foreach (string venue in new[] { "Professional", "LowPoly" }) BuildAudience(venue, prefabs);
            var original = SceneManager.GetActiveScene();
            foreach (string path in new[] { "Assets/Scenes/BotMatch.unity", "Assets/Scenes/MultiplayerCourt.unity" })
            {
                var scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    if (scene.isDirty) throw new InvalidOperationException("Scene has unsaved changes: " + path);
                    var selection = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CourtVenueSelection>(true)).Single();
                    selection.includeAudience = true;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(selection);
                    var old = selection.current.GetComponentInChildren<StadiumAudience>(true);
                    if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
                    PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Output + "/" + selection.Selected + ".prefab"), selection.current.transform);
                    EditorUtility.SetDirty(selection); EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            Debug.Log("Seated audiences installed in BotMatch and MultiplayerCourt, including both venue choices.");
        }

        static GameObject BuildSpectator(int index)
        {
            string path = Folder + "/Models/" + Models[index] + "_Crowd.glb";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!model) throw new InvalidOperationException("Build crowd model first: " + path);
            var root = new GameObject(Models[index] + " Spectator");
            try
            {
                var visual = UnityEngine.Object.Instantiate(model, root.transform); visual.name = "Character";
                var pelvis = visual.GetComponentsInChildren<Transform>(true).Single(t => t.name == "pelvis");
                var actor = root.AddComponent<AudienceSpectator>();
                actor.visual = visual.transform; actor.pelvis = pelvis;
                actor.animator = pelvis.parent.GetComponent<Animator>();
                if (!actor.animator) actor.animator = pelvis.parent.gameObject.AddComponent<Animator>();
                actor.animator.applyRootMotion = false;
                actor.cheering = Clip(index, "cheering"); actor.pointing = Clip(index, "sitting_pointing");
                actor.disappointed = Clip(index, "disappointed"); actor.angry = Clip(index, "sitting_angry");
                var renderers = visual.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    if (r.name.Contains("Icosphere")) { UnityEngine.Object.DestroyImmediate(r.gameObject); continue; }
                    r.sharedMaterials = r.sharedMaterials.Select(m => CrowdMaterial(index, m)).ToArray();
                    r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = true;
                    r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                    r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    if (r is SkinnedMeshRenderer skin)
                    {
                        skin.updateWhenOffscreen = false; skin.quality = SkinQuality.Bone4;
                        skin.localBounds = new Bounds(new Vector3(0, .1f, 0), new Vector3(3, 3, 3));
                    }
                }
                var lod = root.AddComponent<LODGroup>();
                lod.SetLODs(new[] {
                    new LOD(.09f, visual.GetComponentsInChildren<Renderer>().Where(r => r.name.Contains("LOD0")).ToArray()),
                    new LOD(.009f, visual.GetComponentsInChildren<Renderer>().Where(r => r.name.Contains("LOD1")).ToArray()) });
                lod.localReferencePoint = new Vector3(0, .45f, 0); lod.size = 1.9f;
                actor.Initialize(); actor.DisposeGraph();
                return PrefabUtility.SaveAsPrefabAsset(root, Folder + "/Prefabs/" + Models[index] + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static AnimationClip Clip(int index, string action)
        {
            string path = "Assets/Animation/Characters/Audience/" + AnimationNames[index] + "__" + action + ".anim";
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(path) ?? throw new InvalidOperationException("Missing supplied audience clip: " + path);
        }

        static Material CrowdMaterial(int index, Material source)
        {
            Directory.CreateDirectory(Folder + "/Materials");
            string path = Folder + "/Materials/" + Models[index] + "-" + source.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { name = Models[index] + " crowd" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", source.HasProperty("baseColorTexture") ? source.GetTexture("baseColorTexture") : source.mainTexture);
            material.SetColor("_BaseColor", source.HasProperty("baseColorFactor") ? source.GetColor("baseColorFactor") : Color.white);
            // Background fans retain the supplied colours, without expensive high-poly
            // normal maps and mirror-like fabric highlights after mesh simplification.
            material.SetFloat("_Smoothness", .08f); material.SetColor("_SpecColor", new Color(.025f, .025f, .025f, 1));
            material.SetFloat("_Cull", 0); material.SetFloat("_Surface", 0); material.renderQueue = 2000;
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); material.DisableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        public static List<Vector3> SeatSurfaces(GameObject stadium)
        {
            var seats = new List<Vector3>();
            foreach (var f in stadium.GetComponentsInChildren<MeshFilter>(true))
            {
                var r = f.GetComponent<Renderer>(); if (!r) continue;
                var mesh = f.sharedMesh; var vertices = mesh.vertices;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    if (sub >= r.sharedMaterials.Length || !r.sharedMaterials[sub] || !r.sharedMaterials[sub].name.Contains("Seat")) continue;
                    var triangles = mesh.GetTriangles(sub);
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        var a = f.transform.TransformPoint(vertices[triangles[i]]);
                        var b = f.transform.TransformPoint(vertices[triangles[i+1]]);
                        var c = f.transform.TransformPoint(vertices[triangles[i+2]]);
                        if (Vector3.Cross(b-a,c-a).y < .045f || Mathf.Abs(a.y-b.y) > .002f || Mathf.Abs(a.y-c.y) > .002f) continue;
                        var center = (Vector3.Min(a, Vector3.Min(b,c)) + Vector3.Max(a, Vector3.Max(b,c))) * .5f;
                        int previous = seats.FindIndex(p => Vector3.Distance(p,center) < .15f);
                        if (previous < 0) seats.Add(center); else seats[previous] = (seats[previous] + center) * .5f;
                    }
                }
            }
            return seats.OrderBy(p => p.x).ThenBy(p => p.y).ThenBy(p => p.z).ToList();
        }

        static void BuildAudience(string venue, GameObject[] prefabs)
        {
            var stadium = PrefabUtility.LoadPrefabContents("Assets/Resources/Courts/" + venue + ".prefab");
            var crowd = new GameObject(venue + " Seated Audience"); crowd.AddComponent<StadiumAudience>();
            try
            {
                // The outdoor venue has no bleachers. Reuse the authored side stands
                // as presentation geometry, leaving its court, fence and physics intact.
                if (venue == "LowPoly") BuildOutdoorStands(crowd.transform);
                var seats = SeatSurfaces(venue == "LowPoly" ? crowd : stadium);
                if (seats.Count < 16) throw new InvalidOperationException("Could not identify chair surfaces: " + venue);
                var random = new System.Random(venue == "Professional" ? 10831 : 10832);
                // Coherent clusters, quiet sections and empty seats rather than a uniform fill.
                var chosen = seats.Select(p => new {position=p, rank=random.NextDouble() /
                    (.25 + .75 * Math.Max(0, Math.Cos(p.z * .63 + Math.Sign(p.x) * 1.9))) * (p.x > 0 ? 1.25 : 1)})
                    .OrderBy(p => p.rank).Take(venue == "Professional" ? 48 : 32).Select(p => p.position)
                    .OrderBy(p => p.x).ThenBy(p => p.z).ToArray();
                float sideThreshold = seats.Max(p => Mathf.Abs(p.x)) * .72f;
                for (int i = 0; i < chosen.Length; i++)
                {
                    var seat = chosen[i]; bool side = Mathf.Abs(seat.x) >= sideThreshold;
                    var forward = side ? new Vector3(-Mathf.Sign(seat.x),0,0) : new Vector3(0,0,-Mathf.Sign(seat.z));
                    int model = i < 4 ? i : random.Next(4);
                    var fan = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[model], crowd.transform);
                    fan.name = "Fan " + i.ToString("D2") + " - " + Models[model];
                    fan.transform.position = seat - forward * .045f;
                    fan.transform.rotation = Quaternion.LookRotation(forward);
                    fan.transform.localScale = Vector3.one * Mathf.Lerp(.94f, 1.02f, (float)random.NextDouble());
                    var actor = fan.GetComponent<AudienceSpectator>();
                    actor.variation = i + (venue == "Professional" ? 203 : 409);
                    actor.supportedTeam = random.NextDouble() < .23 ? (seat.x < 0 ? 1 : 0) : (seat.x < 0 ? 0 : 1);
                    actor.showOnMobile = i % 2 == 0;
                    actor.Initialize();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(actor);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(fan.transform);
                }
                PrefabUtility.SaveAsPrefabAsset(crowd, Output + "/" + venue + ".prefab");
                Debug.Log(venue + ": " + chosen.Length + " seated spectators / " + seats.Count + " actual chairs.");
            }
            finally { PrefabUtility.UnloadPrefabContents(stadium); UnityEngine.Object.DestroyImmediate(crowd); }
        }

        static void BuildOutdoorStands(Transform parent)
        {
            var source = PrefabUtility.LoadPrefabContents("Assets/Resources/Courts/Professional.prefab");
            try
            {
                var filter = source.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "Refined_Venue_Structure");
                var renderer = filter.GetComponent<MeshRenderer>();
                var original = filter.sharedMesh;
                var vertices = original.vertices.Select(filter.transform.TransformPoint).ToArray();
                var normals = original.normals.Select(n => filter.transform.TransformDirection(n).normalized).ToArray();
                var uv = original.uv;
                var used = new Dictionary<int, int>();
                var positions = new List<Vector3>(); var directions = new List<Vector3>(); var texcoords = new List<Vector2>();
                var groups = new List<int[]>(); var materials = new List<Material>();
                int Vertex(int old)
                {
                    if (used.TryGetValue(old, out int index)) return index;
                    index = positions.Count; used.Add(old, index);
                    positions.Add(vertices[old]); directions.Add(normals[old]); texcoords.Add(uv.Length > old ? uv[old] : Vector2.zero);
                    return index;
                }
                bool InStand(Vector3 v) => Mathf.Abs(v.x) > 6.1f && Mathf.Abs(v.z) < 9.6f;
                for (int sub = 0; sub < original.subMeshCount; sub++)
                {
                    var material = renderer.sharedMaterials[sub];
                    if (!(material.name.Contains("Concrete") || material.name.Contains("Silver") || material.name.Contains("Rail_Black") || material.name.Contains("Seat_Blue"))) continue;
                    var input = original.GetTriangles(sub); var triangles = new List<int>();
                    for (int t = 0; t < input.Length; t += 3)
                    {
                        int a = input[t], b = input[t+1], c = input[t+2];
                        if (!InStand(vertices[a]) || !InStand(vertices[b]) || !InStand(vertices[c]) ||
                            Mathf.Sign(vertices[a].x) != Mathf.Sign(vertices[b].x) || Mathf.Sign(vertices[a].x) != Mathf.Sign(vertices[c].x)) continue;
                        triangles.Add(Vertex(a)); triangles.Add(Vertex(b)); triangles.Add(Vertex(c));
                    }
                    if (triangles.Count == 0) continue;
                    groups.Add(triangles.ToArray()); materials.Add(material);
                }
                if (positions.Count == 0) throw new InvalidOperationException("Authored side stands were not found.");
                var mesh = new Mesh { name = "Outdoor spectator side stands" };
                mesh.SetVertices(positions); mesh.SetNormals(directions); mesh.SetUVs(0, texcoords); mesh.subMeshCount = groups.Count;
                for (int i = 0; i < groups.Count; i++) mesh.SetTriangles(groups[i], i);
                mesh.RecalculateBounds();
                Directory.CreateDirectory(Folder + "/Meshes");
                string path = Folder + "/Meshes/OutdoorBleachers.asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved) { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); AssetDatabase.SaveAssetIfDirty(saved); }
                else { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
                var stands = new GameObject("Authored side bleachers", typeof(MeshFilter), typeof(MeshRenderer));
                stands.transform.SetParent(parent, false); stands.GetComponent<MeshFilter>().sharedMesh = saved;
                var display = stands.GetComponent<MeshRenderer>(); display.sharedMaterials = materials.ToArray();
                display.shadowCastingMode = ShadowCastingMode.Off; display.receiveShadows = true;
                display.lightProbeUsage = LightProbeUsage.Off; display.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            finally { PrefabUtility.UnloadPrefabContents(source); }
        }
    }
}
