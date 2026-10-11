using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using PickleVerse.FrontEnd;
using PickleVerse.Progression;
using PickleVerse.Shop;
using UnityEngine;
using UnityEngine.EventSystems;

public static class LocalPlayerEditingValidation
{
    static readonly List<object> checks = new List<object>();
    static void Check(string name, bool passed)
    {
        checks.Add(new { name, passed });
        if (!passed) throw new InvalidOperationException(name);
    }
    static async Task Click(UnityEngine.UI.Button button)
    {
        Canvas.ForceUpdateCanvases();
        await Task.Delay(80);
        var rect = (RectTransform)button.transform;
        var canvas = button.GetComponentInParent<Canvas>();
        var data = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.TransformPoint(rect.rect.center)),
            button = PointerEventData.InputButton.Left
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, hits);
        Check(button.name + " accepts pointer input", hits.Count > 0 &&
            (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
        await Task.Delay(80);
    }
    static void Fill(PlayerProgressionMenu ui, string name, string elo, string coins, string elite)
    {
        ui.editName.text = name; ui.editElo.text = elo; ui.editTokenpad.text = coins; ui.editEliteToken.text = elite;
    }
    public static async Task<object> Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play Menu first.");
        checks.Clear();
        var runtime = ProgressionRuntime.Instance;
        var original = runtime.Player;
        string originalId = original.id, originalName = original.playerName;
        long originalCoins = original.coins, originalElite = original.gems, originalXp = original.totalXp;
        int originalElo = original.elo, originalHistory = original.history.Count;
        string testId = "PE" + Guid.NewGuid().ToString("N").Substring(0,20);
        string otherId = "PE" + Guid.NewGuid().ToString("N").Substring(0,20);
        var ui = UnityEngine.Object.FindAnyObjectByType<PlayerProgressionMenu>();
        var save = ui.editForm.Find("SaveName").GetComponent<UnityEngine.UI.Button>();
        var change = ui.editForm.Find("Switch").GetComponent<UnityEngine.UI.Button>();
        try
        {
            runtime.SwitchPlayer(testId, "TEST");
            runtime.UpdateWallet(p => { ProgressionEngine.Grant(p,new RewardBundle(0,0,17)); p.rankLossStreak=5; });
            await Click(ui.editButton);
            Check("All three numeric inputs are present and show saved data",
                ui.editElo.text=="100" && ui.editTokenpad.text=="0" && ui.editEliteToken.text=="0");
            Fill(ui,"DEV TEST","1250","12345678901","345");
            await Click(save);
            Check("One save updates name, Elo and both balances", runtime.Player.playerName=="DEV TEST" &&
                runtime.Player.elo==1250 && runtime.Player.coins==12345678901L && runtime.Player.gems==345);
            Check("Editor does not alter XP, streaks or history", runtime.Player.totalXp==17 &&
                runtime.Player.rankLossStreak==5 && runtime.Player.history.Count==0);
            var wallet=UnityEngine.Object.FindAnyObjectByType<WalletBar>();
            Check("Menu wallet updates immediately", wallet.tokenpad.text==runtime.Player.coins.ToString("N0") && wallet.elite.text=="345");
            Check("Menu rank updates immediately", ui.menuLevel.text.Contains("Vàng") && ui.menuLevel.text.Contains("1250"));
            Check("Save action is labelled LƯU PLAYER", save.GetComponentInChildren<TMPro.TMP_Text>().text=="LƯU PLAYER");
            foreach(var invalid in new[]
            {
                new[]{"WRONG","99","20","30"},
                new[]{"WRONG","9999999999","20","30"},
                new[]{"WRONG","1250","-1","30"},
                new[]{"WRONG","1250","20","-1"},
                new[]{"WRONG","1250","9999999999999999999","30"},
                new[]{"WRONG","","20","30"},
                new[]{"","1250","20","30"}
            })
            {
                Fill(ui,invalid[0],invalid[1],invalid[2],invalid[3]); await Click(save);
                Check("Invalid values reject the entire edit: " + string.Join("/",invalid), runtime.Player.playerName=="DEV TEST" &&
                    runtime.Player.elo==1250 && runtime.Player.coins==12345678901L && runtime.Player.gems==345 &&
                    !ui.feedback.text.StartsWith("Đã lưu"));
            }
            ui.editId.text=otherId; await Click(change);
            Check("Switch ID does not apply stale editor values", runtime.Player.id==otherId && runtime.Player.elo==100 &&
                runtime.Player.coins==0 && runtime.Player.gems==0 && ui.editElo.text=="100" && ui.editTokenpad.text=="0" && ui.editEliteToken.text=="0");
            Fill(ui,"SECOND","100","0","0"); await Click(save);
            Check("Floor Elo and zero balances are accepted", runtime.Player.elo==100 && runtime.Player.coins==0 && runtime.Player.gems==0);
            ui.editId.text=testId; await Click(change);
            Check("Reopening profile loads the persisted edit", runtime.Player.playerName=="DEV TEST" && runtime.Player.elo==1250 &&
                runtime.Player.coins==12345678901L && runtime.Player.gems==345 && ui.editTokenpad.text=="12345678901");
            var disk=JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(Path.Combine(Application.persistentDataPath,"LocalPlayers",testId+".json")));
            Check("JSON contains the same balances and Elo", disk.elo==1250 && disk.coins==12345678901L && disk.gems==345);
            Check("Premium migration preserves saved developer test balance", !CurrencyPolicy.Migrate(disk) && disk.gems==345);
            ui.Close(); await Click(ui.profileButton);
            Check("Profile displays the edited rank and Elo", ui.identityRank.text.Contains("VÀNG") && ui.identityRank.text.Contains(1250.ToString("N0")));
            ui.Close();
            var result=new {passed=true,count=checks.Count,checks};
            File.WriteAllText("Docs/Progression/LocalPlayerEditingValidation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
            return result;
        }
        finally
        {
            ui.Close(); runtime.SwitchPlayer(originalId,null);
            if(runtime.Player.playerName!=originalName || runtime.Player.coins!=originalCoins || runtime.Player.gems!=originalElite ||
                runtime.Player.elo!=originalElo || runtime.Player.totalXp!=originalXp || runtime.Player.history.Count!=originalHistory)
                throw new InvalidOperationException("Real player was unexpectedly modified.");
        }
    }
    public static async Task<object> Layout()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play Menu first.");
        checks.Clear();
        var ui=UnityEngine.Object.FindAnyObjectByType<PlayerProgressionMenu>();
        var assembly=typeof(UnityEditor.Editor).Assembly;
        var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance|BindingFlags.FlattenHierarchy;
        var viewType=assembly.GetType("UnityEditor.GameView");
        var view=Resources.FindObjectsOfTypeAll(viewType).Cast<UnityEditor.EditorWindow>().First();
        var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var sizes=sizesType.GetProperty("instance",flags).GetValue(null);
        var group=sizesType.GetMethod("GetGroup",flags).Invoke(sizes,new[]{viewType.GetProperty("currentSizeGroupType",flags).GetValue(view)});
        var groupType=group.GetType();
        var selection=viewType.GetProperty("selectedSizeIndex",flags);
        int previous=(int)selection.GetValue(view), previousCustom=(int)groupType.GetMethod("GetCustomCount").Invoke(group,null);
        try
        {
            foreach(var size in new[]{new[]{1280,720},new[]{1920,1080},new[]{1024,768},new[]{2560,1080}})
            {
                var fixedType=Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution");
                var entry=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),fixedType,size[0],size[1],"Player editor validation");
                groupType.GetMethod("AddCustomSize").Invoke(group,new[]{entry});
                selection.SetValue(view,(int)groupType.GetMethod("GetTotalCount").Invoke(group,null)-1);
                view.Focus(); view.Repaint();
                var deadline=DateTime.UtcNow.AddSeconds(5);
                while(Screen.width!=size[0] || Screen.height!=size[1])
                {if(DateTime.UtcNow>deadline)throw new TimeoutException("Game view resize");await Task.Delay(50);}
                ui.OpenEditor();
                ui.editElo.text="2147483647"; ui.editTokenpad.text=ui.editEliteToken.text="9223372036854775807";
                Canvas.ForceUpdateCanvases(); await Task.Delay(100);
                string tag=size[0]+"x"+size[1];
                foreach(var input in new[]{ui.editElo,ui.editTokenpad,ui.editEliteToken})
                {
                    input.textComponent.ForceMeshUpdate();
                    var corners=new Vector3[4];((RectTransform)input.transform).GetWorldCorners(corners);
                    Check(input.name+" fits window at "+tag,corners.All(c=>RectTransformUtility.RectangleContainsScreenPoint(ui.window,RectTransformUtility.WorldToScreenPoint(null,c),null)) && !input.textComponent.isTextOverflowing);
                }
                Check("Labels fit at "+tag,ui.editForm.GetComponentsInChildren<TMPro.TMP_Text>().All(t=>!t.isTextOverflowing));
            }
            var result=new{passed=true,count=checks.Count,checks};
            File.WriteAllText("Docs/Progression/LocalPlayerEditingLayout.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
            return result;
        }
        finally
        {
            selection.SetValue(view,previous);
            while((int)groupType.GetMethod("GetCustomCount").Invoke(group,null)>previousCustom)
                groupType.GetMethod("RemoveCustomSize").Invoke(group,new object[]{previousCustom});
            view.Repaint();ui.Close();
        }
    }
}
