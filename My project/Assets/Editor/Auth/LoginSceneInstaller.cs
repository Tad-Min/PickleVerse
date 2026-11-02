using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PickleVerse.FrontEnd;
using PickleVerse.Settings;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PickleVerse.EditorTools
{
    public static class LoginSceneInstaller
    {
        public const string ScenePath = "Assets/Scenes/UI/Login.unity";
        public const string MenuPath = "Assets/Scenes/UI/Menu.unity";
        public static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/login-20261101"));
        static TMP_FontAsset font;
        static Material glass, quiet, gold;
        static readonly Color Text = new Color32(241,245,255,255), Muted = new Color32(164,181,207,255), Gold = new Color32(232,205,158,255);
        static readonly List<Selectable> navigation = new List<Selectable>();
        public static object Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play before installation.");
            Directory.CreateDirectory(Output);
            Directory.CreateDirectory("Assets/Resources/Auth");
            AssetDatabase.Refresh();
            AnimatedArtworkInstaller.ConfigureImports();
            font = ScreenUiTheme.Current.font;
            glass = AssetDatabase.LoadAssetAtPath<Material>("Assets/FrontEndContent/AnimeUI/Glass.mat");
            quiet = ScreenUiTheme.Current.quiet;
            gold = AssetDatabase.LoadAssetAtPath<Material>("Assets/FrontEndContent/AnimeUI/Gold Action.mat");
            var original = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var root = CanvasRoot("Login Screen");
                var art = Rect(root.transform, "Cinematic Stadium"); Stretch(art);
                var image = art.gameObject.AddComponent<RawImage>(); AnimatedArtworkInstaller.ConfigureLogin(image); image.raycastTarget = false;
                var aspect = art.gameObject.AddComponent<AspectRatioFitter>(); aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; aspect.aspectRatio = image.texture.width/(float)image.texture.height;
                var shade = Block(root.transform,"Atmosphere",new Color(.025f,.045f,.085f,.20f)); Stretch(shade);
                var safe = Rect(root.transform,"Safe Area"); Stretch(safe);
                var brand = Label(safe,"Wordmark","PICKLEVERSE",23,Gold); brand.characterSpacing = 3;
                Anchor(brand.rectTransform,new Vector2(0,1),new Vector2(34,-28),new Vector2(300,42)); brand.rectTransform.pivot = new Vector2(0,1);
                var edition = Label(safe,"Welcome","SÂN ĐẤU CỦA BẠN",11,Muted); edition.alignment = TextAlignmentOptions.Right;
                Anchor(edition.rectTransform,Vector2.one,new Vector2(-34,-28),new Vector2(210,42)); edition.rectTransform.pivot=Vector2.one;
                var footer = Label(safe,"Tagline","LUYỆN KỸ THUẬT. VÀO SÂN THI ĐẤU.",12,Gold); footer.characterSpacing = 1;
                Anchor(footer.rectTransform,Vector2.zero,new Vector2(34,25),new Vector2(450,26)); footer.rectTransform.pivot=Vector2.zero;
                var copyright = Label(safe,"Copyright","© PICKLEVERSE",10,Muted); copyright.alignment=TextAlignmentOptions.Right;
                Anchor(copyright.rectTransform,Vector2.right,new Vector2(-34,25),new Vector2(180,26)); copyright.rectTransform.pivot=Vector2.right;
                var view = Panel(safe,false); view.safeArea=safe;
                var cam = new GameObject("Login Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>(); cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.03f,.05f,.09f); cam.cullingMask=0;
                new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule)).GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                EditorSceneManager.SaveScene(scene,ScenePath);
            }
            finally { SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(scene,true); }
            var work = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive); SceneManager.SetActiveScene(work);
            try
            {
                var root = CanvasRoot("Menu Account"); root.GetComponent<Canvas>().sortingOrder=140;
                var menu = root.AddComponent<MenuAccountView>();
                var open = Button(root.transform,"Account Button","TÀI KHOẢN",false,140,36);
                Anchor((RectTransform)open.transform,new Vector2(.5f,1),new Vector2(0,-14),new Vector2(150,36)); ((RectTransform)open.transform).pivot=new Vector2(.5f,1);
                var cover = Block(root.transform,"Profile Cover",new Color(.015f,.025f,.05f,.72f),true); Stretch(cover);
                var safe = Rect(cover,"Safe Area"); Stretch(safe);
                menu.open=open; menu.panel=Panel(safe,true); menu.panel.safeArea=safe;
                // The cover and profile are a single scene-owned modal; no authentication fields in Menu.
                var panel = menu.panel; panel.transform.SetParent(cover,false);
                var card = panel.card; card.SetParent(panel.transform,false);
                panel.safeArea=(RectTransform)panel.transform;
                Object.DestroyImmediate(safe.gameObject);
                panel.gameObject.SetActive(false);
                // Panel's own scrim covers Menu and receives clicks while profile is open.
                var scrim = panel.gameObject.AddComponent<Image>(); scrim.color=new Color(.015f,.025f,.05f,.72f); scrim.raycastTarget=true;
                cover.GetComponent<Image>().enabled=false;
                PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/Auth/MenuAccount.prefab");
            }
            finally { SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(work,true); }
            var settings = EditorBuildSettings.globalScenes.Where(s=>s.path!=ScenePath).ToList(); settings.Insert(0,new EditorBuildSettingsScene(ScenePath,true)); EditorBuildSettings.globalScenes=settings.ToArray();
            var active = UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile();
            if(active && active.overrideGlobalScenes)
            {
                var profileScenes=active.scenes.Where(s=>s.path!=ScenePath).ToList();
                profileScenes.Insert(0,new EditorBuildSettingsScene(ScenePath,true)); active.scenes=profileScenes.ToArray();
                EditorUtility.SetDirty(active); AssetDatabase.SaveAssetIfDirty(active);
            }
            return new {scene=ScenePath,menuProfile="Assets/Resources/Auth/MenuAccount.prefab",startup=EditorBuildSettings.scenes[0].path};
        }
        static GameObject CanvasRoot(string name)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)); go.layer=5;
            var canvas=go.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=30;
            var scaler=go.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1280,800); scaler.matchWidthOrHeight=1;
            return go;
        }
        static AccountPanelView Panel(RectTransform parent,bool profile)
        {
            navigation.Clear();
            var host=Rect(parent,profile?"Profile Panel":"Authentication Panel"); Stretch(host);
            var view=host.gameObject.AddComponent<AccountPanelView>(); view.profileOnly=profile;
            var canvas=host.gameObject.AddComponent<Canvas>(); canvas.overrideSorting=true; canvas.sortingOrder=150;
            host.gameObject.AddComponent<GraphicRaycaster>();
            var card=Block(host,"Account Card",Color.white,true); card.GetComponent<Image>().material=glass;
            Anchor(card,new Vector2(.5f,.5f),Vector2.zero,new Vector2(444,profile?460:615)); view.card=card;
            view.form=card.gameObject.AddComponent<CanvasGroup>();
            var layout=card.gameObject.AddComponent<VerticalLayoutGroup>(); layout.padding=new RectOffset(32,32,26,26); layout.spacing=8; layout.childControlWidth=layout.childControlHeight=true; layout.childForceExpandWidth=true; layout.childForceExpandHeight=false;
            var identity=Label(card,"Identity","P I C K L E V E R S E   /   TÀI KHOẢN",11,Gold); Height(identity.gameObject,26);
            view.title=Label(card,"Title",profile?"Tài khoản của bạn":"Chào mừng trở lại",27,Text); Height(view.title.gameObject,42);
            view.subtitle=Label(card,"Subtitle",profile?"Hồ sơ PickleVerse":"Đăng nhập để tiếp tục hành trình PickleVerse.",14,Muted); view.subtitle.alignment=TextAlignmentOptions.TopLeft; Height(view.subtitle.gameObject,40);
            var line=Block(card,"Header Rule",new Color(Gold.r,Gold.g,Gold.b,.24f)); Height(line.gameObject,1);
            if (!profile)
            {
                view.email=Field(card,"Email","ĐỊA CHỈ EMAIL","ban@example.com",254,false,out _); view.email.keyboardType=TouchScreenKeyboardType.EmailAddress;
                view.password=Field(card,"Password","MẬT KHẨU","Nhập mật khẩu",128,true,out view.passwordVisibility);
                view.confirmPassword=Field(card,"Confirm Password","NHẬP LẠI MẬT KHẨU","Ít nhất 6 ký tự",128,true,out view.confirmVisibility);
                view.otp=Field(card,"OTP","MÃ XÁC THỰC","Nhập mã 6 chữ số",6,false,out _); view.otp.characterValidation=TMP_InputField.CharacterValidation.Digit; view.otp.keyboardType=TouchScreenKeyboardType.NumberPad;
                view.confirmPassword.transform.parent.gameObject.SetActive(false); view.otp.transform.parent.gameObject.SetActive(false);
            }
            else view.playerName=Field(card,"Player Name","TÊN NGƯỜI CHƠI","Từ 3 đến 18 ký tự",18,false,out _);
            view.feedback=Label(card,"Feedback","",13,ScreenUiTheme.Mint); view.feedback.richText=false; view.feedback.alignment=TextAlignmentOptions.TopLeft; Height(view.feedback.gameObject,42);
            view.submit=Button(card,"Submit",profile?"LƯU THAY ĐỔI":"ĐĂNG NHẬP",true,0,48); view.submitLabel=view.submit.GetComponentInChildren<TMP_Text>();
            if (!profile)
            {
                view.verifyLink=Button(card,"Verify Email","Bạn cần xác thực email?",false,0,28,true);
                view.resend=Button(card,"Resend OTP","Gửi lại mã xác thực",false,0,36,true); view.resendLabel=view.resend.GetComponentInChildren<TMP_Text>(); view.resend.gameObject.SetActive(false);
                var row=Rect(card,"Switch Form"); Height(row.gameObject,44);
                var horizontal=row.gameObject.AddComponent<HorizontalLayoutGroup>(); horizontal.spacing=6; horizontal.childAlignment=TextAnchor.MiddleCenter; horizontal.childControlWidth=horizontal.childControlHeight=true; horizontal.childForceExpandHeight=false; horizontal.childForceExpandWidth=false;
                view.switchHint=Label(row,"Hint","Chưa có tài khoản?",14,Muted); Width(view.switchHint.gameObject,170); Height(view.switchHint.gameObject,38); view.switchHint.alignment=TextAlignmentOptions.Right;
                view.switchPage=Button(row,"Switch Page","Đăng ký",false,88,38,true); view.switchLabel=view.switchPage.GetComponentInChildren<TMP_Text>();
            }
            view.logout=Button(card,"Logout",profile?"ĐĂNG XUẤT":"DÙNG TÀI KHOẢN KHÁC",false,0,38,true); view.logout.gameObject.SetActive(profile);
            if (profile) view.close=Button(card,"Close Profile","TRỞ VỀ SẢNH",false,0,32,true);
            view.navigation=navigation.ToArray(); view.FitContent(); return view;
        }
        static TMP_InputField Field(Transform parent,string name,string caption,string hint,int limit,bool secret,out Button reveal)
        {
            var group=Rect(parent,name+" Field"); Height(group.gameObject,72);
            var label=Label(group,"Caption",caption,11,Muted); label.characterSpacing=1;
            label.rectTransform.anchorMin=new Vector2(0,1); label.rectTransform.anchorMax=Vector2.one; label.rectTransform.pivot=Vector2.up; label.rectTransform.sizeDelta=new Vector2(0,20); label.rectTransform.anchoredPosition=Vector2.zero;
            var block=Block(group,name,Color.white,true); block.GetComponent<Image>().material=quiet;
            block.anchorMin=Vector2.zero; block.anchorMax=new Vector2(1,0); block.pivot=Vector2.zero; block.sizeDelta=new Vector2(0,46); block.anchoredPosition=new Vector2(0,2);
            var field=block.gameObject.AddComponent<TMP_InputField>(); field.targetGraphic=block.GetComponent<Image>();
            var viewport=Rect(block,"Viewport"); Stretch(viewport); viewport.offsetMin=new Vector2(14,5); viewport.offsetMax=new Vector2(secret?-62:-14,-5); viewport.gameObject.AddComponent<RectMask2D>();
            var value=Label(viewport,"Value","",16,Text); Stretch(value.rectTransform); value.richText=false;
            var placeholder=Label(viewport,"Placeholder",hint,15,new Color(Muted.r,Muted.g,Muted.b,.65f)); Stretch(placeholder.rectTransform);
            field.textViewport=viewport; field.textComponent=value; field.placeholder=placeholder; field.characterLimit=limit; field.lineType=TMP_InputField.LineType.SingleLine;
            field.contentType=secret?TMP_InputField.ContentType.Password:TMP_InputField.ContentType.Standard; field.caretColor=Gold; field.customCaretColor=true; field.caretWidth=2; field.selectionColor=new Color(Gold.r,Gold.g,Gold.b,.25f);
            var focus=Block(block,"Focus Indicator",Gold); focus.anchorMin=Vector2.zero; focus.anchorMax=Vector2.right; focus.offsetMin=Vector2.zero; focus.offsetMax=new Vector2(0,2); focus.gameObject.SetActive(false); block.gameObject.AddComponent<AccountInputFocus>().indicator=focus.gameObject;
            navigation.Add(field); reveal=null;
            if(secret)
            {
                reveal=Button(block,"Password Visibility","HIỆN",false,52,40,true);
                Object.DestroyImmediate(reveal.GetComponent<LayoutElement>());
                Anchor((RectTransform)reveal.transform,new Vector2(1,.5f),new Vector2(-4,0),new Vector2(52,40)); ((RectTransform)reveal.transform).pivot=new Vector2(1,.5f);
                reveal.GetComponentInChildren<TMP_Text>().fontSize=10;
            }
            return field;
        }
        static Button Button(Transform parent,string name,string text,bool primary,float width,float height,bool link=false)
        {
            var rect=Block(parent,name,Color.white,true); rect.GetComponent<Image>().material=primary?gold:quiet;
            if (link) rect.GetComponent<Image>().color=new Color(1,1,1,.01f);
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=rect.GetComponent<Image>();
            var colors=button.colors; colors.highlightedColor=new Color(1.17f,1.17f,1.17f); colors.selectedColor=new Color(1.25f,1.25f,1.25f); colors.pressedColor=new Color(.72f,.8f,.9f); colors.disabledColor=new Color(.55f,.55f,.55f,.6f); colors.fadeDuration=.1f; button.colors=colors;
            var label=Label(rect,"Label",text,primary?15:14,primary?ScreenUiTheme.Ink:Gold); Stretch(label.rectTransform); label.alignment=TextAlignmentOptions.Center;
            var focus=Block(rect,"Focus Indicator",primary?ScreenUiTheme.Ink:Gold); focus.anchorMin=new Vector2(.08f,0); focus.anchorMax=new Vector2(.92f,0); focus.offsetMin=Vector2.zero; focus.offsetMax=new Vector2(0,2); focus.gameObject.SetActive(false); rect.gameObject.AddComponent<AccountInputFocus>().indicator=focus.gameObject;
            Height(rect.gameObject,height); if(width>0) Width(rect.gameObject,width); navigation.Add(button); return button;
        }
        static TMP_Text Label(Transform parent,string name,string text,float size,Color color)
        {
            var rect=Rect(parent,name); var label=rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font=font; label.fontSize=size; label.text=text; label.color=color; label.alignment=TextAlignmentOptions.MidlineLeft; label.raycastTarget=false; return label;
        }
        static RectTransform Rect(Transform parent,string name) { var go=new GameObject(name,typeof(RectTransform)); go.layer=5; var r=(RectTransform)go.transform; r.SetParent(parent,false); return r; }
        static RectTransform Block(Transform parent,string name,Color color,bool raycast=false) { var r=Rect(parent,name); var i=r.gameObject.AddComponent<Image>(); i.color=color; i.raycastTarget=raycast; return r; }
        static void Height(GameObject go,float value) { var l=go.GetComponent<LayoutElement>()??go.AddComponent<LayoutElement>(); l.minHeight=l.preferredHeight=value; l.flexibleHeight=0; }
        static void Width(GameObject go,float value) { var l=go.GetComponent<LayoutElement>()??go.AddComponent<LayoutElement>(); l.minWidth=l.preferredWidth=value; l.flexibleWidth=0; }
        static void Stretch(RectTransform r) { r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.offsetMin=r.offsetMax=Vector2.zero; }
        static void Anchor(RectTransform r,Vector2 anchor,Vector2 position,Vector2 size) { r.anchorMin=r.anchorMax=anchor; r.pivot=new Vector2(.5f,.5f); r.anchoredPosition=position; r.sizeDelta=size; }
    }
}
