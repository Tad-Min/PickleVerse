using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;

namespace PickleVerse.LocalMatch.Editor
{
    public static class MatchAnnouncementsSetup
    {
        public static void Configure(BotMatch match)
        {
            var hud = match.hud;
            var notices = hud.GetComponent<MatchAnnouncements>() ?? hud.gameObject.AddComponent<MatchAnnouncements>();
            hud.announcements = notices; notices.match = match;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/FrontEndContent/Mode Vietnamese Font.asset") ?? hud.modalTitle.font;
            var root = Rect("RallyAnnouncement", hud.transform);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f,.5f); root.sizeDelta = new Vector2(900,120); root.anchoredPosition = Vector2.zero;
            var background = root.GetComponent<UnityEngine.UI.Image>() ?? root.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color(.025f,.075f,.105f,.94f); background.raycastTarget = false;
            notices.opacity = root.GetComponent<CanvasGroup>();
            if (!notices.opacity) notices.opacity = root.gameObject.AddComponent<CanvasGroup>();
            notices.opacity.blocksRaycasts = false; notices.opacity.interactable = false;
            var line = Rect("Accent",root); Stretch(line,new Vector2(0,.98f),Vector2.one);
            notices.accent = line.GetComponent<UnityEngine.UI.Image>() ?? line.gameObject.AddComponent<UnityEngine.UI.Image>();
            notices.accent.color = new Color(.51f,.90f,.80f); notices.accent.raycastTarget = false;
            notices.headline = Label("Headline",root,font,42,new Vector2(.03f,.48f),new Vector2(.97f,.90f));
            notices.headline.fontStyle = FontStyles.Bold; notices.headline.enableAutoSizing = true; notices.headline.fontSizeMin = 26; notices.headline.fontSizeMax = 42;
            notices.detail = Label("Detail",root,font,20,new Vector2(.03f,.075f),new Vector2(.97f,.45f));
            notices.detail.enableAutoSizing = true; notices.detail.fontSizeMin = 16; notices.detail.fontSizeMax = 20;
            notices.panel = root; notices.Clear();

            var actions = Rect("MatchResultActions",hud.transform); actions.anchorMin = actions.anchorMax = actions.pivot = new Vector2(.5f,0);
            actions.anchoredPosition = new Vector2(0,18); actions.sizeDelta = new Vector2(560,100);
            hud.resultSummary = Label("Summary",actions,font,18,new Vector2(0,.66f),Vector2.one);
            Button("Replay",actions,font,"CHƠI LẠI",new Vector2(0,0),new Vector2(.48f,.57f),new Color(.51f,.90f,.80f),new Color(.025f,.075f,.105f),hud.Restart);
            Button("ChooseMode",actions,font,"CHỌN CHẾ ĐỘ",new Vector2(.52f,0),new Vector2(1,.57f),new Color(.075f,.20f,.25f),Color.white,hud.ChooseMode);
            hud.resultActions = actions.gameObject; hud.resultActions.SetActive(false);
            EditorUtility.SetDirty(hud); EditorUtility.SetDirty(notices);
        }

        static RectTransform Rect(string name,Transform parent)
        {
            var old=parent.Find(name) as RectTransform;if(old)return old;
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);return rect;
        }
        static void Stretch(RectTransform rect,Vector2 min,Vector2 max)
        {rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        static TMP_Text Label(string name,Transform parent,TMP_FontAsset font,float size,Vector2 min,Vector2 max)
        {
            var rect=Rect(name,parent);Stretch(rect,min,max);
            var label=rect.GetComponent<TMP_Text>()??rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font=font;label.fontSize=size;label.color=Color.white;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;return label;
        }
        static void Button(string name,Transform parent,TMP_FontAsset font,string text,Vector2 min,Vector2 max,Color background,Color foreground,UnityEngine.Events.UnityAction action)
        {
            var rect=Rect(name,parent);Stretch(rect,min,max);
            var image=rect.GetComponent<UnityEngine.UI.Image>()??rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=background;image.raycastTarget=true;
            var button=rect.GetComponent<UnityEngine.UI.Button>()??rect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.1f,1.1f,1.1f);colors.selectedColor=new Color(1.08f,1.08f,1.08f);colors.pressedColor=new Color(.85f,.92f,.9f);button.colors=colors;
            var label=Label("Label",rect,font,19,Vector2.zero,Vector2.one);label.text=text;label.color=foreground;label.fontStyle=FontStyles.Bold;
            for(int i=button.onClick.GetPersistentEventCount()-1;i>=0;i--)UnityEventTools.RemovePersistentListener(button.onClick,i);
            UnityEventTools.AddPersistentListener(button.onClick,action);
        }
    }
}
