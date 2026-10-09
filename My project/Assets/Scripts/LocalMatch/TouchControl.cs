using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PickleVerse.Settings;

namespace PickleVerse.LocalMatch
{
    public interface IMatchTouchInput
    {
        int TouchShotMask { get; set; }
        Vector2 TouchMovement { get; set; }
        bool TouchRun { get; set; }
    }
    public sealed class TouchControl : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IEndDragHandler
    {
        public BotMatch match;
        public IMatchTouchInput Input { get; set; }
        IMatchTouchInput Target => Input ?? match;
        public int shotMask;
        public bool joystick, modifier;
        public RectTransform knob;
        int pointer = int.MinValue;
        bool active;
        Vector2 baseSize;
        void OnEnable() { if(Application.isPlaying) { GameSettings.Changed+=ApplySettings; ApplySettings(); } }
        public void ApplySettings()
        {
            var rect=(RectTransform)transform;
            if(baseSize==Vector2.zero)baseSize=rect.sizeDelta;
            int index=joystick?0:shotMask==1?1:shotMask==2?2:shotMask==4?3:shotMask==3?4:shotMask==5?5:shotMask==6?6:shotMask==8?7:8;
            var settings=GameSettings.Current;Vector2 position=settings.touchPositions[index];
            if(settings.leftHanded)position.x=1-position.x;
            rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=new Vector2(.5f,.5f);
            rect.anchoredPosition=Vector2.Scale(position,((RectTransform)rect.parent).rect.size);rect.sizeDelta=baseSize*settings.touchScale;
            var group=GetComponent<CanvasGroup>();if(!group)group=gameObject.AddComponent<CanvasGroup>();group.alpha=settings.touchOpacity;
            var hint=rect.parent.Find("MoveLabel");if(hint)hint.gameObject.SetActive(false);
            if(!joystick){var label=GetComponentInChildren<TMPro.TMP_Text>();if(label)label.text=new[]{"Drive","Dink","Volley","Drop","Smash","Reset","Lực","Chéo"}[index-1];}
        }
        void Update() { if(modifier && active && (Target==null || (Target.TouchShotMask&shotMask)==0)) {active=false;GetComponent<Image>().color=new Color(.07f,.16f,.21f,.9f);} }
        public void OnPointerDown(PointerEventData e)
        {
            if (pointer != int.MinValue) return;
            if (Target == null) return;
            if (modifier) { active = !active; if (active) Target.TouchShotMask |= shotMask; else Target.TouchShotMask &= ~shotMask; var image = GetComponent<Image>(); if (image) image.color = active ? new Color(.1f,.65f,.5f,.95f) : new Color(.07f,.16f,.21f,.9f); return; }
            pointer = e.pointerId;
            if (joystick) OnDrag(e); else Target.TouchShotMask |= shotMask;
        }
        public void OnDrag(PointerEventData e)
        {
            if (!joystick || e.pointerId != pointer || Target == null) return;
            var rect = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out var point);
            point -= rect.rect.center;
            Vector2 value = point / (rect.rect.width * .42f);
            var settings=GameSettings.Current;
            float magnitude=value.magnitude;
            value=magnitude<=settings.deadzone?Vector2.zero:value.normalized*Mathf.InverseLerp(settings.deadzone,1,magnitude);
            Target.TouchRun = value.magnitude >= settings.sprintThreshold; Target.TouchMovement = Vector2.ClampMagnitude(value, 1);
            if (knob) knob.anchoredPosition = Target.TouchMovement * (rect.rect.width * .32f);
        }
        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != pointer) return;
            Clear();
        }
        public void OnEndDrag(PointerEventData e) { if (e.pointerId == pointer) Clear(); }
        void Clear()
        {
            pointer = int.MinValue;
            if (Target == null) return;
            if (joystick) { Target.TouchMovement = Vector2.zero; Target.TouchRun = false; if (knob) knob.anchoredPosition = Vector2.zero; }
            else Target.TouchShotMask &= ~shotMask;
        }
        void OnDisable() { GameSettings.Changed-=ApplySettings; Clear(); if (modifier && Target!=null) { Target.TouchShotMask &= ~shotMask; active = false; } }
    }
}
