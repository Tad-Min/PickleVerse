using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.Settings
{
    /// <summary>Retains the small, fixed set of menu pages. Hidden pages never rebuild their layout.</summary>
    public sealed class UiListPages
    {
        public sealed class Page
        {
            public RectTransform content;
            public long revision = long.MinValue;
            public string filter;
            public object context;
            public string count;
        }
        readonly Dictionary<string, Page> pages = new Dictionary<string, Page>();
        readonly ScrollRect scroll;
        readonly RectTransform template;
        Page current;
        public UiListPages(ScrollRect owner, RectTransform initial)
        {
            scroll = owner;
            template = initial;
        }
        public Page Select(string key)
        {
            if (!pages.TryGetValue(key, out var page))
            {
                var root = pages.Count == 0 ? template : CreateContent(key);
                page = new Page { content = root };
                pages.Add(key, page);
            }
            if (current != page)
            {
                if (current != null)
                    current.content.gameObject.SetActive(false);
                current = page;
                scroll.content = page.content;
                page.content.gameObject.SetActive(true);
                scroll.StopMovement();
            }
            return page;
        }
        RectTransform CreateContent(string key)
        {
            var root = new GameObject("Content " + key, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            root.gameObject.SetActive(false);
            root.SetParent(template.parent, false);
            root.anchorMin = template.anchorMin;
            root.anchorMax = template.anchorMax;
            root.pivot = template.pivot;
            root.sizeDelta = template.sizeDelta;
            root.anchoredPosition = Vector2.zero;
            var source = template.GetComponent<VerticalLayoutGroup>();
            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(source.padding.left, source.padding.right, source.padding.top, source.padding.bottom);
            layout.spacing = source.spacing;
            layout.childAlignment = source.childAlignment;
            layout.childControlWidth = source.childControlWidth;
            layout.childControlHeight = source.childControlHeight;
            layout.childForceExpandWidth = source.childForceExpandWidth;
            layout.childForceExpandHeight = source.childForceExpandHeight;
            var fit = root.GetComponent<ContentSizeFitter>();
            var original = template.GetComponent<ContentSizeFitter>();
            fit.horizontalFit = original.horizontalFit;
            fit.verticalFit = original.verticalFit;
            return root;
        }
    }
}
