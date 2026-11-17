using System;
using System.Linq;
using PickleVerse.Progression;
using PickleVerse.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.FrontEnd
{
    public sealed partial class PlayerProgressionMenu
    {
        [Serializable] sealed class DetailData
        {
            public int teamAWins, teamBWins, mode;
            public int? eloDelta;
            public string kind, reason;
            public DateTime endedAt;
            public GameData[] games;
            public PlayerData[] players;
        }
        [Serializable] sealed class GameData
        {
            public int gameNumber, teamAScore, teamBScore;
            public int? teamAStartSide;
            public bool completed;
        }
        [Serializable] sealed class PlayerData
        {
            public Guid playerId;
            public string name, analysisState;
            public int team;
            public bool canReport;
            public GameTechniqueMetrics[] games;
        }

        RectTransform detailPanel, detailRows;
        ScrollRect detailScroll;
        TMP_Text detailTitle, detailSeriesScore, detailMetadata, detailElo;
        Button detailScores, detailStats;
        MatchResult detailMatch;
        DetailData detailData;
        bool showStatistics;
        int detailRequest;
        float historyPosition;

        void BuildMatchDetail()
        {
            detailPanel = Panel("MatchDetail", window, Ink);
            detailPanel.GetComponent<Image>().raycastTarget = true;
            MakeButton("BackToHistory", detailPanel, "‹  Quay lại", BackToHistory, new Vector2(.025f,.885f),
                new Vector2(.17f,.963f), Card, White, 18);
            detailTitle = Text("MatchHeading", detailPanel, "", 30, White, new Vector2(.20f,.918f), new Vector2(.74f,.980f),
                Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
            detailSeriesScore = Text("SeriesScore", detailPanel, "", 23, Mint, new Vector2(.20f,.860f), new Vector2(.74f,.918f),
                Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
            detailMetadata = Text("MatchMetadata", detailPanel, "", 18, Muted, new Vector2(.75f,.918f), new Vector2(.965f,.980f),
                Vector2.zero, Vector2.zero, TextAlignmentOptions.MidlineRight);
            detailElo = Text("MatchElo", detailPanel, "", 18, White, new Vector2(.75f,.865f), new Vector2(.965f,.915f),
                Vector2.zero, Vector2.zero, TextAlignmentOptions.MidlineRight);
            detailScores = MakeButton("ScoreTab", detailPanel, "Tỉ số", () => SelectMatchDetail(false), new Vector2(.025f,.772f),
                new Vector2(.18f,.85f), Card, White, 19);
            detailStats = MakeButton("StatisticsTab", detailPanel, "Thông số", () => SelectMatchDetail(true), new Vector2(.19f,.772f),
                new Vector2(.36f,.85f), Card, White, 19);
            MakeButton("RefreshMatch", detailPanel, "Làm mới", LoadMatchDetail, new Vector2(.82f,.772f),
                new Vector2(.965f,.85f), Card, White, 17);
            var holder = Panel("DetailScroll", detailPanel, Surface, new Vector2(.025f,.035f), new Vector2(.975f,.748f));
            var viewport = Panel("Viewport", holder, Ink, new Vector2(.007f,.01f), new Vector2(.993f,.99f));
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            detailRows = Rect("Content", viewport, new Vector2(0,1), Vector2.one);
            detailRows.pivot = new Vector2(.5f,1);
            var layout = detailRows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(8,8,8,8);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            detailRows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            detailScroll = holder.gameObject.AddComponent<ScrollRect>();
            detailScroll.viewport = viewport;
            detailScroll.content = detailRows;
            detailScroll.horizontal = false;
            detailScroll.movementType = ScrollRect.MovementType.Clamped;
            detailScroll.scrollSensitivity = 42;
        }

        void OpenMatchDetail(MatchResult result)
        {
            if (!detailPanel) BuildMatchDetail();
            historyPosition = scroll.verticalNormalizedPosition;
            detailMatch = result;
            detailData = null;
            showStatistics = false;
            detailPanel.SetAsLastSibling();
            detailPanel.gameObject.SetActive(true);
            detailTitle.text = result.KindLabel;
            detailSeriesScore.text = detailMetadata.text = detailElo.text = "";
            LoadMatchDetail();
        }

        void BackToHistory()
        {
            detailRequest++;
            detailPanel.gameObject.SetActive(false);
            scroll.verticalNormalizedPosition = historyPosition;
        }

        async void LoadMatchDetail()
        {
            if (detailMatch == null) return;
            int request = ++detailRequest;
            var account = AccountRuntime.Ensure().Account;
            int session = account.SessionVersion;
            bool Current() => this && detailPanel.gameObject.activeSelf && session == account.SessionVersion && request == detailRequest;
            ResetDetailRows();
            DetailMessage("Đang tải chi tiết trận…");
            try
            {
                if (!account.IsSignedIn)
                {
                    detailData = LocalDetail(detailMatch);
                    RenderMatchDetail();
                    return;
                }
                var result = await account.SendAuthorizedAsync<DetailData>("GET", $"api/Matches/{detailMatch.id}");
                if (!Current()) return;
                detailData = result;
                RenderMatchDetail();
            }
            catch (Exception error)
            {
                if (!Current()) return;
                ResetDetailRows();
                DetailMessage(error.Message);
            }
        }

        DetailData LocalDetail(MatchResult result) => new DetailData
        {
            teamAWins = result.gamesWon, teamBWins = result.gamesLost, kind = result.kind.ToString(), mode = 1,
            eloDelta = result.Ranked ? result.eloDelta : (int?)null,
            endedAt = new DateTime(result.endedUtcTicks, DateTimeKind.Utc), reason = result.reason,
            games = result.scores.Select((score, index) => new GameData
            {
                gameNumber = index + 1, teamAScore = score.own, teamBScore = score.opponent,
                completed = score.completed, teamAStartSide = score.teamAStartSide
            }).ToArray(),
            players = new[] { new PlayerData { name = ProgressionRuntime.Instance.Player.playerName, team = 0 } }
                .Concat(result.opponents.Select(player => new PlayerData { name = player.name, team = 1 })).ToArray()
        };

        void SelectMatchDetail(bool statistics)
        {
            showStatistics = statistics;
            if (detailData != null) RenderMatchDetail();
        }

        void ResetDetailRows()
        {
            foreach (Transform row in detailRows)
            {
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }
        }

        RectTransform DetailRow(string name, float height, Color color) => AddListPanel(name, detailRows, color, height);

        void DetailMessage(string message)
        {
            var row = DetailRow("Status", 52, Surface);
            Text("Message", row, message, 17, Muted, new Vector2(.012f,0), new Vector2(.988f,1), Vector2.zero, Vector2.zero).richText = false;
        }

        void RenderMatchDetail()
        {
            var data = detailData;
            ResetDetailRows();
            Tab(detailScores, !showStatistics);
            Tab(detailStats, showStatistics);
            detailTitle.text = detailMatch.kind == MatchKind.Custom
                ? $"{detailMatch.KindLabel} · {data.mode}v{data.mode}" : detailMatch.KindLabel;
            detailSeriesScore.text = $"{data.teamAWins} : {data.teamBWins}";
            detailMetadata.text = data.endedAt.ToUniversalTime().AddHours(7).ToString("dd/MM/yyyy  HH:mm:ss");
            detailElo.text = data.eloDelta.HasValue ? $"{data.eloDelta.Value:+0;-0;0} Elo" : "";
            if (!string.IsNullOrEmpty(data.reason)) DetailMessage(data.reason);
            if (data.games.Length == 0) DetailMessage("Trận này chưa ghi tỷ số từng ván.");
            foreach (var game in data.games)
            {
                if (showStatistics) RenderGameTags(game, data.players);
                else RenderGameScore(game, data.players);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(detailRows);
            detailScroll.verticalNormalizedPosition = 1;
        }

        void RenderGameScore(GameData game, PlayerData[] players)
        {
            bool swapped = game.teamAStartSide == 1;
            var leftTeam = players.Where(player => player.team == (swapped ? 1 : 0)).ToArray();
            var rightTeam = players.Where(player => player.team == (swapped ? 0 : 1)).ToArray();
            var row = DetailRow("Game" + game.gameNumber, 82 + 32 * Math.Max(leftTeam.Length, rightTeam.Length), Card);
            string note = !game.completed ? " · Chưa hoàn tất" : "";
            if (!game.teamAStartSide.HasValue) note += " · Chưa ghi phía sân";
            Text("GameNumber", row, $"VÁN {game.gameNumber}{note}", 16, Mint, new Vector2(.03f,.73f), new Vector2(.97f,.97f),
                Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
            TeamColumn(row, leftTeam, swapped ? "ĐỘI B" : "ĐỘI A", swapped ? game.teamBScore : game.teamAScore, .025f, .49f);
            TeamColumn(row, rightTeam, swapped ? "ĐỘI A" : "ĐỘI B", swapped ? game.teamAScore : game.teamBScore, .51f, .975f);
        }

        void TeamColumn(RectTransform row, PlayerData[] players, string team, int score, float x0, float x1)
        {
            var column = Panel(team, row, Surface, new Vector2(x0,.06f), new Vector2(x1,.72f));
            Text("TeamScore", column, $"{team}   {score}", 21, White, new Vector2(.05f,.63f), new Vector2(.95f,.98f),
                Vector2.zero, Vector2.zero);
            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i];
                float top = .58f - .28f * i;
                // The shorter one-player card can use the space reserved for the second row in doubles.
                float bottom = players.Length == 1 ? .12f : top - .27f;
                Text("Player" + i, column, player.name, 19, White, new Vector2(.05f,bottom), new Vector2(.8f,top),
                    Vector2.zero, Vector2.zero).richText = false;
#if !UNITY_SERVER
                if (player.canReport)
                    MakeButton("ReportPlayer" + i, column, "!", () => ModerationOverlay.ShowReport(detailMatch.id, player.playerId),
                        new Vector2(.85f,bottom), new Vector2(.96f,top), Card, Lime, 17);
#endif
            }
        }

        void RenderGameTags(GameData game, PlayerData[] players)
        {
            var heading = DetailRow("GameTags" + game.gameNumber, 48, Card);
            Text("GameNumber", heading, "VÁN " + game.gameNumber, 18, Mint, new Vector2(.025f,0),
                new Vector2(.975f,1), Vector2.zero, Vector2.zero);
            foreach (var player in players)
            {
                var metrics = player.games?.FirstOrDefault(entry => entry.gameNumber == game.gameNumber)?.metrics;
                string[] lines = metrics?.Where(metric => metric.opportunities > 0).OrderByDescending(metric => metric.errors)
                    .ThenBy(metric => metric.technique).Select(metric =>
                        $"{metric.technique} · {metric.code}    {metric.errors}/{metric.opportunities} lỗi    {100d * metric.errors / metric.opportunities:0.#}%").ToArray();
                if (lines == null || lines.Length == 0)
                    lines = new[] { player.analysisState == "pending" ? "Đang phân tích…" : player.analysisState == "failed"
                        ? "Phân tích lỗi" : "Chưa có thống kê cho ván này" };
                var row = DetailRow("PlayerTags", 52 + 28 * lines.Length, Surface);
                var names = Text("PlayerAndTags", row, player.name + "\n" + string.Join("\n", lines), 18, White,
                    new Vector2(.025f,0), new Vector2(.975f,1), Vector2.zero, Vector2.zero);
                names.richText = false;
                names.lineSpacing = 10;
            }
        }
    }
}
