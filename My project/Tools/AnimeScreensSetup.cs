using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using PickleVerse.Settings;
using PickleVerse.FrontEnd;
using PickleVerse.Multiplayer.UI;
using PickleVerse.Shop;
using PickleVerse.LocalMatch;

// Native Editor authoring, preserving existing scene objects, callbacks and service bindings.
public static class AnimeScreensSetup
{
 const string Folder="Assets/FrontEndContent/ScreenRefresh";
 static ScreenUiTheme theme;
 static Color White=>ScreenUiTheme.Text;static Color Muted=>ScreenUiTheme.Muted;static Color Gold=>ScreenUiTheme.Gold;static Color Mint=>ScreenUiTheme.Mint;
 static TMP_FontAsset Font=>theme.font;
 public static object Run()
 {
  if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
  if(SceneManager.GetActiveScene().isDirty)throw new Exception("Save or discard the current scene before authoring");
  string original=SceneManager.GetActiveScene().path;
  Theme();Mode();Settings();Multiplayer();Products();Shop(false);Shop(true);Tutorial();Replay();
  AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(original);
  return new{scenes=7,theme=Folder,newTextures=0,newAudio=0,privateRooms="1v1 / 2v2 + code invite/join"};
 }
 static void Theme()
 {
  if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/FrontEndContent","ScreenRefresh");
  theme=AssetDatabase.LoadAssetAtPath<ScreenUiTheme>("Assets/Resources/ScreenUiTheme.asset");
  if(!theme){theme=ScriptableObject.CreateInstance<ScreenUiTheme>();AssetDatabase.CreateAsset(theme,"Assets/Resources/ScreenUiTheme.asset");}
  theme.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Menu Vietnamese Bold.asset");
  theme.panel=Mat("Obsidian Glass","23334F","111C31","829EB9",.55f);
  theme.quiet=Mat("Quiet Glass","17253D","0C172A","7890AF",.25f);
  theme.action=Mat("Champagne Action","FAEDD2","D7BC87","FFEDC2",.95f);
  theme.background=Mat("Midnight Background","172840","080F1F","273A55",.1f);
  EditorUtility.SetDirty(theme);AssetDatabase.SaveAssetIfDirty(theme);
 }
 public static object Verify()
 {
  if(EditorApplication.isPlaying)throw new Exception("Stop Play first");int missing=0,scenes=0;
  string[] paths={"Assets/Scenes/UI/ModeSelection.unity","Assets/Scenes/UI/Settings.unity","Assets/Scenes/UI/PlayMultiplayer.unity","Assets/Scenes/Shop.unity","Assets/Scenes/Collection.unity","Assets/Scenes/Tutorial.unity","Assets/Scenes/Replay.unity"};
  foreach(var path in paths){var scene=EditorSceneManager.OpenScene(path);scenes++;foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))missing+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
   if(scene.name=="ModeSelection"){var mode=Find<ModeSelectionMenu>(scene);if(!mode.roomPage||!mode.roomJoinCode||mode.modePage.GetComponentsInChildren<Button>(true).Length!=4)throw new Exception("Mode room bindings");var room=mode.modePage.transform.Find("PrivateRoomMode").GetComponent<Button>();if(room.onClick.GetPersistentMethodName(0)!="OpenRooms")throw new Exception("Room entry callback");}
   if(scene.name=="PlayMultiplayer"){var room=Find<PlayMultiplayerPresenter>(scene);if(room.custom||!room.invite||!room.inviteDialog||!room.retry||room.customSlots.Length!=4)throw new Exception("Room bindings");}
   if(scene.name=="Settings"){var settings=Find<SettingsScreen>(scene);if(settings.pages.Length!=4||settings.sliders.Length!=8||settings.bindingButtons.Length!=15)throw new Exception("Settings binding counts");}
  }
  EditorSceneManager.OpenScene("Assets/Scenes/UI/ModeSelection.unity");if(missing>0)throw new Exception("Missing scripts: "+missing);return new{scenes,missingScripts=missing,roomEntryWired=true,customTabRemoved=true,settingsBindingsPreserved=true};
 }
 static Material Mat(string name,string top,string bottom,string edge,float alpha)
 {string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("PickleVerse/UI/Arena Glass")){name=name};AssetDatabase.CreateAsset(m,path);}m.SetColor("_Top",Hex(top));m.SetColor("_Bottom",Hex(bottom));var border=Hex(edge);border.a=alpha;m.SetColor("_Border",border);m.SetFloat("_Cut",.025f);EditorUtility.SetDirty(m);return m;}
 static Scene Open(string path){var s=EditorSceneManager.OpenScene(path);string dest="../UnityWork/AnimeScreens20261016/Backup/"+s.name+".unity";if(!File.Exists(dest))EditorSceneManager.SaveScene(s,dest,true);return s;}
 static void Save(Scene scene){EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
 static T Find<T>(Scene scene)where T:Component=>scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).Single();
 static void Mode()
 {
  var s=Open("Assets/Scenes/UI/ModeSelection.unity");var ui=Find<ModeSelectionMenu>(s);var root=ui.safeArea;
  Backdrop(root.parent);StyleAll(ui.transform);Header(root,"CHỌN CHẾ ĐỘ",ui.title);
  Text(ui.title,"Sẵn sàng vào sân?",.055f,.785f,.89f,.865f,40,White);Text(ui.subtitle,null,.055f,.735f,.90f,.78f,17,Muted);
  Place(ui.breadcrumb.rectTransform,.20f,.91f,.72f,.95f);ui.breadcrumb.fontSize=13;ui.breadcrumb.color=Gold;
  Hide(root,"TopAccent");Hide(root,"FooterBrand");Hide(root,"FooterLine");
  Place((RectTransform)ui.modePage.transform,.055f,.245f,.945f,.71f);var row=ui.modePage.GetComponent<HorizontalLayoutGroup>();row.spacing=16;
  var extra=root.Find("ModePage/PrivateRoomMode");Button room;
  if(!extra){room=UnityEngine.Object.Instantiate(root.Find("ModePage/MultiplayerMode").gameObject,ui.modePage.transform).GetComponent<Button>();room.name="PrivateRoomMode";room.onClick=new Button.ButtonClickedEvent();UnityEventTools.AddPersistentListener(room.onClick,ui.OpenRooms);}else room=extra.GetComponent<Button>();
  string[] names={"BotMode","MultiplayerMode","TrainingMode","PrivateRoomMode"};
  string[] tags={"01  /  ĐẤU ĐƠN","02  /  TRỰC TUYẾN","03  /  HỌC VIỆN","04  /  CÙNG BẠN BÈ"};
  string[] titles={"Đấu với bot","Đấu online","Luyện tập","Tạo phòng"};
  string[] descriptions={"Chọn độ khó phù hợp.\nLàm chủ nhịp đấu.","Đấu đơn hoặc đánh đôi.\nTìm đối thủ cùng Elo.","Luyện từng kỹ thuật.\nTiến bộ mỗi ngày.","Phòng riêng 1v1 / 2v2.\nMời bạn bè bằng mã."};
  ArenaGlyph[] glyphs={ArenaGlyph.Play,ArenaGlyph.Star,ArenaGlyph.Book,ArenaGlyph.Collection};
  for(int i=0;i<4;i++)
  {
   var card=root.Find("ModePage/"+names[i]);var button=card.GetComponent<Button>();button.interactable=true;ButtonStyle(button);
   foreach(var outline in card.GetComponents<Outline>())UnityEngine.Object.DestroyImmediate(outline);
   Text(card.Find("ModeTag").GetComponent<TMP_Text>(),tags[i],.075f,.43f,.94f,.49f,11,Gold);
   Text(card.Find("ModeTitle").GetComponent<TMP_Text>(),titles[i],.075f,.30f,.95f,.42f,26,White);
   Text(card.Find("Description").GetComponent<TMP_Text>(),descriptions[i],.075f,.12f,.94f,.29f,16,Muted);
   foreach(var n in new[]{"Available","ComingSoon","Action","Pending"})Hide(card,n);
   var frame=card.Find("CourtPreviewFrame");if(frame)Place((RectTransform)frame,.01f,.52f,.99f,.99f);
   var illustration=card.Find("Illustration");if(illustration){Place((RectTransform)illustration,.035f,.53f,.965f,.97f);var im=illustration.GetComponent<Image>();im.color=new Color(.10f,.18f,.28f,1);}
   if(i==2&&illustration){Hide(illustration,"Court/Target");Glyph("Training Target",illustration,ArenaGlyph.Star,Gold,.40f,.36f,.60f,.68f);}
   Glyph("Mode Symbol",card,glyphs[i],Gold,.075f,.84f,.19f,.94f);
   Label("Enter",card,"→",.82f,.025f,.94f,.12f,23,Mint,TextAlignmentOptions.Right);
   Block("Card Accent",card,.075f,.045f,.34f,.05f,null,i==0?Gold:Mint);
  }
  ButtonStyle(ui.startButton,true);Place((RectTransform)ui.botPage.transform,.055f,.16f,.945f,.71f);
  Place((RectTransform)ui.tutorialButton.transform,.055f,.125f,.945f,.21f);ButtonStyle(ui.tutorialButton);
  Text(root.Find("Footer").GetComponent<TMP_Text>(),"CHỌN BẰNG CHUỘT HOẶC CẢM ỨNG    •    ESC  QUAY LẠI",.055f,.045f,.945f,.083f,11,Muted);
  BuildRoomPage(ui);Save(s);
 }
 static void BuildRoomPage(ModeSelectionMenu ui)
 {
  var p=Rect("Private Room Setup",ui.safeArea,.055f,.12f,.945f,.71f);ui.roomPage=p.gameObject;Add<CanvasGroup>(p);var previousReveal=p.GetComponent<UiPanelReveal>();if(previousReveal)UnityEngine.Object.DestroyImmediate(previousReveal);
  string[] heads={"1  v  1","2  v  2"};string[] sub={"ĐẤU ĐƠN","ĐẤU ĐÔI"};
  for(int i=0;i<2;i++)
  {
   var b=NewButton("Create "+(i==0?"Singles":"Doubles"),p,.015f+i*.505f,.27f,.485f+i*.505f,1,true);
   Label("Eyebrow",b.transform,sub[i],.07f,.76f,.93f,.90f,13,Gold);
   Label("Versus",b.transform,heads[i],.12f,.39f,.88f,.75f,56,White,TextAlignmentOptions.Center);
   Label("Description",b.transform,i==0?"Một đối thủ. Một trận đấu của riêng bạn.":"Hai người mỗi đội. Phối hợp để chiến thắng.",.07f,.24f,.93f,.38f,16,Muted,TextAlignmentOptions.Center);
   Label("Action",b.transform,i==0?"TẠO PHÒNG 1V1   →":"TẠO PHÒNG 2V2   →",.07f,.07f,.93f,.23f,18,Mint,TextAlignmentOptions.Center);
   if(i==0)UnityEventTools.AddPersistentListener(b.onClick,ui.CreateSinglesRoom);else UnityEventTools.AddPersistentListener(b.onClick,ui.CreateDoublesRoom);
  }
  var join=Block("Join Room",p,0,0,1,.21f,theme.quiet);Label("Heading",join,"ĐÃ CÓ MÃ PHÒNG?",.025f,.25f,.30f,.78f,15,Gold);
  ui.roomJoinCode=Input("Room Code",join,"Nhập mã phòng",.34f,.15f,.72f,.85f);
  var enter=NewButton("Join",join,.745f,.15f,.98f,.85f);Label("Label",enter.transform,"THAM GIA  →",0,0,1,1,16,ScreenUiTheme.Ink,TextAlignmentOptions.Center);UnityEventTools.AddPersistentListener(enter.onClick,ui.JoinPrivateRoom);
  p.gameObject.SetActive(false);
 }
 static void Settings()
 {
  var s=Open("Assets/Scenes/UI/Settings.unity");var ui=Find<SettingsScreen>(s);StyleAll(ui.transform);var root=ui.safeArea;
  Glass(ui.transform.Find("Dim"),theme.background,true);
  var body=Block("Settings Surface",root,.26f,.20f,.965f,.80f,theme.quiet);body.SetAsFirstSibling();
  Text(root.Find("Title").GetComponent<TMP_Text>(),"Cài đặt",.045f,.885f,.70f,.96f,36,White);
  Text(ui.contextLabel,null,.045f,.83f,.82f,.875f,14,Muted);
  Place((RectTransform)root.Find("Tabs"),.045f,.20f,.235f,.80f);
  ArenaGlyph[] glyphs={ArenaGlyph.Star,ArenaGlyph.Music,ArenaGlyph.Orbit,ArenaGlyph.Settings};
  for(int i=0;i<4;i++)
  {
   var tab=ui.tabs[i];Place((RectTransform)tab.transform,0,.79f-i*.205f,1,.97f-i*.205f);var text=tab.GetComponentInChildren<TMP_Text>();Text(text,null,.25f,0,.95f,1,18,White);
   Glyph("Tab Icon",tab.transform,glyphs[i],Gold,.045f,.23f,.20f,.77f);Block("Selection Mark",tab.transform,0,.2f,.015f,.80f,null,Gold);
   var page=ui.pages[i].transform as RectTransform;Place(page,.29f,.225f,.935f,.775f);Reveal(page);
   var heading=page.Find("Heading");if(heading)heading.GetComponent<TMP_Text>().color=Gold;
  }
  ButtonStyle(ui.applyButton,true);ButtonStyle(root.Find("Close").GetComponent<Button>());
  foreach(var slider in ui.sliders){var fill=slider.fillRect?slider.fillRect.GetComponent<Image>():null;if(fill)fill.color=Mint;var handle=slider.handleRect?slider.handleRect.GetComponent<Image>():null;if(handle)handle.color=Gold;}
  foreach(var overlay in new[]{ui.captureOverlay,ui.confirmOverlay})
  {Glass(overlay.transform,theme.panel,true);Reveal(overlay.transform);}
  Label("Preferences Index",root,"PREFERENCES  /  PICKLEVERSE",.045f,.02f,.42f,.045f,10,Muted);
  Save(s);
 }
 static void Multiplayer()
 {
  var s=Open("Assets/Scenes/UI/PlayMultiplayer.unity");var ui=Find<PlayMultiplayerPresenter>(s);var canvas=ui.GetComponent<Canvas>();StyleAll(ui.transform);canvas.GetComponent<CanvasScaler>().matchWidthOrHeight=1;
  Glass(ui.transform.Find("RoomBackground"),theme.background);var safe=Rect("Room Safe Area",ui.transform,0,0,1,1);Add<UiSafeArea>(safe);
  foreach(var child in ui.transform.Cast<Transform>().Where(t=>t!=safe&&t.name!="RoomBackground").ToArray())child.SetParent(safe,false);
  var nav=safe.Find("NavPanel");var team=safe.Find("TeamPanel");Glass(nav,theme.quiet);Glass(team,theme.quiet);Hide(nav,"PanelBorder");Hide(team,"PanelBorder");
  Place((RectTransform)nav,.035f,.18f,.265f,.82f);Place((RectTransform)team,.29f,.18f,.965f,.82f);
  Place((RectTransform)ui.back.transform,.035f,.89f,.18f,.96f);Text(ui.back.GetComponentInChildren<TMP_Text>(),"‹  CHỌN CHẾ ĐỘ",0,0,1,1,16,White);
  Text(ui.roomType,null,.30f,.89f,.86f,.96f,27,White);Text(ui.matchTime,null,.87f,.89f,.965f,.96f,20,Mint);
  Place(ui.joinCode.GetComponent<RectTransform>(),.07f,.70f,.93f,.80f);Place((RectTransform)ui.join.transform,.07f,.57f,.93f,.67f);ButtonStyle(ui.join);
  ui.join.GetComponentInChildren<TMP_Text>().text="THAM GIA BẰNG MÃ";
  Place((RectTransform)ui.singles.transform,.07f,.40f,.93f,.51f);Place((RectTransform)ui.doubles.transform,.07f,.26f,.93f,.37f);
  foreach(var b in new[]{ui.singles,ui.doubles})b.GetComponentInChildren<TMP_Text>().fontSize=17;
  ui.singles.GetComponentInChildren<TMP_Text>().text="ĐẤU ĐƠN  1v1";ui.doubles.GetComponentInChildren<TMP_Text>().text="ĐẤU ĐÔI  2v2";
  if(ui.custom){UnityEngine.Object.DestroyImmediate(ui.custom.gameObject);ui.custom=null;}
  Text(ui.roomCode,null,.07f,.075f,.70f,.23f,16,Gold);Place((RectTransform)ui.copy.transform,.73f,.10f,.93f,.20f);ui.copy.GetComponentInChildren<TMP_Text>().text="COPY";ui.copy.GetComponentInChildren<TMP_Text>().fontSize=11;
  var heading=nav.Find("RoomCodeHeading");if(heading)Text(heading.GetComponent<TMP_Text>(),"KẾT NỐI BẠN BÈ",.07f,.85f,.93f,.94f,15,Gold);Hide(nav,"RoomCodeIcon");
  foreach(var p in new[]{ui.singlesPanel,ui.doublesPanel,ui.customPanel}){p.GetComponent<Image>().color=Color.clear;Reveal(p.transform);}
  foreach(var p in new[]{ui.singlesPanel.transform,ui.doublesPanel.transform})
  {
   var cards=p.Cast<Transform>().Where(t=>t.GetComponent<Image>()&&t.GetComponentInChildren<TMP_Text>()&&t.name.Contains("Player")).ToArray();
   for(int i=0;i<cards.Length;i++)Place((RectTransform)cards[i],cards.Length==1?.32f:.15f+i*.37f,.13f,cards.Length==1?.68f:.48f+i*.37f,.90f);
  }
  var custom=ui.customPanel.transform;var a=custom.Find("TeamAPanel");var bteam=custom.Find("TeamBPanel");Place((RectTransform)a,.02f,.08f,.46f,.98f);Place((RectTransform)bteam,.54f,.08f,.98f,.98f);a.GetComponent<Image>().color=Color.clear;bteam.GetComponent<Image>().color=Color.clear;
  foreach(var t in new[]{a,bteam}){var h=t.Find("TeamHeading");if(h)Text(h.GetComponent<TMP_Text>(),t==a?"ĐỘI A":"ĐỘI B",0,.88f,1,.97f,17,t==a?Mint:Gold,TextAlignmentOptions.Center);}
  Label("Versus",custom,"VS",.46f,.42f,.54f,.64f,34,Gold,TextAlignmentOptions.Center);Place((RectTransform)ui.swap.transform,.36f,.015f,.64f,.09f);ui.swap.GetComponentInChildren<TMP_Text>().fontSize=13;
  foreach(var slot in new[]{ui.singleSlot}.Concat(ui.duoSlots).Concat(ui.customSlots))StyleSlot(slot);
  Place((RectTransform)ui.play.transform,.75f,.045f,.965f,.13f);ButtonStyle(ui.play,true);
  ui.roomHint=ui.navigationError?ui.navigationError:ui.roomHint;Text(ui.roomHint,"",.035f,.015f,.71f,.085f,15,Muted);ui.navigationError=null;
  ui.memberCount=Label("Room Capacity",safe,"",.30f,.13f,.72f,.17f,12,Gold);
  ui.invite=NewButton("Invite Friends",safe,.52f,.045f,.73f,.13f,true);Label("Label",ui.invite.transform,"MỜI BẠN BÈ",0,0,1,1,16,White,TextAlignmentOptions.Center);
  ui.retry=NewButton("Retry Connection",safe,.30f,.045f,.50f,.13f,true);Label("Label",ui.retry.transform,"THỬ KẾT NỐI LẠI",0,0,1,1,14,White,TextAlignmentOptions.Center);ui.retry.gameObject.SetActive(false);
  BuildInvite(ui,safe);Save(s);
 }
 static void StyleSlot(RoomPlayerSlot slot)
 {
  if(slot==null||!slot.name)return;var card=slot.name.transform.parent;Glass(card,theme.panel);Hide(card,"CardBorder");Hide(card,"AvatarBackground");Hide(card,"AvatarBorder");
  var art=card.Find("CourtArtwork");if(art){Place((RectTransform)art,.02f,.38f,.98f,.98f);var i=art.GetComponent<Image>();i.color=new Color(.45f,.55f,.70f,.25f);}
  var portrait=card.Find("ImgIcon");if(portrait){Place((RectTransform)portrait,.15f,.40f,.85f,.91f);foreach(var image in portrait.GetComponentsInChildren<Image>(true))image.preserveAspect=true;}
  Text(slot.name,null,.06f,.19f,.94f,.36f,17,White,TextAlignmentOptions.Center);slot.name.enableAutoSizing=true;slot.name.fontSizeMin=12;slot.name.fontSizeMax=17;
  if(slot.kick){Place((RectTransform)slot.kick.transform,.78f,.85f,.96f,.98f);ButtonStyle(slot.kick);}
  var invite=card.Find("BtnInvite");if(!invite){var button=NewButton("BtnInvite",card,.10f,.04f,.90f,.17f,true);Label("Label",button.transform,"+  MỜI BẠN",0,0,1,1,12,Mint,TextAlignmentOptions.Center);}else{Place((RectTransform)invite,.10f,.04f,.90f,.17f);ButtonStyle(invite.GetComponent<Button>());}
 }
 static void BuildInvite(PlayMultiplayerPresenter ui,Transform parent)
 {
  var overlay=Block("Invite Friends Dialog",parent,0,0,1,1,null,new Color(.015f,.025f,.05f,.92f));overlay.GetComponent<Image>().raycastTarget=true;ui.inviteDialog=overlay.gameObject;
  var p=Block("Invite Card",overlay,.24f,.22f,.76f,.79f,theme.panel);Reveal(p);
  Glyph("Invite Icon",p,ArenaGlyph.Collection,Gold,.43f,.78f,.57f,.95f);Label("Heading",p,"CÙNG BẠN BÈ VÀO SÂN",.05f,.65f,.95f,.79f,24,White,TextAlignmentOptions.Center);
  ui.inviteCode=Label("Room Code",p,"",.05f,.41f,.95f,.65f,38,Gold,TextAlignmentOptions.Center);
  Label("Guide",p,"Sao chép mã và chia sẻ cho bạn bè.\nBạn bè chọn Tạo phòng → nhập mã → Tham gia.",.075f,.23f,.925f,.40f,16,Muted,TextAlignmentOptions.Center);
  ui.inviteClose=NewButton("Close",p,.065f,.065f,.47f,.20f,true);Label("Label",ui.inviteClose.transform,"ĐÓNG",0,0,1,1,16,White,TextAlignmentOptions.Center);
  ui.inviteCopy=NewButton("Copy Code",p,.50f,.065f,.935f,.20f);Label("Label",ui.inviteCopy.transform,"SAO CHÉP MÃ",0,0,1,1,16,ScreenUiTheme.Ink,TextAlignmentOptions.Center);overlay.gameObject.SetActive(false);
 }
 static void Products()
 {
  string path="Assets/ShopContent/Prefabs/ProductTile.prefab";var root=PrefabUtility.LoadPrefabContents(path);
  try{StyleAll(root.transform);var card=root.GetComponent<ShopProductCard>();Glass(root.transform,theme.panel);ButtonStyle(card.button);Text(card.title,null,.06f,.22f,.94f,.34f,24,White);Text(card.collection,null,.06f,.33f,.94f,.40f,11,Gold);Text(card.price,null,.06f,.12f,.94f,.22f,12,Muted);Text(card.action,null,.06f,.025f,.94f,.10f,11,Mint);Place(card.thumbnail.rectTransform,.05f,.41f,.95f,.98f);card.thumbnail.preserveAspect=true;Block("Collection Corner",root.transform,.055f,.91f,.22f,.92f,null,Gold);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
 }
 static void Shop(bool collection)
 {
  var s=Open(collection?"Assets/Scenes/Collection.unity":"Assets/Scenes/Shop.unity");var ui=Find<ShopScreen>(s);StyleAll(ui.transform);var browser=ui.browser;var root=ui.safeArea;
  var bg=Block("Browse Backdrop",browser.browseRoot.transform,0,0,1,.895f,theme.background);bg.SetAsFirstSibling();Add<RectMask2D>(bg);Decorate(bg);
  Text(root.Find("Brand").GetComponent<TMP_Text>(),collection?"Bộ sưu tập":"Cửa hàng",.215f,.91f,.57f,.98f,29,White);
  var hero=browser.heroButton.transform;Glass(hero,theme.panel);ButtonStyle(browser.heroButton);Glyph("Hero Halo",hero,ArenaGlyph.RecordFrame,new Color(.6f,.75f,.85f,.14f),.51f,.06f,.98f,.98f).transform.SetAsFirstSibling();
  Text(browser.heroTitle,null,.045f,.48f,.52f,.76f,40,White);Text(browser.heroCollection,null,.045f,.76f,.51f,.88f,12,Gold);Text(browser.heroDescription,null,.045f,.25f,.47f,.48f,17,Muted);Text(browser.heroPrice,null,.045f,.08f,.49f,.24f,18,Gold);
  browser.heroImage.preserveAspect=true;Place(browser.heroImage.rectTransform,.54f,.05f,.94f,.96f);
  ButtonStyle(browser.signatureTab);ButtonStyle(browser.allTab);browser.signatureTab.GetComponentInChildren<TMP_Text>().text="SIGNATURE / THỊNH HÀNH";
  if(!collection){Place((RectTransform)browser.signatureTab.transform,.04f,.80f,.31f,.87f);Place((RectTransform)browser.allTab.transform,.325f,.80f,.59f,.87f);}
  var detail=browser.detailRoot.transform;var rail=Block("Details Glass",detail,.735f,.30f,.98f,.73f,theme.quiet);rail.SetAsFirstSibling();
  Text(ui.title,null,.755f,.55f,.96f,.65f,32,White);ui.title.enableAutoSizing=true;ui.title.fontSizeMin=23;ui.title.fontSizeMax=32;
  Text(ui.collection,null,.755f,.65f,.96f,.70f,12,Gold);Text(ui.description,null,.755f,.34f,.96f,.54f,17,Muted);
  var underline=Block("Rarity Accent",detail,.755f,.315f,.83f,.318f,null,Gold);ButtonStyle(ui.tokenpadBuy,true);ButtonStyle(ui.eliteBuy,true);
  ui.tokenpadPrice.fontSize=15;ui.elitePrice.fontSize=15;
  ui.status.fontSize=11;
  var panel=ui.confirmation.transform.Find("Panel");Glass(panel,theme.panel);Reveal(panel);
  ButtonStyle(ui.confirmBuy,true);ButtonStyle(ui.cancelBuy);
  foreach(var rootPage in new[]{browser.browseRoot,browser.detailRoot,browser.signaturePage,browser.allPage})Reveal(rootPage.transform);
  Save(s);
 }
 static void Tutorial()
 {
  var s=Open("Assets/Scenes/Tutorial.unity");var ui=Find<TutorialHud>(s);StyleAll(ui.transform);var root=ui.safeArea;
  Glass(root.Find("Header"),theme.panel);Place((RectTransform)root.Find("Header"),.035f,.885f,.68f,.97f);
  Text(ui.chapter,null,.035f,.15f,.48f,.85f,17,Gold);Text(root.Find("Header/Brand").GetComponent<TMP_Text>(),"HỌC VIỆN  /  CƠ BẢN",.50f,.15f,.96f,.85f,13,Muted);
  var coach=root.Find("Explanation");Glass(coach,theme.panel);Place((RectTransform)coach,.735f,.34f,.97f,.86f);Reveal(coach);
  Text(ui.title,null,.08f,.72f,.93f,.92f,27,White);Text(ui.body,null,.08f,.18f,.93f,.70f,18,White);Text(ui.progress,null,.08f,.035f,.93f,.15f,12,Gold);
  ui.title.enableAutoSizing=true;ui.title.fontSizeMin=20;ui.title.fontSizeMax=27;ui.body.enableAutoSizing=true;ui.body.fontSizeMin=15;ui.body.fontSizeMax=18;
  root.Find("Explanation/Accent").GetComponent<Image>().color=Gold;
  Glyph("Coach Mark",coach,ArenaGlyph.Star,Gold,.79f,.92f,.92f,.99f);
  ButtonStyle(ui.next,true);ButtonStyle(ui.replay);ButtonStyle(ui.skip);ButtonStyle(ui.touchToggle);
  Glass(ui.keyPanel.transform,theme.panel);Reveal(ui.keyPanel.transform);ui.keys.color=Gold;ui.keys.enableAutoSizing=true;ui.keys.fontSizeMin=16;ui.keys.fontSizeMax=34;ui.instruction.fontSize=16;ui.feedback.color=Mint;ui.feedback.fontSize=22;
  var dialog=ui.skipDialog.transform.Find("Panel");Glass(dialog,theme.panel);Reveal(dialog);
  Save(s);
 }
 static void Replay(){var s=Open("Assets/Scenes/Replay.unity");Save(s);}
 static void Backdrop(Transform parent){var bg=Block("Anime Screen Backdrop",parent,0,0,1,1,theme.background);bg.SetAsFirstSibling();Decorate(bg);}
 static void Decorate(Transform bg)
 {
  Glyph("Orbit Ornament",bg,ArenaGlyph.RecordFrame,new Color(.45f,.65f,.8f,.085f),.64f,.18f,1.16f,1.08f);
  Block("Top Hairline",bg,.035f,.985f,.965f,.987f,null,new Color(Gold.r,Gold.g,Gold.b,.45f));
  Label("Corner Index",bg,"P I C K L E V E R S E",.73f,.025f,.965f,.055f,10,Muted,TextAlignmentOptions.Right);
 }
 static void Header(Transform root,string name,TMP_Text heading)
 {var back=root.Find("Back");if(back)Place((RectTransform)back,.045f,.90f,.18f,.97f);var brand=root.Find("Brand");if(brand)Text(brand.GetComponent<TMP_Text>(),"PICKLEVERSE  /  PLAY",.73f,.91f,.945f,.95f,12,Gold,TextAlignmentOptions.Right);}
 static void StyleAll(Transform root)
 {
  foreach(var t in root.GetComponentsInChildren<TMP_Text>(true)){if(t is TextMeshProUGUI){t.font=Font;t.raycastTarget=false;if(t.color.maxColorComponent>.3f)t.color=White;}}
  foreach(var b in root.GetComponentsInChildren<Button>(true))ButtonStyle(b);
  foreach(var input in root.GetComponentsInChildren<TMP_InputField>(true)){Glass(input.transform,theme.quiet,true);input.caretColor=Gold;input.selectionColor=new Color(.5f,.8f,.8f,.3f);}
  foreach(var scaler in root.GetComponentsInChildren<CanvasScaler>(true)){scaler.matchWidthOrHeight=1;}
 }
 static void ButtonStyle(Button button,bool primary=false)
 {
  if(!button)return;Glass(button.transform,primary?theme.action:theme.panel,true);button.targetGraphic=button.GetComponent<Image>();var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.1f,1.1f,1.1f);colors.selectedColor=new Color(1.08f,1.08f,1.08f);colors.pressedColor=new Color(.72f,.80f,.9f);colors.disabledColor=new Color(.4f,.45f,.55f,.65f);colors.fadeDuration=.12f;button.colors=colors;
  foreach(var text in button.GetComponentsInChildren<TMP_Text>(true)){text.color=primary?ScreenUiTheme.Ink:White;text.raycastTarget=false;}
  Add<UiInteractionVfx>(button.transform);
 }
 static Button NewButton(string name,Transform parent,float x,float y,float r,float t,bool secondary=false){var rect=Rect(name,parent,x,y,r,t);var b=Add<Button>(rect);ButtonStyle(b,!secondary);return b;}
 static TMP_InputField Input(string name,Transform parent,string placeholder,float x,float y,float r,float t)
 {
  var root=Block(name,parent,x,y,r,t,theme.quiet);root.GetComponent<Image>().raycastTarget=true;var input=Add<TMP_InputField>(root);var area=Rect("Text Area",root,.05f,.08f,.95f,.92f);Add<RectMask2D>(area);input.textViewport=area;
  input.textComponent=Label("Text",area,"",0,0,1,1,19,White);input.placeholder=Label("Placeholder",area,placeholder,0,0,1,1,17,Muted);input.characterLimit=16;input.lineType=TMP_InputField.LineType.SingleLine;input.caretColor=Gold;input.targetGraphic=root.GetComponent<Image>();return input;
 }
 static void Reveal(Transform transform){Add<CanvasGroup>(transform);Add<UiPanelReveal>(transform);}
 static T Add<T>(Transform t)where T:Component=>t.GetComponent<T>()??t.gameObject.AddComponent<T>();
 static void Glass(Transform t,Material material,bool raycast=false){if(!t)return;var image=Add<Image>(t);image.sprite=null;image.material=material;image.color=Color.white;image.raycastTarget=raycast;}
 static RectTransform Block(string name,Transform parent,float x,float y,float r,float t,Material material,Color? tint=null){var rect=Rect(name,parent,x,y,r,t);Glass(rect,material);rect.GetComponent<Image>().color=tint??Color.white;return rect;}
 static UiLineIcon Glyph(string name,Transform parent,ArenaGlyph glyph,Color color,float x,float y,float r,float t){var rect=Rect(name,parent,x,y,r,t);var icon=Add<UiLineIcon>(rect);icon.glyph=glyph;icon.color=color;icon.raycastTarget=false;return icon;}
 static TMP_Text Label(string name,Transform parent,string value,float x,float y,float r,float t,float size,Color color,TextAlignmentOptions align=TextAlignmentOptions.Left){var rect=Rect(name,parent,x,y,r,t);var label=rect.GetComponent<TMP_Text>()??rect.gameObject.AddComponent<TextMeshProUGUI>();Text(label,value,x,y,r,t,size,color,align);return label;}
 static void Text(TMP_Text label,string value,float x,float y,float r,float t,float size,Color color,TextAlignmentOptions align=TextAlignmentOptions.Left){Place(label.rectTransform,x,y,r,t);if(value!=null)label.text=value;label.font=Font;label.fontSize=size;label.enableAutoSizing=false;label.color=color;label.alignment=align;label.raycastTarget=false;label.textWrappingMode=TextWrappingModes.Normal;}
 static RectTransform Rect(string name,Transform parent,float x,float y,float r,float t){var existing=parent.Find(name);var rect=existing?(RectTransform)existing:new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();if(!existing)rect.SetParent(parent,false);rect.gameObject.layer=5;Place(rect,x,y,r,t);return rect;}
 static void Place(RectTransform rect,float x,float y,float r,float t){rect.anchorMin=new Vector2(x,y);rect.anchorMax=new Vector2(r,t);rect.offsetMin=rect.offsetMax=Vector2.zero;rect.pivot=new Vector2(.5f,.5f);rect.localScale=Vector3.one;}
 static void Hide(Transform parent,string path){var child=parent.Find(path);if(child)child.gameObject.SetActive(false);}
 static Color Hex(string hex){ColorUtility.TryParseHtmlString("#"+hex,out var color);return color;}
}
