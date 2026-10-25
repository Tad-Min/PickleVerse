using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PickleVerse.CharacterMotion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PickleVerse.LocalMatch.Editor
{
    public static class SecondaryMotionSetup
    {
        [Serializable]
        class Plan { public int baseVertexCount; public Bone[] bones; public Paint[] paint; }
        [Serializable]
        class Bone { public string name, anchor; public int parent, kind; public Vector3 pivot, end; public float stiffness, damping, maximumAngle, gravityScale; }
        [Serializable]
        class Paint { public int vertex, boneA, boneB; public float blend, amount; }
        const string AssetFolder = "Assets/CharacterMotion";
        const string ResourcesFolder = "Assets/Resources/CharacterMotion";
        public static string Build()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory(AssetFolder);
            Directory.CreateDirectory(ResourcesFolder);
            AssetDatabase.Refresh();
            var rows = new List<object>();
            foreach (string gender in new[] { "Female", "Male" })
            {
                string json = File.ReadAllText("../UnityWork/SecondaryMotion/" + gender + "_plan.json");
                var plan = JsonUtility.FromJson<Plan>(json);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LocalMatchContent/Models/" + gender + ".glb");
                var skin = source.GetComponentInChildren<SkinnedMeshRenderer>();
                var original = skin.sharedMesh;
                if (original.vertexCount != plan.baseVertexCount || skin.bones.Length != 59)
                    throw new InvalidDataException("Unexpected source mesh or skeleton: " + gender);
                var mesh = UnityEngine.Object.Instantiate(original);
                mesh.name = gender + " secondary motion skin";
                var definitions = new CharacterMotionProfile.Joint[plan.bones.Length];
                var bindPoses = original.bindposes.ToList();
                int count = skin.bones.Length;
                for (int i = 0; i < plan.bones.Length; i++)
                {
                    var bone = plan.bones[i];
                    int anchor = Array.FindIndex(skin.bones, t => t.name == bone.anchor);
                    if (anchor < 0)
                        throw new InvalidDataException(bone.anchor);
                    var toAnchor = original.bindposes[anchor];
                    Vector3 pivot = toAnchor.MultiplyPoint3x4(bone.pivot), end = toAnchor.MultiplyPoint3x4(bone.end);
                    Vector3 position = bone.parent < 0 ? pivot : pivot - toAnchor.MultiplyPoint3x4(plan.bones[bone.parent].pivot);
                    definitions[i] = new CharacterMotionProfile.Joint
                    {
                        name = bone.name,
                        anchor = bone.anchor,
                        parent = bone.parent,
                        localPosition = position,
                        endOffset = end - pivot,
                        part = (SecondaryPart)bone.kind,
                        stiffness = bone.stiffness,
                        damping = bone.damping,
                        maximumAngle = bone.maximumAngle,
                        gravityScale = bone.gravityScale,
                        meshPivot = bone.pivot,
                        meshEnd = bone.end
                    };
                    bindPoses.Add((toAnchor.inverse * Matrix4x4.Translate(pivot)).inverse);
                }
                mesh.bindposes = bindPoses.ToArray();
                var weights = original.boneWeights;
                foreach (var paint in plan.paint)
                {
                    if (plan.bones[paint.boneA].kind == 2)
                        continue;
                    var w = weights[paint.vertex];
                    var values = new Dictionary<int, float>();
                    void Add(int index, float value)
                    {
                        if (value > 0)
                            values[index] = (values.TryGetValue(index, out var previous) ? previous : 0) + value;
                    }
                    if (plan.bones[paint.boneA].kind == 0)
                    {
                        // Ponytail texels can contain shirt colors; the entire
                        // selected surface must follow Head + hair, never an arm.
                        int head = Array.FindIndex(skin.bones, t => t.name == "mixamorig:Head");
                        Add(head, 1 - paint.amount);
                    }
                    else
                    {
                        Add(w.boneIndex0, w.weight0 * (1 - paint.amount));
                        Add(w.boneIndex1, w.weight1 * (1 - paint.amount));
                        Add(w.boneIndex2, w.weight2 * (1 - paint.amount));
                        Add(w.boneIndex3, w.weight3 * (1 - paint.amount));
                    }
                    Add(count + paint.boneA, paint.amount * (1 - paint.blend));
                    Add(count + paint.boneB, paint.amount * paint.blend);
                    var sorted = values.OrderByDescending(v => v.Value).Take(4).ToArray();
                    float sum = sorted.Sum(v => v.Value);
                    var result = new BoneWeight();
                    if (sorted.Length > 0)
                    {
                        result.boneIndex0 = sorted[0].Key;
                        result.weight0 = sorted[0].Value / sum;
                    }
                    if (sorted.Length > 1)
                    {
                        result.boneIndex1 = sorted[1].Key;
                        result.weight1 = sorted[1].Value / sum;
                    }
                    if (sorted.Length > 2)
                    {
                        result.boneIndex2 = sorted[2].Key;
                        result.weight2 = sorted[2].Value / sum;
                    }
                    if (sorted.Length > 3)
                    {
                        result.boneIndex3 = sorted[3].Key;
                        result.weight3 = sorted[3].Value / sum;
                    }
                    weights[paint.vertex] = result;
                }
                mesh.boneWeights = weights;
                if (gender == "Female")
                {
                    var vertices = original.vertices;
                    float centerZ = .065f;
                    foreach (int bone in Enumerable.Range(0, plan.bones.Length).Where(i => plan.bones[i].kind == 2 && plan.bones[i].parent >= 0))
                    {
                        float sector = Mathf.Atan2(plan.bones[bone].pivot.x, plan.bones[bone].pivot.z - centerZ);
                        var x = new Vector3[vertices.Length];
                        var z = new Vector3[vertices.Length];
                        foreach (var paint in plan.paint.Where(p => plan.bones[p.boneA].kind == 2))
                        {
                            var v = vertices[paint.vertex];
                            float angle = Mathf.Atan2(v.x, v.z - centerZ);
                            float radial = Mathf.Max(0, 1 - Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, sector * Mathf.Rad2Deg)) / 45f);
                            float amount = paint.amount * radial;
                            x[paint.vertex] = new Vector3(.03f * amount, 0, 0);
                            z[paint.vertex] = new Vector3(0, 0, .03f * amount);
                        }
                        definitions[bone].blendShapeX = mesh.blendShapeCount;
                        mesh.AddBlendShapeFrame("SkirtSwayX_" + bone, 100, x, null, null);
                        definitions[bone].blendShapeZ = mesh.blendShapeCount;
                        mesh.AddBlendShapeFrame("SkirtSwayZ_" + bone, 100, z, null, null);
                    }
                }
                string meshPath = AssetFolder + "/" + gender + "MotionMesh.asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existing)
                {
                    EditorUtility.CopySerialized(mesh, existing);
                    existing.bindposes = mesh.bindposes;
                    existing.boneWeights = mesh.boneWeights;
                    existing.UploadMeshData(false);
                    UnityEngine.Object.DestroyImmediate(mesh);
                    mesh = existing;
                    EditorUtility.SetDirty(mesh);
                }
                else AssetDatabase.CreateAsset(mesh, meshPath);
                AssetDatabase.SaveAssetIfDirty(mesh);
                string profilePath = ResourcesFolder + "/" + gender + "Motion.asset";
                var profile = AssetDatabase.LoadAssetAtPath<CharacterMotionProfile>(profilePath);
                if (!profile)
                {
                    profile = ScriptableObject.CreateInstance<CharacterMotionProfile>();
                    AssetDatabase.CreateAsset(profile, profilePath);
                }
                profile.mesh = mesh;
                profile.originalVertexCount = original.vertexCount;
                profile.originalBoneCount = count;
                profile.originalBoneNames = skin.bones.Select(t => t.name).ToArray();
                profile.joints = definitions;
                if (gender == "Female")
                    BuildFringe(profile, skin, original);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
                var changed = new HashSet<int>(plan.paint.Select(p => p.vertex));
                bool untouched = true;
                var oldWeights = original.boneWeights;
                for (int i = 0; i < weights.Length; i++)
                    if (!changed.Contains(i))
                        untouched &= weights[i].Equals(oldWeights[i]);
                if (!untouched)
                    throw new InvalidDataException("Unselected body weights changed");
                rows.Add(new
                {
                    gender = gender,
                    originalBones = count,
                    physicsBones = definitions.Length,
                    vertices = original.vertexCount,
                    affectedVertices = changed.Count,
                    unselectedWeightsPreserved = untouched,
                    mesh = meshPath,
                    profile = profilePath
                });
            }
            var report = Newtonsoft.Json.JsonConvert.SerializeObject(rows, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText("../UnityWork/SecondaryMotion/rig-report.json", report);
            return report;
        }
        static void BuildFringe(CharacterMotionProfile profile, SkinnedMeshRenderer skin, Mesh original)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFolder + "/FemaleFringe.glb");
            if (!source)
                return;
            var filter = source.GetComponentInChildren<MeshFilter>();
            int head = Array.FindIndex(skin.bones, t => t.name == "mixamorig:Head");
            var toHead = original.bindposes[head] * skin.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
            mesh.name = "Female fringe in Head space";
            mesh.vertices = mesh.vertices.Select(v => toHead.MultiplyPoint3x4(v)).ToArray();
            var normals = toHead.inverse.transpose;
            mesh.normals = mesh.normals.Select(n => normals.MultiplyVector(n).normalized).ToArray();
            mesh.RecalculateBounds();
            string path = AssetFolder + "/FemaleFringeHead.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing)
            {
                EditorUtility.CopySerialized(mesh, existing);
                existing.vertices = mesh.vertices;
                existing.normals = mesh.normals;
                existing.UploadMeshData(false);
                UnityEngine.Object.DestroyImmediate(mesh);
                mesh = existing;
                EditorUtility.SetDirty(mesh);
            }
            else AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssetIfDirty(mesh);
            profile.foreheadMesh = mesh;
            string materialPath = AssetFolder + "/FemaleFringeMaterial.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material)
            {
                material = new Material(filter.GetComponent<MeshRenderer>().sharedMaterial);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else
            {
                EditorUtility.CopySerialized(filter.GetComponent<MeshRenderer>().sharedMaterial, material);
                EditorUtility.SetDirty(material);
            }
            profile.foreheadMaterial = material;
            AssetDatabase.SaveAssetIfDirty(material);
        }
        [MenuItem("PickleVerse/Add hair and skirt physics")]
        public static void ApplyMenu()
        {
            Apply();
        }
        public static string Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/BotMatch.unity")
                throw new InvalidOperationException("Open BotMatch first.");
            Directory.CreateDirectory("../UnityWork/SecondaryMotion");
            if (!File.Exists("../UnityWork/SecondaryMotion/Scene_before.unity"))
                EditorSceneManager.SaveScene(scene, "../UnityWork/SecondaryMotion/Scene_before.unity", true);
            string report = Build();
            var match = UnityEngine.Object.FindAnyObjectByType<BotMatch>();
            foreach (var actor in new[] { match.human, match.bot })
            {
                var component = CharacterSecondaryMotion.Attach(actor.visual.gameObject, actor.playerId == 0);
                EditorUtility.SetDirty(component);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return report;
        }
    }
}
