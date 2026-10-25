using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Entities.Models;
using Repositories.UnitOfWork;
using Services.DTOs;
using Services.Interfaces;

namespace Services.Implementations
{
    public class ReplayService : IReplayService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStorageService _storageService;

        public ReplayService(IUnitOfWork unitOfWork, IStorageService storageService)
        {
            _unitOfWork = unitOfWork;
            _storageService = storageService;
        }

        public async Task<(bool Success, string Message, string? ReplayUrl)> SaveReplayAsync(Guid matchId, string replayJsonData)
        {
            var match = await _unitOfWork.Matches.GetMatchByIdAsync(matchId);
            if (match == null)
            {
                return (false, "Trận đấu không tồn tại.", null);
            }

            var fileName = $"replays/match_{matchId}.json";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(replayJsonData));
            var storedKey = await _storageService.UploadFileAsync(stream, fileName, "application/json");
            var replayUrl = _storageService.GetFileUrl(storedKey);

            var existingReplay = await _unitOfWork.Matches.GetTelemetryReplayAsync(matchId);
            if (existingReplay == null)
            {
                existingReplay = new Matchhistoryreplay
                {
                    Matchid = matchId,
                    Replayurl = replayUrl
                };
                await _unitOfWork.Matches.AddTelemetryReplayAsync(existingReplay);
            }
            else
            {
                existingReplay.Replayurl = replayUrl;
                await _unitOfWork.Matches.UpdateTelemetryReplayAsync(existingReplay);
            }

            await _unitOfWork.SaveChangesAsync();
            return (true, "Lưu file Replay trận đấu thành công!", replayUrl);
        }

        public async Task<MatchReplayResponse?> GetReplayAsync(Guid matchId)
        {
            var replay = await _unitOfWork.Matches.GetTelemetryReplayAsync(matchId);
            if (replay == null)
                return null;

            string? content = null;
            var uri = new Uri(replay.Replayurl);
            var key = Path.GetFileName(uri.LocalPath);

            var stream = await _storageService.GetFileAsync(key);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                content = await reader.ReadToEndAsync();
            }

            return new MatchReplayResponse
            {
                MatchId = matchId,
                ReplayUrl = replay.Replayurl,
                ReplayContent = content
            };
        }
    }
}
