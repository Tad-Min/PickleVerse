using System;
using System.Globalization;
using System.IO;
using System.Linq;
using PickleVerse.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PickleVerse.VFX.Editor
{
    /// <summary>
    /// Films a paddle-skin effect in a court scene for review, without entering Play Mode: one shot from the paddle with the
    /// gameplay camera behind the hitter, then a side view. The ball, the effect and its particles are stepped by hand at
    /// 60 Hz and frames are written as PNG. Batch use:
    ///   -executeMethod PickleVerse.VFX.Editor.PaddleSkinVfxFilm.Shoot  (env PKV_FILM_SKIN, PKV_FILM_OUT, PKV_FILM_SCENE)
    /// The open scene is replaced, so the method refuses to run when the Editor has unsaved scenes.
    /// </summary>
    public static class PaddleSkinVfxFilm
    {
        const float Step = 1 / 60f;

        [MenuItem("PickleVerse/VFX/Film paddle skin effect (Blue Phoenix)")]
        static void FilmFromMenu() => Film("paddle.bluephoenix", Path.GetFullPath("../artifacts/vfx-film"), "Assets/Scenes/BotMatch.unity");

        public static void Shoot()
        {
            string skin = Environment.GetEnvironmentVariable("PKV_FILM_SKIN") ?? "paddle.bluephoenix";
            string output = Environment.GetEnvironmentVariable("PKV_FILM_OUT") ?? Path.GetFullPath("../artifacts/vfx-film");
            string scene = Environment.GetEnvironmentVariable("PKV_FILM_SCENE") ?? "Assets/Scenes/BotMatch.unity";
            Film(skin, output, scene);
        }

        public static void Film(string skin, string output, string scenePath)
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard the open scenes before filming: the film replaces them.");
            var profile = BallSkinVfx.Library ? BallSkinVfx.Library.Find(skin) : null;
            if (!profile)
                throw new InvalidOperationException("No effect for " + skin + " in Resources/VFX/PaddleSkinVfxLibrary.");
            Directory.CreateDirectory(output);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            foreach (var other in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
                other.enabled = false;

            var camera = new GameObject("Film Camera", typeof(Camera)).GetComponent<Camera>();
            camera.fieldOfView = 56;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 200;
            camera.allowHDR = true;
            var data = camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            camera.tag = "MainCamera";
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.targetTexture = rt;

            var offer = PlayerEconomy.Catalog.Find(skin);
            Vector3 contact = new Vector3(-.4f, 1.0f, -5.6f), velocity = new Vector3(.55f, 3.1f, 10.8f);
            GameObject paddle = null;
            if (offer != null && offer.gameplayPrefab)
            {
                paddle = (GameObject)UnityEngine.Object.Instantiate(offer.gameplayPrefab);
                paddle.transform.position = contact + new Vector3(-.05f, -.05f, -.14f);
                paddle.transform.rotation = Quaternion.Euler(78, 20, 0);
                paddle.transform.localScale = Vector3.one * .17f;          // the match socket's scale
            }
            if (offer != null && offer.previewPrefab)
            {
                // close-up of the paddle itself (shop model, game shader) under the court's light
                var still = (GameObject)UnityEngine.Object.Instantiate(offer.previewPrefab);
                still.transform.position = new Vector3(0, 1.6f, contact.z - 1.5f);
                still.transform.localScale = Vector3.one * .1f;
                camera.Render();                                          // the first render of a batch session lights nothing yet
                foreach (var (label, yaw) in new[] { ("front", 155f), ("three-quarter", 205f), ("back", -25f) })
                {
                    still.transform.rotation = Quaternion.Euler(0, yaw, 0);
                    camera.transform.position = still.transform.position + new Vector3(0, .02f, -.62f);
                    camera.transform.LookAt(still.transform.position);
                    camera.fieldOfView = 30;
                    Save(camera, rt, Path.Combine(output, "paddle_" + label + ".png"));
                }
                camera.fieldOfView = 56;
                UnityEngine.Object.DestroyImmediate(still);
            }
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DestroyImmediate(ball.GetComponent<Collider>());
            ball.transform.localScale = Vector3.one * .074f;
            ball.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.95f, .9f, .2f) };
            var trail = ball.AddComponent<TrailRenderer>();
            trail.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            trail.time = .2f;
            trail.widthMultiplier = .05f;
            var fx = BallSkinVfx.Attach(ball, trail);

            int shot = 0;
            var travel = velocity.normalized;
            foreach (var (name, eye) in new (string, Func<Vector3, Vector3>)[] { ("behind", b => new Vector3(contact.x * .5f, 3f, contact.z - 5f)), ("side", b => new Vector3(4.6f, 1.5f, contact.z + 4.2f)),
                ("chase", b => b - travel * 2.6f + Vector3.up * .9f + Vector3.right * .6f) })
            {
                ball.transform.position = contact;
                trail.Clear();
                fx.Apply(null);
                fx.Apply(profile);
                fx.Advance(Step, true);
                var motions = ball.GetComponentsInChildren<SpineBallMotion>(true);
                foreach (var m in ball.GetComponentsInChildren<BallSkinMotion>(true))
                    m.Restart(contact);
                ParticleSystem[] impact = Array.Empty<ParticleSystem>();
                if (profile.impactEffect)
                {
                    var go = UnityEngine.Object.Instantiate(profile.impactEffect, contact, Quaternion.identity);
                    impact = go.GetComponentsInChildren<ParticleSystem>(true);
                    foreach (var ps in impact)
                    {
                        ps.Clear(false);
                        ps.Play(false);
                    }
                }
                var systems = ball.GetComponentsInChildren<ParticleSystem>(true).Concat(impact).ToArray();
                float[] keep = { .05f, .12f, .22f, .34f, .48f, .64f, .80f };
                int k = 0;
                for (float t = Step; t <= keep[keep.Length - 1] + 1e-4f; t += Step)
                {
                    ball.transform.position = contact + velocity * t + .5f * Physics.gravity * t * t;
                    fx.Advance(Step, true);
                    camera.transform.position = eye(ball.transform.position);
                    camera.transform.LookAt(name == "behind" ? new Vector3(0, 1.1f, contact.z + 5.5f) : name == "side" ? new Vector3(contact.x, 1.4f, contact.z + 3.6f) : ball.transform.position - travel * .7f);
                    foreach (var m in motions)
                        m.Advance(ball.transform.position, Step, camera);
                    foreach (var ps in systems)
                        if (ps)
                            ps.Simulate(Step, false, false, false);
                    if (k < keep.Length && t >= keep[k] - 1e-4f)
                    {
                        Save(camera, rt, Path.Combine(output, string.Format(CultureInfo.InvariantCulture, "{0}_{1:00}_{2:0.00}s.png", name, shot, t)));
                        k++;
                        shot++;
                    }
                }
                foreach (var ps in impact)
                    if (ps)
                        UnityEngine.Object.DestroyImmediate(ps.transform.root.gameObject);
            }
            Debug.Log("[PaddleSkinVfxFilm] " + skin + ": " + shot + " frames in " + output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        static void Save(Camera camera, RenderTexture rt, string path)
        {
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
