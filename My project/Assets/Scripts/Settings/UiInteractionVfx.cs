using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PickleVerse.Settings
{
    /// <summary>A finite, vertex-only sweep. No particle system, texture, material instance or idle canvas dirties.</summary>
    [DisallowMultipleComponent]
    public sealed class UiInteractionVfx : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, ISubmitHandler
    {
        UiPulseGraphic pulse;
        Selectable control;
        float started = -10, duration;
        void Awake()
        {
            control = GetComponent<Selectable>();
        }
        public void OnPointerEnter(PointerEventData data)
        {
            Trigger(.32f);
        }
        public void OnPointerDown(PointerEventData data)
        {
            Trigger(.42f);
        }
        public void OnSubmit(BaseEventData data)
        {
            Trigger(.42f);
        }
        void Trigger(float seconds)
        {
            if (control && !control.IsInteractable())
                return;
            if (!pulse)
            {
                var go = new GameObject("Interaction Light", typeof(RectTransform), typeof(CanvasRenderer), typeof(UiPulseGraphic));
                go.layer = gameObject.layer;
                go.transform.SetParent(transform, false);
                var r = (RectTransform)go.transform;
                r.anchorMin = Vector2.zero;
                r.anchorMax = Vector2.one;
                r.offsetMin = r.offsetMax = Vector2.zero;
                go.AddComponent<Canvas>(); // isolate the short sweep from the static screen geometry
                pulse = go.GetComponent<UiPulseGraphic>();
                pulse.raycastTarget = false;
                pulse.color = new Color(.9f, .96f, 1, .24f);
            }
            started = Time.unscaledTime;
            duration = seconds;
            pulse.enabled = true;
            pulse.Progress = 0;
        }
        void Update()
        {
            if (!pulse || !pulse.enabled)
                return;
            float t = (Time.unscaledTime - started) / duration;
            if (t >= 1)
                pulse.enabled = false;
            else pulse.Progress = t;
        }
        void OnDisable()
        {
            if (pulse)
                pulse.enabled = false;
        }
    }
}
