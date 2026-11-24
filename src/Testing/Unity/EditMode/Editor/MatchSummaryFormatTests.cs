using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using F = PickleVerse.Multiplayer.MatchSummaryFormat;

namespace PickleVerse.Tests.Multiplayer
{
    public sealed class MatchSummaryFormatTests
    {
        static MatchResult Result(MatchOutcome outcome, params (int own, int opp, bool done)[] games) => new MatchResult
        {
            id = "m", kind = MatchKind.RankedSingles, outcome = outcome, ownElo = 116, eloDelta = 15,
            gamesWon = games.Count(g => g.done && g.own > g.opp), gamesLost = games.Count(g => g.done && g.own < g.opp),
            scores = games.Select(g => new GameScore { own = g.own, opponent = g.opp, completed = g.done }).ToList(),
            opponents = new List<OpponentInfo> { new OpponentInfo { id = "o", name = "Huy", elo = 120 } },
        };

        [Test]
        public void TitleNamesTheOutcome()
        {
            Assert.That(F.Title(Result(MatchOutcome.Won)), Is.EqualTo("CHIẾN THẮNG"));
            Assert.That(F.Title(Result(MatchOutcome.Lost)), Is.EqualTo("THẤT BẠI"));
            Assert.That(F.Title(Result(MatchOutcome.Interrupted)), Is.EqualTo("TRẬN ĐẤU GIÁN ĐOẠN"));
        }

        [Test]
        public void ContextExplainsHowTheMatchWasDecided()
        {
            Assert.That(F.Context(Result(MatchOutcome.Won, (11, 7, true), (8, 11, true), (11, 9, true))), Is.EqualTo("Thắng 2–1 sau 3 ván"));
            // The reported screen: a win with game 1 unfinished at 0–0 is the opponent leaving, not "Ván 1: 0–0 (chưa xong)".
            var forfeit = Result(MatchOutcome.Won, (0, 0, false));
            Assert.That(F.EndedEarly(forfeit), Is.True);
            Assert.That(F.Context(forfeit), Does.StartWith("Đối thủ rời trận"));
            // Leaving between two games stores no unfinished game, but one game won is not a finished best of three.
            Assert.That(F.Context(Result(MatchOutcome.Won, (11, 5, true))), Does.StartWith("Đối thủ rời trận"));
            var left = Result(MatchOutcome.Lost, (3, 5, false));
            left.localForfeit = true;
            Assert.That(F.Context(left), Is.EqualTo("Bạn đã rời trận · trận được xử thua"));
            Assert.That(F.Context(Result(MatchOutcome.Lost, (11, 4, true), (6, 2, false))), Does.StartWith("Trận kết thúc sớm"));
            Assert.That(F.Context(Result(MatchOutcome.Interrupted)), Does.StartWith("Trận đã dừng"));
        }

        [Test]
        public void EloLineShowsBeforeAndAfterOnlyForRatedMatches()
        {
            var ranked = Result(MatchOutcome.Won, (11, 7, true));
            Assert.That(F.Signed(15), Is.EqualTo("+15"));
            Assert.That(F.Signed(-12), Is.EqualTo("-12"));
            Assert.That(F.Signed(0), Is.EqualTo("0"));
            Assert.That(F.EloLine(ranked, 131), Is.EqualTo("116 → 131 Elo"));
            ranked.kind = MatchKind.Custom;
            Assert.That(F.EloLine(ranked, 131), Is.EqualTo("Phòng riêng không tính Elo"));
        }

        [Test]
        public void RankFloorsMatchTheEloPolicyRanks()
        {
            Assert.That(F.RankFloors.Select(EloPolicy.Rank).Distinct().Count(), Is.EqualTo(F.RankFloors.Length));
            for (int i = 1; i < F.RankFloors.Length; i++)
            {
                int floor = F.RankFloors[i];
                Assert.That(EloPolicy.Rank(floor - 1), Is.Not.EqualTo(EloPolicy.Rank(floor)), "floor " + floor);
                Assert.That(F.RankTier(floor), Is.EqualTo(i));
                Assert.That(F.RankTier(floor - 1), Is.EqualTo(i - 1));
            }
        }

        [Test]
        public void RankProgressCountsTowardsTheNextRank()
        {
            Assert.That(F.NextRankElo(131), Is.EqualTo(500));
            Assert.That(F.RankProgress(EloPolicy.Minimum), Is.EqualTo(0));
            Assert.That(F.RankProgress(300), Is.EqualTo(.5f).Within(1e-4f));
            Assert.That(F.RankNote(131), Is.EqualTo("131 Elo · còn 369 lên BẠC"));
            Assert.That(F.NextRankElo(9000), Is.Null);
            Assert.That(F.RankProgress(9000), Is.EqualTo(1));
            Assert.That(F.RankNote(9000), Does.EndWith("hạng cao nhất"));
        }

        [Test]
        public void RankShiftOnlyForRatedMatchesThatCrossAFloor()
        {
            var promoted = Result(MatchOutcome.Won, (11, 7, true));
            promoted.ownElo = 490;
            Assert.That(F.RankShift(promoted), Is.EqualTo(1));
            promoted.eloDelta = -15;
            promoted.ownElo = 505;
            Assert.That(F.RankShift(promoted), Is.EqualTo(-1));
            promoted.kind = MatchKind.Custom;
            Assert.That(F.RankShift(promoted), Is.EqualTo(0));
        }

        [Test]
        public void GameChipsAndProgressRatio()
        {
            Assert.That(F.GameChip(new GameScore { own = 11, opponent = 7, completed = true }), Is.EqualTo("11–7"));
            Assert.That(F.GameChip(new GameScore { own = 4, opponent = 6, completed = false }), Is.EqualTo("4–6 · dở"));
            Assert.That(F.Ratio(10, 20), Is.EqualTo(.5f));
            Assert.That(F.Ratio(5, 0), Is.EqualTo(0));
            Assert.That(F.Ratio(30, 20), Is.EqualTo(1));
        }
    }
}
