using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace UI
{
    public class EquipButtonBehavior : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public enum EquipState
        {
            Unowned,
            Equip,
            Equipped
        }

        public EquipState currentState = EquipState.Equip;
        
        [Header("Components")]
        public Image background;
        public Image border;
        public TextMeshProUGUI text;
        public GameObject[] decorativeHandles;
        public Button button;
        
        // Glow effect components that should be disabled when not hovered
        public UIGradient hoverGradient;
        public Outline hoverOutline;

        [Header("Colors")]
        private Color navyDark = new Color(0.05f, 0.1f, 0.2f, 1f);
        private Color cream = new Color(0.95f, 0.95f, 0.9f, 1f);
        private Color cyan = new Color(0.0f, 1.0f, 1.0f, 1.0f);
        private Color grey = new Color(0.6f, 0.6f, 0.6f, 1f);
        private Color darkTransparent = new Color(0.1f, 0.1f, 0.15f, 0.6f);

        private bool isHovered = false;

        private void Start()
        {
            UpdateVisuals();
        }

        public void SetState(EquipState state)
        {
            currentState = state;
            UpdateVisuals();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            UpdateVisuals();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (button == null) button = GetComponent<Button>();

            // Reset effects
            if (hoverGradient != null) hoverGradient.enabled = false;
            if (hoverOutline != null) hoverOutline.enabled = false;
            
            switch (currentState)
            {
                case EquipState.Unowned:
                    button.interactable = false;
                    background.color = darkTransparent;
                    border.color = grey;
                    SetBorderThickness(2);
                    text.text = "UNOWNED";
                    text.color = grey;
                    ToggleHandles(false);
                    break;
                    
                case EquipState.Equipped:
                    button.interactable = false;
                    background.color = darkTransparent;
                    border.color = grey;
                    SetBorderThickness(2);
                    text.text = "EQUIPPED";
                    text.color = grey;
                    ToggleHandles(false);
                    break;
                    
                case EquipState.Equip:
                    button.interactable = true;
                    text.text = "EQUIP";
                    ToggleHandles(true);
                    SetBorderThickness(4);
                    
                    if (isHovered)
                    {
                        // Hover State: Teal-green gradient, cyan glow, white text
                        background.color = Color.white;
                        if (hoverGradient != null) 
                        {
                            hoverGradient.enabled = true;
                            hoverGradient.colorLeft = new Color(0, 0.6f, 0.5f); // teal
                            hoverGradient.colorRight = new Color(0, 0.3f, 0.3f);
                        }
                        
                        if (hoverOutline != null) 
                        {
                            hoverOutline.enabled = true;
                            hoverOutline.effectColor = cyan;
                        }
                        
                        border.color = cyan;
                        text.color = cream;
                        
                        foreach(var handle in decorativeHandles) {
                            if(handle != null) handle.GetComponent<Image>().color = cyan;
                        }
                    }
                    else
                    {
                        // Normal State: Cream bg, Navy border, Navy text
                        background.color = cream;
                        border.color = navyDark;
                        text.color = navyDark;
                        
                        foreach(var handle in decorativeHandles) {
                            if(handle != null) handle.GetComponent<Image>().color = navyDark;
                        }
                    }
                    break;
            }
        }

        private void SetBorderThickness(float thickness)
        {
            if (border != null)
            {
                border.rectTransform.offsetMin = new Vector2(thickness, thickness);
                border.rectTransform.offsetMax = new Vector2(-thickness, -thickness);
            }
        }

        private void ToggleHandles(bool show)
        {
            if (decorativeHandles == null) return;
            foreach (var handle in decorativeHandles)
            {
                if (handle != null) handle.SetActive(show);
            }
        }
    }
}
