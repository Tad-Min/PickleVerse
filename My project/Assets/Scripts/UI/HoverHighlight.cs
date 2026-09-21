using UnityEngine;
using UnityEngine.EventSystems;

namespace UI
{
    public class HoverHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public GameObject hoverVisuals; // Target gameobject that contains the glow border and gradient background

        private void Start()
        {
            if (hoverVisuals != null)
            {
                hoverVisuals.SetActive(false);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (hoverVisuals != null)
            {
                hoverVisuals.SetActive(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (hoverVisuals != null)
            {
                hoverVisuals.SetActive(false);
            }
        }
    }
}
