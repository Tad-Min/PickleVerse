using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PickleVerse.Settings
{
    /// <summary>Subtle input feedback without moving the button's hit area or rebuilding its layout.</summary>
    public sealed class UiButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public Graphic focus;
        public RectTransform symbol;
        public bool keepFocus;
        Selectable button;
        bool hover, selected;
        float amount;
        void Awake()
        {
            button = GetComponent<Selectable>();
        }
        public void OnPointerEnter(PointerEventData data)
        {
            hover = true;
        }
        public void OnPointerExit(PointerEventData data)
        {
            hover = false;
        }
        public void OnSelect(BaseEventData data)
        {
            selected = true;
        }
        public void OnDeselect(BaseEventData data)
        {
            selected = false;
        }
        void Update()
        {
            float target = button && button.IsInteractable() && (hover || selected || keepFocus) ? 1 : 0;
            float next = Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime * 8);
            if (Mathf.Approximately(next, amount))
                return;
            amount = next;
            if (focus)
            {
                var tint = focus.color;
                tint.a = amount;
                focus.color = tint;
            }
            if (symbol)
                symbol.localScale = Vector3.one * (1 + .06f * amount);
        }
        void OnDisable()
        {
            hover = selected = false;
            amount = 0;
            if (symbol)
                symbol.localScale = Vector3.one;
            if (focus)
            {
                var tint = focus.color;
                tint.a = 0;
                focus.color = tint;
            }
        }
    }
}
