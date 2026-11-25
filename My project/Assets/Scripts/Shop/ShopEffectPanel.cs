using System.Linq;
using PickleVerse.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.Shop
{
    /// <summary>
    /// The "exclusive effect" part of the product details (right panel of the shop and the collection). Shown for skins whose
    /// offer describes a match effect; every other skin keeps the standard panel layout.
    /// </summary>
    public sealed class ShopEffectPanel : MonoBehaviour
    {
        RectTransform glass, title, collection, description, accent;
        Vector4 glassBox, titleBox, collectionBox, descriptionBox, accentBox;
        float descriptionSize;
        TMP_Text descriptionText, heading, details;
        GameObject rule;

        public bool Visible => heading && heading.gameObject.activeSelf;
        public string Heading => heading ? heading.text : "";
        public string Details => details ? details.text : "";

        public static void Show(ShopScreen screen, PaddleOffer offer)
        {
            var panel = screen.GetComponent<ShopEffectPanel>();
            if (!panel)
                panel = screen.gameObject.AddComponent<ShopEffectPanel>();
            if (panel.Bind(screen))
                panel.Apply(offer);
        }

        /// <summary>One bullet per line of the offer's description.</summary>
        public static string Bullets(string text) => string.Join("\n", (text ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0)
            .Select(l => "<color=#E9C46A>•</color><indent=1.05em>" + l + "</indent>"));

        bool Bind(ShopScreen screen)
        {
            if (glass)
                return true;
            if (!screen.browser || !screen.description || !screen.title || !screen.collection)
                return false;
            glass = screen.browser.detailRoot.transform.Find("Details Glass") as RectTransform;
            accent = screen.browser.detailRoot.transform.Find("Rarity Accent") as RectTransform;
            if (!glass)
                return false;
            title = screen.title.rectTransform;
            collection = screen.collection.rectTransform;
            descriptionText = screen.description;
            description = descriptionText.rectTransform;
            glassBox = Box(glass);
            titleBox = Box(title);
            collectionBox = Box(collection);
            descriptionBox = Box(description);
            accentBox = accent ? Box(accent) : default;
            descriptionSize = descriptionText.fontSize;
            return true;
        }

        void Build()
        {
            rule = new GameObject("Effect Rule", typeof(RectTransform), typeof(Image));
            rule.transform.SetParent(description.parent, false);
            var image = rule.GetComponent<Image>();
            image.color = new Color(.91f, .77f, .42f, .55f);
            image.raycastTarget = false;
            heading = Copy("Effect Title");
            heading.color = ScreenUiTheme.Gold;
            heading.fontStyle = FontStyles.Bold;
            heading.alignment = TextAlignmentOptions.Left;
            heading.characterSpacing = 1;
            heading.lineSpacing = 4;
            heading.textWrappingMode = TextWrappingModes.NoWrap;
            Fit(heading, 11, 19);
            details = Copy("Effect Details");
            details.color = ScreenUiTheme.Text;
            details.alignment = TextAlignmentOptions.TopLeft;
            details.textWrappingMode = TextWrappingModes.Normal;
            details.lineSpacing = 0;
            details.paragraphSpacing = 7;
            Fit(details, 10, 15);
        }

        TMP_Text Copy(string name)
        {
            var text = Instantiate(descriptionText, description.parent);
            text.name = name;
            text.raycastTarget = false;
            text.richText = true;
            return text;
        }

        static void Fit(TMP_Text text, float min, float max)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = min;
            text.fontSizeMax = max;
        }

        void Apply(PaddleOffer offer)
        {
            bool effect = offer != null && offer.HasEffect;
            if (effect && !heading)
                Build();
            Place(glass, effect ? new Vector4(glassBox.x, .215f, glassBox.z, .835f) : glassBox);
            Place(collection, effect ? new Vector4(collectionBox.x, .78f, collectionBox.z, .82f) : collectionBox);
            Place(title, effect ? new Vector4(titleBox.x, .705f, titleBox.z, .78f) : titleBox);
            Place(description, effect ? new Vector4(descriptionBox.x, .575f, descriptionBox.z, .70f) : descriptionBox);
            if (accent)
                Place(accent, effect ? new Vector4(accentBox.x, .225f, accentBox.z, .225f + accentBox.w - accentBox.y) : accentBox);
            descriptionText.enableAutoSizing = effect;
            descriptionText.fontSizeMin = 10;
            descriptionText.fontSizeMax = descriptionSize;
            if (!effect)
                descriptionText.fontSize = descriptionSize;
            if (!heading)
                return;
            rule.SetActive(effect);
            heading.gameObject.SetActive(effect);
            details.gameObject.SetActive(effect);
            if (!effect)
                return;
            Place((RectTransform)rule.transform, new Vector4(descriptionBox.x, .5635f, descriptionBox.z, .565f));
            Place(heading.rectTransform, new Vector4(descriptionBox.x, .490f, descriptionBox.z, .555f));
            Place(details.rectTransform, new Vector4(descriptionBox.x, .240f, descriptionBox.z, .482f));
            heading.text = "<size=72%><color=#A4B5CF>HIỆU ỨNG ĐỘC QUYỀN</color></size>\n" + offer.effectTitle;
            details.text = Bullets(offer.effectDetails);
        }

        static Vector4 Box(RectTransform r) => new Vector4(r.anchorMin.x, r.anchorMin.y, r.anchorMax.x, r.anchorMax.y);

        static void Place(RectTransform r, Vector4 box)
        {
            r.anchorMin = new Vector2(box.x, box.y);
            r.anchorMax = new Vector2(box.z, box.w);
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
    }
}
