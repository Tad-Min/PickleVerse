using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Entities.Models;
using Microsoft.EntityFrameworkCore;
using Repositories.Base;
using Repositories.Context;
using Repositories.Interfaces;

namespace Repositories.Implementations
{
    public class UserRepository : GenericRepository<User>, IUserRepository
    {
        public UserRepository(PickleVerseContext context) : base(context)
        {
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Playerprofile)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        }

        public async Task<User?> GetByIdWithRoleAndProfileAsync(Guid userId)
        {
            return await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Playerprofile)
                .FirstOrDefaultAsync(u => u.Userid == userId);
        }

        public async Task<List<User>> GetPagedUsersAsync(string? search, UserStatus? status, int page, int pageSize)
        {
            var query = _context.Users
                .Include(u => u.Verificationemails).AsSplitQuery()
                .Include(u => u.Role)
                .Include(u => u.Playerprofile)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.ToLower();
                query = query.Where(u => u.Email.ToLower().Contains(term) ||
                                         (u.Playerprofile != null && u.Playerprofile.Playername.ToLower().Contains(term)));
            }

            if (status.HasValue)
            {
                var s = status.Value;
                query = query.Where(u => u.Status == s);
            }

            return await query
                .OrderByDescending(u => u.Createdat)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalUsersCountAsync(string? search, UserStatus? status)
        {
            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.ToLower();
                query = query.Where(u => u.Email.ToLower().Contains(term) ||
                                         (u.Playerprofile != null && u.Playerprofile.Playername.ToLower().Contains(term)));
            }

            if (status.HasValue)
            {
                var s = status.Value;
                query = query.Where(u => u.Status == s);
            }

            return await query.CountAsync();
        }

        public async Task<Refreshtoken?> GetRefreshTokenAsync(string tokenHash)
        {
            return await _context.Refreshtokens
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Tokenhash == tokenHash);
        }

        public async Task AddRefreshTokenAsync(Refreshtoken token)
        {
            await _context.Refreshtokens.AddAsync(token);
        }

        public Task RevokeRefreshTokenAsync(Refreshtoken token)
        {
            token.Isrevoked = true;
            _context.Refreshtokens.Update(token);
            return Task.CompletedTask;
        }

        public async Task<Role?> GetRoleByNameAsync(string roleName)
        {
            return await _context.Roles
                .FirstOrDefaultAsync(r => r.Rolename.ToLower() == roleName.ToLower());
        }

        public async Task<Role?> GetRoleByIdAsync(int roleId)
        {
            return await _context.Roles.FindAsync(roleId);
        }

        public async Task<List<Role>> GetAllRolesAsync()
        {
            return await _context.Roles.ToListAsync();
        }

        public async Task AssignRoleAsync(Guid userId, int roleId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.Roleid = roleId;
                _context.Users.Update(user);
            }
        }

        public async Task AddVerificationEmailAsync(Verificationemail ve)
        {
            await _context.Verificationemails.AddAsync(ve);
        }

        public async Task<Verificationemail?> GetLatestVerificationEmailAsync(Guid userId)
        {
            return await _context.Verificationemails
                .Where(v => v.Userid == userId)
                .OrderByDescending(v => v.Createdat)
                .ThenByDescending(v => v.Verificationemailid)
                .FirstOrDefaultAsync();
        }

        public Task<Passwordresetchallenge?> GetPasswordResetAsync(Guid userId) =>
            _context.Passwordresetchallenges.SingleOrDefaultAsync(x => x.Userid == userId);

        public async Task SavePasswordResetAsync(Passwordresetchallenge challenge)
        {
            var previous = await GetPasswordResetAsync(challenge.Userid);
            if (previous == null) await _context.Passwordresetchallenges.AddAsync(challenge);
            else _context.Entry(previous).CurrentValues.SetValues(challenge);
        }

        public Task RevokeAllRefreshTokensAsync(Guid userId) => _context.Refreshtokens
            .Where(t => t.Userid == userId && !t.Isrevoked).ExecuteUpdateAsync(s => s.SetProperty(t => t.Isrevoked, true));

        public Task<bool> IsAuthVersionValidAsync(Guid userId, int version) => _context.Users
            .AsNoTracking().AnyAsync(u => u.Userid == userId && u.Authversion == version && u.Status == UserStatus.Active);

        public async Task LockUserAsync(Guid userId)
        {
            // All OTP attempts/resends for an account share this transaction lock.
            await _context.Users.FromSqlInterpolated($"SELECT * FROM users WHERE userid = {userId} FOR UPDATE").ToListAsync();
            var tracked = _context.ChangeTracker.Entries<User>().FirstOrDefault(x => x.Entity.Userid == userId);
            if (tracked != null) await tracked.ReloadAsync();
        }
    }
}
