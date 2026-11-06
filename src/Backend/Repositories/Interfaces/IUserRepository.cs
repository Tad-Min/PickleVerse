using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Entities.Models;
using Repositories.Base;

namespace Repositories.Interfaces
{
    public interface IUserRepository : IGenericRepository<User>
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByIdWithRoleAndProfileAsync(Guid userId);
        Task<List<User>> GetPagedUsersAsync(string? search, UserStatus? status, int page, int pageSize);
        Task<int> GetTotalUsersCountAsync(string? search, UserStatus? status);
        Task<Refreshtoken?> GetRefreshTokenAsync(string tokenHash);
        Task AddRefreshTokenAsync(Refreshtoken token);
        Task RevokeRefreshTokenAsync(Refreshtoken token);
        Task<Role?> GetRoleByNameAsync(string roleName);
        Task<Role?> GetRoleByIdAsync(int roleId);
        Task<List<Role>> GetAllRolesAsync();
        Task AssignRoleAsync(Guid userId, int roleId);
        Task AddVerificationEmailAsync(Verificationemail ve);
        Task<Verificationemail?> GetLatestVerificationEmailAsync(Guid userId);
        Task LockUserAsync(Guid userId);
        Task<Passwordresetchallenge?> GetPasswordResetAsync(Guid userId);
        Task SavePasswordResetAsync(Passwordresetchallenge challenge);
        Task RevokeAllRefreshTokensAsync(Guid userId);
        Task<bool> IsAuthVersionValidAsync(Guid userId, int version);
    }
}
