#if !UNITY_SERVER
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.FrontEnd
{
    internal static class RuntimeForms
    {
        public static TMP_FontAsset Font(Transform outside = null)
        {
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (text.font && (!outside || !text.transform.IsChildOf(outside))) return text.font;
            return TMP_Settings.defaultFontAsset;
        }
        public static GameObject Canvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; go.GetComponent<Canvas>().sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f; return go;
        }
        public static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent,false);
            var rect = (RectTransform)go.transform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
        }
        public static RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        { var rect = Rect(name,parent,min,max); rect.gameObject.AddComponent<Image>().color=color; return rect; }
        public static TMP_Text Text(string name, Transform parent, string value, int size, Vector2 min, Vector2 max)
        {
            var rect = Rect(name,parent,min,max); var text=rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font=Font(parent);text.fontSize=size;text.color=Color.white;text.text=value;text.richText=false;text.raycastTarget=false;
            text.textWrappingMode=TextWrappingModes.Normal;return text;
        }
        public static Button Button(string name,Transform parent,string label,UnityEngine.Events.UnityAction action,Vector2 min,Vector2 max)
        {
            var rect=Panel(name,parent,min,max,new Color(.08f,.28f,.32f,1));var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();
            button.onClick.AddListener(action);var text=Text("Label",rect,label,22,new Vector2(.03f,.06f),new Vector2(.97f,.94f));text.alignment=TextAlignmentOptions.Center;return button;
        }
        public static TMP_InputField Input(Transform parent,string placeholder,Vector2 min,Vector2 max,int limit)
        {
            var rect=Panel("Statement",parent,min,max,new Color(.025f,.05f,.07f,1));
            var viewport=Rect("Viewport",rect,new Vector2(.02f,.05f),new Vector2(.98f,.95f));viewport.gameObject.AddComponent<RectMask2D>();
            var text=Text("Text",viewport,"",22,Vector2.zero,Vector2.one);var hint=Text("Placeholder",viewport,placeholder,22,Vector2.zero,Vector2.one);hint.color=new Color(.65f,.72f,.76f);
            var input=rect.gameObject.AddComponent<TMP_InputField>();input.textViewport=viewport;input.textComponent=(TextMeshProUGUI)text;input.placeholder=hint;
            input.lineType=TMP_InputField.LineType.MultiLineNewline;input.characterLimit=limit;input.richText=false;return input;
        }
        public static TMP_Text ScrollText(Transform parent,Vector2 min,Vector2 max)
        {
            var root=Panel("Evidence",parent,min,max,new Color(.025f,.05f,.07f,1));var viewport=Rect("Viewport",root,new Vector2(.02f,.02f),new Vector2(.98f,.98f));viewport.gameObject.AddComponent<RectMask2D>();
            var text=Text("Details",viewport,"",23,new Vector2(0,1),Vector2.one);var content=(RectTransform)text.transform;content.pivot=new Vector2(.5f,1);
            var fit=text.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=root.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;return text;
        }
    }
}
#endif
