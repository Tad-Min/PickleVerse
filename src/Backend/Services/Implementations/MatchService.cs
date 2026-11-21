using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Entities.Models;
using Repositories.UnitOfWork;
using Entities.DTOs;
using Services.Interfaces;
using Entities.Game;
using Entities.Exceptions;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using Repositories.Game;

namespace Services.Implementations
{
    public class MatchService : IMatchService
    {
        private readonly IUnitOfWork _unitOfWork;

        public MatchService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public Task<Guid> FinishMatchAsync(FinishMatchRequest request)
            => throw new Entities.Exceptions.AppException("Dùng API trận đấu đã xác thực để hoàn tất trận.", System.Net.HttpStatusCode.Gone);

        public async Task<List<MatchItemDto>> GetMyMatchHistoryAsync(Guid playerId, int take = 20)
        {
            var matches = await _unitOfWork.Matches.GetPlayerMatchesAsync(playerId, take);

            var result = new List<MatchItemDto>();
            foreach (var m in matches)
            {
                var replay = await _unitOfWork.Matches.GetTelemetryReplayAsync(m.Matchid);
                var participant = m.Gamematchplayers.FirstOrDefault(p => p.Playerid == playerId);
                var score = Repositories.Game.MatchScoreStore.GameWins(m.Matchscores);

                result.Add(new MatchItemDto
                {
                    MatchId = m.Matchid,
                    MatchType = m.Kind,
                    TeamAScore = score.teamA,
                    TeamBScore = score.teamB,
                    CreatedAt = m.Endedat!.Value,
                    IsWinner = participant?.Outcome == "WIN",
                    ReceivedElo = m.Ratingledgers.SingleOrDefault(r => r.Playerid == playerId)?.Delta ?? 0,
                    ReplayUrl = replay == null ? null : $"/api/Replays/{m.Matchid}"
                });
            }

            return result;
        }

        public async Task<List<ReportTargetDto>> ReportTargets(Guid player, Guid id)
        {
            var match = await _unitOfWork.Matches.GetMatchByIdAsync(id);
            if (match == null || !match.Gamematchplayers.Any(p => p.Playerid == player && p.Finalizedat != null))
                throw new Entities.Exceptions.NotFoundException("Không tìm thấy trận đã hoàn tất của bạn.");
            return match.Gamematchplayers.Where(p => CanReport(player, p))
                .Select(p => new ReportTargetDto { PlayerId = p.Playerid, PlayerName = p.Player.Playername, Team = p.Team }).ToList();
        }

        static bool CanReport(Guid viewer, Gamematchplayer seat) => seat.Playerid != viewer && seat.Finalizedat != null
            && !RankedBotAccounts.Contains(seat.Playerid.ToString("N"));

        public async Task<MatchDetail> Detail(Guid player, Guid id)
        {
            var match = await _unitOfWork.Game.Match(m => m.Matchid == id && m.Status == Entities.Enums.MatchStatus.Finished
                && m.Gamematchplayers.Any(seat => seat.Playerid == player && seat.Finalizedat != null))
                ?? throw new NotFoundException("Không tìm thấy trận đã hoàn tất của bạn.");
            var analyses = (await _unitOfWork.Game.MatchAnalyses(id)).ToDictionary(row => row.PlayerId);
            var games = match.Matchscores.OrderBy(score => score.Gamenumber)
                .Select(score => new MatchDetailGame(score.Gamenumber, score.Teamascore, score.Teambscore,
                    score.Completed, score.Teamastartside)).ToList();
            var players = match.Gamematchplayers.OrderBy(seat => seat.Rosterindex).ThenBy(seat => seat.Playerid)
                .Select(seat =>
                {
                    var analysis = analyses.GetValueOrDefault(seat.Playerid);
                    var byGame = analysis?.Result == null ? [] : GameJson.Read<CoachingAnalysis>(analysis.Result).gameMetrics ?? [];
                    var tags = byGame.Select(game => new MatchGameTags(game.gameNumber, game.metrics
                        .Select(metric => new MatchTag(metric.technique, metric.code, metric.errors, metric.opportunities)).ToList())).ToList();
                    string state = analysis?.Result != null ? (tags.Count > 0 ? "ready" : "missing")
                        : analysis?.State == "failed" ? "failed" : analysis != null ? "pending" : "missing";
                    return new MatchDetailPlayer(seat.Playerid, seat.Player.Playername, seat.Team,
                        CanReport(player, seat), state, tags);
                }).ToList();
            var wins = MatchScoreStore.GameWins(match.Matchscores);
            var own = match.Gamematchplayers.Single(seat => seat.Playerid == player);
            return new MatchDetail(id, match.Kind, match.Mode, match.Endedat!.Value,
                match.Ratingledgers.SingleOrDefault(row => row.Playerid == player)?.Delta,
                wins.teamA, wins.teamB, own.Endreason, games, players);
        }

        public async Task<bool> ReportPlayerAsync(Guid reporterId, ReportPlayerRequest request)
        {
            if (request.Reason == null || request.Reason.Trim().Length < 3 || request.Reason.Trim().Length > 254)
                throw new Entities.Exceptions.BadRequestException("Lý do tố cáo cần từ 3 đến 254 ký tự.");
            if (PickleVerse.Multiplayer.RankedBotAccounts.Contains(request.ReportedPlayerId.ToString("N"))) return false;
            await using var tx = await _unitOfWork.Game.BeginTransactionAsync();
            await _unitOfWork.Game.Lock($"report:{reporterId}:{request.ReportedPlayerId}:{request.MatchId}");
            var match = await _unitOfWork.Matches.GetMatchByIdAsync(request.MatchId);
            if (match == null || reporterId == request.ReportedPlayerId
                || !match.Gamematchplayers.Any(p => p.Playerid == reporterId && p.Finalizedat != null)
                || !match.Gamematchplayers.Any(p => p.Playerid == request.ReportedPlayerId && p.Finalizedat != null)) return false;
            var existing = await _unitOfWork.Matches.GetReportAsync(reporterId, request.ReportedPlayerId, request.MatchId);
            string reason = request.Reason.Trim();
            if (existing != null)
            {
                if (existing.Reason != reason) throw new Entities.Exceptions.AppException("Bạn đã gửi tố cáo cho người chơi này trong trận.", System.Net.HttpStatusCode.Conflict);
                return true;
            }
            await _unitOfWork.Matches.AddReportAsync(new Reportplayerinmatch
            {
                Reporterid = reporterId, Reportedplayerid = request.ReportedPlayerId, Matchid = request.MatchId, Reason = reason,
                Status = ReportStatus.Pending, Createdat = DateTime.UtcNow, Updatedat = DateTime.UtcNow
            });
            await _unitOfWork.SaveChangesAsync(); await tx.CommitAsync(); return true;
        }
    }
}
