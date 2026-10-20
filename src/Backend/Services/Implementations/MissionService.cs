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
    public class MissionService : IMissionService
    {
        private readonly IUnitOfWork _unitOfWork;

        public MissionService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<MissionDto>> GetMyMissionsAsync(Guid playerId)
        {
            var missions = await _unitOfWork.Missions.GetAllActiveMissionsAsync();
            var playerMissions = await _unitOfWork.Missions.GetPlayerMissionsAsync(playerId);

            var result = new List<MissionDto>();
            foreach (var mission in missions)
            {
                var pm = playerMissions.FirstOrDefault(x => x.Missionid == mission.Missionid);
                var progress = pm?.Progress ?? 0;
                result.Add(new MissionDto
                {
                    MissionId = mission.Missionid,
                    MissionName = mission.Missionname,
                    Description = mission.Description,
                    Type = mission.Type,
                    Target = mission.Target,
                    Reward = mission.Reward,
                    CurrentProgress = progress,
                    IsCompleted = progress >= mission.Target,
                    IsClaimed = pm?.Isclaimed ?? false
                });
            }

            return result;
        }

        public async Task<bool> IncrementProgressAsync(Guid playerId, int missionId, int amount = 1)
        {
            var pm = await _unitOfWork.Missions.GetPlayerMissionAsync(playerId, missionId);

            if (pm == null)
            {
                pm = new Playermission
                {
                    Playerid = playerId,
                    Missionid = missionId,
                    Progress = amount,
                    Isclaimed = false
                };
                await _unitOfWork.Missions.AddPlayerMissionAsync(pm);
            }
            else
            {
                pm.Progress += amount;
                await _unitOfWork.Missions.UpdatePlayerMissionAsync(pm);
            }

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<(bool Success, string Message, int ExpGained)> ClaimRewardAsync(Guid playerId, int missionId)
        {
            var mission = await _unitOfWork.Missions.GetByIdAsync(missionId);
            if (mission == null) return (false, "Nhiệm vụ không tồn tại.", 0);

            var pm = await _unitOfWork.Missions.GetPlayerMissionAsync(playerId, missionId);

            if (pm == null || pm.Progress < mission.Target)
            {
                return (false, "Chưa hoàn thành chỉ tiêu nhiệm vụ.", 0);
            }

            if (pm.Isclaimed)
            {
                return (false, "Phần thưởng nhiệm vụ này đã được nhận rồi.", 0);
            }

            pm.Isclaimed = true;
            pm.Claimedat = DateTime.UtcNow;
            await _unitOfWork.Missions.UpdatePlayerMissionAsync(pm);

            // Tặng thưởng cơ bản (50 EXP + cập nhật Profile)
            var profile = await _unitOfWork.Profiles.GetByUserIdAsync(playerId);
            int exp = 50;
            if (profile != null)
            {
                profile.Exp += exp;
                if (profile.Exp >= profile.Level * 100)
                {
                    profile.Level += 1;
                    profile.Exp = 0;
                }
                await _unitOfWork.Profiles.UpdateProfileAsync(profile);
            }

            await _unitOfWork.SaveChangesAsync();
            return (true, $"Nhận thưởng thành công: {mission.Reward}!", exp);
        }
    }
}
