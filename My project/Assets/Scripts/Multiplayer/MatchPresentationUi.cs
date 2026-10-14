using TMPro;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    public static class MatchPresentationUi
    {
        public static readonly Color Gold=new Color(.94f,.82f,.57f), Ink=new Color(.035f,.07f,.12f);
        public static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
        {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
        public static UnityEngine.UI.Image Image(string name,Transform parent,Vector2 min,Vector2 max,Color color,bool blocks=false)
        {var i=Rect(name,parent,min,max).gameObject.AddComponent<UnityEngine.UI.Image>();i.color=color;i.raycastTarget=blocks;return i;}
        public static TMP_Text Label(string name,Transform parent,TMP_FontAsset font,string text,float size,Vector2 min,Vector2 max,Color color)
        {var t=Rect(name,parent,min,max).gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=text;t.fontSize=size;t.color=color;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;t.fontStyle=FontStyles.Bold;t.textWrappingMode=TextWrappingModes.Normal;return t;}
        public static UnityEngine.UI.Button Button(string name,Transform parent,TMP_FontAsset font,string text,Vector2 min,Vector2 max,bool primary)
        {
            var i=Image(name,parent,min,max,primary?Gold:new Color(.09f,.16f,.23f),true);
            var b=i.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=i;
            Label("Label",i.transform,font,text,23,Vector2.zero,Vector2.one,primary?Ink:Color.white);
            return b;
        }
        public static void SafeArea(RectTransform r)
        {if(Screen.width<1||Screen.height<1)return;var a=Screen.safeArea;r.anchorMin=new Vector2(a.xMin/Screen.width,a.yMin/Screen.height);r.anchorMax=new Vector2(a.xMax/Screen.width,a.yMax/Screen.height);r.offsetMin=r.offsetMax=Vector2.zero;}
    }
}
