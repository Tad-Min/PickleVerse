using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PickleVerse.LocalMatch.Editor
{
    public static class BallPresentationSetup
    {
        public static void Configure(BotMatch match)
        {
            if (match.ballShadow)
            {
                var shadow = match.ballShadow.gameObject;
                match.ballShadow = null;
                UnityEngine.Object.DestroyImmediate(shadow);
            }
            var trail = match.ball.GetComponent<TrailRenderer>();
            if (!trail)
                trail = match.ball.gameObject.AddComponent<TrailRenderer>();
            match.ballTrail = trail;
            trail.time = .18f;
            trail.minVertexDistance = .025f;
            trail.widthMultiplier = 1;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, .055f), new Keyframe(.4f, .025f), new Keyframe(1, 0));
            trail.numCapVertices = 4;
            trail.numCornerVertices = 3;
            trail.alignment = LineAlignment.View;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(1f, 1f, .50f), 0), new GradientColorKey(new Color(.60f, 1f, .80f), 1) },
                new[] { new GradientAlphaKey(.48f, 0), new GradientAlphaKey(.18f, .4f), new GradientAlphaKey(0, 1) });
            trail.colorGradient = gradient;
            trail.emitting = false;
            trail.Clear();
            const string path = "Assets/LocalMatchContent/Materials/BallTrail.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                material.name = "BallTrail";
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 2);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetColor("_BaseColor", new Color(1.15f, 1.25f, .75f, 1));
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            trail.sharedMaterial = material;

            var hud = match.hud;
            var panel = (RectTransform)hud.selection.transform.parent;
            hud.shotPanel = panel.gameObject;
            panel.anchorMin = panel.anchorMax = new Vector2(1, 0);
            panel.pivot = new Vector2(1, 0);
            panel.anchoredPosition = new Vector2(-16, 16);
            panel.sizeDelta = new Vector2(208, 56);
            panel.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            var label = hud.selection.rectTransform;
            label.anchorMin = label.anchorMax = label.pivot = new Vector2(.5f, .5f);
            label.anchoredPosition = Vector2.zero;
            label.sizeDelta = new Vector2(192, 48);
            hud.selection.fontSize = 18;
            hud.selection.alignment = TextAlignmentOptions.Center;
            hud.selection.raycastTarget = false;
            hud.selection.text = "";
            hud.timing.text = "";
            hud.timing.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
            // Leave space below the touch buttons for the same compact corner indicator.
            foreach (var control in hud.touchControls.GetComponentsInChildren<TouchControl>(true).Where(c => !c.joystick))
            {
                int[] masks = { 1, 2, 4, 3, 5, 6, 8, 16 };
                int index = Array.IndexOf(masks, control.shotMask);
                if (index < 0)
                    continue;
                var rect = (RectTransform)control.transform;
                var position = rect.anchoredPosition;
                position.y = 86 + index / 3 * 56;
                rect.anchoredPosition = position;
            }
            match.human.Initialize();
            match.bot.Initialize();
            match.human.ResetAt(match.human.transform.position, match.human.end);
            match.bot.ResetAt(match.bot.transform.position, match.bot.end);
            match.human.RefreshPose();
            match.bot.RefreshPose();
            match.ball.position = match.human.HeldBallPosition;
            EditorUtility.SetDirty(match);
            EditorUtility.SetDirty(hud);
        }

        [MenuItem("PickleVerse/Update ball and compact shot HUD")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/BotMatch.unity")
                throw new InvalidOperationException("Open BotMatch first.");
            var match = UnityEngine.Object.FindAnyObjectByType<BotMatch>();
            Configure(match);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
