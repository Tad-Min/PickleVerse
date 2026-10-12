using System;
using System.Linq;
using PickleVerse.Progression;
using PickleVerse.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.LocalMatch
{
    public sealed class TrainingHud : MonoBehaviour
    {
        public TrainingDirector training;
        public TMP_FontAsset font;
        public Material glass,tab,action;
        public RectTransform safeArea,catalogue,content,briefing,practice,results;
        public TMP_Text heading,overview,lessonTitle,lessonBody,coachText,keys,status,timing,trial,resultTitle,resultBody;
        public Button start,continueButton,retry,next;
        public GameObject pausePanel;
        public TrainingStars resultStars;
        public MatchAnnouncements announcements;
        int filter=-1;
        Rect lastSafe;Vector2 lastScreen;
        static readonly Color White=new Color(.95f,.97f,1),Muted=new Color(.67f,.74f,.84f),Gold=new Color(.94f,.81f,.59f),Mint=new Color(.45f,.89f,.8f);
        public void Bind(TrainingDirector owner)
        {
            training=owner;
            EnsureRating();
            EnsurePracticeNavigation();
            EnsureAnnouncements();
            foreach(var b in GetComponentsInChildren<Button>(true))
            {
                b.onClick.RemoveAllListeners();
                switch(b.name)
                {
                    case "Start":b.onClick.AddListener(()=>training.StartLesson());break;
                    case "Retry":b.onClick.AddListener(training.Retry);break;
                    case "Next":b.onClick.AddListener(training.NextLesson);break;
                    case "Catalogue":case "Course":case "Lessons":b.onClick.AddListener(training.Catalogue);break;
                    case "Modes":b.onClick.AddListener(training.BackToModes);break;
                    case "Settings":b.onClick.AddListener(()=>SettingsOverlay.Open(training));break;
                    case "Touch":b.onClick.AddListener(training.ToggleTouch);break;
                    case "Resume":b.onClick.AddListener(()=>training.SetPaused(false));break;
                    case "Continue Practice":b.onClick.AddListener(Continue);break;
                    default:if(b.name.StartsWith("Filter ")){int f=int.Parse(b.name.Substring(7))-1;b.onClick.AddListener(()=>{filter=f;ShowCatalogue();});}break;
                }
            }
        }
        public void Build(TrainingDirector owner,TMP_FontAsset textFont,Material panel,Material card,Material gold)
        {
            training=owner;font=textFont;glass=panel;tab=card;action=gold;
            safeArea=Rect("Safe Area",transform,0,0,1,1);
            catalogue=Block("Lesson Catalogue",safeArea,.04f,.065f,.96f,.94f,glass);
            Button("Modes",catalogue,"‹ CHỌN CHẾ ĐỘ",()=>training.BackToModes(),.025f,.875f,.23f,.97f,false);
            heading=Text("Heading",catalogue,"HỌC VIỆN PICKLEVERSE",.27f,.89f,.97f,.97f,27,White);
            overview=Text("Overview",catalogue,"12 bài • kỹ thuật, di chuyển và đánh đôi",.035f,.80f,.69f,.875f,17,Muted);
            continueButton=Button("Continue Practice",catalogue,"TIẾP TỤC",Continue,.76f,.80f,.965f,.865f,true);
            string[] groups={"TẤT CẢ","CƠ BẢN","KIỂM SOÁT","NÂNG CAO","ĐÁNH ĐÔI"};
            for(int i=0;i<groups.Length;i++){int value=i-1;Button("Filter "+i,catalogue,groups[i],()=>{filter=value;ShowCatalogue();},.035f+i*.185f,.715f,.205f+i*.185f,.78f,false);}
            var scrollRect=Rect("Scroll",catalogue,.035f,.035f,.965f,.69f);
            var scroll=scrollRect.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
            var viewport=Rect("Viewport",scrollRect,0,0,1,1);var image=viewport.gameObject.AddComponent<Image>();image.color=new Color(.04f,.07f,.12f,.9f);var mask=viewport.gameObject.AddComponent<Mask>();mask.showMaskGraphic=false;
            content=Rect("Content",viewport,0,1,1,1);content.pivot=new Vector2(.5f,1);var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=10;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.viewport=viewport;scroll.content=content;
            briefing=Block("Briefing",safeArea,.60f,.15f,.96f,.88f,glass);
            Text("Eyebrow",briefing,"HLV / HƯỚNG DẪN",.07f,.875f,.93f,.96f,13,Gold);
            lessonTitle=Text("Title",briefing,"Bài học",.07f,.70f,.93f,.875f,30,White);
            lessonBody=Text("Description",briefing,"",.07f,.29f,.93f,.69f,18,Muted);
            start=Button("Start",briefing,"BẮT ĐẦU",()=>training.StartLesson(),.07f,.13f,.93f,.24f,true);
            Button("Catalogue",briefing,"‹ DANH SÁCH BÀI",()=>training.Catalogue(),.07f,.025f,.93f,.105f,false);
            practice=Rect("Practice",safeArea,0,0,1,1);
            var top=Block("Exercise Bar",practice,.035f,.86f,.965f,.97f,glass);trial=Text("Trial",top,"",.025f,.18f,.70f,.87f,21,White);
            Button("Settings",top,"CÀI ĐẶT",()=>SettingsOverlay.Open(training),.78f,.20f,.975f,.85f,false);
            var coach=Block("Coach Dialogue",practice,.69f,.26f,.96f,.76f,glass);
            Text("Coach Name",coach,"HLV LINH",.075f,.85f,.925f,.96f,14,Gold);coachText=Text("Coach Text",coach,"",.075f,.12f,.925f,.84f,17,White);
            coachText.enableAutoSizing=true;coachText.fontSizeMin=14;coachText.fontSizeMax=17;
            var cue=Block("Input Cue",practice,.315f,.09f,.665f,.235f,glass);
            keys=Text("Keys",cue,"",.04f,.44f,.96f,.94f,25,Mint);timing=Text("Timing",cue,"",.04f,.04f,.96f,.43f,16,White);
            status=Text("Feedback",practice,"",.07f,.745f,.65f,.84f,18,Gold);
            Button("Course",practice,"BÀI TẬP",()=>training.Catalogue(),.035f,.885f,.15f,.96f,false).gameObject.SetActive(false);
            EnsurePracticeNavigation();
            Button("Touch",practice,"CẢM ỨNG",()=>training.ToggleTouch(),.035f,.025f,.18f,.09f,false);
            results=Block("Exercise Results",safeArea,.23f,.13f,.77f,.89f,glass);
            resultTitle=Text("Title",results,"KẾT QUẢ",.07f,.79f,.93f,.95f,34,Gold);
            resultBody=Text("Assessment",results,"",.07f,.30f,.93f,.77f,19,White);
            EnsureRating();
            retry=Button("Retry",results,"LUYỆN LẠI",()=>training.Retry(),.06f,.15f,.475f,.255f,false);
            next=Button("Next",results,"BÀI TIẾP THEO",()=>training.NextLesson(),.525f,.15f,.94f,.255f,true);
            Button("Lessons",results,"DANH SÁCH BÀI",()=>training.Catalogue(),.06f,.025f,.94f,.115f,false);
            pausePanel=Block("Pause",safeArea,.36f,.40f,.64f,.60f,glass).gameObject;
            Text("Paused",pausePanel.transform,"ĐÃ TẠM DỪNG",.05f,.52f,.95f,.94f,22,White);
            Button("Resume",pausePanel.transform,"TIẾP TỤC",()=>training.SetPaused(false),.08f,.08f,.92f,.43f,true);
        }
        void Update()
        {
            var area=Screen.safeArea;var screen=new Vector2(Screen.width,Screen.height);
            if(safeArea&&screen.x>0&&screen.y>0&&(area!=lastSafe||screen!=lastScreen)){lastSafe=area;lastScreen=screen;safeArea.anchorMin=new Vector2(area.xMin/screen.x,area.yMin/screen.y);safeArea.anchorMax=new Vector2(area.xMax/screen.x,area.yMax/screen.y);safeArea.offsetMin=safeArea.offsetMax=Vector2.zero;}
            if(pausePanel)pausePanel.SetActive(training&&training.Paused&&!SettingsOverlay.IsOpen&&!SettingsOverlay.Loading);
        }
        void Pages(TrainingPhase phase){catalogue.gameObject.SetActive(phase==TrainingPhase.Catalogue);briefing.gameObject.SetActive(phase==TrainingPhase.Briefing);practice.gameObject.SetActive(phase==TrainingPhase.Practice||phase==TrainingPhase.Recovery);results.gameObject.SetActive(phase==TrainingPhase.Finished);}
        public void ShowCatalogue()
        {
            Pages(TrainingPhase.Catalogue);var data=TrainingEngine.Ensure(ProgressionRuntime.Instance.Player);
            foreach(var old in content.Cast<Transform>().ToArray()){old.gameObject.SetActive(false);Destroy(old.gameObject);}
            int complete=data.lessons.Count(r=>TrainingEngine.Completed(data,r.id));overview.text=$"{complete}/{TrainingCurriculum.Lessons.Length} bài đã đạt • Thưởng lần đầu • Chấm 1–3 sao";
            foreach(var d in TrainingCurriculum.Lessons.Where(d=>filter<0||d.tier==filter))
            {
                var record=data.lessons.Find(r=>r.id==d.id);bool unlocked=TrainingEngine.Unlocked(data,d),review=TrainingEngine.Completed(data,d.id);
                var row=Block("Lesson "+d.id,content,0,0,1,1,tab);var layout=row.gameObject.AddComponent<LayoutElement>();layout.preferredHeight=108;layout.minHeight=108;
                Text("Title",row,d.title,.035f,.49f,.66f,.93f,22,unlocked?White:Muted);
                string state=record!=null&&record.bestStars>0?record.bestStars+"/3 sao":unlocked?"Sẵn sàng luyện tập":"Cần: "+string.Join(", ",d.prerequisites.Select(id=>TrainingCurriculum.Find(id).title));
                Text("Info",row,d.TierName+" • "+(review?"Ôn lại không thưởng":d.reward+" Tokenpad lần đầu")+" • "+state,.035f,.04f,.76f,.47f,15,unlocked?Muted:new Color(.45f,.5f,.6f));
                var b=Button("Open",row,unlocked?(review?"ÔN LẠI":"LUYỆN TẬP"):"CHƯA MỞ",()=>training.Select(d.id),.79f,.19f,.97f,.80f,unlocked);b.interactable=unlocked;
            }
        }
        void Continue(){training.Select(TrainingEngine.Recommended(TrainingEngine.Ensure(ProgressionRuntime.Instance.Player)));}
        public void ShowBriefing()
        {
            Pages(TrainingPhase.Briefing);lessonTitle.text=training.Lesson.title;
            var d=training.Lesson;bool review=TrainingEngine.Completed(TrainingEngine.Ensure(ProgressionRuntime.Instance.Player),d.id);
            start.GetComponentInChildren<TMP_Text>().text=review?"BẮT ĐẦU ÔN LẠI":"BẮT ĐẦU";
            lessonBody.text=d.explanation+"\n\n"+Controls(d.coaching)+"\n\nĐạt "+d.required+"/"+d.trials+" cú • "+(review?"Ôn lại: không nhận thưởng Tokenpad hoặc XP.":"Thưởng lần đầu: "+d.reward+" Tokenpad • "+d.xp+" XP")+"\n\n1 sao: đạt mục tiêu; 2 sao: từ 80%; 3 sao: từ 90% và timing không quá 0,16 s.";
            lessonBody.enableAutoSizing=true;lessonBody.fontSizeMin=14;lessonBody.fontSizeMax=18;
        }
        public void ShowPractice(){Pages(TrainingPhase.Practice);Refresh();}
        public void Refresh()
        {
            if(training==null||training.Lesson==null||training.Session==null)return;
            var s=training.Session;trial.text=training.Lesson.title+"  •  "+s.successes+" đạt / "+s.trials+" đã thử  •  "+training.Lesson.trials+" lượt";
            keys.text=training.PartnerTurn?"BÓNG CỦA ĐỒNG ĐỘI":GameControls.ShotKeys(training.ExpectedMask);
            timing.text=training.Phase==TrainingPhase.Recovery?"HỒI VỊ / THEO VÙNG SÁNG":training.PartnerTurn?"GIỮ CÙNG HÀNG VỚI AI":training.Returning?"THEO DÕI VÙNG ĐÍCH":training.Lesson.skill==TrainingSkill.Serve?"GIỮ RỒI THẢ ĐỂ GIAO BÓNG":!training.Receiving?"CHỜ HLV CẤP BÓNG":training.ReleaseIn>.20f?"Thả sau "+training.ReleaseIn.ToString("0.00")+" s":"THẢ NÚT ĐÁNH";
            coachText.text=Controls(training.Lesson.coaching)+"\n\n"+training.Feedback;
            status.text=training.Phase==TrainingPhase.Recovery?training.Feedback:"";
        }
        public void ShowResults()
        {
            Pages(TrainingPhase.Finished);var a=training.Session;var d=training.Lesson;
            resultTitle.text=a.passed?(a.review?"ÔN LẠI HOÀN THÀNH":"HOÀN THÀNH"):"CHƯA ĐẠT MỤC TIÊU";resultStars.SetRating(a.stars);
            var frequent=a.errors.OrderByDescending(e=>e.count).FirstOrDefault();
            resultBody.text=$"{a.successes}/{a.trials} cú đạt vùng đích  •  {a.Accuracy:P0}\nTiming trung bình: {a.Timing:0.00} s\nThời gian luyện: {ProgressionRuntime.FormatTime(a.seconds)}\n\n"+(frequent==null?"Kiểm soát tốt. Hãy thử bài khó hơn.":TrainingCurriculum.ErrorName(frequent.error)+" ("+frequent.count+" lần)\n"+TrainingCurriculum.Tip(frequent.error))+"\n\n"+(a.review?"Ôn lại không nhận thưởng. Sao và thống kê vẫn được lưu.":a.rewardTokenpad>0?$"Đã nhận {a.rewardTokenpad} TOKENPAD + {a.rewardXp} XP":"Không mất Tokenpad. Luyện lại để đạt "+d.required+" cú.");
            retry.GetComponentInChildren<TMP_Text>().text=TrainingEngine.Completed(ProgressionRuntime.Instance.Player.training,d.id)?"ÔN LẠI":"LUYỆN LẠI";
            next.interactable=a.passed;
        }
        public void EnsureRating()
        {
            if(!resultStars)
            {
                var existing=results.Find("Star Rating");var r=existing?(RectTransform)existing:Rect("Star Rating",results,.35f,.675f,.65f,.77f);
                if(!r.GetComponent<CanvasRenderer>())r.gameObject.AddComponent<CanvasRenderer>();resultStars=r.GetComponent<TrainingStars>();if(!resultStars)resultStars=r.gameObject.AddComponent<TrainingStars>();resultStars.raycastTarget=false;
            }
            resultBody.rectTransform.anchorMax=new Vector2(.93f,.655f);resultBody.enableAutoSizing=true;resultBody.fontSizeMin=14;resultBody.fontSizeMax=19;
        }
        public void EnsureAnnouncements()
        {
            if(!announcements)announcements=GetComponent<MatchAnnouncements>();
            if(!announcements)announcements=gameObject.AddComponent<MatchAnnouncements>();
            announcements.externallyDriven=true;
            if(announcements.panel)return;
            var root=Block("Training Announcement",safeArea,.5f,.5f,.5f,.5f,null);
            root.GetComponent<Image>().color=new Color(.025f,.06f,.09f,.96f);
            root.pivot=new Vector2(.5f,.5f);root.sizeDelta=new Vector2(900,120);
            announcements.panel=root;announcements.opacity=root.gameObject.AddComponent<CanvasGroup>();
            announcements.opacity.blocksRaycasts=false;announcements.opacity.interactable=false;
            var line=Block("Accent",root,0,.975f,1,1,null);announcements.accent=line.GetComponent<Image>();announcements.accent.color=Mint;
            announcements.headline=Text("Headline",root,"",.03f,.52f,.97f,.93f,38,Mint);
            announcements.headline.fontStyle=FontStyles.Bold;announcements.headline.alignment=TextAlignmentOptions.Center;
            announcements.headline.enableAutoSizing=true;announcements.headline.fontSizeMin=24;announcements.headline.fontSizeMax=38;
            announcements.detail=Text("Detail",root,"",.03f,.055f,.97f,.52f,20,White);
            announcements.detail.alignment=TextAlignmentOptions.Center;
            announcements.detail.enableAutoSizing=true;announcements.detail.fontSizeMin=16;announcements.detail.fontSizeMax=20;
            announcements.Clear();
        }
        public void EnsurePracticeNavigation()
        {
            var back=practice.Find("Course");if(back)back.gameObject.SetActive(true);
            trial.rectTransform.anchorMin=new Vector2(.18f,.18f);trial.rectTransform.anchorMax=new Vector2(.75f,.87f);trial.rectTransform.offsetMin=trial.rectTransform.offsetMax=Vector2.zero;
        }
        static string Controls(string text)
        {return text.Replace("A/D",GameSettings.KeyName(GameSettings.Current.KeyFor(GameAction.Left))+"/"+GameSettings.KeyName(GameSettings.Current.KeyFor(GameAction.Right))).Replace("di chuyển W","di chuyển "+GameSettings.KeyName(GameSettings.Current.KeyFor(GameAction.Forward)));}
        RectTransform Rect(string name,Transform parent,float x,float y,float right,float top)
        {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.gameObject.layer=5;r.anchorMin=new Vector2(x,y);r.anchorMax=new Vector2(right,top);r.offsetMin=r.offsetMax=Vector2.zero;return r;}
        RectTransform Block(string name,Transform parent,float x,float y,float right,float top,Material material)
        {var r=Rect(name,parent,x,y,right,top);var image=r.gameObject.AddComponent<Image>();image.material=material;image.color=Color.white;image.raycastTarget=false;return r;}
        TMP_Text Text(string name,Transform parent,string value,float x,float y,float right,float top,float size,Color color)
        {var r=Rect(name,parent,x,y,right,top);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=size;t.color=color;t.raycastTarget=false;t.alignment=TextAlignmentOptions.MidlineLeft;return t;}
        Button Button(string name,Transform parent,string label,UnityEngine.Events.UnityAction callback,float x,float y,float right,float top,bool primary)
        {
            var r=Block(name,parent,x,y,right,top,primary?action:tab);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();b.targetGraphic.raycastTarget=true;
            Text("Label",r,label,.04f,0,.96f,1,16,primary?new Color(.07f,.09f,.14f):White).alignment=TextAlignmentOptions.Center;
            b.onClick.AddListener(callback);return b;
        }
    }
}
