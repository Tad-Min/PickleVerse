using Entities.Game;

namespace Services.Interfaces;

public interface IAppActivityService
{
    Task<AppActivityReceipt> OpenActivity(AppActivityRequest request);
    Task<AppActivityReceipt> Activity(Guid id, AppActivityRequest request);
}
