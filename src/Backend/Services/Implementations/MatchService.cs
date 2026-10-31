using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Entities.Models;
using Repositories.UnitOfWork;
using Entities.DTOs;
using Services.Interfaces;

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

        public async Task<bool> ReportPlayerAsync(Guid reporterId, ReportPlayerRequest request)
        {
            var match = await _unitOfWork.Matches.GetMatchByIdAsync(request.MatchId);
            if (match == null || reporterId == request.ReportedPlayerId
                || !match.Gamematchplayers.Any(p => p.Playerid == reporterId && p.Finalizedat != null)
                || !match.Gamematchplayers.Any(p => p.Playerid == request.ReportedPlayerId && p.Finalizedat != null))
                return false;

            var existingReport = await _unitOfWork.Matches.GetReportAsync(reporterId, request.ReportedPlayerId, request.MatchId);

            if (existingReport != null)
            {
                existingReport.Reason = request.Reason;
                existingReport.Updatedat = DateTime.UtcNow;
                await _unitOfWork.Matches.UpdateReportAsync(existingReport);
            }
            else
            {
                var report = new Reportplayerinmatch
                {
                    Reporterid = reporterId,
                    Reportedplayerid = request.ReportedPlayerId,
                    Matchid = request.MatchId,
                    Reason = request.Reason,
                    Status = "PENDING",
                    Createdat = DateTime.UtcNow,
                    Updatedat = DateTime.UtcNow
                };
                await _unitOfWork.Matches.AddReportAsync(report);
            }

            await _unitOfWork.SaveChangesAsync();
            return true;
        }
    }
}
