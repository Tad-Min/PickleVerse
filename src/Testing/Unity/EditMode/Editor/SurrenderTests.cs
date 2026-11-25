using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using TMPro;
using UnityEngine;
using PlayMode = PickleVerse.Multiplayer.PlayMode;

namespace PickleVerse.Tests.Multiplayer
{
    public sealed class SurrenderTests
    {
        /// <summary>One match server with the clients of every human, wired the way NetworkMatch wires them.</summary>
        sealed class Court
        {
            public sealed class Client : ISurrenderCourt
            {
                readonly Court court;
                readonly string id;
                public Client(Court court, string id) { this.court = court; this.id = id; }
                public bool CanSurrender => court.loser < 0;
                public string LocalPlayer => id;
                public MatchRoster Roster => court.roster;
                public RoomMember SurrenderPartner => Roster.members.FirstOrDefault(m => m.team == Roster.Find(id).team && !m.bot && m.id != id);
                public SurrenderRequest Surrender { get; } = new SurrenderRequest();
                public void AskToSurrender() => court.Send(() => court.desk.Ask(id));
                public void AgreeToSurrender() => court.Send(() => court.desk.Agree(id));
                public void RefuseToSurrender() => court.Send(() => court.desk.Refuse(id));
            }

            public readonly MatchRoster roster;
            public readonly SurrenderDesk desk;
            public readonly Dictionary<string, Client> clients = new Dictionary<string, Client>();
            public readonly Dictionary<string, SurrenderPanel> panels = new Dictionary<string, SurrenderPanel>();
            public readonly HashSet<string> offline = new HashSet<string>();
            public bool serverAnswers = true;
            public double now = 100;
            public int loser = -1;
            public string[] losers;
            readonly GameObject canvas;

            public Court(PlayMode mode, bool custom, params RoomMember[] members)
            {
                roster = new MatchRoster { matchId = "m", mode = mode, custom = custom, members = members.ToList() };
                desk = new SurrenderDesk(roster, () => now, id => !offline.Contains(id));
                desk.Opened = (id, proposer, seconds, agreed) => clients[id].Surrender.Show(proposer, seconds, agreed, now);
                desk.Closed = (id, end, actor, seconds) => clients[id].Surrender.Finish(end, actor, seconds);
                desk.Surrendered = (team, ids) => { loser = team; losers = ids; };
                canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
                foreach (var member in members.Where(m => !m.bot))
                {
                    clients[member.id] = new Client(this, member.id);
                    var panel = SurrenderPanel.Create(canvas.transform, null, TMP_Settings.defaultFontAsset);
                    panel.Clock = () => now;
                    panels[member.id] = panel;
                }
            }

            void Send(Action message)
            {
                if (serverAnswers)
                    message();
            }

            public SurrenderPanel Panel(string id) => panels[id];
            public SurrenderPanel.View View(string id) => panels[id].Current;
            public void Press(string id) { panels[id].Ask(clients[id]); Frame(); }
            public void Yes(string id) { panels[id].AcceptButton.onClick.Invoke(); Frame(); }
            public void No(string id) { panels[id].RefuseButton.onClick.Invoke(); Frame(); }
            public void Wait(double seconds) { now += seconds; desk.Tick(); Frame(); }
            public void Frame()
            {
                foreach (var entry in panels)
                    entry.Value.Refresh(clients[entry.Key], false);
            }
            public void Destroy() => UnityEngine.Object.DestroyImmediate(canvas);
        }

        static RoomMember Human(string id, int team) => new RoomMember { id = id, name = id.ToUpperInvariant(), team = team };
        static RoomMember Bot(string id, int team) => new RoomMember { id = id, name = "Bot " + id, team = team, bot = true };

        Court court;
        Court Singles(bool custom = false) => court = new Court(PlayMode.Singles, custom, Human("a", 0), Human("x", 1));
        Court Doubles(bool custom = false) => court = new Court(PlayMode.Doubles, custom, Human("a", 0), Human("b", 0), Human("x", 1), Human("y", 1));

        [TearDown]
        public void Clean()
        {
            court?.Destroy();
            court = null;
        }

        [Test]
        public void SinglesNeedsOneConfirmationAndNeverMoreThanThat()
        {
            var c = Singles();
            c.Press("a");
            Assert.That(c.loser, Is.EqualTo(-1), "pressing the button alone must not end the match");
            Assert.That(c.View("a"), Is.EqualTo(SurrenderPanel.View.Ask));
            Assert.That(c.View("x"), Is.EqualTo(SurrenderPanel.View.Hidden), "the opponent sees nothing");
            Assert.That(c.Panel("a").BodyText, Does.Contain("bạn bị xử thua").And.Contain("trừ Elo"));
            c.Yes("a");
            Assert.That(c.loser, Is.EqualTo(0));
            Assert.That(c.losers, Is.EqualTo(new[] { "a" }));
            Assert.That(c.View("a"), Is.EqualTo(SurrenderPanel.View.Hidden), "the box closes with the match");
        }

