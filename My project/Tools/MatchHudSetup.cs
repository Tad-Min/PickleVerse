using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using PickleVerse.Settings;
using PickleVerse.LocalMatch;
using PickleVerse.Multiplayer;

public static class MatchHudSetup
{
    static TMP_FontAsset font;
    static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    {
        var child=parent.Find(name);var r=child?(RectTransform)child:new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.gameObject.layer=parent.gameObject.layer;
        r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
    }
    static UnityEngine.UI.Image Image(string name,Transform parent,float x,float y,float w,float h,Color color)
    {var r=Rect(name,parent,x,y,w,h);var image=r.GetComponent<UnityEngine.UI.Image>();if(!image)image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;return image;}
    static TMP_Text Text(string name,Transform parent,string value,float x,float y,float w,float h,int size,Color color,TextAlignmentOptions align=TextAlignmentOptions.MidlineLeft)
    {
        var r=Rect(name,parent,x,y,w,h);var text=r.GetComponent<TextMeshProUGUI>();if(!text)text=r.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.fontSize=size;text.color=color;text.text=value;text.alignment=align;text.enableAutoSizing=false;text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Ellipsis;text.raycastTarget=false;text.margin=Vector4.zero;return text;
    }
    static HudScoreDigit Digit(string name,Transform parent,float x,float y,float w,float h,int size,Color tint)
    {
        var rect=Rect(name,parent,x,y,w,h);var oldClip=rect.GetComponent<UnityEngine.UI.RectMask2D>();if(oldClip)UnityEngine.Object.DestroyImmediate(oldClip);
        var digit=rect.GetComponent<HudScoreDigit>();if(!digit)digit=rect.gameObject.AddComponent<HudScoreDigit>();digit.baseFontSize=size;
        digit.flash=Image("ScoreFlash",rect,0,0,w,h,new Color(tint.r,tint.g,tint.b,0));
        digit.previous=Text("Outgoing",rect,"0",0,0,w,h,size,tint,TextAlignmentOptions.Center);digit.current=Text("Value",rect,"0",0,0,w,h,size,tint,TextAlignmentOptions.Center);
        foreach(var text in new[]{digit.previous,digit.current}){text.rectTransform.anchorMin=text.rectTransform.anchorMax=new Vector2(.5f,.5f);text.rectTransform.pivot=new Vector2(.5f,.5f);text.rectTransform.anchoredPosition=Vector2.zero;}
        digit.previousOpacity=digit.previous.GetComponent<CanvasGroup>();if(!digit.previousOpacity)digit.previousOpacity=digit.previous.gameObject.AddComponent<CanvasGroup>();digit.previousOpacity.alpha=0;
        digit.currentOpacity=digit.current.GetComponent<CanvasGroup>();if(!digit.currentOpacity)digit.currentOpacity=digit.current.gameObject.AddComponent<CanvasGroup>();digit.currentOpacity.alpha=1;digit.enabled=false;return digit;
    }
    static MatchScoreboard Board(Transform root)
    {
        var panel=Rect("Scoreboard",root,18,18,304,164);var surface=panel.GetComponent<UnityEngine.UI.Image>();surface.material=AssetDatabase.LoadAssetAtPath<Material>("Assets/FrontEndContent/AnimeUI/Dock.mat");surface.color=Color.white;surface.raycastTarget=false;
        var nestedCanvas=panel.GetComponent<Canvas>();if(nestedCanvas)UnityEngine.Object.DestroyImmediate(nestedCanvas);
        var legacy=panel.Find("Score");if(legacy)legacy.gameObject.SetActive(false);
        var board=panel.GetComponent<MatchScoreboard>();if(!board)board=panel.gameObject.AddComponent<MatchScoreboard>();
        Image("GoldSignature",panel,14,6,42,2,ScreenUiTheme.Gold);
        board.context=Text("Context",panel,"ĐẤU BOT · VỪA",14,11,198,18,11,ScreenUiTheme.Muted);
        board.gameCaption=Text("GameCaption",panel,"GAME",222,10,42,18,12,ScreenUiTheme.Gold,TextAlignmentOptions.Right);
        board.game=Text("CurrentGame",panel,"1",266,10,24,18,12,ScreenUiTheme.Gold,TextAlignmentOptions.Right);
        Text("GamesHeader",panel,"GAME",190,29,44,12,9,ScreenUiTheme.Muted,TextAlignmentOptions.Center);Text("PointsHeader",panel,"ĐIỂM",244,29,48,12,9,ScreenUiTheme.Muted,TextAlignmentOptions.Center);
        var accents=new Color[]{ScreenUiTheme.Mint,new Color32(156,187,239,255)};
        for(int row=0;row<2;row++)
        {
            float y=42+row*36;var tint=accents[row];Image("Row"+row,panel,8,y,288,34,new Color(.03f,.055f,.09f,row==0?.52f:.32f));Image("TeamLine"+row,panel,0,y+2,3,30,tint);
            var name=Text("TeamName"+row,panel,row==0?"BẠN":"BOT",29,y+1,154,32,13,ScreenUiTheme.Text);name.richText=false;
            var marker=Image("Serving"+row,panel,15,y+14,6,6,ScreenUiTheme.Gold);marker.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var games=Digit("Games"+row,panel,192,y,40,34,18,ScreenUiTheme.Muted);var points=Digit("Points"+row,panel,244,y-1,48,36,28,row==0?ScreenUiTheme.Gold:ScreenUiTheme.Text);
            if(row==0){board.firstName=name;board.firstServing=marker.gameObject;board.firstGames=games;board.firstPoints=points;}else{board.secondName=name;board.secondServing=marker.gameObject;board.secondGames=games;board.secondPoints=points;marker.gameObject.SetActive(false);}
        }
        Image("FooterRule",panel,14,118,276,1,new Color(.52f,.61f,.73f,.27f));
        board.phase=Text("Phase",panel,"CHUẨN BỊ",14,125,210,17,11,ScreenUiTheme.Mint);
        board.clock=Text("Clock",panel,"",226,121,64,24,18,ScreenUiTheme.Gold,TextAlignmentOptions.Right);
        board.serviceDetail=Text("ServiceDetail",panel,"",14,145,276,13,9,ScreenUiTheme.Muted);board.serviceDetail.richText=false;
        Image("ClockRail",panel,8,161,288,2,new Color(.29f,.36f,.48f,.3f));board.clockFill=Image("ClockProgress",panel,8,161,288,2,ScreenUiTheme.Gold);board.clockFill.type=UnityEngine.UI.Image.Type.Filled;board.clockFill.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;board.clockFill.fillAmount=0;
        // A filled image requires a sprite; this built-in white sprite is already used by uGUI.
        board.clockFill.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");return board;
    }
    static TMP_Text Button(Transform root,string name,string label,float width,float height,ArenaGlyph? glyph=null,string key=null)
    {
        var node=root.Find(name);if(!node)return null;var button=node.GetComponent<UnityEngine.UI.Button>();var rect=(RectTransform)node;rect.anchorMin=rect.anchorMax=Vector2.one;rect.pivot=Vector2.one;rect.sizeDelta=new Vector2(width,height);ScreenUiTheme.ButtonStyle(button);
        var old=node.Find("Label")?.GetComponent<TMP_Text>();if(!old)old=node.GetComponentInChildren<TMP_Text>(true);old.gameObject.SetActive(true);old.font=font;old.fontSize=11;old.fontStyle=FontStyles.Bold;old.text=label;old.color=ScreenUiTheme.Text;old.raycastTarget=false;old.enableAutoSizing=false;old.overflowMode=TextOverflowModes.Ellipsis;old.textWrappingMode=TextWrappingModes.NoWrap;old.alignment=glyph.HasValue?TextAlignmentOptions.MidlineLeft:TextAlignmentOptions.Center;
        old.rectTransform.anchorMin=Vector2.zero;old.rectTransform.anchorMax=Vector2.one;old.rectTransform.pivot=new Vector2(.5f,.5f);old.rectTransform.offsetMin=new Vector2(glyph.HasValue?38:6,0);old.rectTransform.offsetMax=new Vector2(key!=null?-42:-8,0);
        if(glyph.HasValue){var r=Rect("Symbol",node,12,(height-20)/2,20,20);var icon=r.GetComponent<UiLineIcon>();if(!icon)icon=r.gameObject.AddComponent<UiLineIcon>();icon.glyph=glyph.Value;icon.color=ScreenUiTheme.Gold;icon.raycastTarget=false;icon.SetAllDirty();}
        if(key==null)return old;
        var cap=Image("ShortcutCap",node,width-37,(height-24)/2,29,24,new Color(.45f,.55f,.71f,.14f));var text=Text("Key",cap.transform,key,1,0,27,24,11,ScreenUiTheme.Muted,TextAlignmentOptions.Center);return text;
    }
    public static object Run()
    {
        if(Application.isPlaying)throw new Exception("Stop Play first");var rows=new System.Collections.Generic.List<object>();
        foreach(var path in new[]{"Assets/Scenes/BotMatch.unity","Assets/Scenes/MultiplayerCourt.unity"})
        {
            var scene=EditorSceneManager.OpenScene(path);var bot=UnityEngine.Object.FindAnyObjectByType<MatchHud>();var online=UnityEngine.Object.FindAnyObjectByType<MultiplayerCourtUI>();var root=bot?bot.transform:online.transform;var canvas=root.GetComponent<Canvas>();font=ScreenUiTheme.Current.font?ScreenUiTheme.Current.font:(bot?bot.scoreboard:online.score).font;
            var board=Board(root);var layout=root.GetComponent<MatchHudLayout>();if(!layout)layout=root.gameObject.AddComponent<MatchHudLayout>();layout.scoreboard=(RectTransform)board.transform;
            layout.settingsKey=Button(root,"Settings","CÀI ĐẶT",148,44,ArenaGlyph.Settings,"Esc");layout.settings=(RectTransform)root.Find("Settings");
            layout.helpKey=Button(root,"Help","F1",70,36,ArenaGlyph.Book);layout.help=(RectTransform)root.Find("Help");Button(root,"Touch","CẢM ỨNG",70,36);layout.touch=(RectTransform)root.Find("Touch");
            layout.timeoutKey=Button(root,"Timeout","HỘI Ý",140,36,ArenaGlyph.Pause,"T");layout.timeout=(RectTransform)root.Find("Timeout");layout.timeout.anchorMin=layout.timeout.anchorMax=layout.timeout.pivot=new Vector2(0,1);
            var status=bot?bot.status:online.status;layout.notice=Rect("RefereeNotice",root,18,234,304,48);var background=layout.notice.GetComponent<UnityEngine.UI.Image>();background.material=ScreenUiTheme.Current.quiet;background.color=Color.white;background.raycastTarget=false;
            status.rectTransform.anchorMin=Vector2.zero;status.rectTransform.anchorMax=Vector2.one;status.rectTransform.offsetMin=new Vector2(14,5);status.rectTransform.offsetMax=new Vector2(-14,-5);status.fontSize=13;status.alignment=TextAlignmentOptions.MidlineLeft;status.textWrappingMode=TextWrappingModes.Normal;status.overflowMode=TextOverflowModes.Ellipsis;status.raycastTarget=false;
            layout.technique=(RectTransform)(bot?bot.shotPanel:online.shotPanel).transform;layout.technique.anchorMin=layout.technique.anchorMax=new Vector2(.5f,0);layout.technique.pivot=new Vector2(.5f,0);layout.technique.sizeDelta=new Vector2(248,66);var techniqueSurface=layout.technique.GetComponent<UnityEngine.UI.Image>();techniqueSurface.material=ScreenUiTheme.Current.panel;techniqueSurface.color=Color.white;techniqueSurface.raycastTarget=false;
            var selection=bot?bot.selection:online.selection;selection.rectTransform.anchorMin=Vector2.zero;selection.rectTransform.anchorMax=Vector2.one;selection.rectTransform.offsetMin=new Vector2(12,5);selection.rectTransform.offsetMax=new Vector2(-12,-5);selection.fontSize=20;selection.color=ScreenUiTheme.Mint;selection.alignment=TextAlignmentOptions.Center;selection.raycastTarget=false;
            root.Find("ServeClock").gameObject.SetActive(false);root.Find("Difficulty").gameObject.SetActive(false);
            var dialog=bot?bot.modal:online.modal;var help=bot?bot.help:online.help;
            foreach(var panel in new[]{dialog,help}){var image=panel.GetComponent<UnityEngine.UI.Image>();if(image){image.material=ScreenUiTheme.Current.panel;image.color=Color.white;image.raycastTarget=true;}foreach(var label in panel.GetComponentsInChildren<TMP_Text>(true))label.raycastTarget=false;foreach(var button in panel.GetComponentsInChildren<UnityEngine.UI.Button>(true))ScreenUiTheme.ButtonStyle(button,button.name=="Continue");}
            if(bot){bot.broadcast=board;bot.layout=layout;EditorUtility.SetDirty(bot);}else{online.broadcast=board;online.layout=layout;board.SetTeams("ĐỘI BẠN","ĐỐI THỦ","TRỰC TUYẾN");EditorUtility.SetDirty(online);Button(root,"Leave","RỜI TRẬN",148,34,ArenaGlyph.Close);Button(root,"ReturnToRoom","VỀ PHÒNG",148,34,ArenaGlyph.Arrow);layout.leave=(RectTransform)root.Find("Leave");layout.returnToRoom=(RectTransform)root.Find("ReturnToRoom");}
            layout.ApplyInsets(0,0,0,0);EditorUtility.SetDirty(board);EditorUtility.SetDirty(layout);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            rows.Add(new{scene=path,board=board.name,dimensions=((RectTransform)board.transform).sizeDelta.ToString(),buttons=root.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length});
        }
        EditorSceneManager.OpenScene("Assets/Scenes/UI/ModeSelection.unity");return rows;
    }
}
