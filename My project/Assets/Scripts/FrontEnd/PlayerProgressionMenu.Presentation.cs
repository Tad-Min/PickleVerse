using PickleVerse.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.FrontEnd
{
    public sealed partial class PlayerProgressionMenu
    {
        UiRowPool rows;
        Vector2 presentationSize;
        TMP_Text avatarInitial;
        UiRowPool Rows => rows ? rows : (rows = content.GetComponent<UiRowPool>() ?? content.gameObject.AddComponent<UiRowPool>());
        public void ApplyPresentation()
        {
            bool wasActive = modal.gameObject.activeSelf;
            modal.gameObject.SetActive(false);
            var back = modal.GetComponent<Image>();
            back.color = LobbyPanelStyle.Background;
            back.sprite = null;
            back.material = null;
            back.raycastTarget = true;
            UiModalPresentation.Attach(modal, true);
            LobbyPanelStyle.SurfaceStyle(window, true);
            LobbyPanelStyle.SurfaceStyle(left, true, true);
            LobbyPanelStyle.SurfaceStyle(summary, true, true);
            foreach (var button in window.GetComponentsInChildren<Button>(true))
                LobbyPanelStyle.Button(button, button == bonusButton);
            LobbyPanelStyle.Text(title, 38, ScreenUiTheme.Text);
            LobbyPanelStyle.Text(subtitle, 12, ScreenUiTheme.Gold);
            title.fontStyle = FontStyles.Bold;
            subtitle.characterSpacing = 2;
            LobbyPanelStyle.Text(identityName, 24, ScreenUiTheme.Text);
            LobbyPanelStyle.Text(identityId, 11, ScreenUiTheme.Muted, true);
            identityName.richText = identityId.richText = false;
            LobbyPanelStyle.Text(identityRank, 24, ScreenUiTheme.Gold, true);
            xpFill.color = ScreenUiTheme.Gold;
            summaryFill.color = ScreenUiTheme.Mint;
            LobbyPanelStyle.Place((RectTransform)xpFill.transform.parent, .025f, .20f, .975f, .27f);
            LobbyPanelStyle.Place((RectTransform)summaryFill.transform.parent, .025f, .17f, .655f, .23f);
            var viewport = scroll.viewport;
            var stencil = viewport.GetComponent<Mask>();
            if (stencil)
                stencil.enabled = false;
            if (!viewport.GetComponent<RectMask2D>())
                viewport.gameObject.AddComponent<RectMask2D>();
            var maskImage = viewport.GetComponent<Image>();
            if (maskImage)
            {
                maskImage.color = Color.clear;
                maskImage.raycastTarget = true;
            }
            scroll.decelerationRate = .08f;
            scroll.scrollSensitivity = 38;
            presentationSize = Vector2.zero;
            LayoutPresentation();
            modal.gameObject.SetActive(wasActive);
        }
        void UpdateAvatar(PickleVerse.Progression.PlayerProfile player)
        {
            var face = left.Find("Avatar") as RectTransform;
            if (!face)
                return;
            var old = face.GetComponent<Image>();
            if (old)
                old.enabled = player.avatarId != "initial";
            var frame = face.Find("Avatar Engraving") as RectTransform;
            if (!frame)
            {
                frame = Rect("Avatar Engraving", face, Vector2.zero, Vector2.one);
                var icon = frame.gameObject.AddComponent<UiLineIcon>();
                icon.glyph = ArenaGlyph.RecordFrame;
                icon.color = ScreenUiTheme.Gold;
                icon.raycastTarget = false;
            }
            frame.gameObject.SetActive(player.avatarId == "initial");
            avatarInitial = Text("Avatar Initial", face, string.IsNullOrEmpty(player.playerName) ? "P" : player.playerName.Substring(0, 1).ToUpperInvariant(), 34, ScreenUiTheme.Gold, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
            avatarInitial.richText = false;
            avatarInitial.gameObject.SetActive(player.avatarId == "initial");
        }
        void LayoutPresentation()
        {
            Vector2 size = modalSafe.rect.size;
            if (size.x < 10 || size.y < 10 || size == presentationSize)
                return;
            presentationSize = size;
            window.anchorMin = Vector2.zero;
            window.anchorMax = Vector2.one;
            window.offsetMin = new Vector2(20, 18);
            window.offsetMax = new Vector2(-20, -18);
            float width = size.x - 40, height = size.y - 36, side = Mathf.Clamp(width * .235f, 220, 285);
            float header = 100;
            Fixed(title.rectTransform, 32, 18, width - 140, 48, true, height);
            Fixed(subtitle.rectTransform, 34, 69, width - 150, 20, true, height);
            Fixed((RectTransform)window.Find("Close"), width - 98, 26, 66, 44, true, height);
            Fixed(left, 20, header, side, height - header - 20, true, height);
            Fixed(right, side + 38, header, width - side - 58, height - header - 20, true, height);
            var face = left.Find("Avatar") as RectTransform;
            if (face)
            {
                face.anchorMin = face.anchorMax = new Vector2(.5f, 1);
                face.pivot = new Vector2(.5f, 1);
                face.sizeDelta = new Vector2(86, 86);
                face.anchoredPosition = new Vector2(0, -24);
            }
            float leftHeight = height - header - 20;
            Fixed(identityName.rectTransform, 12, 122, side - 24, 32, true, leftHeight);
            Fixed(identityId.rectTransform, 12, 157, side - 24, 40, true, leftHeight);
            Fixed(identityRank.rectTransform, 12, 204, side - 24, 65, true, leftHeight);
            float tabH = Mathf.Clamp((leftHeight - 290) / 3, 42, 58);
            foreach (var b in new[] { statsTab, achievementTab, dailyTab, weeklyTab, trainingTab })
                if (b)
                {
                    float top = b == achievementTab || b == dailyTab ? 290 + tabH + 8 : b == weeklyTab ? 290 + (tabH + 8) * 2 : 290;
                    Fixed((RectTransform)b.transform, 12, top, side - 24, tabH, true, leftHeight);
                }
            // Statistics has two tabs; missions/training use the three lower slots.
            Fixed((RectTransform)dailyTab.transform, 12, 290 + tabH + 8, side - 24, tabH, true, leftHeight);
            Fixed(summary, 0, 0, width - side - 58, 118, true, leftHeight);
            var root = (RectTransform)scroll.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = new Vector2(0, -134);
            if (editForm)
            {
                editForm.anchorMin = Vector2.zero;
                editForm.anchorMax = Vector2.one;
                editForm.offsetMin = editForm.offsetMax = Vector2.zero;
            }
        }
        static void Fixed(RectTransform r, float x, float top, float width, float height, bool fromTop, float parentHeight)
        {
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(top));
            r.sizeDelta = new Vector2(Mathf.Round(width), Mathf.Round(height));
        }
    }
}
