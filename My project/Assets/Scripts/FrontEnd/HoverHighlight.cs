using UnityEngine;
using UnityEngine.EventSystems;

namespace PickleVerse.FrontEnd
{
    public sealed class HoverHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public GameObject hoverVisuals;
        void OnEnable()
        {
            SetHovered(false);
        }
        void OnDisable()
        {
            SetHovered(false);
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHovered(true);
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            SetHovered(false);
        }
        void SetHovered(bool hovered)
        {
            if (hoverVisuals)
                hoverVisuals.SetActive(hovered);
        }
    }
}
