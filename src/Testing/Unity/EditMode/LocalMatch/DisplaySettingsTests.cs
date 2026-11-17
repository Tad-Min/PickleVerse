using NUnit.Framework;
using PickleVerse.Settings;
using UnityEngine;

namespace PickleVerse.LocalMatch.Tests
{
    public sealed class DisplaySettingsTests
    {
        [Test] public void DefaultsKeepBorderlessStartupAndAutomaticWindowSize()
        {
            var data = SettingsData.Defaults();
            Assert.AreEqual(DisplayMode.Borderless, data.Display);
            Assert.AreEqual(0, data.windowWidth);
            Assert.AreEqual(0, data.windowHeight);
        }
        [Test] public void PreferencesSavedBeforeDisplayModesStayBorderless()
        {
            var old = SettingsData.Defaults();
            var json = JsonUtility.ToJson(old).Replace("\"displayMode\":2,", "").Replace("\"windowWidth\":0,", "").Replace("\"windowHeight\":0,", "");
            StringAssert.DoesNotContain("displayMode", json);
            var loaded = JsonUtility.FromJson<SettingsData>(json);
            loaded.Validate();
            Assert.AreEqual(DisplayMode.Borderless, loaded.Display);
        }
        [Test] public void ValidateRepairsUnknownModesAndImpossibleWindowSizes()
        {
            var data = SettingsData.Defaults();
            data.displayMode = 7;
            data.windowWidth = 320;
            data.windowHeight = 200;
            data.Validate();
            Assert.AreEqual(DisplayMode.Borderless, data.Display);
            Assert.AreEqual(0, data.windowWidth);
            Assert.AreEqual(0, data.windowHeight);

            data.displayMode = (int)DisplayMode.Windowed;
            data.windowWidth = 1600;
            data.windowHeight = 900;
            data.Validate();
            Assert.AreEqual(DisplayMode.Windowed, data.Display);
            Assert.AreEqual(new Vector2Int(1600, 900), new Vector2Int(data.windowWidth, data.windowHeight));
        }
        [Test] public void FullHdMonitorOffersTheThreeLargestWindowsThatFit()
        {
            CollectionAssert.AreEqual(new[] { new Vector2Int(1280, 720), new Vector2Int(1366, 768), new Vector2Int(1600, 900) }, WindowSizes.Choices(1920, 1080));
            CollectionAssert.AreEqual(new[] { new Vector2Int(1366, 768), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) }, WindowSizes.Choices(2560, 1440));
        }
        [Test] public void SmallMonitorStillGetsTheSmallestWindow()
        {
            CollectionAssert.AreEqual(new[] { new Vector2Int(960, 540) }, WindowSizes.Choices(1024, 600));
        }
        [Test] public void ResolveKeepsAFittingSizeAndOtherwiseUsesTheLargestChoice()
        {
            Assert.AreEqual(new Vector2Int(1280, 720), WindowSizes.Resolve(1280, 720, 1920, 1080));
            Assert.AreEqual(new Vector2Int(1600, 900), WindowSizes.Resolve(0, 0, 1920, 1080));
            // A 1440p window saved on another monitor does not fit a 1080p screen.
            Assert.AreEqual(new Vector2Int(1600, 900), WindowSizes.Resolve(2560, 1440, 1920, 1080));
        }
        [Test] public void TitleBarIsRemovedOnlyFromTheTopOfTheSafeArea()
        {
            Assert.AreEqual(new Rect(0, 0, 1600, 866), DesktopWindow.Inset(new Rect(0, 0, 1600, 900), 900, 34));
            // A notch that already lowers the top edge further is kept.
            Assert.AreEqual(new Rect(0, 20, 1600, 820), DesktopWindow.Inset(new Rect(0, 20, 1600, 820), 900, 34));
            Assert.AreEqual(new Rect(0, 0, 1600, 900), DesktopWindow.Inset(new Rect(0, 0, 1600, 900), 900, 0));
        }
    }
}
