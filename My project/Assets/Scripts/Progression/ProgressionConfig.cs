using System.Collections.Generic;
using UnityEngine;

namespace PickleVerse.Progression
{
    [CreateAssetMenu(menuName = "PickleVerse/Progression Config")]
    public sealed class ProgressionConfig : ScriptableObject
    {
        public int winXp = 10, lossXp = 5;
        public RewardBundle dailyBonus = new RewardBundle(200, 0, 20), weeklyBonus = new RewardBundle(1000, 0, 50);
        public List<ProgressDefinition> definitions = new List<ProgressDefinition>();
        public void SetDefaults()
        {
            definitions.Clear();
            Add("A.Win5", "Thắng 5 trận", ProgressScope.Achievement, ProgressMetric.Wins, 5, 100, 1, 10);
            Add("A.Win25", "Thắng 25 trận", ProgressScope.Achievement, ProgressMetric.Wins, 25, 300, 3, 25);
            Add("A.Win100", "Thắng 100 trận", ProgressScope.Achievement, ProgressMetric.Wins, 100, 1000, 10, 50);
            Add("A.Play10", "Hoàn tất 10 trận", ProgressScope.Achievement, ProgressMetric.Matches, 10, 100, 1, 10);
            Add("A.Play50", "Hoàn tất 50 trận", ProgressScope.Achievement, ProgressMetric.Matches, 50, 400, 4, 25);
            Add("A.Play200", "Hoàn tất 200 trận", ProgressScope.Achievement, ProgressMetric.Matches, 200, 1500, 15, 75);
            Add("A.Point100", "Ghi 100 điểm cho đội", ProgressScope.Achievement, ProgressMetric.Points, 100, 200, 2, 15);
            Add("A.Point500", "Ghi 500 điểm cho đội", ProgressScope.Achievement, ProgressMetric.Points, 500, 600, 6, 35);
            Add("A.Streak5", "Đạt chuỗi thắng 5 trận", ProgressScope.Achievement, ProgressMetric.BestStreak, 5, 500, 5, 30);
            Add("A.Higher5", "Thắng 5 đối thủ rank Elo cao hơn", ProgressScope.Achievement, ProgressMetric.HigherRatedWins, 5, 300, 3, 25);
            Add("D.Play1", "Hoàn tất 1 trận", ProgressScope.Daily, ProgressMetric.Matches, 1, 50, 0, 5);
            Add("D.Win1", "Thắng 1 trận", ProgressScope.Daily, ProgressMetric.Wins, 1, 75, 0, 5);
            Add("D.Point20", "Ghi 20 điểm cho đội", ProgressScope.Daily, ProgressMetric.Points, 20, 75, 0, 5);
            Add("D.Return30", "Trả bóng hợp lệ 30 lần", ProgressScope.Daily, ProgressMetric.Returns, 30, 75, 0, 5);
            Add("D.Serve10", "Giao bóng hợp lệ 10 lần", ProgressScope.Daily, ProgressMetric.Serves, 10, 75, 0, 5);
            Add("D.Bot1", "Hoàn tất 1 trận với bot", ProgressScope.Daily, ProgressMetric.BotMatches, 1, 75, 0, 5);
            Add("D.HumanWin1", "Thắng 1 trận tìm đối thủ người thật", ProgressScope.Daily, ProgressMetric.HumanWins, 1, 125, 1, 10);
            Add("W.Play10", "Hoàn tất 10 trận", ProgressScope.Weekly, ProgressMetric.Matches, 10, 300, 1, 20);
            Add("W.Win5", "Thắng 5 trận", ProgressScope.Weekly, ProgressMetric.Wins, 5, 250, 1, 15);
            Add("W.Point100", "Ghi 100 điểm cho đội", ProgressScope.Weekly, ProgressMetric.Points, 100, 250, 1, 15);
            Add("W.Return200", "Trả bóng hợp lệ 200 lần", ProgressScope.Weekly, ProgressMetric.Returns, 200, 250, 1, 15);
            Add("W.Serve50", "Giao bóng hợp lệ 50 lần", ProgressScope.Weekly, ProgressMetric.Serves, 50, 250, 1, 15);
            Add("W.Bot5", "Hoàn tất 5 trận với bot", ProgressScope.Weekly, ProgressMetric.BotMatches, 5, 250, 1, 15);
            Add("W.Human5", "Hoàn tất 5 trận tìm đối thủ người thật", ProgressScope.Weekly, ProgressMetric.HumanMatches, 5, 350, 2, 20);
            Add("W.RankWin3", "Thắng 3 trận rank 1v1", ProgressScope.Weekly, ProgressMetric.RankedWins, 3, 350, 2, 20);
            Add("W.DoubleWin2", "Thắng 2 trận tìm trận 2v2", ProgressScope.Weekly, ProgressMetric.DoublesWins, 2, 400, 2, 20);
            Add("W.Streak3", "Đạt chuỗi thắng 3 trận trong tuần", ProgressScope.Weekly, ProgressMetric.BestStreak, 3, 350, 2, 20);
        }
        void Add(string id, string title, ProgressScope scope, ProgressMetric metric, int target, int coins, int gems, int xp)
        {
            definitions.Add(new ProgressDefinition(id, title, scope, metric, target, new RewardBundle(coins, 0, xp)));
        }
        public static ProgressionConfig Defaults()
        {
            var config = CreateInstance<ProgressionConfig>();
            config.SetDefaults();
            return config;
        }
    }
}
