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
    // Explicit development verification. It waits the real six minutes, then drives
    // controlled rally faults through the normal network scoring/end-game pipeline.
    public sealed class RankedFallbackSmokeRunner : MonoBehaviour
    {
        public static Dictionary<string, object> Report { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-pkvRankedFallbackSmoke");
            if (!Debug.isDebugBuild || i < 0 || i + 2 >= args.Length)
                return;
            StartCheck(args[i + 1], args[i + 2], Array.IndexOf(args, "-pkvSmokeLose") < 0);
        }
        public static void StartCheck(string fixturePath, string reportPath, bool win = true)
        {
            if (!Application.isEditor && !Debug.isDebugBuild)
                throw new InvalidOperationException("Development build required.");
            var go = new GameObject("Ranked Fallback Verification");
            DontDestroyOnLoad(go);
            _ = go.AddComponent<RankedFallbackSmokeRunner>().Run(fixturePath, reportPath, win);
        }
        async Task Run(string fixturePath, string reportPath, bool win)
        {
            Report = new Dictionary<string, object> { { "passed", false }, { "controlledScoring", true }, { "platform", Application.isEditor ? "Editor" : "Windows" } };
            void Write()
            {
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                File.WriteAllText(reportPath, JsonConvert.SerializeObject(Report, Formatting.Indented));
            }
            void Stage(string name)
            {
                Report["stage"] = name;
                Write();
            }
            try
            {
                Application.runInBackground = true;
                Stage("login");
                var fixture = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(fixturePath));
                var account = AccountRuntime.Ensure().Account;
                await account.LoginAsync(fixture["email"], fixture["password"]);
                await ProgressionRuntime.Instance.EnsureReadyAsync();
                var activity = AppActivityRuntime.Ensure();
                await activity.FlushAsync();
                var app = MultiplayerApp.Ensure();
                await app.PlayAsync(account.Profile.UserId.ToString("N"));
                await SceneManager.LoadSceneAsync("PlayMultiplayer");
                double began = Time.realtimeSinceStartupAsDouble;
                await app.FindAsync();
                Stage("searching six real minutes");
                await PhotonSession.WaitAsync(() => app.CanAcceptMatch, "No ranked ready check: " + app.Notice, 430);
                Report["waitSeconds"] = Time.realtimeSinceStartupAsDouble - began;
                if (!app.Roster.RankedBot || app.ReadyCount != 1 || Time.realtimeSinceStartupAsDouble - began < 360)
                    throw new InvalidOperationException("Fallback was early or not a virtual Photon seat.");
                Report["photonRoom"] = app.Match.Id;
                Report["opponentId"] = app.Roster.members.Single(m => m.bot).id;
                Report["readyShown"] = app.ReadyCheckActive;
                Stage("accepting");
                await app.AcceptMatchAsync();
                await PhotonSession.WaitAsync(() => NetworkMatch.Instance && NetworkMatch.Instance.Started, "Photon court did not start: " + app.Notice, 120);
                var match = NetworkMatch.Instance;
                var ui = MultiplayerCourtUI.Instance;
                var sceneManager = match.Runner.GetComponent<Fusion.NetworkSceneManagerDefault>();
                Report["matchId"] = match.Roster.matchId;
                Report["scene"] = SceneManager.GetActiveScene().name;
                Report["sceneRef"] = sceneManager.GetSceneRef(ui.gameObject).ToString();
                if (sceneManager.GetSceneRef(ui.gameObject) != sceneManager.GetSceneRef("MultiplayerCourt") || NetworkPlayer.All.Count != 2 || NetworkPlayer.All.Count(p => p.Automated) != 1 || NetworkPlayer.All.Count(p => p.IsLocalHuman) != 1)
                    throw new InvalidOperationException("Fallback did not retain the multiplayer court and actors.");
                if (ProgressionRuntime.Instance.Player.pendingMatch?.kind != MatchKind.RankedSingles)
                    throw new InvalidOperationException("Fallback is not classified as Rank Online.");
                await PhotonSession.WaitAsync(() => ui && !ui.InputBlocked, "Court input remains blocked.", 30);
                match.AutomateLocalPlayerForSmoke();
                Stage("bot gameplay");
                await Task.Delay(25000);
                Report["botShots"] = match.BotShots;
                Report["botReturns"] = match.BotReturns;
                if (match.BotShots == 0)
                    throw new InvalidOperationException("Virtual opponent did not play any shot.");
                Stage("controlled match scoring");
                int team = match.Roster.Find(app.PlayerId).team, loser = win ? 1 - team : team;
                double finishBy = Time.realtimeSinceStartupAsDouble + 230;
                while (!match.Finished && Time.realtimeSinceStartupAsDouble < finishBy)
                {
                    if (!match.BetweenGames && !match.BetweenPoints && !match.Paused && !match.TimedOut)
                        match.Rules.Fault(loser, "Kiểm thử lỗi pha bóng để kiểm tra tổng kết.");
                    await Task.Delay(100);
                }
                if (!match.Finished)
                    throw new InvalidOperationException("Controlled match did not finish.");
                Stage("scoreboard");
                await PhotonSession.WaitAsync(() => ui.cinematic && ui.cinematic.OutcomeReady, "End scoreboard/outcome did not finish.", 40);
                Report["scoreboardBeforeSummary"] = ui.broadcast != null && !ui.resultScreen.IsVisible;
                ui.ReturnToRoom();
                Stage("summary");
                await PhotonSession.WaitAsync(() => ui.resultScreen.IsVisible && ui.resultScreen.RenderedMatchId == match.Roster.matchId, "Saved Elo summary was not shown: " + app.Notice, 40);
                var saved = ProgressionRuntime.Instance.Player.history.Single(r => r.id == match.Roster.matchId);
                Report["eloDelta"] = saved.eloDelta;
                Report["summaryText"] = ui.resultScreen.delta.text;
                Report["summaryMatchesDatabase"] = ui.resultScreen.delta.text.Contains(saved.eloDelta.ToString()) && saved.kind == MatchKind.RankedSingles && saved.opponents.Single().id == (string)Report["opponentId"];
                await activity.FlushAsync();
                await ProgressionRuntime.Instance.ReloadAsync();
                Report["activitySeconds"] = ProgressionRuntime.Instance.Player.activeSeconds;
                Report["appElapsedSeconds"] = Time.realtimeSinceStartupAsDouble;
                if (activity.Error != null || ProgressionRuntime.Instance.Player.activeSeconds < 360)
                    throw new InvalidOperationException("App lifetime was not persisted.");
                Report["passed"] = Report["summaryMatchesDatabase"] is true && Report["scoreboardBeforeSummary"] is true && (win ? saved.eloDelta > 0 : saved.eloDelta <= 0);
                ScreenCapture.CaptureScreenshot(Path.ChangeExtension(reportPath, ".png"));
                await Task.Delay(1000);
                Stage("completed");
            }
            catch (Exception error)
            {
                Report["error"] = error.ToString();
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
