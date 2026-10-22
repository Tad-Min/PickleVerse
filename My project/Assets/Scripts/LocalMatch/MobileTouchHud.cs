using System.Collections.Generic;
using UnityEngine;

namespace PickleVerse.LocalMatch
{
    // Keep the original desktop layout intact when the optional pad is switched off.
    sealed class MobileTouchHud
    {
        struct Saved
        {
            public RectTransform rect;
            public Vector2 min,max,offsetMin,offsetMax;
            public bool visible;
        }
        readonly List<Saved> saved=new List<Saved>();
        readonly IMatchTouchShotReceiver receiver;
        int lesson=-1;
        public MobileTouchHud(IMatchTouchShotReceiver receiver){this.receiver=receiver;}
        void Remember(RectTransform rect)
        {
            if(!rect)return;
            foreach(var s in saved)if(s.rect==rect)return;
            saved.Add(new Saved {rect=rect,min=rect.anchorMin,max=rect.anchorMax,offsetMin=rect.offsetMin,offsetMax=rect.offsetMax,visible=rect.gameObject.activeSelf});
        }
        void Place(RectTransform r,float left,float bottom,float right,float top)
        {
            if(!r)return;Remember(r);
            r.anchorMin=new Vector2(left,bottom);r.anchorMax=new Vector2(right,top);r.offsetMin=r.offsetMax=Vector2.zero;
        }
        void Hide(RectTransform r){if(r){Remember(r);r.gameObject.SetActive(false);}}
        void SideCue(RectTransform r,float bottom,float top)
        {
            bool left=PickleVerse.Settings.GameSettings.Current.leftHanded;
            Place(r,left?.77f:.025f,bottom,left?.975f:.23f,top);
        }
        public void Apply()
        {
            if(receiver is TrainingDirector training)
            {
                var hud=training.hud;
                Place((RectTransform)hud.coachText.transform.parent,.25f,.66f,.69f,.85f);
                Place(hud.coachText.rectTransform,.035f,.05f,.965f,.78f);
                SideCue((RectTransform)hud.keys.transform.parent,.46f,.65f);
                Hide(hud.status.rectTransform); // The same feedback is already in the coach dialogue.
            }
            else if(receiver is TutorialDirector tutorial)
            {
                lesson=tutorial.LessonIndex;var hud=tutorial.hud;
                Place((RectTransform)hud.body.transform.parent,.25f,.62f,.70f,.87f);
                Place(hud.title.rectTransform,.04f,.73f,.96f,.98f);
                Place(hud.body.rectTransform,.04f,.18f,.96f,.72f);
                Place(hud.progress.rectTransform,.04f,.015f,.96f,.16f);
                if(tutorial.Lesson.kind==TutorialKind.Movement)
                {
                    Hide((RectTransform)hud.keyPanel.transform);
                    SideCue((RectTransform)hud.next.transform,.56f,.65f);
                    SideCue((RectTransform)hud.replay.transform,.48f,.55f);
                }
                else SideCue((RectTransform)hud.keyPanel.transform,.48f,.69f);
            }
        }
        public void Poll(){if(receiver is TutorialDirector t&&t.LessonIndex!=lesson)Apply();}
        public void Restore()
        {
            foreach(var s in saved)if(s.rect)
            {
                s.rect.anchorMin=s.min;s.rect.anchorMax=s.max;s.rect.offsetMin=s.offsetMin;s.rect.offsetMax=s.offsetMax;
                s.rect.gameObject.SetActive(s.visible);
            }
            saved.Clear();lesson=-1;
        }
    }
}
