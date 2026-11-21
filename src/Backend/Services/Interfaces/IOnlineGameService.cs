using Entities.Game;
using PickleVerse.Progression;

namespace Services.Interfaces;

public interface IOnlineGameService
{
    Task<GameSnapshot> Read(Guid id);
    Task<GameSnapshot> Onboarding(Guid id, OnboardingRequest request);
    Task<GameSnapshot> Claim(Guid id, GameCommand request);
    Task<GameSnapshot> Tutorial(Guid id, TutorialUpdateRequest request);
    Task<GameSnapshot> Purchase(Guid id, GameCommand request);
    Task<GameSnapshot> Equip(Guid id, GameCommand request);
    Task<GameSnapshot> LegacyClaim(Guid id, int definition, bool achievement, string requestId);
    Task<GameSnapshot> LegacyCosmetic(Guid id, int cosmetic, bool purchase, bool equip, string requestId);
}
