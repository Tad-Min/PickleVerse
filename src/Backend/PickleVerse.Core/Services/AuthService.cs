using System;
using PickleVerse.Core.Entities;
using BCrypt.Net;

namespace PickleVerse.Core.Services
{
    public class AuthService
    {
        public PlayerProfile Register(string username, string email, string password)
        {
            var profile = new PlayerProfile
            {
                Id = Guid.NewGuid(),
                Username = username,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                DuprRating = 2000f, // Explicitly set per FR-302, though default is 2000
                ReliabilityScore = 0f
            };
            return profile;
        }

        public bool VerifyPassword(string rawPassword, string hash)
        {
            return BCrypt.Net.BCrypt.Verify(rawPassword, hash);
        }
    }
}
