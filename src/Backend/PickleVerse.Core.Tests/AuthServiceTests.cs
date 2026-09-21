using System;
using Xunit;
using PickleVerse.Core.Services;

namespace PickleVerse.Core.Tests
{
    public class AuthServiceTests
    {
        [Fact]
        public void Register_CreatesNewProfile_WithHashedPassword_AndDefaultRating()
        {
            // Arrange
            var service = new AuthService();
            string password = "SecretPassword123!";
            
            // Act
            var profile = service.Register("testuser", "test@example.com", password);
            
            // Assert
            Assert.NotNull(profile);
            Assert.Equal("testuser", profile.Username);
            Assert.Equal("test@example.com", profile.Email);
            
            // Verify Password is hashed and can be verified
            Assert.NotEqual(password, profile.PasswordHash);
            Assert.True(service.VerifyPassword(password, profile.PasswordHash));
            
            // Verify FR-302 default rating
            Assert.Equal(2000f, profile.DuprRating);
            Assert.Equal(0f, profile.ReliabilityScore);
        }
    }
}
