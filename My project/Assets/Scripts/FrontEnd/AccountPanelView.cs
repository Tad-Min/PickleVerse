using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.FrontEnd
{
    /// <summary>Scene-owned UI. The persistent account coordinator never carries login fields into Menu.</summary>
    public sealed class AccountPanelView : MonoBehaviour
    {
        public RectTransform safeArea, card;
        public CanvasGroup form;
        public TMP_Text title, subtitle, feedback, submitLabel, switchHint, switchLabel, resendLabel;
        public TMP_InputField email, password, confirmPassword, playerName, otp;
        public Button submit, switchPage, verifyLink, resend, logout, close, passwordVisibility, confirmVisibility;
        public Selectable[] navigation;
        public bool profileOnly;
        Rect lastSafe;
        int lastWidth, lastHeight;
        float desiredHeight = 590;

        void OnEnable()
        {
            lastWidth = 0;
            if (passwordVisibility) passwordVisibility.onClick.AddListener(TogglePassword);
            if (confirmVisibility) confirmVisibility.onClick.AddListener(ToggleConfirm);
        }
        void OnDisable()
        {
            if (passwordVisibility) passwordVisibility.onClick.RemoveListener(TogglePassword);
            if (confirmVisibility) confirmVisibility.onClick.RemoveListener(ToggleConfirm);
            ConcealPasswords();
        }
        void TogglePassword() => Toggle(password, passwordVisibility);
        void ToggleConfirm() => Toggle(confirmPassword, confirmVisibility);
        static void Toggle(TMP_InputField field, Button button)
        {
            field.contentType = field.contentType == TMP_InputField.ContentType.Password
                ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
            button.GetComponentInChildren<TMP_Text>().text = field.contentType == TMP_InputField.ContentType.Password ? "HIỆN" : "ẨN";
            field.ForceLabelUpdate();
        }
        public void ConcealPasswords()
        {
            Conceal(password, passwordVisibility);
            Conceal(confirmPassword, confirmVisibility);
        }
        static void Conceal(TMP_InputField field, Button button)
        {
            if (!field) return;
            field.contentType = TMP_InputField.ContentType.Password;
            field.ForceLabelUpdate();
            if (button) button.GetComponentInChildren<TMP_Text>().text = "HIỆN";
        }
        public void SetHeight(float height)
        {
            desiredHeight = height;
            lastWidth = 0;
            RefreshLayout();
        }
        public void FitContent()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
            SetHeight(LayoutUtility.GetPreferredHeight(card));
        }
        void LateUpdate()
        {
            if (lastWidth != Screen.width || lastHeight != Screen.height || lastSafe != Screen.safeArea)
                RefreshLayout();
        }
        public void RefreshLayout()
        {
            if (!safeArea || !card || Screen.width <= 0 || Screen.height <= 0) return;
            lastWidth = Screen.width; lastHeight = Screen.height; lastSafe = Screen.safeArea;
            safeArea.anchorMin = lastSafe.min / new Vector2(Screen.width, Screen.height);
            safeArea.anchorMax = lastSafe.max / new Vector2(Screen.width, Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            var canvas = GetComponentInParent<Canvas>().rootCanvas;
            var available = lastSafe.size / Mathf.Max(.01f, canvas.scaleFactor);
            float width = Mathf.Min(444, available.x - 32);
            float scale = Mathf.Min(1, (available.y - 32) / desiredHeight);
            card.sizeDelta = new Vector2(width / Mathf.Max(.5f, scale), desiredHeight);
            card.localScale = Vector3.one * scale;
        }
    }
}
