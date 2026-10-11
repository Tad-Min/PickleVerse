using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using PickleVerse.FrontEnd;
using PickleVerse.Progression;
using PickleVerse.Social;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class OfflineSocialValidation
{
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool passed)
    { checks.Add(new{name,passed}); if(!passed) throw new InvalidOperationException(name); }
    static async Task Click(UnityEngine.UI.Button button)
    {
        Canvas.ForceUpdateCanvases(); await Task.Delay(80);
        var rect=(RectTransform)button.transform;
        var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(button.GetComponentInParent<Canvas>().worldCamera,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(data,hits);
        Check(button.name+" receives pointer",hits.Count>0 && (hits[0].gameObject==button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerClickHandler); await Task.Delay(80);
    }
    static object Report(string path)
    { var result=new{passed=true,count=checks.Count,checks}; File.WriteAllText(path,Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented)); return result; }
    public static async Task<object> Run()
    {
        if(!Application.isPlaying) throw new InvalidOperationException("Play Menu first.");
        checks.Clear();
        var gameViewType=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
        Resources.FindObjectsOfTypeAll(gameViewType).Cast<UnityEditor.EditorWindow>().First().Focus();
        await Task.Delay(100);
        var social=UnityEngine.Object.FindAnyObjectByType<OfflineSocialMenu>(); var ui=social.ui;
        var runtime=ProgressionRuntime.Instance;
        string originalId=runtime.Player.id, originalName=runtime.Player.playerName;
        int originalElo=runtime.Player.elo, originalHistory=runtime.Player.history.Count;
        long originalCoins=runtime.Player.coins, originalElite=runtime.Player.gems, originalXp=runtime.Player.totalXp;
        string a="SOC"+Guid.NewGuid().ToString("N").Substring(0,20), b="SOC"+Guid.NewGuid().ToString("N").Substring(0,20);
        try
        {
            runtime.SwitchPlayer(a,null); runtime.EditLocalPlayer("SOCIAL TEST",100,0,0);
            social.Close(); ui.Close();
            await Click(social.friendsButton);
            Check("Friends opens six seeded friends",social.modal.gameObject.activeSelf && social.content.childCount==6);
            Check("Menu buttons are blocked",!ui.profileButton.IsInteractable() && !ui.missionsButton.IsInteractable());
            Check("Modal renders above the persistent music UI",social.modal.GetComponent<Canvas>().overrideSorting && social.modal.GetComponent<Canvas>().sortingOrder>500);
            social.scroll.verticalNormalizedPosition=0; Canvas.ForceUpdateCanvases(); await Task.Delay(80);
            var last=(RectTransform)social.content.GetChild(5);
            Check("Friends scroll reaches last row",RectTransformUtility.RectangleContainsScreenPoint(social.scroll.viewport,RectTransformUtility.WorldToScreenPoint(null,last.TransformPoint(last.rect.center)),null));
            await Click(social.directoryButton);
            Check("Find friends shows thirty fixtures",social.content.childCount==30);
            social.search.text="dan thanh"; await Task.Delay(80);
            Check("Search finds accent-free Vietnamese name",social.content.childCount==1 && social.content.GetChild(0).name=="DEMO-005");
            await Click(social.content.GetChild(0).Find("FriendAction").GetComponent<UnityEngine.UI.Button>());
            Check("Adding friend changes session count",OfflineSocialSession.Directory.FriendCount(a)==7);
            Check("Added fixture cannot be added twice",!social.content.GetChild(0).Find("FriendAction").GetComponent<UnityEngine.UI.Button>().interactable);
            await Click(social.listButton); social.search.text="demo-005"; await Task.Delay(80);
            Check("Added friend appears by ID",social.content.childCount==1 && social.content.GetChild(0).name=="DEMO-005");
            await Click(social.content.GetChild(0).Find("FriendAction").GetComponent<UnityEngine.UI.Button>());
            Check("Removing friend updates list and empty state",OfflineSocialSession.Directory.FriendCount(a)==6 && social.content.GetChild(0).name=="Empty");
            social.search.text="nothing matches";
            Check("Unknown search displays empty state",social.content.childCount==1 && social.content.GetChild(0).name=="Empty");
            await Click(social.closeButton);
            Check("Close restores Menu input",!social.modal.gameObject.activeSelf && ui.profileButton.IsInteractable());
            await Click(social.leaderboardButton);
            Check("Leaderboard contains fixtures and current player",social.content.childCount==31 && social.selfRow && social.selfName.text=="SOCIAL TEST");
            await Click(social.myPositionButton);
            Check("My position scrolls to current player",RectTransformUtility.RectangleContainsScreenPoint(social.scroll.viewport,RectTransformUtility.WorldToScreenPoint(null,social.selfRow.TransformPoint(social.selfRow.rect.center)),null));
            social.search.text="demo-001";
            Check("Filtered leaderboard retains original position",social.content.childCount==1 && social.content.GetChild(0).Find("Position").GetComponent<TMPro.TMP_Text>().text=="#1");
            await Click(social.myPositionButton);
            Check("My position clears filter",social.search.text=="" && social.content.childCount==31);
            runtime.EditLocalPlayer("UPDATED TEST",8000,0,0);
            Check("Saved Elo and name update leaderboard immediately",social.content.GetChild(0).name==a && social.selfPosition.text.Contains("#1 / 31") && social.selfName.text=="UPDATED TEST");
            Check("Current player row is highlighted",social.selfRow.Find("Tag").GetComponent<TMPro.TMP_Text>().text=="BẠN");
            runtime.SwitchPlayer(b,null);
            Check("Switching player replaces current leaderboard row",social.content.childCount==31 && !social.content.Cast<Transform>().Any(t=>t.name==a) && social.selfRow.name==b);
            Check("New player receives independent six friends",OfflineSocialSession.Directory.FriendCount(b)==6);
            runtime.SwitchPlayer(a,null);
            Check("Returning player gets current progress",social.selfName.text=="UPDATED TEST" && social.selfPosition.text.Contains("#1 / 31"));
            ui.OpenProfile();
            Check("Opening Profile closes Social and keeps Menu blocked",!social.modal.gameObject.activeSelf && ui.modal.gameObject.activeSelf && !ui.profileButton.IsInteractable());
            social.OpenFriends();
            Check("Opening Social closes Profile and keeps Menu blocked",social.modal.gameObject.activeSelf && !ui.modal.gameObject.activeSelf && !ui.profileButton.IsInteractable());
            if(Keyboard.current==null) throw new InvalidOperationException("No keyboard.");
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Escape)); await Task.Delay(120);
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState()); await Task.Delay(80);
            Check("Escape closes window and restores Menu input",!social.modal.gameObject.activeSelf && ui.profileButton.IsInteractable());
            social.OpenFriends(); social.Close(); social.OpenFriends();
            Check("Reopening does not duplicate rows",social.content.childCount==6);
            social.Close();
        }
        finally
        {
            if(Keyboard.current!=null) InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
            social.Close(); ui.Close(); runtime.SwitchPlayer(originalId,null);
            Check("Original profile is preserved",runtime.Player.playerName==originalName && runtime.Player.elo==originalElo && runtime.Player.coins==originalCoins && runtime.Player.gems==originalElite && runtime.Player.totalXp==originalXp && runtime.Player.history.Count==originalHistory);
        }
        return Report("Docs/Social/InteractionValidation.json");
    }
    static bool Inside(RectTransform child,RectTransform parent)
    { var corners=new Vector3[4]; child.GetWorldCorners(corners); return corners.All(c=>RectTransformUtility.RectangleContainsScreenPoint(parent,RectTransformUtility.WorldToScreenPoint(null,c),null)); }
    public static async Task<object> Layout()
    {
        if(!Application.isPlaying) throw new InvalidOperationException("Play Menu first.");
        checks.Clear(); var social=UnityEngine.Object.FindAnyObjectByType<OfflineSocialMenu>();
        var assembly=typeof(UnityEditor.Editor).Assembly;
        var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance|BindingFlags.FlattenHierarchy;
        var viewType=assembly.GetType("UnityEditor.GameView"); var view=Resources.FindObjectsOfTypeAll(viewType).Cast<UnityEditor.EditorWindow>().First();
        var sizesType=assembly.GetType("UnityEditor.GameViewSizes"); var sizes=sizesType.GetProperty("instance",flags).GetValue(null);
        var group=sizesType.GetMethod("GetGroup",flags).Invoke(sizes,new[]{viewType.GetProperty("currentSizeGroupType",flags).GetValue(view)});
        var groupType=group.GetType(); var selection=viewType.GetProperty("selectedSizeIndex",flags);
        int previous=(int)selection.GetValue(view),custom=(int)groupType.GetMethod("GetCustomCount").Invoke(group,null);
        try
        {
            foreach(var size in new[]{new[]{1280,720},new[]{1920,1080},new[]{1024,768},new[]{2560,1080}})
            {
                var entry=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),size[0],size[1],"Offline social validation");
                groupType.GetMethod("AddCustomSize").Invoke(group,new[]{entry}); selection.SetValue(view,(int)groupType.GetMethod("GetTotalCount").Invoke(group,null)-1); view.Focus(); view.Repaint();
                var deadline=DateTime.UtcNow.AddSeconds(5);
                while(Screen.width!=size[0] || Screen.height!=size[1]) { if(DateTime.UtcNow>deadline)throw new TimeoutException("Resize GameView"); await Task.Delay(50); }
                string tag=size[0]+"x"+size[1];
                social.Close(); social.ui.Close();
                var card=(RectTransform)social.friendsButton.transform.parent;
                Check("Welcome buttons fit at "+tag,new[]{social.friendsButton,social.leaderboardButton,social.ui.missionsButton}.All(b=>Inside((RectTransform)b.transform,card)));
                Check("Welcome text fits at "+tag,card.GetComponentsInChildren<TMPro.TMP_Text>().All(t=>{t.ForceMeshUpdate();return !t.isTextOverflowing;}));
                await Click(social.friendsButton); await Click(social.directoryButton);
                Check("Friends search and controls fit at "+tag,new[]{(RectTransform)social.search.transform,(RectTransform)social.listButton.transform,(RectTransform)social.directoryButton.transform}.All(r=>Inside(r,social.window)));
                Check("Window follows safe area at "+tag,Inside(social.window,social.modalSafe));
                Check("Visible labels fit at "+tag,social.modal.GetComponentsInChildren<TMPro.TMP_Text>().All(t=>{t.ForceMeshUpdate();return !t.isTextOverflowing;}));
                await Click(social.leaderboardTab); await Click(social.myPositionButton);
                Check("Current leaderboard row visible at "+tag,RectTransformUtility.RectangleContainsScreenPoint(social.scroll.viewport,RectTransformUtility.WorldToScreenPoint(null,social.selfRow.TransformPoint(social.selfRow.rect.center)),null));
                await Click(social.closeButton); Check("Close restores Menu at "+tag,social.ui.profileButton.IsInteractable());
            }
            return Report("Docs/Social/LayoutValidation.json");
        }
        finally
        {
            selection.SetValue(view,previous);
            while((int)groupType.GetMethod("GetCustomCount").Invoke(group,null)>custom) groupType.GetMethod("RemoveCustomSize").Invoke(group,new object[]{custom});
            view.Repaint(); social.Close();
        }
    }
}
