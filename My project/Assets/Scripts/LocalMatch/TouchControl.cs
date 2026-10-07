using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PickleVerse.LocalMatch
{
    public sealed class TouchControl : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IEndDragHandler
    {
        public BotMatch match;
        public int shotMask;
        public bool joystick, modifier;
        public RectTransform knob;
        int pointer = int.MinValue;
        bool active;
        void Update() { if(modifier && active && (match.TouchShotMask&shotMask)==0) {active=false;GetComponent<Image>().color=new Color(.07f,.16f,.21f,.9f);} }
        public void OnPointerDown(PointerEventData e)
        {
            if (pointer != int.MinValue) return;
            if (modifier) { active = !active; if (active) match.TouchShotMask |= shotMask; else match.TouchShotMask &= ~shotMask; var image = GetComponent<Image>(); if (image) image.color = active ? new Color(.1f,.65f,.5f,.95f) : new Color(.07f,.16f,.21f,.9f); return; }
            pointer = e.pointerId;
            if (joystick) OnDrag(e); else match.TouchShotMask |= shotMask;
        }
        public void OnDrag(PointerEventData e)
        {
            if (!joystick || e.pointerId != pointer) return;
            var rect = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out var point);
            point -= rect.rect.center;
            Vector2 value = point / (rect.rect.width * .42f);
            match.TouchRun = value.magnitude >= .88f; match.TouchMovement = Vector2.ClampMagnitude(value, 1);
            if (knob) knob.anchoredPosition = match.TouchMovement * (rect.rect.width * .32f);
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
            if (!match) return;
            if (joystick) { match.TouchMovement = Vector2.zero; match.TouchRun = false; if (knob) knob.anchoredPosition = Vector2.zero; }
            else match.TouchShotMask &= ~shotMask;
        }
        void OnDisable() { Clear(); if (modifier && match) { match.TouchShotMask &= ~shotMask; active = false; } }
    }
}
