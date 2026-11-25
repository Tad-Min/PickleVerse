using NUnit.Framework;
using PickleVerse.LocalMatch;
using PickleVerse.Progression;

namespace PickleVerse.Tests
{
    public sealed class RallyCallTests
    {
        [Test]
        public void OwnFaultIsARedLostTurn()
        {
            var lostServe = RallyCall.For(true, RallyCall.Serve.SideOut, "Bóng ngoài sân", "Bot");
            Assert.That(lostServe.title, Is.EqualTo("MẤT LƯỢT"));
            Assert.That(lostServe.tone, Is.EqualTo(AnnouncementTone.Fault));
            Assert.That(lostServe.detail, Does.Contain("Bóng ngoài sân").And.Contain("mất quyền giao bóng"));
            Assert.That(RallyCall.For(true, RallyCall.Serve.Point, "Bóng nảy hai lần", "Bot").detail, Does.Contain("Bot ghi điểm"));
        }

        [Test]
        public void OpponentFaultIsAGreenCallAndFollowsSideOutScoring()
        {
            var point = RallyCall.For(false, RallyCall.Serve.Point, "Bóng nảy hai lần", "Bot");
            Assert.That(point.tone, Is.EqualTo(AnnouncementTone.Success));
            Assert.That(point.title, Is.EqualTo("BẠN GHI ĐIỂM"));
            Assert.That(point.detail, Does.Contain("nhận 1 điểm").And.Contain("giao bóng"));

            // The receiving side wins the serve, not a point.
            var serve = RallyCall.For(false, RallyCall.Serve.SideOut, "Bóng ngoài sân", "Bot");
            Assert.That(serve.title, Is.EqualTo("ĐẾN LƯỢT BẠN GIAO BÓNG"));
            Assert.That(serve.detail, Does.Not.Contain("điểm"));
        }

        [Test]
        public void DoublesCallsNameTheTeamAndKeepTheSecondServer()
        {
            Assert.That(RallyCall.For(false, RallyCall.Serve.SideOut, "Lỗi", "Đối thủ", true).title, Is.EqualTo("ĐẾN LƯỢT ĐỘI BẠN GIAO BÓNG"));
            var second = RallyCall.For(false, RallyCall.Serve.SecondServer, "Lỗi", "Đối thủ", true);
            Assert.That(second.title, Is.EqualTo("ĐỐI THỦ MẤT LƯỢT"));
            Assert.That(second.detail, Does.Contain("người giao thứ hai").And.Not.Contain("giành quyền"));
        }

        [Test]
        public void CallStaysReadableForAboutOneAndAHalfSeconds()
        {
            Assert.That(RallyCall.Seconds - .18f - .22f, Is.EqualTo(1.5f).Within(.01f));
        }

        // The codes of src/AiService/app/engine.py (NAMES).
        [TestCase("Early"), TestCase("Late"), TestCase("LateServe"), TestCase("Kitchen"), TestCase("Momentum"), TestCase("Net"), TestCase("Out"),
         TestCase("Target"), TestCase("Movement"), TestCase("DinkTooHigh"), TestCase("ServeFeet"), TestCase("WrongInput"), TestCase("Spacing"),
         TestCase("Miss"), TestCase("DoubleBounce")]
        public void EveryFaultOfTheAiServiceHasItsOwnName(string code)
        {
            Assert.That(CoachingAnalysisText.Name(code), Is.Not.EqualTo("Tiêu chí kỹ thuật"));
        }
    }
}
