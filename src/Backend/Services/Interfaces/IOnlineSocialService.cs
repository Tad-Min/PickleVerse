using Entities.Game;
using PickleVerse.Progression;
using PickleVerse.Multiplayer;
using PickleVerse.Replays;
namespace Services.Interfaces;

public interface IOnlineSocialService
{
    Task<OnlineSocialPage> Read(Guid self, string view, string? query, int offset);
    Task Act(Guid self, SocialActionRequest input);
}
