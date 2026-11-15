using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PickleVerse.Progression;
using PickleVerse.Services;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace PickleVerse.Multiplayer
{
    // Explicit development-only integration check; it uses the normal account, lobby and input paths.
    public sealed class PhotonGameplaySmokeRunner : MonoBehaviour
    {
        public static Dictionary<string, object> Report { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-pkvPhotonSmoke");
            if (i < 0 || i + 4 >= args.Length || !Debug.isDebugBuild)
                return;
            int hold = Array.IndexOf(args, "-pkvHoldCourtSeconds");
            int holdSeconds = hold >= 0 && hold + 1 < args.Length && int.TryParse(args[hold + 1], out int seconds) ? Math.Max(0, seconds) : 0;
            StartCheck(args[i + 1], args[i + 2], args[i + 3], args[i + 4] == "host", Array.IndexOf(args, "-pkvPublicSearch") >= 0, Array.IndexOf(args, "-pkvDoubles") >= 0, Array.IndexOf(args, "-pkvPartyPair") >= 0, Array.IndexOf(args, "-pkvPartyFollower") >= 0, Array.IndexOf(args, "-pkvReconnectCheck") >= 0, Array.IndexOf(args, "-pkvServerLossCheck") >= 0, holdSeconds);
        }
        public static void StartCheck(string fixturePath, string reportPath, string roomPath, bool host, bool publicSearch = false, bool doubles = false, bool pair = false, bool follower = false, bool reconnectCheck = false, bool serverLoss = false, int holdSeconds = 0)
        {
            if (!Application.isEditor && !Debug.isDebugBuild)
                throw new InvalidOperationException("Development build required.");
            var go = new GameObject("Photon Gameplay Verification");
            DontDestroyOnLoad(go);
            _ = go.AddComponent<PhotonGameplaySmokeRunner>().Run(fixturePath, reportPath, roomPath, host, publicSearch, doubles, pair, follower, reconnectCheck, serverLoss, holdSeconds);
        }
        async Task Run(string fixturePath, string reportPath, string roomPath, bool host, bool publicSearch, bool doubles, bool pair, bool follower, bool reconnectCheck, bool serverLoss, int holdSeconds)
        {
            Report = new Dictionary<string, object> { { "host", host }, { "publicSearch", publicSearch }, { "doubles", doubles }, { "passed", false }, { "stage", "login" } };
            int runtimeExceptions = 0;
            Application.LogCallback countException = (message, stack, type) => { if (type == LogType.Exception) runtimeExceptions++; };
            Application.logMessageReceived += countException;
            Application.runInBackground = true;
            void Stage(string name)
            {
                Report["stage"] = name;
                Write();
            }
            void Write()
            {
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                File.WriteAllText(reportPath, JsonConvert.SerializeObject(Report, Formatting.Indented));
            }
            try
            {
                var fixture = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(fixturePath));
                var runtime = AccountRuntime.Ensure();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (fixture.TryGetValue("baseUrl", out var testUrl))
                {
                    if (!Uri.TryCreate(testUrl, UriKind.Absolute, out var target) || !target.IsLoopback)
                        throw new InvalidOperationException("The smoke test override must use a loopback API.");
                    runtime.Settings.baseUrl = testUrl;
                }
#endif
                var account = runtime.Account;
                await account.LoginAsync(fixture["email"], fixture["password"]);
                await ProgressionRuntime.Instance.EnsureReadyAsync();
                var app = MultiplayerApp.Ensure();
                await app.PlayAsync(account.Profile.UserId.ToString("N"));
                await SceneManager.LoadSceneAsync("PlayMultiplayer");
                await Task.Delay(500);
                double searchStarted = Time.realtimeSinceStartupAsDouble;
                Stage("party");
                if (publicSearch)
                {
                    Stage("searching");
                    if (follower)
                    {
                        await PhotonSession.WaitAsync(() => File.Exists(roomPath), "Party captain did not publish a room.", 150);
                        await app.JoinRoomAsync(File.ReadAllText(roomPath).Trim());
                    }
                    else
                    {
                        if (doubles)
                            await app.SelectModeAsync(PlayMode.Doubles);
                        if (pair)
                        {
                            File.WriteAllText(roomPath, app.RoomCode);
                            await PhotonSession.WaitAsync(() => app.Members().Count == 2, "Party member did not join.", 150);
                        }
                        await app.FindAsync();
                    }
                }
                else if (host)
                {
                    await app.CreateCustomAsync(doubles ? PlayMode.Doubles : PlayMode.Singles);
                    File.WriteAllText(roomPath, app.RoomCode);
                    await PhotonSession.WaitAsync(() => app.Members().Count == (doubles ? 4 : 2), "Guests did not join the party.", 150);
                    await app.StartCustomAsync();
                }
                else
                {
                    await PhotonSession.WaitAsync(() => File.Exists(roomPath), "Host room was not published.", 150);
                    await app.JoinRoomAsync(File.ReadAllText(roomPath).Trim());
                }
                Stage("ready");
                await PhotonSession.WaitAsync(() => app.CanAcceptMatch, "Ready check was not shown: " + app.Notice, 90);
                Report["readyShown"] = app.ReadyCheckActive;
                await app.AcceptMatchAsync();
                Report["readyAccepted"] = app.ReadyCheckAccepted;
                Stage("court");
                await PhotonSession.WaitAsync(() => NetworkMatch.Instance && NetworkMatch.Instance.Started, "Court did not start.", doubles ? 180 : 100);
                var match = NetworkMatch.Instance;
                Report["matchId"] = match.Roster.matchId;
                Report["courtStartedUtc"] = DateTime.UtcNow;
                Report["secondsToCourt"] = Math.Round(Time.realtimeSinceStartupAsDouble - searchStarted, 2);
                Report["courtStarted"] = match.Started;
                Report["dedicated"] = app.Match.IsDedicated;
                Report["replicatedActors"] = NetworkPlayer.All.Count == (doubles ? 4 : 2);
                Report["onlinePending"] = ProgressionRuntime.Instance.Player.pendingMatch?.id == match.Roster.matchId;
                if (serverLoss)
                {
                    Stage("server loss");
                    await PhotonSession.WaitAsync(() => app.State == FlowState.WaitingRoom && app.Match == null &&
                        SceneManager.GetActiveScene().name == app.RoomSceneName,
                        "Client did not return to the lobby after server loss.", 75);
                    Report["returnedToLobby"] = true;
                    Report["returnedToLobbyUtc"] = DateTime.UtcNow;
                    await PhotonSession.WaitAsync(() => ProgressionRuntime.Instance.Player.pendingMatch?.id != Report["matchId"].ToString(), "Interrupted result was not refreshed.", 75);
                    Report["pendingCleared"] = true;
                    Report["frames"] = Time.frameCount;
                    Report["passed"] = runtimeExceptions == 0;
                    Stage("completed");
                    return;
                }
                Stage("movement");
                var local = NetworkPlayer.All.Single(p => p.IsLocalHuman);
                var start = local.Frame.position;
                var ui = MultiplayerCourtUI.Instance;
                try
                {
                    await PhotonSession.WaitAsync(() => local && local.TouchShotsEnabled, "The court did not accept input.", 30);
                }
                catch (TimeoutException)
                {
                    Report["inputState"] = new
                    {
                        hud = (bool)ui, overlay = PickleVerse.Settings.SceneLoading.IsBlocking,
                        destination = PickleVerse.Settings.SceneLoading.Destination, overlayState = PickleVerse.Settings.SceneLoading.View ? PickleVerse.Settings.SceneLoading.View.status.text : null,
                        cinematic = ui && ui.cinematic && ui.cinematic.OwnsPose, paused = match.Paused,
                        settings = PickleVerse.Settings.SettingsOverlay.IsOpen, actor = (bool)local.Actor
                    };
                    throw;
                }
                await Move(ui, Vector2.right, PickleVerse.Settings.GameAction.Right);
                await Task.Delay(1000);
                Report["movementConfirmed"] = Vector3.Distance(start, local.Frame.position) > .05f;
                Report["inputAcknowledged"] = local.Frame.sequence > 0;
                Stage("replay");
                await Task.Delay(5000);
                var replay = match.GetComponent<PickleVerse.Replays.ReplayRecorder>();
                Report["replayStarted"] = replay != null;
                Report["sharedReplayProducer"] = (replay != null) == match.IsServer;
                if (holdSeconds > 0)
                {
                    double began = Time.realtimeSinceStartupAsDouble;
                    int frame = Time.frameCount;
                    while (Time.realtimeSinceStartupAsDouble < began + holdSeconds)
                    {
                        if (!match || match.Finished || match.Paused || !match.Runner || !match.Runner.IsRunning)
                            throw new InvalidOperationException("Court stopped during the load test.");
                        double elapsed = Time.realtimeSinceStartupAsDouble - began;
                        Report["holdSeconds"] = Math.Round(elapsed, 2);
                        Report["meanFrameMs"] = Math.Round(elapsed * 1000 / Math.Max(1, Time.frameCount - frame), 2);
                        Stage("soak");
                        await Task.Delay(Math.Min(10000, Math.Max(1, (int)((holdSeconds - elapsed) * 1000))));
                    }
                    Report["holdSeconds"] = holdSeconds;
                    Report["holdCompletedUtc"] = DateTime.UtcNow;
                    await Task.Delay(3000);
                }
                if (reconnectCheck)
                {
                    if (host)
                    {
                        Stage("waiting reconnect");
                        await Task.Delay(23000);
                    }
                    else
                    {
                        Stage("reconnect");
                        var previous = app.Match;
                        await previous.Runner.Shutdown(false, Fusion.ShutdownReason.Error);
                        await PhotonSession.WaitAsync(() => app.Match != null && app.Match != previous && NetworkMatch.Instance && NetworkMatch.Instance.Started && NetworkPlayer.All.Any(p => p.IsLocalHuman), "Reconnect did not restore the court.", 40);
                        match = NetworkMatch.Instance;
                        ui = MultiplayerCourtUI.Instance;
                        local = NetworkPlayer.All.Single(p => p.IsLocalHuman);
                        await PhotonSession.WaitAsync(() => local && local.TouchShotsEnabled, "Reconnected input remains blocked.", 20);
                        start = local.Frame.position;
                        await Move(ui, Vector2.left, PickleVerse.Settings.GameAction.Left);
                        await Task.Delay(1000);
                        Report["reconnected"] = Vector3.Distance(start, local.Frame.position) > .05f && local.Frame.sequence > 0;
                        Stage("replay resumed");
                        await Task.Delay(5000);
                    }
                }
                if (host)
                {
                    Stage("leave");
                    await app.LeaveGameAsync();
                    for (int i = 0; i < 20 && ProgressionRuntime.Instance.Player.pendingMatch?.id == Report["matchId"].ToString(); i++)
                    { await Task.Delay(500); await ProgressionRuntime.Instance.ReloadAsync(); }
                    Report["pendingCleared"] = ProgressionRuntime.Instance.Player.pendingMatch == null;
                }
                else
                {
                    Stage("result");
                    await PhotonSession.WaitAsync(() => match == null || match.Finished, "Host leave did not replicate.", 30);
                    for (int i = 0; i < 30; i++)
                    {
                        await ProgressionRuntime.Instance.ReloadAsync();
                        if (ProgressionRuntime.Instance.Player.pendingMatch?.id != Report["matchId"].ToString()) break;
                        await Task.Delay(1000);
                    }
                    Report["pendingCleared"] = ProgressionRuntime.Instance.Player.pendingMatch == null;
                    await app.ReturnToRoomAsync("Kiểm thử Photon hoàn tất.");
                }
                await Task.Delay(1000);
                Report["frames"] = Time.frameCount;
                Report["passed"] = runtimeExceptions == 0 && new[] { "readyShown", "readyAccepted", "courtStarted", "replicatedActors", "onlinePending", "movementConfirmed", "inputAcknowledged", "sharedReplayProducer", "pendingCleared" }.All(k => Report.TryGetValue(k, out var v) && v is true) && (!reconnectCheck || host || Report.TryGetValue("reconnected", out var connected) && connected is true);
                Stage("completed");
            }
            catch (Exception error)
            {
                Report["error"] = error.Message;
                Report["errorType"] = error.GetType().Name;
                Report["notice"] = MultiplayerApp.Instance ? MultiplayerApp.Instance.Notice : "no app";
            }
            finally
            {
                Application.logMessageReceived -= countException;
                Report["runtimeExceptions"] = runtimeExceptions;
                Report["completed"] = true;
                Write();
                if (!Application.isEditor)
                    Application.Quit(Report["passed"] is true ? 0 : 1);
            }
        }

        static async Task Move(MultiplayerCourtUI ui, Vector2 axes, PickleVerse.Settings.GameAction action)
        {
            if (ui)
            {
                ui.TouchMovement = axes;
                await Task.Delay(1400);
                if (ui) ui.TouchMovement = Vector2.zero;
                return;
            }
            var background = InputSystem.settings.backgroundBehavior;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(PickleVerse.Settings.GameSettings.Current.KeyFor(action)));
                await Task.Delay(1400);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = background;
            }
        }
    }
}
