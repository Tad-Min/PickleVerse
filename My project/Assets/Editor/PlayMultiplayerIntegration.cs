using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using PickleVerse.Multiplayer;
using PickleVerse.Multiplayer.UI;
using PickleVerse.Multiplayer.Services;
using PickleVerse.LocalMatch;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;
using PlayMode = PickleVerse.Multiplayer.PlayMode;

namespace PickleVerse.EditorTools
{
    public static class PlayMultiplayerIntegration
    {
        const string Folder = "Assets/Prefabs/Multiplayer/BotMatch";
        [MenuItem("PickleVerse/Multiplayer/Integrate PlayMultiplayer and BotMatch")]
        public static void Integrate()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory("Temp/MultiplayerUpgrade");
            var room = Open(MultiplayerSceneConfiguration.RoomPath);
            if (!File.Exists("Temp/MultiplayerUpgrade/PlayMultiplayer-before.unity")) EditorSceneManager.SaveScene(room, "Temp/MultiplayerUpgrade/PlayMultiplayer-before.unity", true);
            WireRoom(room);
            var botScene = Open("Assets/Scenes/BotMatch.unity");
            var bot = botScene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BotMatch>(true)).Single();
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var settings = AssetDatabase.LoadAssetAtPath<MultiplayerSettings>("Assets/Resources/MultiplayerSettings.asset");
            settings.femaleActorPrefab = SaveActor(bot.human, "FemaleActor");
            settings.maleActorPrefab = SaveActor(bot.bot, "MaleActor");
            settings.ballVisualPrefab = SaveObject(bot.ball.gameObject, "BallVisual");
            settings.contactAssistRadius = bot.contactAssistRadius;
            var camera = botScene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MatchCamera>(true)).Single();
            settings.cameraHeight = camera.height; settings.cameraDistance = camera.followDistance;
            settings.cameraPitch = camera.pitch; settings.cameraFieldOfView = camera.fieldOfView;
            EditorUtility.SetDirty(settings);
            IntegrateCourt(botScene, bot);
            EditorSceneManager.CloseScene(botScene, true);
            ConfigureBuildScenes(); WireMenu();
            EditorSceneManager.SaveScene(room); AssetDatabase.SaveAssets();
            SceneManager.SetActiveScene(room);
            Debug.Log("PlayMultiplayer wired; BotMatch actors/court shared; multiplayer build scenes synchronized.");
        }
        static Scene Open(string path)
        { var scene = SceneManager.GetSceneByPath(path); return scene.IsValid() && scene.isLoaded ? scene : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); }
        static T Find<T>(Transform root, string path) where T : Component
        { var t = root.Find(path); if (!t || !t.GetComponent<T>()) throw new InvalidOperationException("Missing " + path + " / " + typeof(T).Name); return t.GetComponent<T>(); }
        static void WireRoom(Scene scene)
        {
            var canvas = scene.GetRootGameObjects().Single(g => g.name == "Canvas").transform;
            var p = canvas.GetComponent<PlayMultiplayerPresenter>() ?? canvas.gameObject.AddComponent<PlayMultiplayerPresenter>();
            p.playerName = Find<TMP_InputField>(canvas, "TxtPlayerName"); p.joinCode = Find<TMP_InputField>(canvas, "NavPanel/TxtJoinID");
            p.roomType = Find<TMP_Text>(canvas, "TxtRoomType"); p.matchTime = Find<TMP_Text>(canvas, "TxtMatchTime");
            p.roomCode = Find<TMP_Text>(canvas, "NavPanel/TxtRoomId");
            p.join = Find<UnityEngine.UI.Button>(canvas, "NavPanel/BtnJoin"); p.singles = Find<UnityEngine.UI.Button>(canvas, "NavPanel/Btn1Vs1");
            p.doubles = Find<UnityEngine.UI.Button>(canvas, "NavPanel/Btn2Vs2"); p.custom = Find<UnityEngine.UI.Button>(canvas, "NavPanel/BtnCustom");
            p.copy = Find<UnityEngine.UI.Button>(canvas, "NavPanel/BtnCopy"); p.play = Find<UnityEngine.UI.Button>(canvas, "BtnPlay");
            p.singlesPanel = canvas.Find("TeamPanel/1Vs1Panel").gameObject; p.doublesPanel = canvas.Find("TeamPanel/2Vs2Panel").gameObject;
            p.customPanel = canvas.Find("TeamPanel/CustomPanel").gameObject; p.customMode = PlayMode.Doubles;
            var teams = p.customPanel.transform.Cast<Transform>().Where(t => t.name == "TeamAPanel" || t.name == "TeamBPanel").ToArray();
            if (teams.Length != 2) throw new InvalidOperationException("Custom needs two team panels.");
            teams[0].name = "TeamAPanel"; teams[1].name = "TeamBPanel"; foreach (var team in teams) team.gameObject.SetActive(true);
            var nameTemplate = p.singlesPanel.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "TxtName");
            p.singleSlot = Slot(p.singlesPanel.transform.Find("Player1Panel"), nameTemplate, null);
            p.duoSlots = new[] { Slot(p.doublesPanel.transform.Find("Player1Panel"), nameTemplate, null), Slot(p.doublesPanel.transform.Find("Player2Panel"), nameTemplate, null) };
            var kickTemplate = p.customPanel.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name == "BtnKick");
            var swap = p.customPanel.GetComponentsInChildren<UnityEngine.UI.Button>(true).FirstOrDefault(b => b.name == "BtnSwap");
            if (!swap) throw new InvalidOperationException("Custom needs BtnSwap.");
            swap.transform.SetParent(p.customPanel.transform, true); p.swap = swap;
            var swapRect=(RectTransform)swap.transform;
            swapRect.anchorMin=swapRect.anchorMax=new Vector2(.5f,0); swapRect.pivot=new Vector2(.5f,.5f);
            swapRect.anchoredPosition=new Vector2(0,-34); swapRect.sizeDelta=new Vector2(160,40);
            swap.GetComponentInChildren<TMP_Text>().fontSize=24;
            var customNameTemplate=Find<TMP_Text>(teams[0],"Player1Panel/TxtName");
            p.customSlots = teams.SelectMany(team => team.Cast<Transform>().Where(t => t.name.StartsWith("Player")).OrderBy(t => t.name).Select(t => Slot(t, customNameTemplate, kickTemplate))).ToArray();
            if (p.customSlots.Length != 4) throw new InvalidOperationException("Custom needs four player slots.");
            foreach(var slot in p.customSlots)
            {
                if(slot.name==customNameTemplate) continue;
                var rect=slot.name.rectTransform; var templateRect=customNameTemplate.rectTransform;
                rect.anchorMin=templateRect.anchorMin; rect.anchorMax=templateRect.anchorMax; rect.pivot=templateRect.pivot;
                rect.anchoredPosition=templateRect.anchoredPosition; rect.sizeDelta=templateRect.sizeDelta;
                rect.localScale=templateRect.localScale; rect.localRotation=templateRect.localRotation;
                slot.name.fontSize=customNameTemplate.fontSize; slot.name.alignment=customNameTemplate.alignment;
            }
            foreach (var button in new[] { p.join, p.singles, p.doubles, p.custom, p.copy, p.play, p.swap }.Concat(p.customSlots.Select(s => s.kick))) ClearCalls(button);
            p.singlesPanel.SetActive(true); p.doublesPanel.SetActive(false); p.customPanel.SetActive(false);
            p.roomCode.gameObject.SetActive(false); p.copy.gameObject.SetActive(false); p.matchTime.text = "00:00";
            EditorUtility.SetDirty(p); EditorSceneManager.MarkSceneDirty(scene);
        }
        static RoomPlayerSlot Slot(Transform root, TMP_Text template, UnityEngine.UI.Button kickTemplate)
        {
            var name = root.Find("TxtName")?.GetComponent<TMP_Text>();
            if (!name) { name = Object.Instantiate(template, root); name.name = "TxtName"; }
            name.text = ""; name.richText = false; name.raycastTarget = false;
            var kick = root.Find("BtnKick")?.GetComponent<UnityEngine.UI.Button>();
            if (!kick && kickTemplate) { kick = Object.Instantiate(kickTemplate, root); kick.name = "BtnKick"; }
            if (kick) kick.gameObject.SetActive(false);
            return new RoomPlayerSlot { name = name, kick = kick };
        }
        static void ClearCalls(UnityEngine.UI.Button button)
        { if (!button) return; while (button.onClick.GetPersistentEventCount() > 0) UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, 0); }
        static GameObject SaveActor(MatchActor actor, string name)
        {
            var clone = Object.Instantiate(actor.gameObject); clone.name = name;
            clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var result = PrefabUtility.SaveAsPrefabAsset(clone, Folder + "/" + name + ".prefab"); Object.DestroyImmediate(clone); return result;
        }
        static GameObject SaveObject(GameObject source, string name)
        {
            var clone = Object.Instantiate(source); clone.name = name; clone.transform.position = Vector3.zero;
            var result = PrefabUtility.SaveAsPrefabAsset(clone, Folder + "/" + name + ".prefab"); Object.DestroyImmediate(clone); return result;
        }
        static void IntegrateCourt(Scene source, BotMatch bot)
        {
            var court = Open(MultiplayerSceneConfiguration.CourtPath);
            if (!File.Exists("Temp/MultiplayerUpgrade/MultiplayerCourt-before.unity")) EditorSceneManager.SaveScene(court, "Temp/MultiplayerUpgrade/MultiplayerCourt-before.unity", true);
            SceneManager.SetActiveScene(court);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.63f,.75f,.85f); RenderSettings.ambientEquatorColor = new Color(.48f,.58f,.58f); RenderSettings.ambientGroundColor = new Color(.23f,.29f,.26f);
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(.64f,.78f,.82f); RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 30; RenderSettings.fogEndDistance = 100;
            foreach (var root in court.GetRootGameObjects())
            {
                if (root.GetComponent<Camera>() || root.GetComponent<EventSystem>() || root.GetComponent<CourtBootstrap>()) continue;
                if (root.name == "BotMatchArena" || root.name == "BotMatchHUD") Object.DestroyImmediate(root);
                else root.SetActive(false);
            }
            var arena = new GameObject("BotMatchArena"); SceneManager.MoveGameObjectToScene(arena, court);
            foreach (var root in source.GetRootGameObjects().Where(g => g.name == "Arena" || g.GetComponent<Light>()))
                Object.Instantiate(root, arena.transform);
            var sun = arena.GetComponentInChildren<Light>(); if (sun) RenderSettings.sun = sun;
            var camera = court.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).First();
            camera.fieldOfView = 60; camera.backgroundColor = new Color(.58f,.75f,.82f); camera.clearFlags = CameraClearFlags.SolidColor;
            var canvas = new GameObject("BotMatchHUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvas, court);
            var c = canvas.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = .75f;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280,720); scaler.matchWidthOrHeight = .5f;
            foreach (var child in bot.hud.transform.Cast<Transform>().Where(t => new[] { "Scoreboard", "RefereeNotice", "ServeClock", "TechniqueBar" }.Contains(t.name)))
            { var clone = Object.Instantiate(child.gameObject, canvas.transform); clone.name = child.name; }
            var ui = canvas.AddComponent<MultiplayerCourtUI>();
            ui.score = Find<TMP_Text>(canvas.transform, "Scoreboard/Score"); ui.status = Find<TMP_Text>(canvas.transform, "RefereeNotice/RefereeText"); ui.server = Find<TMP_Text>(canvas.transform, "ServeClock");
            Find<TMP_Text>(canvas.transform, "TechniqueBar/Selection").text = "J Drive   K Dink   L Volley\n<size=16>J+K Drop   J+L Smash   K+L Reset   •   +U Lực / +I Chéo</size>";
            Find<TMP_Text>(canvas.transform, "TechniqueBar/ContactFeedback").text = "Giữ tổ hợp rồi thả phím đánh • Backspace hủy • WASD / Shift";
            var buttonTemplate = bot.hud.transform.Find("Pause").GetComponent<UnityEngine.UI.Button>();
            ui.leave = Object.Instantiate(buttonTemplate, canvas.transform); ui.leave.name = "Leave"; ClearCalls(ui.leave);
            ui.returnToRoom = Object.Instantiate(buttonTemplate, canvas.transform); ui.returnToRoom.name = "ReturnToRoom"; ClearCalls(ui.returnToRoom);
            ui.returnToRoom.GetComponentInChildren<TMP_Text>().text = "Về phòng"; ui.returnToRoom.gameObject.SetActive(false);
            foreach(var collider in arena.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
            EditorSceneManager.SaveScene(court); EditorSceneManager.CloseScene(court, true);
        }
        public static void ConfigureBuildScenes()
        {
            var required = new[] { MultiplayerSceneConfiguration.RoomPath, MultiplayerSceneConfiguration.CourtPath, "Assets/Scenes/UI/Menu.unity", "Assets/Scenes/BotMatch.unity" };
            var paths = required.Concat(EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)).Select(s => s.path)).Distinct().Where(File.Exists).ToArray();
            var scenes = paths.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>("Assets/Settings/BuildProfiles/MultiplayerWindows.asset");
            if (!profile) throw new InvalidOperationException("Missing MultiplayerWindows profile.");
            profile.overrideGlobalScenes = true; profile.scenes = scenes; EditorUtility.SetDirty(profile);
            BuildProfile.SetActiveBuildProfile(profile); EditorBuildSettings.scenes = scenes;
            PlayerSettings.runInBackground = true;
        }
        static void WireMenu()
        {
            var scene = Open("Assets/Scenes/UI/Menu.unity");
            var button = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UnityEngine.UI.Button>(true)).FirstOrDefault(b => b.name == "BtnPlay");
            if (button)
            {
                var handler = button.GetComponent<MenuPlayMultiplayer>() ?? button.gameObject.AddComponent<MenuPlayMultiplayer>();
                ClearCalls(button); UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, handler.Play);
                EditorSceneManager.SaveScene(scene);
            }
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
