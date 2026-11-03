using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PickleVerse.FrontEnd;
using PickleVerse.Services;
using PickleVerse.Services.Api;

// Manual UI QA only. All requests are intercepted in memory until Play Mode ends.
public static class AccountUiSmoke
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static string Show(string page)
    {
        var screen = UnityEngine.Object.FindAnyObjectByType<AccountScreen>();
        if (!screen) throw new InvalidOperationException("Authentication must be playing.");
        if (!Camera.main)
        {
            var camera = new GameObject("Authentication Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(screen.transform, false); camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor; camera.cullingMask = 0;
        }
        var pageType = typeof(AccountScreen).GetNestedType("Page", BindingFlags.NonPublic);
        typeof(AccountScreen).GetMethod("Show", Private).Invoke(screen, new object[] { Enum.Parse(pageType, page), true });
        Canvas.ForceUpdateCanvases();
        Debug.Log("[AccountUiSmoke] Authentication page: " + page);
        return page;
    }
    public static async Task<string> Menu()
    {
        var account = AccountRuntime.Ensure().Account;
        typeof(AccountService).GetField("api", Private).SetValue(account, new ApiClient(new FixtureTransport()));
        await account.LoginAsync("qa@example.invalid", "qa-only");
        for (int i = 0; i < 100 && !UnityEngine.Object.FindAnyObjectByType<PlayerProgressionMenu>(); i++) await Task.Delay(50);
        var menu = UnityEngine.Object.FindAnyObjectByType<PlayerProgressionMenu>();
        if (!menu) throw new InvalidOperationException("Menu did not load.");
        menu.OpenProfile();
        Debug.Log("[AccountUiSmoke] Menu fixture loaded; no HTTP requests used.");
        return "Menu fixture ready";
    }
    public static string RenameAndCopy()
    {
        var menu = UnityEngine.Object.FindAnyObjectByType<PlayerProgressionMenu>();
        var copy = menu.GetComponentsInChildren<Button>(true).Single(b => b.name == "CopyPlayerId");
        string prior = GUIUtility.systemCopyBuffer;
        copy.onClick.Invoke();
        bool correct = GUIUtility.systemCopyBuffer == FixtureTransport.Id;
        GUIUtility.systemCopyBuffer = prior;
        if (!correct) throw new InvalidOperationException("Copied ID differs from profile.");
        menu.GetComponentsInChildren<Button>(true).Single(b => b.name == "EditPlayerName").onClick.Invoke();
        var input = (TMP_InputField)typeof(PlayerProgressionMenu).GetField("renameInput", Private).GetValue(menu);
        if (input.text != "Người chơi thử") throw new InvalidOperationException("Rename did not prefill current name.");
        Debug.Log("[AccountUiSmoke] Exact ID copied; rename prefilled current name.");
        return "Copy and rename prefill passed";
    }
    public static string CancelRename()
    {
        var menu = UnityEngine.Object.FindAnyObjectByType<PlayerProgressionMenu>();
        var dialog = (RectTransform)typeof(PlayerProgressionMenu).GetField("renameDialog", Private).GetValue(menu);
        int before = FixtureTransport.ProfileWrites;
        dialog.Find("Cancel").GetComponent<Button>().onClick.Invoke();
        if (dialog.gameObject.activeSelf || FixtureTransport.ProfileWrites != before) throw new InvalidOperationException("Cancel wrote profile.");
        Debug.Log("[AccountUiSmoke] Cancel closed dialog without API call.");
        return "Cancel passed";
    }
    public static string Blocked()
    {
        var menu = UnityEngine.Object.FindAnyObjectByType<PlayerProgressionMenu>(); menu.Close();
        var social = UnityEngine.Object.FindAnyObjectByType<OfflineSocialMenu>(); social.OpenFriends();
        social.GetComponentsInChildren<Button>(true).Single(b => b.name == "BlockedPlayers").onClick.Invoke();
        Debug.Log("[AccountUiSmoke] Own blocked list fixture opened.");
        return "Blocked list ready";
    }
    sealed class FixtureTransport : IApiTransport
    {
        public const string Id = "11111111111141118111111111111111";
        public static int ProfileWrites;
        public Task<ApiHttpResponse> SendAsync(string method, string path, string json, string token, CancellationToken cancellation)
        {
            object data;
            if (path == "api/Auth/login") data = new { success = true, accessToken = "qa-memory-only", refreshToken = "qa-memory-only" };
            else if (path == "api/Profiles/me")
            {
                if (method == "PUT") ProfileWrites++;
                data = new { userId = Guid.Parse(Id), playerName = "Người chơi thử", level = 1, elo = 1000, isMale = true };
            }
            else if (path == "api/Game/state")
            {
                var catalog = JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "../../src/Seed/UnityCatalog.json")));
                catalog["version"] = "ui-qa";
                catalog["matchmaking"] = JObject.Parse("{botFallbackSeconds:30,readyConfirmationSeconds:15}");
                catalog["onlineMatch"] = JObject.Parse("{heartbeatSeconds:5,disconnectTimeoutSeconds:30}");
                data = new { revision = 1, isMale = true, player = new { id = Id, playerName = "Người chơi thử", elo = 1000, level = 1, online = true },
                    catalog, inventory = new { owned = new[] { "paddle.default" }, equippedPaddle = "paddle.default" }, tutorial = new { completed = true, skipped = false } };
            }
            else if (path.Contains("social")) data = new { players = new[] { new { id = "22222222222242228222222222222222", name = "Người chơi đã chặn", relationship = "blocked", elo = 1000 } }, total = 1, friendCount = 0 };
            else if (path.Contains("coaching")) data = new { status = "disabled", insights = new object[0], recommendations = new object[0] };
            else if (path.Contains("presence")) data = new { pollSeconds = 5, invitations = new object[0] };
            else data = new { message = "Đã xử lý dữ liệu kiểm thử." };
            return Task.FromResult(new ApiHttpResponse(200, JsonConvert.SerializeObject(new { success = true, data })));
        }
    }
}
