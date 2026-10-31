using Repositories.UnitOfWork;
using Services.Interfaces;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Entities.Models;
using PickleVerse.Progression;
using Entities.Exceptions;
using Services.Configuration;

namespace Services.Implementations;

public sealed partial class OnlineGameService : IOnlineGameService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGameCatalogService catalogs;
    private readonly TimeProvider clock;
    private readonly ServerSettings settings;
    private readonly RedisGameState matchmaking;
    private readonly LeaderboardCache leaderboard;
    private readonly IMatchReplayService replays;

    public OnlineGameService(IUnitOfWork unitOfWork, IGameCatalogService catalogs, TimeProvider clock, ServerSettings settings, RedisGameState matchmaking, LeaderboardCache leaderboard, IMatchReplayService replays)
    {
        _unitOfWork = unitOfWork;
        this.catalogs = catalogs;
        this.clock = clock;
        this.settings = settings;
        this.matchmaking = matchmaking;
        this.leaderboard = leaderboard;
        this.replays = replays;
    }

    sealed class PlayerState
    {
        public PlayerProfile player = new();
        public TutorialState tutorial = new();
    }
    sealed class Session
    {
        public Playerprofile profile = null!;
        public PlayerState state = new();
        public OnlineInventory inventory = new();
        public GameCatalog catalog = new();
        public long revision, beforeCoins, beforeElite;
        public bool trainingChanged;
        public Dictionary<(ProgressScope,string,string),(int,bool,int)> missionRows = [];
        public HashSet<string> bonusCommands = [];
        public PlayerProfile Player => state.player;
        public ProgressionEngine Engine => new(catalog.Rules());
        public GameSnapshot Snapshot() => new() { player = Player, inventory = inventory, catalog = catalog, isMale = profile.Ismale, revision = revision, tutorial = state.tutorial };
    }
    DateTime Now => clock.GetUtcNow().UtcDateTime;
    static AppException Bad(string message) => new(message, HttpStatusCode.BadRequest);
    static AppException Conflict(string message) => new(message, HttpStatusCode.Conflict);

    async Task<Session> Load(Guid id, GameCatalog catalog)
    {
        var profile = await _unitOfWork.Game.LockProfile(id)
            ?? throw new AppException("Không tìm thấy hồ sơ.", HttpStatusCode.NotFound);
        var s = new Session { profile=profile, catalog=catalog };
        var p = s.Player;
        p.online = true;
        p.id = id.ToString("N");
        p.playerName = profile.Playername;
        p.avatarId = profile.Avatar;
        p.elo = profile.Elo;
        p.level = profile.Level;
        p.levelXp = profile.Exp;
        p.wins = profile.Wincount;
        p.losses = profile.Losecount;
        p.activeSeconds = profile.Timeplayedseconds;
        var inventory = await _unitOfWork.Game.Inventory(id);
        p.coins = inventory.tokenpad;
        p.gems = inventory.elite;
        s.inventory=inventory;
        s.beforeCoins=p.coins;
        s.beforeElite=p.gems;
        await LoadProgress(s,id);
        return s;
    }

    async Task Persist(Session s, Guid id, string command, string reason)
    {
        var p = s.Player;
        if (p.coins < 0 || p.gems < 0 || p.coins > int.MaxValue || p.gems > int.MaxValue)
            throw Bad("Số dư vượt giới hạn hỗ trợ.");
        s.profile.Level = p.level;
        s.profile.Exp = p.levelXp;
        s.profile.Elo = p.elo;
        s.profile.Wincount = checked((int)p.wins);
        s.profile.Losecount = checked((int)p.losses);
        s.profile.Winstreak = p.lifetime.streak;
        s.profile.Daystreak = p.dailyStreak?.current ?? 0;
        s.profile.Timeplayedseconds = (long)p.activeSeconds;
        foreach (var currency in new[] { (name: "TOKENPAD", amount: p.coins, before: s.beforeCoins), (name: "ELITE TOKEN", amount: p.gems, before: s.beforeElite) })
        {
            await _unitOfWork.Game.SaveBalance(id, currency.name, (int)currency.amount, currency.before, command, reason);
        }
        s.inventory.tokenpad = p.coins;
        s.inventory.elite = p.gems;
        p.shopData = GameJson.Write(s.inventory);
        foreach (var def in s.catalog.definitions.Where(d => d.scope == ProgressScope.Achievement))
            await _unitOfWork.Game.SaveAchievement(id, def.id, (int)Math.Min(int.MaxValue, s.Engine.Progress(p, def)), p.achievementClaims.Contains(def.id));
        await PersistProgress(s,id);
        await _unitOfWork.SaveChangesAsync();
    }

    async Task<GameSnapshot> Execute(Guid id, string? requestId, string kind, object? payload, Func<Session, Task> operation)
    {
        var catalog = await catalogs.Load();
        await using var transaction = await _unitOfWork.Game.BeginTransactionAsync();
        var session = await Load(id, catalog);
        string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(GameJson.Payload(payload))));
        if (requestId != null)
        {
            if (requestId.Length < 8 || requestId.Length > 64)
                throw Bad("Mã yêu cầu không hợp lệ.");
            var previous = await _unitOfWork.Game.Command(id, requestId);
            if (previous != null)
            {
                if (previous.Kind != kind || previous.Fingerprint != fingerprint)
                    throw Conflict("Mã yêu cầu đã được dùng cho nội dung khác.");
                await transaction.CommitAsync();
                session.Player.shopData = GameJson.Write(session.inventory);
                return session.Snapshot();
            }
        }
        DailyStreakEngine.Ensure(session.Player, Now);
        session.Engine.RefreshPeriods(session.Player, Now);
        DailyStreakEngine.Refresh(session.Player, Now);
        await operation(session);
        var command = requestId ?? Guid.NewGuid().ToString("N");
        await Persist(session, id, command, kind);
        if (requestId != null)
            _unitOfWork.Game.AddCommand(id, requestId, kind, fingerprint, GameJson.Write(new { session.revision }));
        await _unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
        return session.Snapshot();
    }

    public async Task<GameSnapshot> Read(Guid id)
    {
        await ReconcileOnline(id);
        // Reconciliation uses its own transaction and may track several profiles. Read the
        // subsequent locked snapshot afresh, including results committed by another request.
        _unitOfWork.Game.ClearTracking();
        return await Execute(id, null, "refresh", null, _ => Task.CompletedTask);
    }
    public Task<GameSnapshot> Claim(Guid id, GameCommand request) => Execute(id, request.RequestId, "claim", request, s =>
    {
        if (!s.Engine.Claim(s.Player, request.Code, Now))
            throw Bad("Chưa đủ điều kiện nhận thưởng hoặc đã nhận.");
        return Task.CompletedTask;
    });
    public Task<GameSnapshot> Tutorial(Guid id, TutorialUpdateRequest request) => Execute(id, request.RequestId, "tutorial", request, s =>
    {
        if (request.Key == "completed")
            s.state.tutorial.completed = true;
        else if (request.Key == "skipped")
            s.state.tutorial.skipped = true;
        else throw Bad("Trạng thái hướng dẫn không hợp lệ.");
        return Task.CompletedTask;
    });
    public Task<GameSnapshot> Purchase(Guid id, GameCommand request) => Execute(id, request.RequestId, "purchase", request, async s =>
    {
        if (s.inventory.owned.Contains(request.Code))
            throw Bad("Bạn đã sở hữu vật phẩm này.");
        string currency = request.Currency switch { "Tokenpad" => "TOKENPAD", "EliteToken" => "ELITE TOKEN", _ => throw Bad("Loại tiền không hợp lệ.") };
        var offer = await _unitOfWork.Game.Price(request.Code, currency) ?? throw Bad("Vật phẩm không bán bằng loại tiền này.");
        if (currency == "TOKENPAD")
        {
            if (s.Player.coins < offer.price)
                throw Bad("Không đủ TOKENPAD.");
            s.Player.coins -= offer.price;
        }
        else
        {
            if (s.Player.gems < offer.price)
                throw Bad("Không đủ ELITE TOKEN.");
            s.Player.gems -= offer.price;
        }
        _unitOfWork.Game.AddCosmetic(id, offer.id);
        s.inventory.owned.Add(request.Code);
    });
    public Task<GameSnapshot> Equip(Guid id, GameCommand request) => Execute(id, request.RequestId, "equip", request, async s =>
    {
        if (!s.inventory.owned.Contains(request.Code))
            throw Bad("Bạn chưa sở hữu vật phẩm này.");
        await _unitOfWork.Game.Equip(id, request.Code);
        s.inventory.equippedPaddle = request.Code;
    });
}
