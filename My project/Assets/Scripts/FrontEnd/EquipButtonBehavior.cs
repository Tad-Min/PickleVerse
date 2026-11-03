using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PickleVerse.FrontEnd
{
    public sealed class EquipButtonBehavior : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public enum EquipState { Unowned, Equip, Equipped }
        public EquipState currentState = EquipState.Equip;
        public Image background, border;
        public TextMeshProUGUI text;
        public GameObject[] decorativeHandles;
        public Button button;
        public UIGradient hoverGradient;
        public Outline hoverOutline;
        bool hovered;
        void OnEnable()
        {
            hovered = false;
            UpdateVisuals();
        }
        void OnDisable()
        {
            hovered = false;
        }
        public void SetState(EquipState state)
        {
            currentState = state;
            UpdateVisuals();
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            UpdateVisuals();
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            UpdateVisuals();
        }
        void UpdateVisuals()
        {
            if (!button)
                button = GetComponent<Button>();
            bool available = currentState == EquipState.Equip, glow = available && hovered;
            Color navy = new Color(.05f, .1f, .2f), cream = new Color(.95f, .95f, .9f), grey = new Color(.6f, .6f, .6f);
            if (button)
                button.interactable = available;
            if (background)
                background.color = available ? glow ? Color.white : cream : new Color(.1f, .1f, .15f, .6f);
            if (border)
            {
                border.color = available ? glow ? Color.cyan : navy : grey;
                float thickness = available ? 4 : 2;
                border.rectTransform.offsetMin = new Vector2(thickness, thickness);
                border.rectTransform.offsetMax = new Vector2(-thickness, -thickness);
            }
            if (text)
            {
                text.text = currentState == EquipState.Unowned ? "CHƯA SỞ HỮU" : currentState == EquipState.Equipped ? "ĐÃ TRANG BỊ" : "TRANG BỊ";
                text.color = available ? glow ? cream : navy : grey;
            }
            if (hoverGradient)
            {
                hoverGradient.enabled = glow;
                hoverGradient.colorLeft = new Color(0, .6f, .5f);
                hoverGradient.colorRight = new Color(0, .3f, .3f);
                if (glow)
                    hoverGradient.GetComponent<Graphic>()?.SetVerticesDirty();
            }
            if (hoverOutline)
            {
                hoverOutline.enabled = glow;
                hoverOutline.effectColor = Color.cyan;
            }
            if (decorativeHandles == null)
                return;
            foreach (var handle in decorativeHandles)
            {
                if (!handle)
                    continue;
                handle.SetActive(available);
                var image = handle.GetComponent<Image>();
                if (image)
                    image.color = glow ? Color.cyan : navy;
            }
        }
    }
}
