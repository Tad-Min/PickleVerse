using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PickleVerse.Services.Api;
using UnityEngine;

namespace PickleVerse.Services
{
    // Opt-in development-build verification. Credentials are read from an ignored fixture, never logged.
    public sealed class AccountSmokeRunner : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-pkvAccountSmoke") >= 0)
                new GameObject("Account Smoke Check").AddComponent<AccountSmokeRunner>();
        }
        async void Start()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-pkvAccountSmoke");
            if (index < 0 || index + 2 >= args.Length)
            {
                Application.Quit(1);
                return;
            }
            string reportPath = args[index + 2];
            var report = new Dictionary<string, object>();
            bool passed = false;
            Application.runInBackground = true;
            try
            {
                if (!Application.isEditor && !Debug.isDebugBuild)
                    throw new InvalidOperationException("A development build is required.");
                report["developmentBuild"] = Application.isEditor || Debug.isDebugBuild;
                var fixture = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(args[index + 1]));
                var account = AccountRuntime.Ensure().Account;
                report["startedSignedOut"] = !account.IsSignedIn;
                await account.LoginAsync(fixture["email"], fixture["password"]);
                report["authenticatedProfile"] = account.Profile.UserId == Guid.Parse(fixture["userId"]);
                var catalog = await account.SendAuthorizedAsync<JObject>("GET", "api/Game/catalog");
                report["serverMatchmakingSettings"] = (int?)catalog["matchmaking"]?["botFallbackSeconds"] == 360 && (int?)catalog["matchmaking"]?["readyConfirmationSeconds"] == 120;
                report["serverHeartbeatSettings"] = (int?)catalog["onlineMatch"]?["disconnectTimeoutSeconds"] == 35 && (int?)catalog["onlineMatch"]?["heartbeatSeconds"] == 8;
                report["serverProgressionSettings"] = (int?)catalog["winXp"] == 10 && (int?)catalog["lossXp"] == 5 && (int?)catalog["dailyBonus"]?["coins"] == 200 && (int?)catalog["weeklyBonus"]?["coins"] == 1000;
                await account.UpdateProfileAsync(new UpdateProfileRequest { PlayerName = "WindowsApiCheck", IsMale = true });
                report["serverProfileUpdated"] = account.Profile.PlayerName == "WindowsApiCheck" && account.Profile.IsMale;
                await account.LogoutAsync();
                report["signedOut"] = !account.IsSignedIn;
                report["noLocalPlayerSave"] = !Directory.Exists(Path.Combine(Application.persistentDataPath, "LocalPlayers"));
                report["noLocalReplaySave"] = !Directory.Exists(Path.Combine(Application.persistentDataPath, "Replays"));
                passed = true;
                foreach (var value in report.Values)
                    if (value is bool flag && !flag)
                        passed = false;
                report["frames"] = Time.frameCount;
            }
            catch (Exception error)
            {
                report["errorType"] = error.GetType().Name;
            }
            report["passed"] = passed;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath)));
            File.WriteAllText(reportPath, JsonConvert.SerializeObject(report, Formatting.Indented));
            Application.Quit(passed ? 0 : 1);
        }
    }
}
