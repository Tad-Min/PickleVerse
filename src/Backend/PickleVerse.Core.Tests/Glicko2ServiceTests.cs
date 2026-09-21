using System;
using Xunit;
using PickleVerse.Core.Entities;
using PickleVerse.Core.Services;

namespace PickleVerse.Core.Tests
{
    public class Glicko2ServiceTests
    {
        [Fact]
        public void CalculateNewRating_PlayerWinsAgainstHigherRated_RatingIncreasesSignificantly()
        {
            // Arrange
            var service = new Glicko2Service();
            var player1 = new PlayerProfile { DuprRating = 2000f, RatingDeviation = 350f, Volatility = 0.06f };
            var player2 = new PlayerProfile { DuprRating = 2400f, RatingDeviation = 50f, Volatility = 0.06f };
            
            // Act
            var result = service.CalculateNewRating(player1, player2, 1); // 1 = player1 wins
            
            // Assert
            Assert.True(result.NewRating > 2000f);
            Assert.True(result.NewRating - 2000f > 50f); // Should increase significantly because of upset
            Assert.True(result.NewRD < 350f); // RD should decrease (more confident)
            Assert.True(result.ReliabilityScore > 0f); // Reliability should increase
        }

        [Fact]
        public void CalculateNewRating_PlayerLosesAgainstLowerRated_RatingDecreases()
        {
            // Arrange
            var service = new Glicko2Service();
            var player1 = new PlayerProfile { DuprRating = 2000f, RatingDeviation = 350f, Volatility = 0.06f };
            var player2 = new PlayerProfile { DuprRating = 1600f, RatingDeviation = 50f, Volatility = 0.06f };
            
            // Act
            var result = service.CalculateNewRating(player1, player2, 0); // 0 = player1 loses
            
            // Assert
            Assert.True(result.NewRating < 2000f);
            Assert.True(2000f - result.NewRating > 50f); // Decrease significantly
        }
    }
}
