using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PickleVerse.Audio;
using PickleVerse.FrontEnd;
using PickleVerse.Progression;
using PickleVerse.Settings;
using PickleVerse.Shop;

public static class RemainingUiValidation
{
    const string Folder="D:/Github/PickleVerse/UnityWork/RemainingUi20261022";
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static Task running;
    static readonly List<object> checks=new List<object>(),performance=new List<object>();
    static string qa,original;static int originalViewIndex,customCount;static object sizes;static EditorWindow gameView;static Type viewType;
    static void Save(string status,string error=null)
    {Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/report.json",JsonConvert.SerializeObject(new{status,error,scope="Windows Unity Editor + mobile render/input/safe-area simulation; no physical Galaxy connected",checks,performance},Formatting.Indented));}
    static void Check(string name,bool passed,object detail=null)
    {checks.Add(new{name,passed,detail});if(!passed)throw new Exception(name+" "+JsonConvert.SerializeObject(detail));}
    public static object Prepare()
    {
        if(Application.isPlaying)throw new Exception("Prepare outside Play");Directory.CreateDirectory(Folder+"/ProfilesBefore");
        string root=Path.Combine(Application.persistentDataPath,"LocalPlayers");
        foreach(string path in Directory.GetFiles(root))if(!path.EndsWith(".lock"))File.Copy(path,Folder+"/ProfilesBefore/"+Path.GetFileName(path),true);
        return new{profilesBackedUp=true};
    }
    public static object StartPreview()
    {if(!Application.isPlaying)throw new Exception("Play first");running=Preview();return "Preview started";}
    public static object Start()
    {if(!Application.isPlaying)throw new Exception("Play first");running=Run();return "Responsive UI, interaction and performance QA started";}
    static async Task Run()
    {
        checks.Clear();performance.Clear();Save("running");Touchscreen phone=null;string settings=JsonUtility.ToJson(GameSettings.Current);
        try
        {
            InitView();var runtime=ProgressionRuntime.Instance;original=runtime.Player.id;qa="UiQA"+Guid.NewGuid().ToString("N").Substring(0,12);
            runtime.SwitchPlayer(qa,"Minh Anh");Seed(runtime);
            foreach(bool mobile in new[]{false,true})
            {
                if(mobile)phone=InputSystem.AddDevice<Touchscreen>();MobileRenderPolicy.Preview=mobile;SettingsRuntime.Apply(GameSettings.Current);
                await Load("Menu");var home=UnityEngine.Object.FindAnyObjectByType<HomeMenuShell>();if(home)home.editorPreviewDpi=420;
                var ui=UnityEngine.Object.FindAnyObjectByType<PlayerProgressionMenu>();var social=UnityEngine.Object.FindAnyObjectByType<OfflineSocialMenu>();
                int[][] dimensions=mobile?new[]{new[]{2400,1080},new[]{1600,720},new[]{915,412},new[]{1024,768}}:new[]{new[]{1920,1080},new[]{1280,720},new[]{2560,1080},new[]{1024,768}};
                foreach(var size in dimensions)
                {
                    Resolution(size[0],size[1]);await Frames(16);string tag=(mobile?"phone-":"desktop-")+size[0]+"x"+size[1];
                    foreach(string page in new[]{"profile","achievements","training","daily","weekly","history"})
                    {
                        var camera=Camera.main;Open(ui,page);await Task.Delay(280);await Frames(2);Canvas.ForceUpdateCanvases();
                        Check(tag+" "+page+" opens",ui.IsOpen&&ui.modal.GetComponent<Canvas>().enabled);
                        Check(tag+" "+page+" suspends hidden 3D",camera&&!camera.enabled);
                        Check(tag+" "+page+" native overlay",ui.GetComponent<Canvas>().renderMode==RenderMode.ScreenSpaceOverlay);
                        Check(tag+" "+page+" content has positive area",ui.scroll.viewport.rect.height>120&&ui.scroll.viewport.rect.width>300,new{width=ui.scroll.viewport.rect.width,height=ui.scroll.viewport.rect.height});
                        VisibleButtons(ui.window,ui.scroll,tag+" "+page);
                        Check(tag+" "+page+" title fits",!ui.title.isTextOverflowing);
                        Check(tag+" "+page+" header fits",Inside(ui.title.rectTransform,ui.window));
                        Check(tag+" "+page+" finite reveal finished",!ui.modal.GetComponent<UiModalPresentation>().Animating);
                        var decor=ui.window.GetComponentsInChildren<LobbyEtching>(true);Check(tag+" "+page+" ornaments do not intercept input",decor.Length>0&&decor.All(g=>!g.raycastTarget));
                        ui.scroll.verticalNormalizedPosition=0;await Frames(3);Check(tag+" "+page+" scroll reaches end",ui.scroll.verticalNormalizedPosition<.01f||ui.content.rect.height<=ui.scroll.viewport.rect.height);
                        ui.scroll.verticalNormalizedPosition=1;await Frames(3);
                        if(size[0]==1920||mobile&&(size[0]==2400||size[0]==1024))await Capture(page+"-"+tag);
                        ui.Close();Check(tag+" "+page+" restores camera",camera&&camera.enabled);Check(tag+" "+page+" releases modal input",!FrontEndModalInput.IsOpen);
                        Check(tag+" "+page+" closed panel stops rendering and input",!ui.modal.GetComponent<Canvas>().enabled&&!ui.modal.GetComponent<GraphicRaycaster>().enabled&&!ui.modal.GetComponent<CanvasGroup>().blocksRaycasts);
                    }
                    social.OpenFriends();await Task.Delay(300);VisibleButtons(social.window,social.scroll,tag+" friends");Check(tag+" drawer within safe area",Inside(social.window,social.modalSafe));
                    social.OpenLeaderboard();await Frames(20);VisibleButtons(social.window,social.scroll,tag+" leaderboard");social.ShowMyPosition();await Frames(4);
                    Check(tag+" own leaderboard row exists",social.selfRow&&social.selfRow.gameObject.activeInHierarchy);
                    social.search.text="khánh";await Frames(3);Check(tag+" search filters results",social.content.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("Khánh")));
                    social.search.text="zzzzzzzz";await Frames(3);Check(tag+" empty search shows guidance",social.content.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("Không tìm thấy")));
                    social.search.text="";social.OpenFriends();await Frames(6);
                    if(size[0]==1920||mobile&&size[0]==2400)await Capture("friends-"+tag);
                    social.OpenLeaderboard();await Frames(6);if(size[0]==1920||mobile&&size[0]==2400)await Capture("leaderboard-"+tag);
                    social.Close();await Task.Delay(300);Check(tag+" social releases modal input",!FrontEndModalInput.IsOpen);
                    // Asymmetric notch/gesture insets are simulated on the content, not the full-bleed shade.
                    ui.OpenProfile();ui.modalSafe.anchorMin=new Vector2(.035f,.025f);ui.modalSafe.anchorMax=new Vector2(.99f,.975f);
                    typeof(PlayerProgressionMenu).GetField("presentationSize",Flags).SetValue(ui,Vector2.zero);typeof(PlayerProgressionMenu).GetMethod("LayoutPresentation",Flags).Invoke(ui,null);Canvas.ForceUpdateCanvases();
                    Check(tag+" asymmetric safe area keeps close button",Inside((RectTransform)ui.window.Find("Close"),ui.modalSafe));ui.Close();
                }
                Resolution(mobile?2400:1920,1080);await Frames(20);
                await Measure((mobile?"mobile-preview":"desktop")+" menu closed");
                await Stress(ui,social,mobile);
                ui.OpenMissions();await Frames(20);var claim=ui.content.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name=="Claim"&&b.interactable);
                if(claim)
                {
                    long before=runtime.Player.coins;Click(claim);await Frames(4);long after=runtime.Player.coins;Check("Claim has a real effect once",after>before);
                    Click(claim);await Frames(3);Check("Pooled claim callback does not double-award",runtime.Player.coins==after);
                }
                ui.Close();
                foreach(string scene in new[]{"Shop","Collection"})
                {
                    await Load(scene);var shop=UnityEngine.Object.FindAnyObjectByType<ShopScreen>();
                    foreach(var size in mobile?new[]{new[]{2400,1080},new[]{915,412},new[]{1024,768}}:new[]{new[]{1920,1080},new[]{1024,768}})
                    {
                        Resolution(size[0],size[1]);await Frames(20);string tag=scene+"-"+(mobile?"phone-":"desktop-")+size[0]+"x"+size[1];
                        shop.browser.ShowAll();await Frames(20);VisibleButtons(shop.safeArea,shop.browser.allScroll,tag);
                        Check(tag+" native UI",shop.safeArea.GetComponentInParent<Canvas>().renderMode==RenderMode.ScreenSpaceOverlay);
                        Check(tag+" populated cards",shop.browser.allContent.GetComponentsInChildren<ShopProductCard>().Length==shop.Items.Length);
                        Check(tag+" studio sleeps in catalogue",!shop.browser.detailPresentation.uiCamera.enabled&&!shop.preview.previewCamera.enabled);
                        shop.OpenProduct(0);await Frames(35);VisibleButtons(shop.safeArea,null,tag+" details");Check(tag+" studio wakes",shop.browser.detailPresentation.uiCamera.enabled);
                        var shaderDeadline=DateTime.UtcNow.AddSeconds(45);
                        await Frames(4);while(ShaderUtil.anythingCompiling&&DateTime.UtcNow<shaderDeadline)await Frames(4);
                        Check(tag+" preview shaders ready",!ShaderUtil.anythingCompiling);await Frames(4);if(size[0]>=1920)await Capture(tag+"-detail");
                        shop.preview.ZoomIn();shop.preview.Rotate(35,-8);shop.preview.ResetView();Check(tag+" preview controls work",Mathf.Approximately(shop.preview.zoom,1));
                        shop.Back();await Frames(15);Check(tag+" detail closes to catalogue",!shop.browser.Inspecting&&!shop.browser.detailPresentation.uiCamera.enabled);
                        if(size[0]>=1920)await Capture(tag);
                    }
                    await Measure(scene+" "+(mobile?"mobile-preview":"desktop")+" catalogue");
                }
                if(phone!=null){InputSystem.RemoveDevice(phone);phone=null;}MobileRenderPolicy.Preview=false;SettingsRuntime.Apply(GameSettings.Current);
            }
            Save("passed");
        }
        catch(Exception e){Save("failed",e.ToString());Debug.LogException(e);}
        finally
        {
            if(phone!=null)InputSystem.RemoveDevice(phone);MobileRenderPolicy.Preview=false;SettingsRuntime.Apply(JsonUtility.FromJson<SettingsData>(settings));
            await Load("Menu");Finish();
        }
    }
    static void Open(PlayerProgressionMenu ui,string page)
    {
        if(page=="history")ui.OpenCareer();else if(page=="training")ui.OpenTrainingProgress();else if(page=="daily"||page=="weekly"){ui.OpenMissions();if(page=="weekly")ui.weeklyTab.onClick.Invoke();}
        else{ui.OpenProfile();if(page=="achievements")ui.achievementTab.onClick.Invoke();}
    }
    static bool Inside(RectTransform child,RectTransform parent)
    {var c=new Vector3[4];child.GetWorldCorners(c);return c.All(p=>{var q=parent.InverseTransformPoint(p);return q.x>=parent.rect.xMin-2&&q.x<=parent.rect.xMax+2&&q.y>=parent.rect.yMin-2&&q.y<=parent.rect.yMax+2;});}
    static void VisibleButtons(RectTransform root,ScrollRect scroll,string tag)
    {
        foreach(var b in root.GetComponentsInChildren<Button>())
        {
            if(!b.IsInteractable())continue;var r=(RectTransform)b.transform;
            if(scroll&&r.IsChildOf(scroll.content)&&!Inside(r,scroll.viewport))continue;
            var canvas=b.GetComponentInParent<Canvas>();var cam=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            var point=RectTransformUtility.WorldToScreenPoint(cam,r.TransformPoint(r.rect.center));var ev=new PointerEventData(EventSystem.current){position=point};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(ev,hits);
            Check(tag+" button within frame "+b.name,Inside(r,root));
            Check(tag+" button receives input "+b.name,hits.Any(h=>h.gameObject==b.gameObject||h.gameObject.transform.IsChildOf(b.transform)),hits.Take(3).Select(h=>h.gameObject.name).ToArray());
            var text=b.GetComponentInChildren<TMP_Text>();if(text)Check(tag+" button label fits "+b.name,!text.isTextOverflowing,text.text);
        }
    }
    static void Click(Button b)
    {var e=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);}
    static async Task Stress(PlayerProgressionMenu ui,OfflineSocialMenu social,bool mobile)
    {
        foreach(string p in new[]{"profile","achievements","training","daily","weekly","history"}){Open(ui,p);await Frames(3);ui.Close();}
        social.OpenFriends();social.directoryButton.onClick.Invoke();await Frames(4);social.OpenLeaderboard();await Frames(4);social.Close();await Frames(25);
        int before=ui.scroll.viewport.GetComponentsInChildren<UiRowPool>(true).Sum(p=>p.CreatedCount),friendBefore=social.scroll.viewport.GetComponentsInChildren<UiRowPool>(true).Sum(p=>p.CreatedCount);
        var opens=new List<double>();var transitions=new List<float>();int collections=GC.CollectionCount(0);
        for(int i=0;i<36;i++)
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();Open(ui,new[]{"profile","achievements","training","daily","weekly","history"}[i%6]);watch.Stop();opens.Add(watch.Elapsed.TotalMilliseconds);
            // Capture the opening frame itself, including Canvas/layout work at frame end.
            await Frames(1);transitions.Add(Time.unscaledDeltaTime*1000);ui.Close();await Frames(3);
        }
        int after=ui.scroll.viewport.GetComponentsInChildren<UiRowPool>(true).Sum(p=>p.CreatedCount);
        Check((mobile?"Mobile":"Desktop")+" repeated open/close reuses every row",after==before,new{before,after});
        for(int i=0;i<8;i++){social.OpenLeaderboard();social.search.text="an";social.search.text="";social.OpenFriends();social.directoryButton.onClick.Invoke();social.Close();await Frames(4);}
        int friendAfter=social.scroll.viewport.GetComponentsInChildren<UiRowPool>(true).Sum(p=>p.CreatedCount);
        await Frames(25);Check((mobile?"Mobile":"Desktop")+" search/drawer reuses rows",friendAfter==friendBefore,new{friendBefore,after=friendAfter});
        performance.Add(new{label=(mobile?"mobile-preview":"desktop")+" 36 open-close cycles",openMedianMs=Percentile(opens,.5),openP95Ms=Percentile(opens,.95),openMaxMs=opens.Max(),transitionP95Ms=Percentile(transitions.Select(v=>(double)v),.95),gen0Collections=GC.CollectionCount(0)-collections,pooledRows=before,socialRows=friendBefore});
        ui.OpenTrainingProgress();await Measure((mobile?"mobile-preview":"desktop")+" progression open");ui.Close();
        social.OpenLeaderboard();await Frames(25);await Measure((mobile?"mobile-preview":"desktop")+" leaderboard open");social.Close();await Frames(25);
    }
    static double Percentile(IEnumerable<double> values,double ratio){var a=values.OrderBy(v=>v).ToArray();return a.Length==0?0:a[(int)Math.Min(a.Length-1,Math.Floor((a.Length-1)*ratio))];}
    static async Task Measure(string label)
    {
        await Frames(30);var dt=new List<double>();var batches=new List<double>();var until=DateTime.UtcNow.AddSeconds(2);
        while(DateTime.UtcNow<until){await Frames(1);dt.Add(Time.unscaledDeltaTime*1000);batches.Add(UnityStats.drawCalls);}
        performance.Add(new{label,frames=dt.Count,fps=1000/dt.Average(),medianMs=Percentile(dt,.5),p95Ms=Percentile(dt,.95),maxMs=dt.Max(),drawCalls=Percentile(batches,.5)});Save("running");
    }
    static async Task Preview()
    {
        Save("preview");
        try
        {
            InitView();var runtime=ProgressionRuntime.Instance;original=runtime.Player.id;qa="UiQA"+Guid.NewGuid().ToString("N").Substring(0,12);
            runtime.SwitchPlayer(qa,"Minh Anh");Seed(runtime);await Load("Menu");Resolution(1920,1080);await Frames(8);
            var ui=UnityEngine.Object.FindAnyObjectByType<PlayerProgressionMenu>();ui.OpenProfile();await Frames(20);await Capture("profile-desktop");
            ui.achievementTab.onClick.Invoke();await Frames(20);await Capture("achievements-desktop");
            ui.Close();ui.OpenTrainingProgress();await Frames(20);await Capture("training-desktop");
            ui.Close();ui.OpenMissions();await Frames(20);await Capture("missions-desktop");
            ui.Close();ui.OpenCareer();await Frames(20);await Capture("history-desktop");
            ui.Close();var social=UnityEngine.Object.FindAnyObjectByType<OfflineSocialMenu>();social.OpenFriends();await Frames(24);await Capture("friends-desktop");
            social.OpenLeaderboard();await Frames(24);await Capture("leaderboard-desktop");social.Close();await Frames(24);
            var music=UnityEngine.Object.FindAnyObjectByType<MusicUI>();music.Open();await Frames(20);await Capture("music-reference");music.Close();
            await Load("Shop");await Frames(20);await Capture("shop-desktop");var shop=UnityEngine.Object.FindAnyObjectByType<ShopScreen>();shop.OpenProduct(0);await Frames(20);await Capture("shop-detail-desktop");
            await Load("Menu");Save("preview-ready");
        }
        catch(Exception e){Save("failed",e.ToString());Debug.LogException(e);}
    }
    static void Seed(ProgressionRuntime runtime)
    {
        runtime.UpdateWallet(p=>
        {
            p.elo=1850;p.level=12;p.levelXp=46;p.totalXp=706;p.coins=2400;p.gems=1000;p.wins=32;p.losses=18;p.activeSeconds=7620;
            p.lifetime=new ProgressCounters{matches=50,wins=32,points=382,returns=740,serves=205,bestStreak=6,higherRatedWins=3};
            p.daily.counters=new ProgressCounters{matches=1,wins=1,points=14,returns=24,serves=10,botMatches=1};
            p.weekly.counters=new ProgressCounters{matches=8,wins=4,points=88,returns=163,serves=42,botMatches=4,humanMatches=4};
            p.history.Clear();for(int i=0;i<20;i++)p.history.Add(new MatchResult{id="UiHistory"+i,playerId=p.id,kind=i%3==0?MatchKind.MatchedDoubles:MatchKind.RankedSingles,outcome=i%3==0?MatchOutcome.Lost:MatchOutcome.Won,ownElo=1850,opponentElo=1870,eloDelta=i%3==0?-20:20,startedUtcTicks=DateTime.UtcNow.AddHours(-i-1).Ticks,endedUtcTicks=DateTime.UtcNow.AddMinutes(-i*50).Ticks,gamesWon=i%3==0?1:2,gamesLost=i%3==0?2:1,opponents=new List<OpponentInfo>{new OpponentInfo{id="DEMO"+i,name=i%2==0?"Nguyễn Hoàng Nam":"Phạm Thùy Dương",elo=1870}},scores=new List<GameScore>{new GameScore(11,8,true),new GameScore(7,11,true),new GameScore(i%3==0?8:11,i%3==0?11:6,true)}});
            TrainingEngine.Ensure(p);for(int i=0;i<TrainingCurriculum.Lessons.Length;i++){var r=TrainingEngine.Record(p.training,TrainingCurriculum.Lessons[i].id);r.bestStars=i<6?3:i<9?1:0;r.bestAccuracy=i<6?.94:.68;r.attempts=i%4+1;r.retries=i%4;r.trials=24;r.successes=18;r.seconds=160+i*25;r.completions=i<9?1:0;r.accuracyHistory=new List<float>{.63f,.74f,.82f,.94f};r.errors=new List<TrainingErrorCount>{new TrainingErrorCount{error=i%2==0?TrainingError.Late:TrainingError.Target,count=2+i}};}
        });
    }
    static async Task Load(string name)
    {var op=SceneManager.LoadSceneAsync(name);while(!op.isDone)await Task.Delay(20);await Frames(12);}
    static async Task Frames(int count)
    {for(int i=0;i<count;i++){int frame=Time.frameCount;while(frame==Time.frameCount)await Task.Delay(5);}}
    static async Task Capture(string name)
    {ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");await Frames(3);}
    static void InitView()
    {
        viewType=typeof(Editor).Assembly.GetType("UnityEditor.GameView");gameView=EditorWindow.GetWindow(viewType);originalViewIndex=(int)viewType.GetProperty("selectedSizeIndex",Flags).GetValue(gameView);
        var type=typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");var state=type.BaseType.GetProperty("instance",Flags).GetValue(null);sizes=type.GetProperty("currentGroup",Flags).GetValue(state);customCount=(int)sizes.GetType().GetMethod("GetCustomCount",Flags).Invoke(sizes,null);
    }
    static void Resolution(int w,int h)
    {
        var type=typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");var kind=typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
        var value=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public,null,new object[]{Enum.Parse(kind,"FixedResolution"),w,h,"UI QA "+w+"x"+h},null);
        sizes.GetType().GetMethod("AddCustomSize",Flags).Invoke(sizes,new[]{value});int all=(int)sizes.GetType().GetMethod("GetTotalCount",Flags).Invoke(sizes,null);
        viewType.GetProperty("selectedSizeIndex",Flags).SetValue(gameView,all-1);gameView.Focus();gameView.Repaint();
    }
    public static object Finish()
    {
        if(Application.isPlaying)
        {
            MobileRenderPolicy.Preview=false;SettingsRuntime.Apply(GameSettings.Current);
            if(!string.IsNullOrEmpty(original))ProgressionRuntime.Instance.SwitchPlayer(original,null);
        }
        if(gameView&&sizes!=null)
        {
            viewType.GetProperty("selectedSizeIndex",Flags).SetValue(gameView,originalViewIndex);int builtins=(int)sizes.GetType().GetMethod("GetBuiltinCount",Flags).Invoke(sizes,null);
            int now=(int)sizes.GetType().GetMethod("GetCustomCount",Flags).Invoke(sizes,null);for(int i=now-1;i>=customCount;i--)sizes.GetType().GetMethod("RemoveCustomSize",Flags).Invoke(sizes,new object[]{builtins+i});
        }
        File.WriteAllText(Folder+"/session.json",JsonConvert.SerializeObject(new{original,qa}));
        return "QA state restored; stop Play then RestoreProfiles";
    }
    public static object RestoreProfiles()
    {
        if(Application.isPlaying)throw new Exception("Stop Play first");string root=Path.Combine(Application.persistentDataPath,"LocalPlayers");
        if(string.IsNullOrEmpty(qa)&&File.Exists(Folder+"/session.json"))qa=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Folder+"/session.json")).Value<string>("qa");
        foreach(string path in Directory.GetFiles(Folder+"/ProfilesBefore"))File.Copy(path,Path.Combine(root,Path.GetFileName(path)),true);
        if(!string.IsNullOrEmpty(qa))foreach(string file in Directory.GetFiles(root,qa+".*"))File.Delete(file);
        return new{restored=true,qaRemoved=qa};
    }
}
