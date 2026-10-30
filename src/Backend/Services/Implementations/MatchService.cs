using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Entities.Models;
using Repositories.UnitOfWork;
using Services.DTOs;
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

        public async Task<Guid> FinishMatchAsync(FinishMatchRequest request)
        {
            var matchId = Guid.NewGuid();

            var match = new Matchhistory
            {
                Matchid = matchId,
                Createdat = DateTime.UtcNow,
                Matchtype = request.MatchType,
                Teamascore = request.TeamAScore,
                Teambscore = request.TeamBScore
            };

            await _unitOfWork.Matches.AddMatchAsync(match);

            // Tính toán Elo
            bool teamAWon = request.TeamAScore > request.TeamBScore;

            var playerProfiles = new List<Playerprofile>();
            foreach (var p in request.Players)
            {
                var prof = await _unitOfWork.Profiles.GetByUserIdAsync(p.PlayerId);
                if (prof != null)
                    playerProfiles.Add(prof);
            }

            var teamAPlayers = request.Players.Where(p => p.IsTeamA).Select(p => p.PlayerId).ToList();
            var teamBPlayers = request.Players.Where(p => !p.IsTeamA).Select(p => p.PlayerId).ToList();

            double avgEloA = playerProfiles.Where(p => teamAPlayers.Contains(p.Userid)).Select(p => p.Elo).DefaultIfEmpty(100).Average();
            double avgEloB = playerProfiles.Where(p => teamBPlayers.Contains(p.Userid)).Select(p => p.Elo).DefaultIfEmpty(100).Average();

            // Công thức Elo chuẩn quốc tế
            double expectedA = 1.0 / (1.0 + Math.Pow(10, (avgEloB - avgEloA) / 400.0));
            int kFactor = 32;

            int eloChangeA = (int)Math.Round(kFactor * ((teamAWon ? 1.0 : 0.0) - expectedA));
            int eloChangeB = -eloChangeA;

            foreach (var item in request.Players)
            {
                var profile = playerProfiles.FirstOrDefault(p => p.Userid == item.PlayerId);
                bool isWinner = item.IsTeamA ? teamAWon : !teamAWon;
                int deltaElo = item.IsTeamA ? eloChangeA : eloChangeB;

                var pmh = new Playermatchhistory
                {
                    Playerid = item.PlayerId,
                    Matchid = matchId,
                    Status = isWinner ? "WIN" : "LOSE",
                    Isteama = item.IsTeamA,
                    Receivedelo = deltaElo
                };

                await _unitOfWork.Matches.AddParticipantAsync(pmh);

                if (profile != null)
                {
                    profile.Elo = Math.Max(100, profile.Elo + deltaElo);

                    if (isWinner)
                    {
                        profile.Wincount += 1;
                        profile.Winstreak += 1;
                    }
                    else
                    {
                        profile.Losecount += 1;
                        profile.Winstreak = 0;
                    }

                    await _unitOfWork.Profiles.UpdateProfileAsync(profile);
                }
            }

            await _unitOfWork.SaveChangesAsync();
            return matchId;
        }

        public async Task<List<MatchItemDto>> GetMyMatchHistoryAsync(Guid playerId, int take = 20)
        {
            var matches = await _unitOfWork.Matches.GetPlayerMatchesAsync(playerId, take);

            var result = new List<MatchItemDto>();
            foreach (var m in matches)
            {
                var replay = await _unitOfWork.Matches.GetTelemetryReplayAsync(m.Matchid);
                var participant = m.Playermatchhistories.FirstOrDefault(p => p.Playerid == playerId);

                result.Add(new MatchItemDto
                {
                    MatchId = m.Matchid,
                    MatchType = m.Matchtype,
                    TeamAScore = m.Teamascore,
                    TeamBScore = m.Teambscore,
                    CreatedAt = m.Createdat,
                    IsWinner = participant?.Status == "WIN",
                    ReceivedElo = participant?.Receivedelo ?? 0,
                    ReplayUrl = replay == null ? null : $"/api/Replays/{m.Matchid}"
                });
            }

            return result;
        }

        public async Task<bool> ReportPlayerAsync(Guid reporterId, ReportPlayerRequest request)
        {
            var match = await _unitOfWork.Matches.GetMatchByIdAsync(request.MatchId);
            if (match == null)
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
