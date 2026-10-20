using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Entities.Models;
using Microsoft.Extensions.Logging;
using Repositories.UnitOfWork;
using Services.DTOs;
using Services.Exceptions;
using Services.Interfaces;

namespace Services.Implementations
{
    public class ProfileService : IProfileService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ProfileService> _logger;

        public ProfileService(IUnitOfWork unitOfWork, ILogger<ProfileService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<PlayerProfileResponse?> GetMyProfileAsync(Guid userId)
        {
            try
            {
                var profile = await _unitOfWork.Profiles.GetByUserIdAsync(userId);
                if (profile == null) return null;

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
                if (profile == null) return null;

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
                var profile = await _unitOfWork.Profiles.GetByUserIdAsync(userId);
                if (profile == null)
                {
                    throw new NotFoundException("Không tìm thấy hồ sơ người chơi.");
                }

                if (!string.IsNullOrWhiteSpace(request.PlayerName) && request.PlayerName != profile.Playername)
                {
                    profile.Playername = request.PlayerName;
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
                if (playedSeconds <= 0) return false;

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
