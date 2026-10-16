using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using PickleVerse.Settings;

namespace PickleVerse.LocalMatch
{
    public sealed class TutorialHud : MonoBehaviour
    {
        public RectTransform safeArea;
        public TMP_Text chapter,title,body,keys,instruction,feedback,progress,nextLabel;
        public Button next,replay,skip,stay,confirmSkip,touchToggle;
        public GameObject skipDialog,touchRoot,keyPanel;
        TutorialDirector tutorial;
        Rect lastSafeArea;
        Vector2 lastResolution;
        bool touchWanted;
        bool touchLayoutDirty=true;
        int lastProgressShot=-1;
        TutorialKind? lastProgressKind;
        public void Bind(TutorialDirector value)
        {
            tutorial=value;next.onClick.AddListener(tutorial.Next);replay.onClick.AddListener(tutorial.Replay);
            skip.onClick.AddListener(()=>{skipDialog.SetActive(true);tutorial.SetPaused(true);if(EventSystem.current)EventSystem.current.SetSelectedGameObject(stay.gameObject);});
            stay.onClick.AddListener(()=>{skipDialog.SetActive(false);tutorial.SetPaused(false);});confirmSkip.onClick.AddListener(tutorial.ConfirmSkip);
            touchWanted=Application.isMobilePlatform;touchToggle.onClick.AddListener(()=>{touchWanted=!touchWanted;ShowLesson();});skipDialog.SetActive(false);
            GameSettings.Changed+=ShowLesson;
        }
        public void ShowLesson()
        {
            touchLayoutDirty=true;
            if(!tutorial)return;var lesson=tutorial.Lesson;
            chapter.text=lesson.kind==TutorialKind.Complete?"ĐÃ HOÀN THÀNH":"CƠ BẢN  /  "+(tutorial.LessonIndex+1).ToString("00")+" - 17";
            title.text=lesson.title;body.text=lesson.body;bool shot=lesson.kind==TutorialKind.Shot,move=lesson.kind==TutorialKind.Movement;
            touchRoot.SetActive((shot||move)&&touchWanted);
            var cue=(RectTransform)keyPanel.transform;
            cue.anchorMin=touchRoot.activeSelf?new Vector2(.255f,.755f):new Vector2(.04f,.07f);
            cue.anchorMax=touchRoot.activeSelf?new Vector2(.695f,.875f):new Vector2(.40f,.205f);
            cue.offsetMin=cue.offsetMax=Vector2.zero;
            keyPanel.SetActive(shot||move);keys.text=shot?(touchRoot.activeSelf?TouchKeys(lesson.mask):GameControls.ShotKeys(lesson.mask)):touchRoot.activeSelf?"CẦN DI CHUYỂN":""+MovementKeys();
            if(shot)
            {
                string modifier=(lesson.mask&8)!=0?GameSettings.KeyName(GameSettings.Current.KeyFor(GameAction.Power)):(lesson.mask&16)!=0?GameSettings.KeyName(GameSettings.Current.KeyFor(GameAction.Cross)):"";
                int baseMask=lesson.mask&7;string key=GameControls.ShotKeys(baseMask);
                instruction.text=touchRoot.activeSelf?(modifier.Length>0?"Chạm "+TouchKeys(lesson.mask&24)+" để bật, rồi chạm "+TouchKeys(baseMask)+".":"Chạm "+TouchKeys(baseMask)+" khi bóng đang tới."):
                    modifier.Length>0?"Giữ "+modifier+" → bấm rồi thả "+key+". Sau đó thả "+modifier+".":"Bấm rồi thả "+key+" khi bóng đang tới.";
            }
            else instruction.text="Di chuyển một đoạn, sau đó thử chạy.";
            next.gameObject.SetActive(!shot);replay.gameObject.SetActive(!shot&&lesson.kind!=TutorialKind.Complete);
            nextLabel.text=lesson.kind==TutorialKind.Complete?"CHỌN CHẾ ĐỘ  →":"TIẾP TỤC  →";
            touchToggle.gameObject.SetActive(shot||move);
            Refresh();
        }
        static string MovementKeys()=>GameSettings.KeyName(GameSettings.Current.KeyFor(GameAction.Forward))+" / "+GameSettings.KeyName(GameSettings.Current.KeyFor(GameAction.Left))+" / "+GameSettings.KeyName(GameSettings.Current.KeyFor(GameAction.Backward))+" / "+GameSettings.KeyName(GameSettings.Current.KeyFor(GameAction.Right))+"  +  "+GameSettings.KeyName(GameSettings.Current.KeyFor(GameAction.Run));
        static string TouchKeys(int mask)
        {string[] labels={"DRIVE","DINK","VOLLEY","LỰC","CHÉO"};string text="";for(int i=0;i<5;i++)if((mask&(1<<i))!=0)text+=(text.Length==0?"":" + ")+labels[i];return text;}
        public void Refresh()
        {
            if(!tutorial)return;next.interactable=tutorial.CanContinue;feedback.text=tutorial.Feedback;
            if(lastProgressKind!=tutorial.Lesson.kind||lastProgressShot!=tutorial.ShotsCompleted){lastProgressKind=tutorial.Lesson.kind;lastProgressShot=tutorial.ShotsCompleted;progress.text=tutorial.Lesson.kind==TutorialKind.Shot?"ĐÃ LÀM QUEN  "+tutorial.ShotsCompleted+" / 9 CÚ ĐÁNH":"Khoảng 2–3 phút • Chỉ hướng dẫn cơ bản";}
        }
        void Update()
        {
            if(skipDialog.activeSelf&&Keyboard.current?.escapeKey.wasPressedThisFrame==true){skipDialog.SetActive(false);tutorial.SetPaused(false);}
            var size=new Vector2(Screen.width,Screen.height);var area=Screen.safeArea;
            if(size!=lastResolution||area!=lastSafeArea){lastResolution=size;lastSafeArea=area;if(size.x>0&&size.y>0){safeArea.anchorMin=new Vector2(area.xMin/size.x,area.yMin/size.y);safeArea.anchorMax=new Vector2(area.xMax/size.x,area.yMax/size.y);safeArea.offsetMin=safeArea.offsetMax=Vector2.zero;}}
        }
        void LateUpdate()
        {
            if(!tutorial||!touchRoot.activeSelf||!touchLayoutDirty)return;
            touchLayoutDirty=false;
            var settings=GameSettings.Current;
            foreach(var control in tutorial.touchControls)
            {
                int index=control.joystick?0:control.shotMask==1?1:control.shotMask==2?2:control.shotMask==4?3:control.shotMask==8?7:8;
                Vector2 position=settings.touchPositions[index];if(settings.leftHanded)position.x=1-position.x;
                var rect=(RectTransform)control.transform;
                // Normalized anchors also follow canvas resizing, split-screen and safe area changes.
                rect.anchorMin=rect.anchorMax=position;rect.anchoredPosition=Vector2.zero;
            }
        }
        void OnDestroy(){GameSettings.Changed-=ShowLesson;}
    }
}
