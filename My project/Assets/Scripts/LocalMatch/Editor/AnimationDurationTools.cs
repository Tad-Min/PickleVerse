using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace PickleVerse.LocalMatch.Editor
{
    // Retime the complete motion. Trimming stopTime alone loses follow-through.
    public static class AnimationDurationTools
    {
        const string ReportFolder = "../UnityWork/AnimationDuration";
        public static Dictionary<string, float> Targets()
        {
            var documentation = File.ReadAllText("../Doc/PickleVerse_Project_Documentation.md");
            const string beginMarker = "<!-- BEGIN SOURCE: duration_animation.md -->";
            const string endMarker = "<!-- END SOURCE: duration_animation.md -->";
            int begin = documentation.IndexOf(beginMarker, StringComparison.Ordinal);
            int end = begin < 0 ? -1 : documentation.IndexOf(endMarker, begin + beginMarker.Length, StringComparison.Ordinal);
            if (begin < 0 || end < 0) throw new InvalidDataException("Animation duration section is missing from the project documentation.");
            var rows = Regex.Matches(documentation.Substring(begin + beginMarker.Length, end - begin - beginMarker.Length),
                @"\|\s*\d+\s*\|\s*`(PKV_[^`]+)`\s*\|\s*([\d.]+)s");
            var result = rows.Cast<Match>().ToDictionary(m => m.Groups[1].Value,
                m => float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
            if (result.Count != 20) throw new InvalidDataException("Expected all 20 duration rows.");
            return result;
        }
        public static float DurationFor(string name, float fallback)
        {
            return Targets().TryGetValue(name, out float duration) ? duration : fallback;
        }
        static string Key(string path) => "PKV_" + Path.GetFileNameWithoutExtension(path).Replace("PKV_", "");
        static string[] Paths() => AssetDatabase.FindAssets("t:AnimationClip",
            new[] { "Assets/Animation/Characters", "Assets/LocalMatchContent/Clips" })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".anim")).OrderBy(p => p).ToArray();

        [Serializable] public class Row
        {
            public string path, name, guid;
            public float before, after, target, sourceStart, sourceEnd, scale, maxShapeError;
            public int curves, keys, events;
            public bool passed, valuesPreserved, modesPreserved, guidPreserved, legacyPreserved;
        }
        [Serializable] public class Report { public bool passed; public Row[] clips; public string[] skipped; }

        [MenuItem("PickleVerse/Animations/Apply documented durations")]
        public static void ApplyMenu() => Apply();

        public static string Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Use Edit Mode.");
            var targets = Targets(); var rows = new List<Row>(); var skipped = new List<string>();
            foreach (string path in Paths())
            {
                if (!targets.TryGetValue(Key(path), out float target)) { skipped.Add(path); continue; }
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                var bindings = AnimationUtility.GetCurveBindings(clip);
                var original = bindings.Select(b => AnimationUtility.GetEditorCurve(clip, b)).ToArray();
                var pointerBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                var pointerKeys = pointerBindings.Select(b => AnimationUtility.GetObjectReferenceCurve(clip, b)).ToArray();
                var keyTimes = original.SelectMany(c => c.keys.Select(k => k.time))
                    .Concat(pointerKeys.SelectMany(k => k.Select(v => v.time))).ToArray();
                float start = keyTimes.Min(), end = keyTimes.Max(), scale = target / (end - start);
                if (end <= start || !float.IsFinite(scale)) throw new InvalidDataException(path + " has no motion span.");
                string guid = AssetDatabase.AssetPathToGUID(path); bool legacy = clip.legacy;
                float before = clip.length;
                string backup = Path.Combine(ReportFolder, "backup", path);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.Copy(path, backup);
                if (!File.Exists(backup + ".meta")) File.Copy(path + ".meta", backup + ".meta");
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                var events = AnimationUtility.GetAnimationEvents(clip);
                var retimed = original.Select(curve => {
                    var copy = new AnimationCurve(curve.keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode };
                    var keys = copy.keys;
                    for (int i = 0; i < keys.Length; i++)
                    {
                        keys[i].time = (keys[i].time - start) * scale;
                        keys[i].inTangent /= scale; keys[i].outTangent /= scale;
                    }
                    copy.keys = keys; return copy;
                }).ToArray();
                AnimationUtility.SetEditorCurves(clip, bindings, retimed);
                for (int i = 0; i < pointerBindings.Length; i++)
                {
                    var keys = pointerKeys[i];
                    for (int j = 0; j < keys.Length; j++) keys[j].time = (keys[j].time - start) * scale;
                    AnimationUtility.SetObjectReferenceCurve(clip, pointerBindings[i], keys);
                }
                foreach (var e in events) e.time = Mathf.Clamp((e.time - start) * scale, 0, target);
                AnimationUtility.SetAnimationEvents(clip, events);
                settings.startTime = 0; settings.stopTime = target;
                settings.additiveReferencePoseTime = Mathf.Clamp((settings.additiveReferencePoseTime - start) * scale, 0, target);
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);

                float shapeError = 0; bool values = true, modes = true;
                for (int i = 0; i < bindings.Length; i++)
                {
                    var updated = AnimationUtility.GetEditorCurve(clip, bindings[i]);
                    var a = original[i].keys; var b = updated.keys;
                    values &= a.Length == b.Length;
                    for (int j = 0; j < a.Length; j++)
                    {
                        values &= a[j].value == b[j].value;
                        modes &= a[j].weightedMode == b[j].weightedMode && a[j].inWeight == b[j].inWeight && a[j].outWeight == b[j].outWeight
                            && AnimationUtility.GetKeyLeftTangentMode(original[i], j) == AnimationUtility.GetKeyLeftTangentMode(updated, j)
                            && AnimationUtility.GetKeyRightTangentMode(original[i], j) == AnimationUtility.GetKeyRightTangentMode(updated, j);
                    }
                    for (int sample = 0; sample <= 40; sample++)
                    {
                        float phase = sample / 40f;
                        shapeError = Mathf.Max(shapeError, Mathf.Abs(original[i].Evaluate(start + phase * (end - start)) - updated.Evaluate(phase * target)));
                    }
                }
                var row = new Row { path = path, name = Key(path), guid = guid, before = before, after = clip.length,
                    target = target, sourceStart = start, sourceEnd = end, scale = scale, maxShapeError = shapeError,
                    curves = bindings.Length, keys = original.Sum(c => c.length), events = events.Length,
                    valuesPreserved = values, modesPreserved = modes, guidPreserved = guid == AssetDatabase.AssetPathToGUID(path), legacyPreserved = legacy == clip.legacy };
                row.passed = Mathf.Abs(row.after - target) < .00001f && values && modes && row.guidPreserved && row.legacyPreserved && shapeError < .0001f;
                rows.Add(row);
                if (!row.passed) throw new InvalidDataException("Retime verification failed: " + JsonUtility.ToJson(row));
            }
            AssetDatabase.SaveAssets();
            var report = new Report { passed = rows.All(r => r.passed), clips = rows.ToArray(), skipped = skipped.ToArray() };
            File.WriteAllText(Path.Combine(ReportFolder, "retime-report.json"), JsonUtility.ToJson(report, true));
            CompactEditorCaches();
            return "Retimed " + rows.Count + " clips; all duration, curve shape and GUID checks passed.";
        }

        public static string RestoreFemaleJump()
        {
            const string canonical = "Assets/Animation/Characters/Female/Jump.anim";
            if (File.Exists(canonical)) return "Female Jump already exists.";
            var clip = AnimationData.Create("female", "PKV_Jump", canonical);
            clip.name = "Jump"; clip.legacy = false;
            clip.wrapMode = WrapMode.Once;
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip); AssetDatabase.SaveAssets();
            return "Recovered Female/Jump.anim; length=" + clip.length;
        }

        public static string VerifySaved()
        {
            var targets = Targets();
            var paths = Paths().Where(p => targets.ContainsKey(Key(p))).ToArray();
            foreach (var path in paths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (Mathf.Abs(clip.length - targets[Key(path)]) > .00001f) throw new InvalidDataException(path);
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                if (Mathf.Abs(settings.stopTime - targets[Key(path)]) > .00001f) throw new InvalidDataException("stopTime " + path);
            }
            foreach (string gender in new[] { "Female", "Male" })
                foreach (var target in targets)
                    if (!File.Exists("Assets/Animation/Characters/" + gender + "/" + target.Key.Substring(4) + ".anim"))
                        throw new FileNotFoundException(gender + "/" + target.Key);
            return "Verified " + paths.Length + " saved assets; both genders have all 20 documented clips.";
        }

        // SetEditorCurves creates redundant quaternion/Euler editor caches. Original
        // clips did not store them; retain the runtime curves without multiplying asset size.
        public static string CompactEditorCaches()
        {
            var targets = Targets(); int count = 0;
            foreach (string path in Paths().Where(p => targets.ContainsKey(Key(p))))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                var bindings = AnimationUtility.GetCurveBindings(clip);
                var curves = bindings.Select(b => AnimationUtility.GetEditorCurve(clip, b)).ToArray();
                var saved = UnityEngine.Object.Instantiate(clip);
                var events = AnimationUtility.GetAnimationEvents(clip);
                var pointers = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                var pointerCurves = pointers.Select(b => AnimationUtility.GetObjectReferenceCurve(clip, b)).ToArray();
                saved.name = clip.name;
                saved.ClearCurves();
                for (int i = 0; i < bindings.Length; i++)
                    saved.SetCurve(bindings[i].path, bindings[i].type, bindings[i].propertyName, curves[i]);
                for (int i = 0; i < pointers.Length; i++) AnimationUtility.SetObjectReferenceCurve(saved, pointers[i], pointerCurves[i]);
                AnimationUtility.SetAnimationEvents(saved, events);
                bool valid = AnimationUtility.GetCurveBindings(saved).Length == bindings.Length && Mathf.Abs(saved.length - clip.length) < .00001f;
                for (int i = 0; i < bindings.Length && valid; i++)
                {
                    var curve = AnimationUtility.GetEditorCurve(saved, bindings[i]);
                    valid &= curve != null && curve.length == curves[i].length;
                    for (int sample = 0; sample <= 40 && valid; sample++)
                    {
                        float time = clip.length * sample / 40f;
                        valid &= Mathf.Abs(curves[i].Evaluate(time) - curve.Evaluate(time)) < .0001f;
                    }
                }
                if (!valid) { UnityEngine.Object.DestroyImmediate(saved); throw new InvalidDataException("Editor cache removal changed curves: " + path); }
                // Inspection generates caches again. Rebuild using the validated exact keys
                // one final time, then save without inspecting the temporary editor caches.
                saved.ClearCurves();
                for (int i = 0; i < bindings.Length; i++)
                    saved.SetCurve(bindings[i].path, bindings[i].type, bindings[i].propertyName, curves[i]);
                for (int i = 0; i < pointers.Length; i++) AnimationUtility.SetObjectReferenceCurve(saved, pointers[i], pointerCurves[i]);
                AnimationUtility.SetAnimationEvents(saved, events);
                EditorUtility.CopySerialized(saved, clip);
                UnityEngine.Object.DestroyImmediate(saved);
                EditorUtility.SetDirty(clip); count++;
            }
            AssetDatabase.SaveAssets(); return "Compacted " + count + " assets with identical runtime curves.";
        }
    }
}