        [Test]
        public void SinglesCanBeCancelledAndTheMatchGoesOn()
        {
            var c = Singles(custom: true);
            c.Press("a");
            Assert.That(c.Panel("a").BodyText, Does.Contain("Phòng riêng không tính Elo"));
            Assert.That(c.Panel("a").CanCancel, Is.True, "Esc closes the box");
            c.No("a");
            Assert.That(c.View("a"), Is.EqualTo(SurrenderPanel.View.Hidden));
            c.Press("a");
            c.Panel("a").Cancel();
            c.Frame();
            Assert.That(c.View("a"), Is.EqualTo(SurrenderPanel.View.Hidden));
            Assert.That(c.loser, Is.EqualTo(-1));
            c.Press("a");
            c.Yes("a");
            Assert.That(c.loser, Is.EqualTo(0), "a cancelled box does not start a waiting time in singles");
        }

        [Test]
        public void DoublesShowsTheBoxToBothPartnersAtOnce()
        {
            var c = Doubles();
            c.Press("a");
            Assert.That(c.View("a"), Is.EqualTo(SurrenderPanel.View.Ask));
            Assert.That(c.View("b"), Is.EqualTo(SurrenderPanel.View.Partner));
            Assert.That(c.View("x"), Is.EqualTo(SurrenderPanel.View.Hidden));
            Assert.That(c.View("y"), Is.EqualTo(SurrenderPanel.View.Hidden));
            Assert.That(c.Panel("a").BodyText, Does.StartWith("B cũng phải xác nhận"));
            Assert.That(c.Panel("b").TitleText, Is.EqualTo(SurrenderText.PartnerTitle));
            Assert.That(c.Panel("b").BodyText, Does.StartWith("A đề nghị đầu hàng"));
            Assert.That(c.Panel("b").CanCancel, Is.False, "Esc must not answer for the partner in the middle of a rally");
            Assert.That(c.Panel("b").CountdownText, Is.EqualTo("Tự hủy sau 20 giây"));
        }

        [TestCase("a", "b")]
        [TestCase("b", "a")]
        public void DoublesEndsOnlyWhenBothPartnersConfirmed(string first, string second)
        {
            var c = Doubles();
            c.Press("a");
            c.Yes(first);
            Assert.That(c.loser, Is.EqualTo(-1), "one confirmation is not enough");
            Assert.That(c.View(first), Is.EqualTo(SurrenderPanel.View.Waiting));
            Assert.That(c.Panel(first).BodyText, Does.Contain("Đang chờ " + second.ToUpperInvariant()));
            c.Yes(first);
            Assert.That(c.loser, Is.EqualTo(-1), "the same player cannot confirm twice");
            c.Yes(second);
            Assert.That(c.loser, Is.EqualTo(0));
            Assert.That(c.losers, Is.EquivalentTo(new[] { "a", "b" }));
        }

        [Test]
        public void PartnerCanRefuse()
        {
            var c = Doubles();
            c.Press("a");
            c.Yes("a");
            c.No("b");
            Assert.That(c.loser, Is.EqualTo(-1));
            Assert.That(c.View("b"), Is.EqualTo(SurrenderPanel.View.Hidden));
            Assert.That(c.View("a"), Is.EqualTo(SurrenderPanel.View.Notice));
            Assert.That(c.Panel("a").BodyText, Is.EqualTo("B không đồng ý đầu hàng. Trận đấu tiếp tục."));
            c.Wait(SurrenderPanel.NoticeSeconds + .1);
            Assert.That(c.View("a"), Is.EqualTo(SurrenderPanel.View.Hidden));
        }

