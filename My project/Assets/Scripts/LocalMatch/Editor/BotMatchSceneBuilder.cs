using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;
using PickleVerse.Rules;

namespace PickleVerse.LocalMatch.Editor
{
    public static class BotMatchSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/BotMatch.unity";
        const string Content = "Assets/LocalMatchContent";
        static TMP_FontAsset font;
        static Color ink = new Color(.035f, .085f, .12f, .94f), mint = new Color(.4f, .92f, .77f), amber = new Color(1f, .72f, .4f);
        [MenuItem("PickleVerse/Create or rebuild BotMatch scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode before rebuilding the scene.");
            // Preserve any unrelated unsaved scene before opening the requested new scene.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isDirty)
                    continue;
                Directory.CreateDirectory("../UnityWork/BotMatch/SceneBackups");
                EditorSceneManager.SaveScene(s, "../UnityWork/BotMatch/SceneBackups/" + s.name + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity", true);
            }
            Directory.CreateDirectory(Content + "/Clips/female");
            Directory.CreateDirectory(Content + "/Clips/male");
            Directory.CreateDirectory(Content + "/Materials");
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorUtility.UnloadUnusedAssetsImmediate();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.63f, .75f, .85f);
            RenderSettings.ambientEquatorColor = new Color(.48f, .58f, .58f);
            RenderSettings.ambientGroundColor = new Color(.23f, .29f, .26f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.64f, .78f, .82f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 30;
            RenderSettings.fogEndDistance = 100;
            var arena = new GameObject("Arena").transform;
            BotMatchCourtBuilder.Create(arena);
            Primitive("ArenaFoundation", PrimitiveType.Cube, new Vector3(0, -.13f, 0), new Vector3(32, .2f, 30), new Color(.14f, .24f, .22f), arena);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = -1; row <= 1; row += 2)
                {
                    var stand = Model("Assets/Models/khán_đài/Khan_dai.glb", "Grandstand_" + side + "_" + row, arena);
                    stand.transform.localScale = Vector3.one * 7.5f;
                    stand.transform.position = new Vector3(side * 10.5f, -.02f, row * 4.1f);
                    stand.transform.rotation = Quaternion.Euler(0, side < 0 ? 0 : 180, 0) * stand.transform.rotation;
                }
                Primitive("ArenaRail", PrimitiveType.Cube, new Vector3(side * 5.45f, .45f, 0), new Vector3(.12f, .9f, 20.4f), new Color(.045f, .11f, .14f), arena);
                for (int z = -1; z <= 1; z += 2)
                {
                    Primitive("LightMast", PrimitiveType.Cylinder, new Vector3(side * 5.7f, 2.7f, z * 9.5f), new Vector3(.1f, 2.7f, .1f), new Color(.12f, .17f, .19f), arena);
                    Primitive("LampHousing", PrimitiveType.Cube, new Vector3(side * 5.7f, 5.45f, z * 9.5f), new Vector3(.5f, .16f, .3f), new Color(.85f, .94f, .96f), arena);
                }
            }
            var lightObject = new GameObject("Match Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(55, -35, 0);
            RenderSettings.sun = light;
            font = LoadFont();
            Banner("PICKLEVERSE", new Vector3(0, 1.1f, 10.9f), 180, arena);
            Banner("PLAY / LEARN / COMPETE", new Vector3(0, 1.1f, -10.9f), 0, arena);
            var match = new GameObject("Local Singles Match").AddComponent<BotMatch>();
            match.human = CreateActor("female", 0, -1);
            match.bot = CreateActor("male", 1, 1);
            match.human.ResetAt(new Vector3(1.45f, 0, -7.25f), -1);
            match.bot.ResetAt(new Vector3(-1.45f, 0, 6.1f), 1);
            var ball = Model("Assets/Models/ball/pickleball_ball_cartoon.glb", "Pickleball", match.transform);
            ball.transform.localScale = Vector3.one * Court.BallRadius * 1.5f;
            ball.transform.position = new Vector3(1.45f, .9f, -7.25f);
            match.ball = ball.transform;
            match.landingMarker = Disc("IncomingLanding", .35f, amber, match.transform);
            match.aimMarker = Disc("ShotAim", .3f, mint, match.transform);
            var cameraObject = new GameObject("Match Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.fieldOfView = 60;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100;
            camera.backgroundColor = new Color(.58f, .75f, .82f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            var follow = cameraObject.AddComponent<MatchCamera>();
            follow.player = match.human;
            follow.SnapToPlayer();
            BuildHud(match);
            BallPresentationSetup.Configure(match);
            BotMatchIntroSetup.Configure(match);
            MatchAnnouncementsSetup.Configure(match);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Failed saving BotMatch scene.");
            MatchEnvironmentSetup.Configure();
            if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = match.gameObject;
            if (SceneView.lastActiveSceneView)
                SceneView.lastActiveSceneView.Frame(new Bounds(Vector3.zero, new Vector3(18, 6, 24)), false);
            Debug.Log("BotMatch built: official metre court, female player, male local bot, gameplay animations, HUD and touch controls.");
        }
        static GameObject Model(string path, string name, Transform parent)
        {
            if (path.Contains("female/PKV_Idle_Ready"))
                path = Content + "/Models/Female.glb";
            else if (path.Contains("male/PKV_Idle_Ready"))
                path = Content + "/Models/Male.glb";
            else if (path.Contains("pickleball_ball_cartoon"))
                path = Content + "/Models/Ball.glb";
            else if (path.Contains("Vot_Pro"))
                path = Content + "/Models/Paddle.glb";
            else if (path.Contains("Khan_dai"))
                path = Content + "/Models/Grandstand.glb";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!source)
                throw new FileNotFoundException(path);
            var instance = UnityEngine.Object.Instantiate(source, parent);
            instance.name = name;
            foreach (var camera in instance.GetComponentsInChildren<Camera>(true))
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
            foreach (var light in instance.GetComponentsInChildren<Light>(true))
                UnityEngine.Object.DestroyImmediate(light.gameObject);
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
            return instance;
        }
        static MatchActor CreateActor(string gender, int id, int end)
        {
            var actor = new GameObject(id == 0 ? "Player / Female" : "Bot / Male").AddComponent<MatchActor>();
            actor.playerId = id;
            actor.end = end;
            string basePath = "Assets/Models/Character/" + gender + "/";
            var visual = Model(basePath + "PKV_Idle_Ready.glb", "Character", actor.transform);
            actor.visual = visual.transform;
            foreach (var animator in visual.GetComponentsInChildren<Animator>(true))
                UnityEngine.Object.DestroyImmediate(animator);
            foreach (var animation in visual.GetComponentsInChildren<Animation>(true))
                UnityEngine.Object.DestroyImmediate(animation);
            visual.AddComponent<Animation>().playAutomatically = false;
            var transforms = visual.GetComponentsInChildren<Transform>(true);
            actor.leftFoot = transforms.Single(t => t.name == "mixamorig:LeftFoot");
            actor.rightFoot = transforms.Single(t => t.name == "mixamorig:RightFoot");
            actor.upperArm = transforms.Single(t => t.name == "mixamorig:RightArm");
            actor.forearm = transforms.Single(t => t.name == "mixamorig:RightForeArm");
            actor.hand = transforms.Single(t => t.name == "mixamorig:RightHand");
            var socket = transforms.Single(t => t.name == "PKV_PaddleSocket_R");
            var paddle = Model("Assets/Models/Vợt/Vot_Pro.glb", "Paddle", socket);
            actor.paddle = paddle;
            paddle.transform.localPosition = new Vector3(0, .12568575f, 0);
            paddle.transform.localRotation = new Quaternion(.70703185f, -.01028876f, .01028904f, .70703202f);
            paddle.transform.localScale = Vector3.one * .16537598f;
            var face = new GameObject("Paddle Contact").transform;
            face.SetParent(paddle.transform, false);
            face.localPosition = new Vector3(0, 0, -.60f);
            actor.paddleFace = face;
            var entries = new List<ClipEntry>();
            string dataPath = Path.GetFullPath("../UnityWork/BotMatch/ClipData/" + gender);
            foreach (string path in Directory.GetFiles(dataPath, "PKV_*.json").OrderBy(p => p))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (name.Contains("Walking") || name.Contains("Running") || name.Contains("Jump"))
                    continue;
                string output = Content + "/Clips/" + gender + "/" + name + ".anim";
                var copy = AnimationData.Create(gender, name, output);
                entries.Add(new ClipEntry { name = name, clip = copy });
            }
            actor.clips = entries.ToArray();
            var idle = actor.clips.Single(c => c.name == "PKV_Idle_Ready").clip;
            idle.SampleAnimation(visual, 1f);
            // Imported bind bounds include non-rest poses; ground using the actual sole geometry.
            var skin = visual.GetComponentInChildren<SkinnedMeshRenderer>();
            skin.updateWhenOffscreen = true;
            var baked = new Mesh();
            skin.BakeMesh(baked);
            float minimum = baked.vertices.Min(v => skin.transform.TransformPoint(v).y);
            visual.transform.localPosition += Vector3.up * -minimum;
            UnityEngine.Object.DestroyImmediate(baked);
            for (int i = 0; i < actor.clips.Length; i++)
            {
                var entry = actor.clips[i];
                float best = float.NegativeInfinity;
                float phaseBest = .45f;
                Vector3 offset = Vector3.zero;
                for (int sample = 6; sample <= 90; sample++)
                {
                    float phase = sample / 100f;
                    entry.clip.SampleAnimation(visual, entry.clip.length * phase);
                    Vector3 p = actor.transform.InverseTransformPoint(face.position);
                    bool smash = entry.name.Contains("Smash");
                    float score = smash ? p.y : p.z - Mathf.Abs(p.y - .8f) * .25f;
                    if (!smash && (p.y < .4f || p.y > 1.2f))
                        score -= 3;
                    if (entry.name.EndsWith("_FH") && p.x < .08f)
                        score -= 2;
                    if (entry.name.EndsWith("_BH") && p.x > .05f)
                        score -= 2;
                    if (score > best)
                    {
                        best = score;
                        phaseBest = phase;
                        offset = p;
                    }
                }
                if (entry.name.Contains("Serve"))
                {
                    phaseBest = .5333333f;
                    entry.clip.SampleAnimation(visual, entry.clip.length * phaseBest);
                    actor.ServeGrip(1f);
                    offset = actor.transform.InverseTransformPoint(face.position);
                }
                if (entry.name.Contains("Smash"))
                {
                    phaseBest = .45f;
                    offset = new Vector3(.12f, 2.02f, .35f);
                }
                entry.contactPhase = phaseBest;
                entry.contactOffset = offset;
                actor.clips[i] = entry;
            }
            idle.SampleAnimation(visual, 0);
            return actor;
        }
        static Material Material(string name, Color color)
        {
            string path = Content + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .12f);
            return material;
        }
        static GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = position;
            go.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = Material(name, color);
            return go;
        }
        static Transform Disc(string name, float width, Color color, Transform parent)
        {
            var disc = Primitive(name, PrimitiveType.Cylinder, new Vector3(0, .015f, 0), new Vector3(width, .004f, width), color, parent);
            return disc.transform;
        }
        static TMP_FontAsset LoadFont()
        {
            string sourcePath = Content + "/Arial.ttf", path = Content + "/Match Vietnamese Font.asset";
            if (!File.Exists(sourcePath))
                File.Copy(Environment.GetFolderPath(Environment.SpecialFolder.Fonts) + "/arial.ttf", sourcePath);
            AssetDatabase.ImportAsset(sourcePath);
            var result = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (result && (!AssetDatabase.LoadAllAssetsAtPath(path).OfType<Texture2D>().Any() || !result.material))
            {
                AssetDatabase.DeleteAsset(path);
                result = null;
            }
            if (!result)
            {
                result = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(sourcePath), 42, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
                result.name = "Match Vietnamese Font";
                var textures = result.atlasTextures;
                var material = result.material;
                AssetDatabase.CreateAsset(result, path);
                foreach (var texture in textures)
                {
                    texture.hideFlags = HideFlags.None;
                    AssetDatabase.AddObjectToAsset(texture, result);
                }
                material.hideFlags = HideFlags.None;
                AssetDatabase.AddObjectToAsset(material, result);
                result.atlasTextures = textures;
                result.material = material;
                EditorUtility.SetDirty(result);
                AssetDatabase.SaveAssets();
            }
            return result;
        }
        static void Banner(string value, Vector3 position, float rotation, Transform parent)
        {
            Primitive("SponsorBoard", PrimitiveType.Cube, position, new Vector3(8f, 1.35f, .12f), new Color(.035f, .10f, .14f), parent);
            var text = new GameObject("Arena Branding").AddComponent<TextMeshPro>();
            text.transform.SetParent(parent);
            text.transform.position = position + Quaternion.Euler(0, rotation, 0) * new Vector3(0, 0, -.08f);
            text.transform.rotation = Quaternion.Euler(0, rotation, 0);
            text.font = font;
            text.text = value;
            text.fontSize = 7;
            text.color = mint;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(7.5f, 1f);
        }
        static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
        static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color color)
        {
            var rect = Rect(name, parent, anchor, pivot, position, size);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }
        static TMP_Text Text(string name, Transform parent, string value, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, float sizeText = 20, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rect = Rect(name, parent, anchor, pivot, position, size);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = sizeText;
            text.color = Color.white;
            text.alignment = align;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }
        static Button Button(string name, Transform parent, string label, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var rect = Panel(name, parent, anchor, pivot, position, size, new Color(.10f, .29f, .32f, .98f));
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            // Persist callbacks into the saved scene, rather than losing runtime-only listeners.
            if (action != null)
                UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, action);
            Text("Label", rect, label, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, size - Vector2.one * 8, 18);
            return button;
        }
        static void BuildHud(BotMatch match)
        {
            var canvasObject = new GameObject("Match HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            var hud = canvasObject.AddComponent<MatchHud>();
            match.hud = hud;
            var root = canvasObject.transform;
            var eventSystem = new GameObject("Match EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<EventSystem>().sendNavigationEvents = false;
            var scorePanel = Panel("Scoreboard", root, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -14), new Vector2(440, 79), ink);
            hud.scoreboard = Text("Score", scorePanel, "BẠN  0 : 0  MÁY", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(420, 72), 29);
            var notice = Panel("RefereeNotice", root, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -100), new Vector2(760, 40), ink);
            hud.status = Text("RefereeText", notice, "", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(740, 36), 18);
            hud.serveClock = Text("ServeClock", root, "", new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -20), new Vector2(260, 36), 20, TextAlignmentOptions.Left);
            var bottom = Panel("TechniqueBar", root, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 15), new Vector2(600, 96), ink);
            hud.selection = Text("Selection", bottom, "", new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -10), new Vector2(580, 47), 20);
            hud.timing = Text("ContactFeedback", bottom, "", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 8), new Vector2(580, 25), 17);
            Button("Settings", root, "Esc / Cài đặt", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -16), new Vector2(130, 36), hud.OpenSettings);
            Button("Help", root, "F1 / Hướng dẫn", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -59), new Vector2(150, 36), hud.ToggleHelp);
            Button("Touch", root, "Cảm ứng", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -102), new Vector2(130, 36), hud.ToggleTouch);
            Button("Timeout", root, "T / Time-out", new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -62), new Vector2(140, 36), hud.Timeout);
            hud.difficultyLabel = Text("Difficulty", root, "Máy: Vừa", new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -110), new Vector2(390, 78), 18, TextAlignmentOptions.TopLeft);
            var touch = Rect("TouchControls", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            touch.anchorMax = Vector2.one;
            touch.offsetMax = touch.offsetMin = Vector2.zero;
            hud.touchControls = touch.gameObject;
            var joystick = Panel("MoveJoystick", touch, new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 25), new Vector2(156, 156), new Color(.03f, .12f, .17f, .7f));
            joystick.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            var knob = Panel("Knob", joystick, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(52, 52), mint);
            var circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            joystick.GetComponent<UnityEngine.UI.Image>().sprite = circle;
            knob.GetComponent<UnityEngine.UI.Image>().sprite = circle;
            var control = joystick.gameObject.AddComponent<TouchControl>();
            control.match = match;
            control.joystick = true;
            control.knob = knob;
            Text("MoveLabel", touch, "DI CHUYỂN / VÀNH NGOÀI: CHẠY", Vector2.zero, Vector2.zero, new Vector2(22, 190), new Vector2(250, 22), 13, TextAlignmentOptions.Left);
            string[] labels = { "J / Drive", "K / Dink", "L / Volley", "JK / Drop", "JL / Smash", "KL / Reset", "U / Lực", "I / Chéo" };
            int[] masks = { 1, 2, 4, 3, 5, 6, 8, 16 };
            for (int i = 0; i < labels.Length; i++)
            {
                int col = i % 3, row = i / 3;
                var rect = Panel(labels[i], touch, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20 - col * 110, 28 + row * 56), new Vector2(104, 48), new Color(.07f, .16f, .21f, .9f));
                rect.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
                var c = rect.gameObject.AddComponent<TouchControl>();
                c.match = match;
                c.shotMask = masks[i];
                c.modifier = i >= 6;
                Text("Label", rect, labels[i], new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(100, 44), 16);
            }
            touch.gameObject.SetActive(false);
            var help = Panel("ControlsHelp", root, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(730, 370), ink);
            hud.help = help.gameObject;
            Text("HelpText", help, "<size=28><color=#83E6CD>ĐIỀU KHIỂN & LUẬT</color></size>\n\nWASD di chuyển • Shift chạy • Không có nhảy\nJ Đánh bạt sau nảy | K Bóng ngắn sau nảy | L Vô lê trước nảy\nJ+K Bỏ nhỏ | J+L Đập bóng bóng cao | K+L Hãm bóng vô lê\nJ/K/L + U tấn công | J/K/L + I chéo • Tối đa hai nút\n\nGiữ để chọn, thả phím gốc để swing; swing trễ có thể trượt.\nReturn và cú thứ ba phải nảy. Vô lê trong vùng cấm vô lê hoặc đà vào vùng cấm vô lê là lỗi.\nT gọi hội ý trước giao • Esc dừng • Enter tiếp tục\nCảm ứng: cần điều khiển và giữ/thả nút; U/I bật tắt làm bổ trợ.", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 15), new Vector2(690, 310), 19);
            Button("CloseHelp", help, "Đóng / F1", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 15), new Vector2(180, 36), hud.ToggleHelp);
            help.gameObject.SetActive(false);
            var modal = Panel("MatchDialog", root, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 5), new Vector2(760, 370), ink);
            hud.modal = modal.gameObject;
            modal.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            hud.modalTitle = Text("Title", modal, "PICKLEVERSE", new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -22), new Vector2(710, 42), 28);
            hud.modalTitle.color = mint;
            hud.modalBody = Text("Body", modal, "", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 6), new Vector2(710, 235), 19);
            hud.continueButton = Button("Continue", modal, "Enter / Tiếp tục", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(-120, 20), new Vector2(210, 40), hud.Continue);
            hud.restartButton = Button("Restart", modal, "Chơi lại", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(120, 20), new Vector2(210, 40), hud.Restart);
            hud.modeButton = Button("ChooseMode", modal, "Chọn chế độ", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(245, 20), new Vector2(210, 40), hud.ChooseMode);
        }
    }
    static class AnimationData
    {
        [Serializable]
        sealed class Clip { public string name; public Channel[] channels; }
        [Serializable]
        sealed class Channel { public string path, kind, interpolation; public float[] times, values; }
        public static AnimationClip Create(string gender, string name, string output)
        {
            // Data was read directly from GLB buffers by extract_animation_data.py.
            // Avoid glTF import here: even typed subasset loading allocates embedded textures.
            string input = Path.GetFullPath("../UnityWork/BotMatch/ClipData/" + gender + "/" + name + ".json");
            var data = JsonUtility.FromJson<Clip>(File.ReadAllText(input));
            float start = data.channels.Min(c => c.times[0]);
            float span = data.channels.Max(c => c.times[c.times.Length - 1]) - start;
            float duration = AnimationDurationTools.DurationFor(name, span);
            // Read duration from the production table so regeneration cannot restore 2s clips.
            foreach (var channel in data.channels)
                for (int i = 0; i < channel.times.Length; i++)
                    channel.times[i] = (channel.times[i] - start) * duration / span;
            var idleData = JsonUtility.FromJson<Clip>(File.ReadAllText(Path.GetFullPath("../UnityWork/BotMatch/ClipData/" + gender + "/PKV_Idle_Ready.json")));
            var restHip = idleData.channels.Single(c => c.path == "target_character/mixamorig:Hips" && c.kind == "translation").values;
            var clip = new AnimationClip { name = name, legacy = true, frameRate = 30, wrapMode = WrapMode.ClampForever };
            foreach (var channel in data.channels)
            {
                int components = channel.kind == "rotation" ? 4 : 3;
                string property = channel.kind == "rotation" ? "localRotation" : channel.kind == "translation" ? "localPosition" : "localScale";
                for (int component = 0; component < components; component++)
                {
                    var keys = new Keyframe[channel.times.Length];
                    for (int i = 0; i < keys.Length; i++)
                    {
                        float value = channel.values[i * components + component];
                        bool hipPlanar = channel.kind == "translation" && channel.path == "target_character/mixamorig:Hips" && component != 1;
                        if (hipPlanar)
                            value = restHip[component];
                        float incoming = i == 0 ? 0 : (value - channel.values[(i - 1) * components + component]) / Mathf.Max(.000001f, channel.times[i] - channel.times[i - 1]);
                        float outgoing = i == keys.Length - 1 ? 0 : (channel.values[(i + 1) * components + component] - value) / Mathf.Max(.000001f, channel.times[i + 1] - channel.times[i]);
                        if (channel.interpolation == "STEP")
                            incoming = outgoing = float.PositiveInfinity;
                        if (hipPlanar)
                            incoming = outgoing = 0;
                        keys[i] = new Keyframe(channel.times[i], value, incoming, outgoing);
                    }
                    clip.SetCurve(channel.path, typeof(Transform), property + "." + "xyzw"[component], new AnimationCurve(keys));
                }
            }
            clip.EnsureQuaternionContinuity();
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(output);
            if (existing)
            {
                EditorUtility.CopySerialized(clip, existing);
                UnityEngine.Object.DestroyImmediate(clip);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(clip, output);
            return clip;
        }
    }
}
