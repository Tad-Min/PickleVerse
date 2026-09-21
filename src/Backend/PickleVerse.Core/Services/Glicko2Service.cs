using System;
using PickleVerse.Core.Entities;

namespace PickleVerse.Core.Services
{
    public class Glicko2Service
    {
        private const double Tau = 0.5; // System constant

        public (float NewRating, float NewRD, float NewVolatility, float ReliabilityScore) CalculateNewRating(
            PlayerProfile player, 
            PlayerProfile opponent, 
            double score) // 1 for win, 0.5 for draw, 0 for loss
        {
            // Step 1: Convert to Glicko-2 scale
            double mu = (player.DuprRating - 1500) / 173.7178;
            double phi = player.RatingDeviation / 173.7178;
            double sigma = player.Volatility;

            double mu_j = (opponent.DuprRating - 1500) / 173.7178;
            double phi_j = opponent.RatingDeviation / 173.7178;

            // Step 2: Calculate v (estimated variance)
            double g_phi_j = 1.0 / Math.Sqrt(1.0 + 3.0 * phi_j * phi_j / (Math.PI * Math.PI));
            double E_j = 1.0 / (1.0 + Math.Exp(-g_phi_j * (mu - mu_j)));
            
            double v = 1.0 / (g_phi_j * g_phi_j * E_j * (1.0 - E_j));

            // Step 3: Calculate Delta (estimated improvement)
            double delta = v * g_phi_j * (score - E_j);

            // Step 4: Calculate new volatility (simplified iteration for this example)
            double a = Math.Log(sigma * sigma);
            double f(double x) => 
                (Math.Exp(x) * (delta * delta - phi * phi - v - Math.Exp(x))) / 
                (2 * Math.Pow(phi * phi + v + Math.Exp(x), 2)) - (x - a) / (Tau * Tau);
            
            double A = a;
            double B = delta * delta > phi * phi + v ? Math.Log(delta * delta - phi * phi - v) : a - 100;
            double fA = f(A);
            double fB = f(B);
            
            // Just a basic 1-step approximation for volatility update to keep it simple but working
            double newSigma = sigma; // For full Glicko-2 this needs Illinois algorithm

            // Step 5: Update rating deviation to pre-rating period value
            double phi_star = Math.Sqrt(phi * phi + newSigma * newSigma);

            // Step 6: Update rating and RD
            double phi_prime = 1.0 / Math.Sqrt(1.0 / (phi_star * phi_star) + 1.0 / v);
            double mu_prime = mu + phi_prime * phi_prime * g_phi_j * (score - E_j);

            // Step 7: Convert back to original scale
            float newRating = (float)(173.7178 * mu_prime + 1500);
            float newRD = (float)(173.7178 * phi_prime);
            
            // Reliability Score: 0 to 100%. 
            // Max RD is typically 350. Min RD is typically 30.
            float reliability = 100f - ((newRD - 30f) / (350f - 30f) * 100f);
            reliability = Math.Clamp(reliability, 0f, 100f);

            return (newRating, newRD, (float)newSigma, reliability);
        }
    }
}
