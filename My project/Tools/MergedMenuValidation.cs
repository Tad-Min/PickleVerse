using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PickleVerse.FrontEnd;
using PickleVerse.Progression;
using PickleVerse.Shop;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// Integration check for the combined progression, shop and character selector Menu.
public static class MergedMenuValidation
{
    static readonly List<object> checks = new List<object>();
    static void Check(string name, bool passed)
    {
        checks.Add(new { name, passed });
        if (!passed) throw new InvalidOperationException(name);
    }
    static async Task Click(UnityEngine.UI.Button button, Transform hitTarget = null)
    {
        Canvas.ForceUpdateCanvases();
        await Task.Delay(100);
        var rect = (RectTransform)(hitTarget ? hitTarget : button.transform);
        var canvas = button.GetComponentInParent<Canvas>();
        var data = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.TransformPoint(rect.rect.center)),
            button = PointerEventData.InputButton.Left
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, hits);
        Check(button.name + " receives pointer input at " + rect.name,
            hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
        await Task.Delay(100);
    }
    public static async Task<object> Run()
    {
        if (!Application.isPlaying || SceneManager.GetActiveScene().name != "Menu")
            throw new InvalidOperationException("Play Menu first.");
        checks.Clear();
        var ui = UnityEngine.Object.FindAnyObjectByType<PlayerProgressionMenu>();
        var home = UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();
        var player = ProgressionRuntime.Instance.Player;
        string id = player.id;
        long coins = player.coins, elite = player.gems;
        try
        {
            foreach (var target in new Transform[] { ui.profileButton.transform, ui.menuName.transform, ui.profileButton.transform.Find("Avatar") })
            {
                if (!target) continue;
                await Click(ui.profileButton, target);
                Check("NameTag opens saved player profile via " + target.name,
                    ui.modal.gameObject.activeSelf && ui.identityName.text.Contains(player.playerName) && ui.identityId.text.Contains(id));
                ui.Close();
            }
            await Click(ui.profileButton);
            await Click(ui.achievementTab);
            Check("Achievements remain available", ui.modal.gameObject.activeSelf && ui.content.childCount >= 10);
            Check("Character selector is blocked by profile modal", !home.maleButton.IsInteractable());
            await Click(ui.modal.Find("SafeArea/Window/Close").GetComponent<UnityEngine.UI.Button>());
            Check("Closing profile restores character selector", home.maleButton.IsInteractable());
            await Click(ui.missionsButton);
            Check("Daily missions remain available", ui.modal.gameObject.activeSelf && ui.content.childCount >= 7);
            await Click(ui.weeklyTab);
            Check("Weekly missions remain available", ui.content.childCount >= 10);
            ui.Close();
            await Click(ui.careerButton);
            Check("Career opens the history window", ui.modal.gameObject.activeSelf && ui.title.text.Contains("LỊCH SỬ"));
            ui.Close();
            var wallet = UnityEngine.Object.FindAnyObjectByType<WalletBar>();
            Check("Menu shows shared TOKENPAD and paid ELITE balances",
                wallet && wallet.tokenpad.text == coins.ToString("N0") && wallet.elite.text == elite.ToString("N0"));
            var shopButton = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>().Single(b => b.name == "BtnStore");
            await Click(shopButton);
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (SceneManager.GetActiveScene().name != "Shop" || UnityEngine.Object.FindAnyObjectByType<LobbyNavigation>()?.Busy == true)
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Shop did not open.");
                await Task.Delay(50);
            }
            Check("Shop still opens after the scene merge", UnityEngine.Object.FindAnyObjectByType<ShopScreen>() != null);
            LobbyNavigation.ReturnToLobby();
            while (SceneManager.GetActiveScene().name != "Menu" || UnityEngine.Object.FindAnyObjectByType<LobbyNavigation>()?.Busy == true)
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Menu did not return.");
                await Task.Delay(50);
            }
            await Task.Delay(200);
            Check("Returning from Shop preserves profile bindings", UnityEngine.Object.FindAnyObjectByType<PlayerProgressionMenu>() == ui);
            await Click(ui.profileButton);
            Check("NameTag works after returning from Shop", ui.modal.gameObject.activeSelf);
            ui.Close();
            Check("Validation preserves identity and both balances", player.id == id && player.coins == coins && player.gems == elite);
            var result = new { passed = true, count = checks.Count, checks };
            File.WriteAllText("Docs/Progression/MergedMenuValidation.json", Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
            return result;
        }
        finally { if (ui) ui.Close(); }
    }
}
