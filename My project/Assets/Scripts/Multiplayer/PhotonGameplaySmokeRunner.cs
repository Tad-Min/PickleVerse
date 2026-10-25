using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PickleVerse.Progression;
using PickleVerse.Services;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PickleVerse.Multiplayer
{
    // Explicit development-only integration check; it uses normal account, lobby and touch-input paths.
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
            StartCheck(args[i + 1], args[i + 2], args[i + 3], args[i + 4] == "host", Array.IndexOf(args, "-pkvPublicSearch") >= 0, Array.IndexOf(args, "-pkvDoubles") >= 0, Array.IndexOf(args, "-pkvPartyPair") >= 0, Array.IndexOf(args, "-pkvPartyFollower") >= 0, Array.IndexOf(args, "-pkvReconnectCheck") >= 0);
        }
        public static void StartCheck(string fixturePath, string reportPath, string roomPath, bool host, bool publicSearch = false, bool doubles = false, bool pair = false, bool follower = false, bool reconnectCheck = false)
        {
            if (!Application.isEditor && !Debug.isDebugBuild)
                throw new InvalidOperationException("Development build required.");
            var go = new GameObject("Photon Gameplay Verification");
            DontDestroyOnLoad(go);
            _ = go.AddComponent<PhotonGameplaySmokeRunner>().Run(fixturePath, reportPath, roomPath, host, publicSearch, doubles, pair, follower, reconnectCheck);
        }
        async Task Run(string fixturePath, string reportPath, string roomPath, bool host, bool publicSearch, bool doubles, bool pair, bool follower, bool reconnectCheck)
        {
            Report = new Dictionary<string, object> { { "host", host }, { "publicSearch", publicSearch }, { "doubles", doubles }, { "passed", false }, { "stage", "login" } };
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
                var account = AccountRuntime.Ensure().Account;
                await account.LoginAsync(fixture["email"], fixture["password"]);
                await ProgressionRuntime.Instance.EnsureReadyAsync();
                var app = MultiplayerApp.Ensure();
                await app.PlayAsync(account.Profile.UserId.ToString("N"));
                await SceneManager.LoadSceneAsync("PlayMultiplayer");
                await Task.Delay(500);
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
                    await app.CreateCustomAsync(PlayMode.Singles);
                    File.WriteAllText(roomPath, app.RoomCode);
                    await PhotonSession.WaitAsync(() => app.Members().Count == 2, "Guest did not join the party.", 150);
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
                Report["courtStarted"] = match.Started;
                Report["replicatedActors"] = NetworkPlayer.All.Count == (doubles ? 4 : 2);
                Report["onlinePending"] = ProgressionRuntime.Instance.Player.pendingMatch?.id == match.Roster.matchId;
                Stage("movement");
                var local = NetworkPlayer.All.Single(p => p.IsLocalHuman);
                var start = local.Frame.position;
                var ui = MultiplayerCourtUI.Instance;
                await PhotonSession.WaitAsync(() => ui && !ui.InputBlocked, "Court UI still blocks input.", 30);
                ui.TouchMovement = Vector2.right;
                await Task.Delay(1400);
                ui.TouchMovement = Vector2.zero;
                await Task.Delay(1000);
                Report["movementConfirmed"] = Vector3.Distance(start, local.Frame.position) > .05f;
                Report["inputAcknowledged"] = local.Frame.sequence > 0;
                Stage("replay");
                await Task.Delay(5000);
                var replay = match.GetComponent<PickleVerse.Replays.ReplayRecorder>();
                Report["replayStarted"] = replay != null;
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
                        await PhotonSession.WaitAsync(() => ui && !ui.InputBlocked && !match.Paused, "Reconnected input remains blocked.", 20);
                        local = NetworkPlayer.All.Single(p => p.IsLocalHuman);
                        start = local.Frame.position;
                        ui.TouchMovement = Vector2.left;
                        await Task.Delay(1200);
                        ui.TouchMovement = Vector2.zero;
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
                    Report["pendingCleared"] = ProgressionRuntime.Instance.Player.pendingMatch == null;
                }
                else
                {
                    Stage("result");
                    await PhotonSession.WaitAsync(() => match == null || match.Finished, "Host leave did not replicate.", 30);
                    await ProgressionRuntime.Instance.ReloadAsync();
                    Report["pendingCleared"] = ProgressionRuntime.Instance.Player.pendingMatch == null;
                    await app.ReturnToRoomAsync("Kiểm thử Photon hoàn tất.");
                }
                await Task.Delay(1000);
                Report["frames"] = Time.frameCount;
                Report["passed"] = new[] { "readyShown", "readyAccepted", "courtStarted", "replicatedActors", "onlinePending", "movementConfirmed", "inputAcknowledged", "replayStarted", "pendingCleared" }.All(k => Report.TryGetValue(k, out var v) && v is true) && (!reconnectCheck || host || Report.TryGetValue("reconnected", out var connected) && connected is true);
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
                Report["completed"] = true;
                Write();
                if (!Application.isEditor)
                    Application.Quit(Report["passed"] is true ? 0 : 1);
            }
        }
    }
}
