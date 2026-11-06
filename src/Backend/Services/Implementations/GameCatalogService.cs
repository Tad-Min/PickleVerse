using Repositories.UnitOfWork;
using Services.Interfaces;
using System.Security.Cryptography;
using System.Text;
using PickleVerse.Progression;
using Services.Configuration;

namespace Services.Implementations;

public sealed class GameCatalogService : IGameCatalogService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ServerSettings settings;

    public GameCatalogService(IUnitOfWork unitOfWork, ServerSettings settings)
    {
        _unitOfWork = unitOfWork;
        this.settings = settings;
    }

    public async Task<GameCatalog> Load()
    {
        var catalog = settings.CreateCatalog();
        var stored = await _unitOfWork.Game.Catalog();
        catalog.definitions.AddRange(stored.definitions);
        catalog.lessons = stored.lessons;
        catalog.cosmetics = stored.cosmetics;
        catalog.version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(GameJson.Write(catalog))))[..16].ToLowerInvariant();
        await _unitOfWork.Game.SaveCatalog(catalog.version, GameJson.Write(catalog), DateTime.UtcNow);
        return catalog;
    }
}
