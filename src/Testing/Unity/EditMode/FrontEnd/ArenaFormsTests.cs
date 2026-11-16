using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PickleVerse.FrontEnd;
using PickleVerse.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.Tests.FrontEnd
{
    public sealed class ArenaFormsTests
    {
        readonly List<Object> created = new List<Object>();
        Transform parent;

        [SetUp]
        public void SetUp()
        {
            var canvas = ArenaForms.Canvas("Arena Forms Test", 10);
            created.Add(canvas);
            parent = canvas.transform;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
                if (item)
                    Object.DestroyImmediate(item);
            created.Clear();
        }

        [Test]
        public void CanvasUsesTheAuthoredReferenceLayout()
        {
            var scaler = parent.GetComponent<CanvasScaler>();
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920, 1080)));
            Assert.That(parent.GetComponent<Canvas>().sortingOrder, Is.EqualTo(10));
        }

        [Test]
        public void TypeScaleStaysReadableOnPhones()
        {
            // 16 px at 1920x1080 is the smallest size that stays legible on a 6" Android screen.
            Assert.That(ArenaForms.HintSize, Is.GreaterThanOrEqualTo(16));
            Assert.That(ArenaForms.CaptionSize, Is.GreaterThan(ArenaForms.HintSize));
            Assert.That(ArenaForms.BodySize, Is.GreaterThan(ArenaForms.CaptionSize));
            Assert.That(ArenaForms.HeadingSize, Is.GreaterThan(ArenaForms.BodySize));
            Assert.That(ArenaForms.TitleSize, Is.GreaterThan(ArenaForms.HeadingSize));
        }

        [Test]
        public void LabelsUseTheThemeFontAndNeverBlockClicks()
        {
            var label = ArenaForms.Label("Label", parent, "Xin chào", ArenaForms.BodySize, ScreenUiTheme.Text);
            Assert.That(label.font, Is.SameAs(ArenaForms.Font));
            Assert.That(label.raycastTarget, Is.False);
            Assert.That(label.richText, Is.False, "Server messages must not be interpreted as rich text.");
            Assert.That(label.fontSize, Is.EqualTo(ArenaForms.BodySize));
        }

        [Test]
        public void ActionRunsItsCallbackAndCarriesAnIcon()
        {
            int clicks = 0;
            var button = ArenaForms.Action("Sync", parent, "Đồng bộ", true, () => clicks++, ArenaGlyph.Sync);
            button.onClick.Invoke();
            Assert.That(clicks, Is.EqualTo(1));
            var icon = button.GetComponentInChildren<UiLineIcon>(true);
            Assert.That(icon, Is.Not.Null);
            Assert.That(icon.glyph, Is.EqualTo(ArenaGlyph.Sync));
            Assert.That(icon.raycastTarget, Is.False);
            Assert.That(button.GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("Đồng bộ"));
        }

        [Test]
        public void ActionWithoutCallbackHasNoListeners()
        {
            var button = ArenaForms.Action("Idle", parent, "Đóng", false, null);
            Assert.That(button.onClick.GetPersistentEventCount(), Is.Zero);
            Assert.DoesNotThrow(() => button.onClick.Invoke());
            Assert.That(button.GetComponentInChildren<UiLineIcon>(true), Is.Null);
        }

        [Test]
        public void SecondaryChoiceKeepsAMutedCaptionUnderItsTitle()
        {
            var choice = ArenaForms.Choice("Choice", parent, ArenaGlyph.Play, "Tôi đã biết chơi", "Vào thẳng sảnh chính", false, null);
            var title = choice.transform.Find("Title").GetComponent<TMP_Text>();
            var caption = choice.transform.Find("Caption").GetComponent<TMP_Text>();
            Assert.That(title.color, Is.EqualTo(ScreenUiTheme.Text));
            Assert.That(caption.color, Is.EqualTo(ScreenUiTheme.Muted));
            Assert.That(title.fontSize, Is.GreaterThan(caption.fontSize));
        }

        [Test]
        public void PrimaryChoiceUsesDarkInkOnTheChampagneSurface()
        {
            var choice = ArenaForms.Choice("Choice", parent, ArenaGlyph.Book, "Tôi là người mới", "Học từng bước", true, null);
            var title = choice.transform.Find("Title").GetComponent<TMP_Text>();
            Assert.That(title.color, Is.EqualTo(ScreenUiTheme.Ink));
        }

        [Test]
        public void InputIsMultilineLimitedAndHasAReadablePlaceholder()
        {
            var input = ArenaForms.Input("Statement", parent, "Nhập nội dung…", 254);
            Assert.That(input.characterLimit, Is.EqualTo(254));
            Assert.That(input.lineType, Is.EqualTo(TMP_InputField.LineType.MultiLineNewline));
            Assert.That(input.richText, Is.False);
            Assert.That(input.customCaretColor, Is.True);
            var hint = input.placeholder.GetComponent<TMP_Text>();
            Assert.That(hint.text, Is.EqualTo("Nhập nội dung…"));
            Assert.That(hint.fontSize, Is.GreaterThanOrEqualTo(ArenaForms.CaptionSize));
            Assert.That(hint.color.a, Is.GreaterThanOrEqualTo(.75f), "Placeholder text must stay legible on dark glass.");
        }

        [Test]
        public void ScrollTextScrollsVerticallyOnly()
        {
            var text = ArenaForms.ScrollText("Details", parent, out var scroll);
            Assert.That(scroll.horizontal, Is.False);
            Assert.That(scroll.vertical, Is.True);
            Assert.That(scroll.content, Is.SameAs(text.rectTransform));
            Assert.That(text.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
        }

        [Test]
        public void SetAndShowOnlyTouchChangedState()
        {
            var label = ArenaForms.Label("Label", parent, "A", ArenaForms.BodySize, ScreenUiTheme.Text);
            ArenaForms.Set(label, "A");
            Assert.That(label.text, Is.EqualTo("A"));
            ArenaForms.Set(label, "B");
            Assert.That(label.text, Is.EqualTo("B"));
            Assert.DoesNotThrow(() => ArenaForms.Set(null, "C"));

            ArenaForms.Show(label, false);
            Assert.That(label.gameObject.activeSelf, Is.False);
            ArenaForms.Show(label, true);
            Assert.That(label.gameObject.activeSelf, Is.True);
            Assert.DoesNotThrow(() => ArenaForms.Show(null, true));
        }

        [TestCase(ArenaGlyph.Shield)]
        [TestCase(ArenaGlyph.Sync)]
        [TestCase(ArenaGlyph.CloudOff)]
        public void NewGlyphsDrawGeometry(ArenaGlyph glyph)
        {
            var icon = ArenaForms.Icon("Icon", parent, glyph, ScreenUiTheme.Gold);
            icon.rectTransform.sizeDelta = new Vector2(48, 48);
            var populate = typeof(UiLineIcon).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null);
            using (var mesh = new VertexHelper())
            {
                populate.Invoke(icon, new object[] { mesh });
                Assert.That(mesh.currentVertCount, Is.GreaterThan(8), glyph + " should draw more than a single stroke.");
                Assert.That(mesh.currentIndexCount % 3, Is.Zero);
            }
        }
    }

    public sealed class TrainingAccessBannerTests
    {
        [Test]
        public void OfflineWithoutPendingWorkExplainsLocalSave()
        {
            var text = TrainingAccessOverlay.Describe(true, 0, null, false);
            Assert.That(text.title, Is.EqualTo("Đang luyện tập ngoại tuyến"));
            Assert.That(text.glyph, Is.EqualTo(ArenaGlyph.CloudOff));
            Assert.That(text.attention, Is.True);
            Assert.That(text.action, Is.EqualTo("Đăng nhập"));
            StringAssert.Contains("đăng nhập để đồng bộ", text.detail);
        }

        [Test]
        public void OfflineWithPendingWorkCountsIt()
        {
            var text = TrainingAccessOverlay.Describe(true, 3, "ignored", false);
            StringAssert.StartsWith("3 mục chờ đồng bộ", text.detail);
            Assert.That(text.glyph, Is.EqualTo(ArenaGlyph.CloudOff));
        }

        [Test]
        public void SignedInWithPendingWorkOffersSync()
        {
            var text = TrainingAccessOverlay.Describe(false, 2, null, true);
            Assert.That(text.title, Is.EqualTo("Đang đồng bộ tiến trình"));
            Assert.That(text.action, Is.EqualTo("Đồng bộ"));
            Assert.That(text.glyph, Is.EqualTo(ArenaGlyph.Sync));
            Assert.That(text.attention, Is.True);
            StringAssert.StartsWith("2 mục chờ xác nhận", text.detail);
        }

        [Test]
        public void SettledStateShowsTheLastMessageOrADefault()
        {
            Assert.That(TrainingAccessOverlay.Describe(false, 0, "Không thể kết nối máy chủ.", true).detail, Is.EqualTo("Không thể kết nối máy chủ."));
            var settled = TrainingAccessOverlay.Describe(false, 0, "", true);
            Assert.That(settled.detail, Is.EqualTo("Tiến trình đã đồng bộ."));
            Assert.That(settled.attention, Is.False);
        }
    }
}
