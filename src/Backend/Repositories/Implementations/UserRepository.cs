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

        public async Task<List<User>> GetPagedUsersAsync(string? search, string? status, int page, int pageSize)
        {
            var query = _context.Users
                .Include(u => u.Role)
                .Include(u => u.Playerprofile)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.ToLower();
                query = query.Where(u => u.Email.ToLower().Contains(term) ||
                                         (u.Playerprofile != null && u.Playerprofile.Playername.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var s = status.ToUpper();
                query = query.Where(u => u.Status == s);
            }

            return await query
                .OrderByDescending(u => u.Createdat)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalUsersCountAsync(string? search, string? status)
        {
            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.ToLower();
                query = query.Where(u => u.Email.ToLower().Contains(term) ||
                                         (u.Playerprofile != null && u.Playerprofile.Playername.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var s = status.ToUpper();
                query = query.Where(u => u.Status == s);
            }

            return await query.CountAsync();
        }

        public async Task<Refreshtoken?> GetRefreshTokenAsync(string tokenHash)
        {
            return await _context.Refreshtokens
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

        public async Task<Verificationemail?> GetValidVerificationTokenAsync(Guid userId, string token)
        {
            return await _context.Verificationemails
                .FirstOrDefaultAsync(v => v.Userid == userId && v.Token == token && !v.Isverified && v.Expiresat > DateTime.UtcNow);
        }
    }
}
