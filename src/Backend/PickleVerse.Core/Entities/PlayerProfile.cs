using System;

namespace PickleVerse.Core.Entities
{
    public class PlayerProfile
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        // DUPR Rating equivalents
        public float DuprRating { get; set; } = 2000f; // Default starting rating
        
        // Represents how reliable the rating is, percentage 0 to 100
        public float ReliabilityScore { get; set; } = 0f;

        // Glicko-2 hidden state values needed for next calculations
        public float RatingDeviation { get; set; } = 350f; // Default Glicko-2 RD
        public float Volatility { get; set; } = 0.06f; // Default Glicko-2 Volatility
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