        [Test]
        public void PlayerWhoAskedCanWithdrawBeforeAndAfterConfirming()
        {
            var c = Doubles();
            c.Press("a");
            c.No("a");
            Assert.That(c.View("b"), Is.EqualTo(SurrenderPanel.View.Notice));
            Assert.That(c.Panel("b").BodyText, Is.EqualTo("A đã rút đề nghị đầu hàng. Trận đấu tiếp tục."));
            Assert.That(c.View("a"), Is.EqualTo(SurrenderPanel.View.Hidden), "no explanation for an own decision");
            c.Wait(SurrenderDesk.CooldownSeconds);
            c.Press("a");
            c.Yes("a");
            c.No("a");
            Assert.That(c.loser, Is.EqualTo(-1));
            Assert.That(c.View("b"), Is.EqualTo(SurrenderPanel.View.Notice));
            Assert.That(c.Panel("b").AcceptButton.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void RequestExpiresForBothPartners()
        {
            var c = Doubles();
            c.Press("a");
            c.Yes("a");
            c.Wait(SurrenderDesk.Seconds - 1);
            Assert.That(c.desk.IsOpen(0), Is.True);
            Assert.That(c.Panel("b").CountdownText, Is.EqualTo("Tự hủy sau 1 giây"));
            c.Wait(1);
            Assert.That(c.desk.IsOpen(0), Is.False);
            Assert.That(c.Panel("a").BodyText, Does.StartWith("Hết thời gian"));
            Assert.That(c.Panel("b").BodyText, Does.StartWith("Hết thời gian"));
            // A confirmation that was already on the way must not surrender or open a new request.
            c.desk.Agree("b");
            Assert.That(c.loser, Is.EqualTo(-1));
            Assert.That(c.desk.IsOpen(0), Is.False);
        }

        [Test]
        public void PartnerCannotBeFloodedWithRequests()
        {
            var c = Doubles();
            c.Press("a");
            c.No("b");
            c.Wait(SurrenderPanel.NoticeSeconds + .1);
            c.Press("a");
            Assert.That(c.desk.IsOpen(0), Is.False);
            Assert.That(c.View("b"), Is.EqualTo(SurrenderPanel.View.Hidden));
            Assert.That(c.Panel("a").BodyText, Does.Match(@"^Đội vừa đề nghị đầu hàng\. Thử lại sau \d+ giây\.$"));
            c.Wait(SurrenderDesk.CooldownSeconds);
            c.Press("a");
            Assert.That(c.desk.IsOpen(0), Is.True);
            Assert.That(c.View("b"), Is.EqualTo(SurrenderPanel.View.Partner));
        }

        [Test]
        public void EachTeamHasItsOwnRequest()
        {
            var c = Doubles();
            c.Press("a");
            c.Press("x");
            Assert.That(c.desk.IsOpen(0) && c.desk.IsOpen(1), Is.True);
            c.Yes("a");
            c.Yes("x");
            Assert.That(c.loser, Is.EqualTo(-1), "two players of different teams are not an agreement");
            c.Yes("y");
            Assert.That(c.loser, Is.EqualTo(1));
            Assert.That(c.losers, Is.EquivalentTo(new[] { "x", "y" }));
        }

        [Test]
        public void BothPartnersPressingTheButtonStillHaveToConfirm()
        {
            var c = Doubles();
            c.Press("a");
            c.desk.Ask("b");
            c.Frame();
            Assert.That(c.desk.IsOpen(0), Is.True);
            Assert.That(c.loser, Is.EqualTo(-1));
            c.Yes("a");
            c.Yes("b");
            Assert.That(c.loser, Is.EqualTo(0));
        }

        [Test]
        public void PlayerNextToABotOrAloneDecidesAlone()
        {
            court = new Court(PlayMode.Doubles, false, Human("a", 0), Bot("b", 0), Human("x", 1), Bot("y", 1));
            court.Press("a");
            Assert.That(court.Panel("a").BodyText, Does.StartWith("Trận kết thúc ngay và đội bạn bị xử thua"));
            court.Yes("a");
            Assert.That(court.loser, Is.EqualTo(0));
            Assert.That(court.losers, Is.EqualTo(new[] { "a" }), "the bot is not a deserter");
            court.Destroy();
            court = new Court(PlayMode.Doubles, true, Human("a", 0), Human("x", 1), Human("y", 1));
            court.Press("a");
            court.Yes("a");
            Assert.That(court.loser, Is.EqualTo(0), "an underfilled private room");
        }

        [Test]
        public void StrangersAndBotsCannotSurrenderForATeam()
        {
            court = new Court(PlayMode.Doubles, false, Human("a", 0), Bot("b", 0), Human("x", 1), Human("y", 1));
            court.desk.Agree("b");
            court.desk.Agree("nobody");
            court.desk.Ask("nobody");
            court.desk.Refuse(null);
            Assert.That(court.loser, Is.EqualTo(-1));
        }

        [Test]
        public void DisconnectedPartnerEndsTheRequest()
        {
            var c = Doubles();
            c.Press("a");
            c.Yes("a");
            c.offline.Add("b");
            c.desk.Disconnected("b");
            c.Frame();
            Assert.That(c.desk.IsOpen(0), Is.False);
            Assert.That(c.Panel("a").BodyText, Is.EqualTo("B đang mất kết nối nên chưa thể đầu hàng."));
            c.Wait(SurrenderDesk.CooldownSeconds + SurrenderPanel.NoticeSeconds);
            c.Press("a");
            Assert.That(c.desk.IsOpen(0), Is.False, "the missing partner has to come back first");
            Assert.That(c.Panel("a").BodyText, Does.Contain("đang mất kết nối"));
        }

        [Test]
        public void SilentServerIsReportedInsteadOfWaitingForEver()
        {
            var c = Singles();
            c.serverAnswers = false;
            c.Press("a");
            c.Yes("a");
            Assert.That(c.Panel("a").BodyText, Is.EqualTo(SurrenderText.Sending));
            c.Wait(SurrenderPanel.AnswerSeconds + .1);
            Assert.That(c.View("a"), Is.EqualTo(SurrenderPanel.View.Notice));
            Assert.That(c.Panel("a").BodyText, Is.EqualTo(SurrenderText.NoAnswer));
            c.serverAnswers = true;
            c.Wait(SurrenderPanel.NoticeSeconds + .1);
            c.Press("a");
            c.Yes("a");
            Assert.That(c.loser, Is.EqualTo(0));
        }

        [Test]
        public void AnsweredBoxDoesNotFlashBackWhileTheServerCloses()
        {
            var c = Doubles();
            c.Press("a");
            c.serverAnswers = false;
            c.No("b");
            Assert.That(c.View("b"), Is.EqualTo(SurrenderPanel.View.Hidden));
            // The proposer confirms at the same moment: the server repeats the request to both partners.
            c.desk.Agree("a");
            c.Frame();
            Assert.That(c.View("b"), Is.EqualTo(SurrenderPanel.View.Hidden));
            c.serverAnswers = true;
            c.desk.Refuse("b");
            c.Frame();
            Assert.That(c.View("a"), Is.EqualTo(SurrenderPanel.View.Notice));
        }

        [Test]
        public void HiddenScreensHideTheBoxButKeepTheRequest()
        {
            var c = Doubles();
            c.Press("a");
            c.Panel("b").Refresh(c.clients["b"], hidden: true);
            Assert.That(c.Panel("b").Card.gameObject.activeSelf, Is.False, "settings, help and cinematics cover the court");
            c.Frame();
            Assert.That(c.Panel("b").Card.gameObject.activeSelf, Is.True);
            Assert.That(c.View("b"), Is.EqualTo(SurrenderPanel.View.Partner));
        }

        [Test]
        public void ButtonsNeverTakeTheKeyboardNavigation()
        {
            var c = Singles();
            Assert.That(c.Panel("a").AcceptButton.navigation.mode, Is.EqualTo(UnityEngine.UI.Navigation.Mode.None));
            Assert.That(c.Panel("a").RefuseButton.navigation.mode, Is.EqualTo(UnityEngine.UI.Navigation.Mode.None));
        }

        [Test]
        public void ResultIsLabelledAsASurrender()
        {
            MatchResult Result(MatchOutcome outcome, bool forfeit, int opponents = 1) => new MatchResult
            {
                id = "m", kind = MatchKind.RankedSingles, outcome = outcome, localForfeit = forfeit, reason = MatchResult.SurrenderReason,
                scores = new List<GameScore> { new GameScore { own = 3, opponent = 5 } },
                opponents = Enumerable.Range(0, opponents).Select(i => new OpponentInfo { id = "o" + i, name = "O" + i }).ToList(),
            };
            Assert.That(Result(MatchOutcome.Lost, true).OutcomeLabel, Is.EqualTo("Thua · Đầu hàng"));
            Assert.That(MatchSummaryFormat.Context(Result(MatchOutcome.Lost, true)), Is.EqualTo("Bạn đã đầu hàng · trận được xử thua"));
            Assert.That(MatchSummaryFormat.Context(Result(MatchOutcome.Lost, true, 2)), Is.EqualTo("Đội bạn đã đầu hàng · trận được xử thua"));
            Assert.That(Result(MatchOutcome.Won, false).OutcomeLabel, Is.EqualTo("Thắng"));
            Assert.That(MatchSummaryFormat.Context(Result(MatchOutcome.Won, false)), Is.EqualTo("Đối thủ đã đầu hàng · bạn được xử thắng"));
            var left = Result(MatchOutcome.Lost, true);
            left.reason = "Máy chủ kết thúc trận do rời trận hoặc mất kết nối.";
            Assert.That(left.OutcomeLabel, Is.EqualTo("Thua · Bỏ trận"));
            Assert.That(MatchSummaryFormat.Context(left), Is.EqualTo("Bạn đã rời trận · trận được xử thua"));
            var interrupted = Result(MatchOutcome.Interrupted, false);
            Assert.That(interrupted.OutcomeLabel, Is.EqualTo("Gián đoạn"));
        }

        [Test]
        public void ReasonNamesThePlayersWhoGaveUp()
        {
            Assert.That(SurrenderText.Reason(new[] { "Huy" }), Is.EqualTo("Huy đã đầu hàng"));
            Assert.That(SurrenderText.Reason(new[] { "Huy", "An" }), Is.EqualTo("Huy và An đã đầu hàng"));
            Assert.That(SurrenderText.Reason(Array.Empty<string>()), Is.EqualTo("Một đội đã đầu hàng"));
        }
    }
}
