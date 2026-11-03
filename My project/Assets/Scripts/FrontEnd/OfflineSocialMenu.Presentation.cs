using PickleVerse.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.FrontEnd
{
    public sealed partial class OfflineSocialMenu
    {
        UiRowPool socialRows;
        Vector2 layoutSize;
        UiRowPool SocialRows => socialRows ? socialRows : (socialRows = content.GetComponent<UiRowPool>() ?? content.gameObject.AddComponent<UiRowPool>());
        public void ApplyPresentation()
        {
            bool visible = modal.gameObject.activeSelf;
            modal.gameObject.SetActive(false);
            UiModalPresentation.Attach(modal, false);
            LobbyPanelStyle.SurfaceStyle(window, true);
            foreach (var button in window.GetComponentsInChildren<Button>(true))
                LobbyPanelStyle.Button(button, button == myPositionButton);
            LobbyPanelStyle.SurfaceStyle((RectTransform)selfName.transform.parent, true, true);
            LobbyPanelStyle.SurfaceStyle((RectTransform)search.transform, false, true);
            search.targetGraphic.raycastTarget = true;
            var subtitle = window.Find("Subtitle")?.GetComponent<TMP_Text>();
            if (subtitle)
            {
                subtitle.text = "DỮ LIỆU MẪU  /  CHƯA ĐỒNG BỘ TRỰC TUYẾN";
                LobbyPanelStyle.Text(subtitle, 10, ScreenUiTheme.Gold);
                subtitle.characterSpacing = 1;
            }
            LobbyPanelStyle.Text(title, 30, ScreenUiTheme.Text);
            LobbyPanelStyle.Text(selfName, 21, ScreenUiTheme.Text);
            LobbyPanelStyle.Text(selfId, 10, ScreenUiTheme.Muted);
            LobbyPanelStyle.Text(selfElo, 15, ScreenUiTheme.Gold, true);
            LobbyPanelStyle.Text(selfPosition, 13, ScreenUiTheme.Mint, true);
            var stencil = scroll.viewport.GetComponent<Mask>();
            if (stencil)
                stencil.enabled = false;
            if (!scroll.viewport.GetComponent<RectMask2D>())
                scroll.viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImage = scroll.viewport.GetComponent<Image>();
            if (viewportImage)
            {
                viewportImage.color = Color.clear;
                viewportImage.raycastTarget = true;
            }
            scroll.decelerationRate = .08f;
            scroll.scrollSensitivity = 38;
            layoutSize = Vector2.zero;
            LayoutPresentation();
            modal.gameObject.SetActive(visible);
        }
        void LayoutPresentation()
        {
            var size = modalSafe.rect.size;
            if (size.x < 10 || size.y < 10 || layoutSize == size)
                return;
            layoutSize = size;
            float width = Mathf.Min(size.x - 28, Mathf.Clamp(size.x * .40f, 480, 620)), height = size.y - 28;
            window.anchorMin = window.anchorMax = new Vector2(0, .5f);
            window.pivot = new Vector2(0, .5f);
            window.sizeDelta = new Vector2(Mathf.Round(width), Mathf.Round(height));
            Box(title.rectTransform, 24, 19, width - 104, 40);
            var sub = window.Find("Subtitle") as RectTransform;
            if (sub)
                Box(sub, 25, 63, width - 46, 22);
            Box((RectTransform)closeButton.transform, width - 68, 20, 44, 44);
            Box((RectTransform)friendsTab.transform, 22, 99, (width - 52) * .5f, 44);
            Box((RectTransform)leaderboardTab.transform, width * .5f + 4, 99, (width - 52) * .5f, 44);
            Box((RectTransform)selfName.transform.parent, 22, 157, width - 44, 80);
            Box((RectTransform)search.transform, 22, 251, (width - 44) * .36f, 48);
            Box((RectTransform)listButton.transform, 28 + (width - 44) * .36f, 251, (width - 70) * .21f, 48);
            Box((RectTransform)directoryButton.transform, 34 + (width - 44) * .57f, 251, (width - 70) * .21f, 48);
            if (blockedButton) Box((RectTransform)blockedButton.transform, 40 + (width - 44) * .78f, 251, (width - 70) * .21f, 48);
            Box((RectTransform)myPositionButton.transform, 30 + (width - 44) * .52f, 251, (width - 60) * .48f, 48);
            var listText = listButton.GetComponentInChildren<TMP_Text>();
            listText.text = "ĐÃ KẾT BẠN";
            foreach (var b in new[] { friendsTab, leaderboardTab })
                LobbyPanelStyle.Text(b.GetComponentInChildren<TMP_Text>(), 16, ScreenUiTheme.Text);
            foreach (var b in new[] { listButton, directoryButton, myPositionButton })
                LobbyPanelStyle.Text(b.GetComponentInChildren<TMP_Text>(), 12, b == myPositionButton ? ScreenUiTheme.Ink : ScreenUiTheme.Text);
            LobbyPanelStyle.Text(search.textComponent, 16, ScreenUiTheme.Text);
            LobbyPanelStyle.Text((TMP_Text)search.placeholder, 13, ScreenUiTheme.Muted);
            var list = (RectTransform)scroll.transform;
            list.anchorMin = Vector2.zero;
            list.anchorMax = Vector2.one;
            list.offsetMin = new Vector2(22, 48);
            list.offsetMax = new Vector2(-22, -314);
            Box(count.rectTransform, width * .55f, height - 36, width * .40f, 20);
            count.alignment = TextAlignmentOptions.Right;
            LobbyPanelStyle.Text(count, 11, ScreenUiTheme.Muted);
            Box(feedback.rectTransform, 24, height - 40, width * .53f - 24, 28);
            LobbyPanelStyle.Text(feedback, 11, ScreenUiTheme.Mint, true);
        }
        static void Box(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y));
            r.sizeDelta = new Vector2(Mathf.Round(w), Mathf.Round(h));
        }
    }
}
