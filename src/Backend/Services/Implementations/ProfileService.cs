using Services.Implementations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Entities.Models;
using Microsoft.Extensions.Logging;
using Repositories.UnitOfWork;
using Entities.DTOs;
using Entities.Exceptions;
using Services.Interfaces;

namespace Services.Implementations
{
    public class ProfileService : IProfileService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ProfileService> _logger;

        readonly LeaderboardCache? _leaderboard;
        readonly Repositories.Caching.LobbyRepository? lobby;
        readonly Repositories.Caching.RedisGameState? matchmaking;
        readonly Services.Configuration.ServerSettings settings;
        readonly TimeProvider clock;
        public ProfileService(IUnitOfWork unitOfWork, ILogger<ProfileService> logger, Services.Configuration.ServerSettings settings, TimeProvider clock, LeaderboardCache? leaderboard = null,
            Repositories.Caching.LobbyRepository? lobby = null, Repositories.Caching.RedisGameState? matchmaking = null)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _leaderboard = leaderboard;
            this.lobby = lobby; this.matchmaking = matchmaking;
            this.settings = settings; this.clock = clock;
        }

        public async Task<PlayerProfileResponse?> GetMyProfileAsync(Guid userId)
        {
            try
            {
                var profile = await _unitOfWork.Profiles.GetByUserIdAsync(userId);
                if (profile == null)
                    return null;

                return MapToResponse(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông tin hồ sơ cho UserId {UserId}", userId);
                throw new AppException("Không thể tải thông tin hồ sơ người chơi.", ex);
            }
        }

        public async Task<PlayerProfileResponse?> GetProfileByIdAsync(Guid playerId)
        {
            try
            {
                var profile = await _unitOfWork.Profiles.GetByUserIdAsync(playerId);
                if (profile == null)
                    return null;

                return MapToResponse(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông tin hồ sơ PlayerId {PlayerId}", playerId);
                throw new AppException("Không thể tải thông tin hồ sơ người chơi.", ex);
            }
        }

        public async Task<bool> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
        {
            try
            {
                if (request.PlayerName != null || request.IsMale.HasValue)
                {
                    long now = clock.GetUtcNow().ToUnixTimeMilliseconds();
                    if (await _unitOfWork.Game.InMatch(userId, clock.GetUtcNow().UtcDateTime.AddSeconds(-settings.Matchmaking.ReadyConfirmationSeconds)) ||
                        lobby != null && await lobby.ProfileBusy(userId, now / 1000) ||
                        matchmaking != null && await matchmaking.HasActiveSearch(userId, now))
                        throw new AppException("Hãy rời phòng, dừng tìm trận và kết thúc thi đấu trước khi đổi hồ sơ.", System.Net.HttpStatusCode.Conflict);
                }
                var profile = await _unitOfWork.Profiles.GetByUserIdAsync(userId);
                if (profile == null)
                {
                    throw new NotFoundException("Không tìm thấy hồ sơ người chơi.");
                }

                if (request.PlayerName != null)
                {
                    var name = request.PlayerName.Trim();
                    if (name.Length is < 3 or > 18 || name.Any(char.IsControl))
                        throw new AppException("Tên người chơi cần từ 3 đến 18 ký tự, không chứa ký tự điều khiển.", System.Net.HttpStatusCode.BadRequest);
                    profile.Playername = name;
                }

                if (!string.IsNullOrWhiteSpace(request.Avatar))
                {
                    profile.Avatar = request.Avatar;
                }

                if (request.IsMale.HasValue)
                {
                    profile.Ismale = request.IsMale.Value;
                }

                await _unitOfWork.Profiles.UpdateProfileAsync(profile);
                await _unitOfWork.SaveChangesAsync();
                return true;
            }
            catch (Exception ex) when (PersistenceErrors.IsUniqueViolation(ex, "playerprofiles_playername_key"))
            {
                throw new AppException("Tên người chơi đã được sử dụng. Hãy chọn tên khác.", System.Net.HttpStatusCode.Conflict);
            }
            catch (AppException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật hồ sơ cho UserId {UserId}", userId);
                throw new AppException("Cập nhật hồ sơ thất bại.", ex);
            }
        }

        public async Task<bool> SyncTimePlayedAsync(Guid userId, long playedSeconds)
        {
            try
            {
                if (playedSeconds <= 0)
                    return false;

                var profile = await _unitOfWork.Profiles.GetByUserIdAsync(userId);
                if (profile == null)
                {
                    throw new NotFoundException("Không tìm thấy hồ sơ người chơi để đồng bộ giờ chơi.");
                }

                profile.Timeplayedseconds += playedSeconds;
                await _unitOfWork.Profiles.UpdateProfileAsync(profile);
                await _unitOfWork.SaveChangesAsync();
                return true;
            }
            catch (AppException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đồng bộ giờ chơi cho UserId {UserId}", userId);
                throw new AppException("Đồng bộ giờ chơi thất bại.", ex);
            }
        }

        public async Task<List<PlayerProfileResponse>> GetLeaderboardAsync(int top = 50)
        {
            try
            {
                top = Math.Clamp(top, 1, 1000);
                if (_leaderboard != null)
                {
                    try
                    {
                        var key = await _leaderboard.Snapshot(_unitOfWork.Game);
                        var ids = await _leaderboard.Top(key, top);
                        var cachedProfiles = await _unitOfWork.Game.Profiles(ids);
                        return ids.Select(id => cachedProfiles.FirstOrDefault(p => p.Userid == id)).Where(p => p != null).Select(p => MapToResponse(p!)).ToList();
                    }
                    catch (Exception error) when (PersistenceErrors.IsCacheUnavailable(error))
                    {
                        _logger.LogWarning(error, "Leaderboard cache unavailable; reading PostgreSQL.");
                    }
                }
                var profiles = await _unitOfWork.Profiles.GetLeaderboardAsync(top);
                return profiles.Select(MapToResponse).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải bảng xếp hạng Leaderboard");
                throw new AppException("Không thể tải bảng xếp hạng.", ex);
            }
        }

        private static PlayerProfileResponse MapToResponse(Playerprofile p)
        {
            var totalMatches = p.Wincount + p.Losecount;
            var winRate = totalMatches > 0 ? Math.Round((double)p.Wincount / totalMatches * 100, 1) : 0;

            var timeSpan = TimeSpan.FromSeconds(p.Timeplayedseconds);
            var timeFormatted = $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";

            return new PlayerProfileResponse
            {
                UserId = p.Userid,
                PlayerName = p.Playername,
                Avatar = p.Avatar,
                Elo = p.Elo,
                IsMale = p.Ismale,
                Level = p.Level,
                Exp = p.Exp,
                WinStreak = p.Winstreak,
                DayStreak = p.Daystreak,
                WinCount = p.Wincount,
                LoseCount = p.Losecount,
                WinRate = winRate,
                TimePlayedSeconds = p.Timeplayedseconds,
                TimePlayedFormatted = timeFormatted
            };
        }
    }
}
