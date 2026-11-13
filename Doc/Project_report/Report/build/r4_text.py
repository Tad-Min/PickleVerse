"""Text content of Report 4 (kept apart from the layout code in gen_r4.py)."""
import os

API_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "api_endpoints.json")

ARCH_INTRO = ("PickleVerse uses a client–server architecture with five deployable sub-systems (Figure 1). The Unity game client "
              "contains the presentation, the local gameplay simulation used for training and bot matches, and an offline store. "
              "All persistent data and business rules that must be trusted are on the server side: the ASP.NET Core Web API owns "
              "accounts, progression, economy, matchmaking and moderation; the Unity dedicated server owns the state of online "
              "matches; the AI coaching service analyses shot telemetry; the React administration application is a pure client of "
              "the Web API. Background workers run inside the Web API process. PostgreSQL is the system of record, Redis holds "
              "short-lived coordination data, and object storage keeps replays.")

COMPONENTS = [
    ["Unity game client", "Unity 6000.6.2f1, C#, uGUI, Input System", "Scenes and UI, local physics and rules for training and bot matches, Photon Fusion client, offline training store, REST client (ApiClient) to the Web API"],
    ["Dedicated game server", "Unity Linux headless build, Photon Fusion 2.1.3 (server mode)", "Authoritative simulation of online matches; validates signed tickets; sends heartbeats, telemetry, replay and result to the Web API"],
    ["Web API", "ASP.NET Core (.NET 10), layered API → Services → Repositories → Entities", "REST endpoints, JWT authentication, business rules, transactions through IUnitOfWork and Npgsql EF Core"],
    ["Background workers", "Hosted services in the Web API", "HybridAiWorker (analysis jobs), DedicatedLeaseMonitor (expired leases), ModerationExpiryWorker (expired suspensions), AnalyticsProjectionWorker (daily aggregates), PasswordRecoveryMailQueue (e-mail sending)"],
    ["AI coaching service", "Python 3.12, FastAPI, scikit-learn", "Rule engine for technique errors, summary and drill recommendations, gated tier model; internal API protected by an API key"],
    ["Admin web application", "React 18, TypeScript 5, Vite 5, React Query, Axios", "Administration screens served by nginx; calls the Web API with the administrator's JWT"],
    ["PostgreSQL 16", "Relational database", "51 tables: accounts, profiles, content, matches, economy, moderation, telemetry and coaching"],
    ["Redis 7.4", "In-memory store", "Matchmaking queue and search state, dedicated-server lease, presence, leaderboard cache (15 s)"],
    ["Object storage", "S3-compatible container", "Replay manifests and compressed chunks"],
    ["Photon Cloud", "External service", "Fusion session lookup and relay for clients and the dedicated server"],
    ["Gmail SMTP", "External service", "Delivery of verification and password-reset e-mails"],
]

AUTHORITY = ("Online matches support two authority modes recorded in GameMatches.AuthorityMode. In **dedicated** mode, enabled by "
             "the configuration flag DedicatedServer:Enabled (true on staging), the dedicated server runs the simulation and is the only "
             "authority for points, faults and the result. In **shared** mode, kept for local development without a server, the room host "
             "runs the simulation through Photon Fusion shared mode. The staging deployment has one dedicated-server container, so one "
             "authoritative match runs at a time; the API reports the slot as busy and the client waits (Report 3, MSG36). Scaling to "
             "more matches requires more server containers and is outside release 1.0.")

DEPLOY_INTRO = ("Figure 2 shows the staging deployment on the host tad-min.io.vn (Ubuntu 24.04, Docker Engine). On each push to main the "
                "GitHub Actions workflow dockerhub.yml first reads the release that is deployed and plans which checks and images the changed "
                "files need (plan-release.py). It then runs the selected checks – twelve IntegrationChecks groups on PostgreSQL and Redis, the "
                "FastAPI tests and the admin web type check – and builds the Unity Linux dedicated server with GameCI when the Unity project "
                "changed. **Publish** (publish-release.sh) removes the credentials from the settings files, builds the changed images among "
                "api, ai, web and server, pushes them to Docker Hub with the tag <component>-<commit>-<run>-<attempt> and smoke-tests the four "
                "image digests with Docker Compose on the runner. **Deploy** (ssh-deploy.sh) locks the release on the host, takes a PostgreSQL "
                "dump and checks it with pg_restore, copies docker-compose.linux.yml and Database.sql, writes the four image digests into the "
                "host .env file, runs docker compose up and records the deployed release. **Status on 13/11/2026:** the pipeline passes. Of "
                "its 35 runs, 30 did not pass: two configuration failures on 05/11/2026, 18 runs that did not start while the GitHub account "
                "was locked for a billing issue (06/11 to 11/11), six failures of the dedicated-server build (11/11 and 12/11), one failed "
                "FastAPI test, two failed deployment steps and one cancelled manual run. Runs 27, 28, 30, 33 and 34 of 13/11/2026 completed every "
                "job in about 15 minutes each, and run 34 (17:07) deployed the release that is running now. The four earlier staging releases (Docker Hub tags api-, ai-, web- and "
                "server-manual-<date-time>, 08/11 to 10/11/2026) were built and started by hand. Since 13/11/2026 a Caddy reverse proxy on "
                "the host serves the admin web and the Web API over HTTPS; Table 2 lists the ports that are reachable from the Internet.")


NODES = [
    ["Host reverse proxy", "Caddy with deploy/linux/Caddyfile", "80/tcp, 443/tcp", "Port 80 redirects to HTTPS. Port 443 terminates TLS (Let's Encrypt certificate, TLS 1.3) and forwards /api and /health/ready to backend-api and every other path to the web container"],
    ["web", "tadminm/pickelverse:web-<release>", "3000/tcp (bound to 127.0.0.1)", "nginx serving the React build of the admin web; reached only through the reverse proxy"],
    ["backend-api", "tadminm/pickelverse:api-<release>", "8080/tcp (bound to 127.0.0.1)", "Web API and background workers; reached from the Internet only through the reverse proxy since 13/11/2026 (port 8080 was public before)"],
    ["game-server", "tadminm/pickelverse:server-<release>", "3669/udp", "Unity dedicated server; limited to 2 CPUs and 2 GB RAM"],
    ["ai-service", "tadminm/pickelverse:ai-<release>", "8000/tcp (internal)", "FastAPI coaching service, reachable only from backend-api"],
    ["postgres", "postgres:16-alpine", "5432/tcp (internal)", "Volume for data; schema and seed from src/Database.sql; dumped before each deployment"],
    ["redis", "redis:7.4-alpine", "6379/tcp (internal)", "Coordination data (queue, leases, presence, cache) with an append-only file on the host volume"],
    ["s3-storage", "ghcr.io/shyim/local-s3 (S3-compatible server)", "9000/tcp (internal)", "Bucket pickleverse for replays"],
    ["Player PC / phone", "PickleVerse.exe (Windows, Mono) / PickleVerse.apk (Android 8.0+, IL2CPP, ARM64)", "–", "Connects to the API, Photon Cloud and the game server"],
]

PKG_BACKEND = [
    ["01", "PickleVerse.API", "Entry point: 28 controller classes in 24 files, Program.cs (dependency injection, JWT, CORS, rate limiter, Redis), filters that wrap responses in ApiResponse<T>, the global exception handler that maps service exceptions to HTTP status codes"],
    ["02", "PickleVerse.Services", "Business logic: service interfaces (Interfaces) and implementations (Implementations), background workers, typed settings (ServerSettings) and pure rule classes (TrainingEngine, ProgressionEngine, EloPolicy)"],
    ["03", "PickleVerse.Repositories", "Data access: PickleVerseContext (Npgsql EF Core mapping of the 51 tables), IUnitOfWork, repositories, GameRepository partial classes for match and progress persistence, Redis repositories for queues, leases and caches"],
    ["04", "PickleVerse.Entities", "Shared model: EF entity classes, DTOs and contracts, enums, configuration models (RedisSettings, DedicatedServerSettings) and exception/response types (AppException, ApiResponse)"],
]
PKG_UNITY = [
    ["01", "PickleVerse.FrontEnd", "Menus and screens: login, loading, menu, mode selection, settings entry, overlays, history and missions popups"],
    ["02", "PickleVerse.Multiplayer", "Online flow: MultiplayerApp, PhotonSession, room state, NetworkMatch and NetworkPlayer, dedicated-server client and reconnection"],
    ["03", "PickleVerse.LocalMatch", "Local gameplay: BotMatch, MatchActor, ball flight, paddle contact, shot input, LocalBot and BhTreeBot, training and tutorial directors"],
    ["04", "PickleVerse.Rules", "Engine-independent rules shared by local and online play (scoring, faults, court geometry)"],
    ["05", "PickleVerse.Progression", "ProgressionRuntime, training engine on the client, offline training store, wallet and profile cache"],
    ["06", "PickleVerse.Services", "ApiClient, AccountService and backend settings"],
    ["07", "PickleVerse.Entites", "Data contracts shared by client assemblies (room models, DTOs); the name keeps the original spelling of the assembly"],
    ["08", "PickleVerse.Shop", "Shop and collection scenes"],
    ["09", "PickleVerse.Social", "Friends, blocking and leaderboard UI"],
    ["10", "PickleVerse.Replays", "Replay recording, upload and the replay viewer"],
    ["11", "PickleVerse.Settings", "Settings scene: audio, graphics, key binding and touch layout"],
    ["12", "PickleVerse.Audio", "Music player and sound effects policy"],
    ["13", "PickleVerse.CharacterMotion", "Character animation controller and movement blending"],
]
PKG_WEB_AI = [
    ["01", "features (admin web)", "One folder per area: auth, dashboard, users, content, matches, moderation, analytics (including expert review); each holds its pages"],
    ["02", "api", "Axios client with the JWT interceptor (client.ts) and typed API modules (adminApi.ts, coachingApi.ts)"],
    ["03", "context", "AuthContext: stores the session and exposes sign-in and sign-out"],
    ["04", "components/layout", "AdminLayout, Sidebar and ProtectedRoute, which redirects users without the Admin role to the login page"],
    ["05", "main.py (AI service)", "FastAPI application, request validation and the X-AI-Key check"],
    ["06", "engine.py", "Rule-based findings for 15 error codes, summary and at most three recommendations"],
    ["07", "tier.py", "Gated tier model: 8 features, minimum confidence 0.65, minimum 20 opportunities"],
    ["08", "calibration.py / models.py", "Threshold calibration against expert labels; request and response schemas"],
]

STACK = [
    ["Game client", "Unity 6000.6.2f1, C#, uGUI, Input System, Unity Behavior 1.0.16, Photon Fusion 2.1.3", "Gameplay, UI, bot behaviour graph, networking"],
    ["Game server", "Unity 6000.6.2f1 Linux dedicated server build", "Authoritative online matches"],
    ["Web API", "ASP.NET Core on .NET 10, Npgsql EF Core 10, StackExchange.Redis 3.4, BCrypt.Net-Next 4.2, MailKit 4.17, AWSSDK.S3 4.0, JwtBearer 10", "REST API, persistence, caching, password hashing, e-mail"],
    ["AI service", "Python 3.12, FastAPI, Pydantic, scikit-learn", "Coaching analysis and tier model"],
    ["Admin web", "React 18, TypeScript 5, Vite 5, TanStack React Query 5, Axios 1.6, Recharts 2, Tailwind CSS 3", "Administration UI"],
    ["Data", "PostgreSQL 16, Redis 7.4, S3-compatible storage", "Persistence, coordination, replays"],
    ["DevOps", "Docker, Docker Compose, Docker Hub, Caddy (HTTPS reverse proxy), Ubuntu 24.04; GitHub Actions workflow with plan, check, build, publish and deploy jobs, passing since 13/11/2026", "Build, delivery and hosting"],
]

DECISIONS = [
    ["1", "ASP.NET Core Web API (.NET 10)", "Spring Boot; FastAPI", "The team writes C# for Unity; sharing the language reduces context switching and lets DTOs be checked against the Unity client in IntegrationChecks", "The AI service stays in Python; two runtimes on the server"],
    ["2", "Photon Fusion 2 with a dedicated server", "Netcode for GameObjects with Relay (prototype 07–21/10/2026); Mirror", "The NGO prototype showed unstable relayed connections; Fusion provides input authority, a server mode and tick-based state replication on which client prediction is built", "Dependency on Photon Cloud and its CCU plan (Report 2, risk 4)"],
    ["3", "Custom fixed-step ball simulation (120 Hz)", "Unity PhysX rigidbody", "Deterministic, engine-independent flight and bounce that the server and replays can reproduce exactly", "Collision with paddle and net is implemented by the team (sphere sweep against a contact box)"],
    ["4", "uGUI for the game interface", "UI Toolkit", "Existing art assets and world-space panels were built with uGUI; better support for runtime animation", "Two UI systems are not mixed; layout code is in C#"],
    ["5", "Server-authoritative results and rewards", "Client-reported results", "Prevents cheating on Elo, currency and missions", "Bot matches are reported by the client; only Elo-neutral practice uses this path except Ranked Bot, whose result is validated against the server-created match"],
    ["6", "Single schema file (src/Database.sql) without migrations during development", "EF Core migrations", "The schema changed often; one reviewed file is the single source for schema and seed data", "Database resets are allowed only on the development instance (AGENTS.md)"],
    ["7", "Gated AI tier model with rule fallback", "Always show the model output", "Too little labelled real data; a wrong tier would mislead beginners", "The tier is hidden until the acceptance criteria of NFR-06 are met"],
]

DB_INTRO = ("The logical database design is derived directly from src/Database.sql (51 tables, 366 columns, 71 foreign keys). To keep the "
            "diagrams readable the schema is divided into seven subject areas (Figures %d to %d). Each diagram uses crow's-foot notation: "
            "* marks NOT NULL columns, «PK» and «FK» mark keys, and tables drawn without columns belong to another area and are shown only "
            "for their relationships. Table names follow the PascalCase convention of the EF model; the AI tables (ai*) and ratingledger "
            "use lower case because they were created for the Python service and the rating ledger and are mapped explicitly in "
            "PickleVerseContext.")

ERD_FIGS = [
    ("r4_erd_account.png", "Logical ERD – accounts and profiles"),
    ("r4_erd_social.png", "Logical ERD – friends and blocking"),
    ("r4_erd_training.png", "Logical ERD – techniques, lessons and training"),
    ("r4_erd_match.png", "Logical ERD – matches, ratings, replays and matchmaking"),
    ("r4_erd_economy.png", "Logical ERD – currencies, cosmetics, missions and achievements"),
    ("r4_erd_moderation.png", "Logical ERD – reports, sanctions, appeals and audit"),
    ("r4_erd_coaching.png", "Logical ERD – telemetry, coaching and expert review"),
]

TABLE_DESC = {
    "Roles": "Role catalogue (Player, Admin).",
    "Users": "Account: e-mail, BCrypt password hash, role, status (ACTIVE, SUSPENDED, BANNED), creation time and the auth version used to revoke tokens. Whether the e-mail is verified is read from VerificationEmail.IsVerified.",
    "RefreshTokens": "Refresh tokens stored as SHA-256 hashes with expiry and revocation time.",
    "VerificationEmail": "E-mail verification record of an account: HMAC of the current code, expiry, failed attempts and the IsVerified flag.",
    "PasswordResetChallenges": "The current password-reset challenge of an account (one per user).",
    "PlayerProfiles": "Game profile of an account: display name, avatar, gender of the character, onboarding and tutorial state, level, XP, Elo, wins, losses, streaks, reset epochs and lifetime statistics. Equipped items are stored in PlayerCosmetics.Equipped.",
    "FriendRequests": "Friend requests with status PENDING, ACCEPTED or DECLINED.",
    "Friendships": "Accepted friendships stored once per pair (UserId1 < UserId2).",
    "BlockedUsers": "Blocks from one user to another.",
    "GameMatches": "Matches of every kind with mode, kind, authority mode, status (PREPARING, PLAYING, FINISHED, CANCELLED), rating policy and timestamps.",
    "PlayerLoginDays": "Days (Vietnam time) on which a player signed in; used for retention.",
    "PlayerActivityDays": "Days on which a player was active, with active seconds.",
    "MatchmakingSearches": "Durable log of ranked searches with status SEARCHING, MATCHED, BOT, CANCELLED or TIMEOUT.",
    "ReportPlayerInMatch": "Player reports, one per reporter, reported player and match, with status and decision.",
    "UserSanctions": "Suspensions and bans with actor, reason, expiry and revocation data.",
    "SanctionAppeals": "Appeals, at most one per sanction, with statement, status and decision.",
    "MatchHistoryReplay": "Object-storage key of the replay manifest of a match.",
    "Currencies": "Currency catalogue (TOKENPAD, ELITE TOKEN).",
    "PlayerCurrencies": "Wallet balance of a player per currency.",
    "Cosmetics": "Cosmetic items with code, type, default price and currency, and display configuration.",
    "PlayerCosmetics": "Items owned by a player and the equipped flag.",
    "Achievements": "Achievement definitions with type, target and reward.",
    "PlayerAchievements": "Achievement progress and claim time of a player.",
    "Missions": "Daily and weekly mission definitions with target and reward.",
    "PlayerMissions": "Mission progress of a player in one period (PeriodKey) and the claim time.",
    "Techniques": "Techniques taught by the curriculum.",
    "TechniqueReferences": "Versioned reference values used to judge a technique.",
    "Lessons": "Lessons with code, group, order, target, reward (JSONB), configuration (JSONB), prerequisite and status (DRAFT, PUBLISHED, UNPUBLISHED).",
    "Drills": "Drills that practise one technique.",
    "LessonDrills": "Drills of a lesson with their target (many-to-many).",
    "PlayerTechniqueMastery": "Mastery score of a player per technique.",
    "PlayerTechniquesErrors": "Technique errors detected for a player in a lesson or match.",
    "PlayerLessonDrills": "Best result of a player per lesson drill: stars, accuracy, timing, attempts and first-pass time.",
    "CosmeticPrices": "Prices of an item in each accepted currency.",
    "GameCommands": "Idempotency log of client commands (purchase, claim, sync) per player and command ID.",
    "TrainingSessions": "Each training attempt with lesson, drill, shots, hits, timing, stars and receipt status.",
    "MatchScore": "Score of each game of a match (weak entity identified by match and game number).",
    "GameMatchPlayers": "Participants of a match: team, party group, roster slot, Elo before the match, replay-writer flag, valid serves and returns, end reason, last-seen time and outcome (WIN, LOSE, INTERRUPTED). Names, characters and bot accounts are joined from the profile tables.",
    "CurrencyTransactions": "Ledger of every currency change with reason and reference.",
    "ratingledger": "Elo before, delta and after for one player in one ranked match.",
    "aitelemetrysessions": "Telemetry batch of a drill or match with source, mode and status.",
    "ExpertReviews": "Expert labels (level, errors, data group) for a telemetry batch.",
    "CoachingDemoSessions": "Simulated demonstration sessions kept apart from real player data.",
    "aitelemetryparts": "Compressed parts of a telemetry batch.",
    "aianalysisjobs": "Analysis job of a batch with status pending, processing, completed or failed, attempts and error.",
    "aicoachingsessions": "Coaching result of a batch: findings, summary and recommendations.",
    "aicoachingprofiles": "Aggregated coaching profile of a player.",
    "ContentCatalogs": "Published versions of the content catalogue sent to clients.",
    "ProgressRewardGrants": "Log of one-time progression rewards (for example THOT, GEMSTONE) per player.",
    "DedicatedOperations": "Operations on dedicated matches (stop requests and their acknowledgement).",
    "AdminAudits": "Audit trail of administrative actions with actor, action, target and data.",
}

DICT_TABLES = ["Users", "PlayerProfiles", "GameMatches", "GameMatchPlayers", "MatchScore", "ratingledger", "Lessons", "TrainingSessions", "UserSanctions", "SanctionAppeals"]
COL_NOTES = {
    ("Users", "Status"): "ACTIVE, SUSPENDED, BANNED",
    ("GameMatches", "Status"): "PREPARING, PLAYING, FINISHED, CANCELLED",
    ("GameMatches", "AuthorityMode"): "shared or dedicated",
    ("UserSanctions", "Restriction"): "BANNED or SUSPENDED; SUSPENDED requires ExpiresAt",
    ("SanctionAppeals", "SanctionId"): "UNIQUE: one appeal per sanction",
    ("SanctionAppeals", "Status"): "PENDING, CHECKING, ACCEPTED, DECLINED",
}

DB_NOTES = [
    "Team game totals are not stored in GameMatches; they are derived from the completed MatchScore rows, which avoids inconsistent copies (AGENTS.md).",
    "GameMatchPlayers keeps only facts that belong to the participant in that match, including the fixed Elo input needed before a rating ledger row exists; names and cosmetics are joined from the profile tables.",
    "PlayerProfiles is wide (35 columns) because it is the hot read model of the menu; statistics that change together are kept in one row to avoid joins on every screen.",
    "TrainingSessions references Lessons, Drills and the composite LessonDrills key: the composite key guarantees that the drill belongs to the lesson, and the single keys keep historical attempts when a lesson is re-configured.",
    "Lessons.Reward and Lessons.Config are JSONB because each lesson type has different parameters; the API validates them before saving.",
    "AdminAudits stores the actor ID without a foreign key so that audit records survive the deletion of an administrator account.",
    "CoachingDemoSessions holds simulated data in its own table so that it can never be mixed with real sessions used for AI evaluation.",
    "Idempotency: GameCommands, TrainingSessions receipts and the UNIQUE constraints on rewards (ProgressRewardGrants, PlayerMissions period key) make retried requests safe.",
]

RESP = {
    # account
    "AccountScreen": "Login, register, verification and password-reset pages of the Login scene; client-side validation",
    "AccountService": "Client facade for authentication; keeps the session, scope and profile and refreshes tokens",
    "ApiClient": "Sends HTTP requests with the bearer token and unwraps the ApiResponse envelope",
    "AuthController": "REST endpoints /api/Auth/*; rate-limited",
    "IAuthService": "Contract of the authentication use cases",
    "AuthService": "Registration, OTP, sign-in, token issue and rotation, password reset, session validation",
    "IEmailSender": "Abstraction for sending e-mail",
    "SmtpEmailSender": "Sends e-mail through Gmail SMTP with STARTTLS",
    "IUnitOfWork": "Gives services access to repositories and one shared transaction",
    # training
    "TrainingDirector": "Runs a drill in the Training scene: briefing, shots, feedback and result popup",
    "TrainingPhase": "Phases of the training screen",
    "TrainingEngine": "Pure rules for drill evaluation: hits, timing and stars (BR-18)",
    "ProgressionRuntime": "Client-side cache of profile, wallet, catalogue and the synchronisation queue",
    "OfflineTrainingStore": "Durable local queue of offline attempts with request IDs",
    "TrainingAttempt": "Data of one drill attempt sent to the server",
    "GameTrainingController": "REST endpoints for starting, completing and synchronising training",
    "IGameTrainingService": "Contract of the training use cases",
    "OnlineGameService": "Server implementation of training, match settlement, matchmaking and economy use cases",
    "IGameCatalogService": "Provides the published content catalogue and its version",
    # gameplay
    "BotMatch": "Orchestrates a local singles match: phases, serve, rally, scoring and result",
    "MatchActor": "Moves and animates one player and performs hits",
    "SinglesRules": "Decides faults, points, side-outs, games and match end (BR-10 to BR-15)",
    "ShotInput": "Decodes the J/K/L/U/I key state into one shot intent on the first release",
    "ShotIntent": "Requested shot kind with power and cross modifiers",
    "ShotKind": "Seven shot kinds of the local game",
    "PaddleContact": "Checks the contact window (±65 ms) and the contact quality (Perfect, Good)",
    "BallFlight": "Fixed-step ball flight: gravity, drag-free arc and bounce with restitution 0.73",
    "ShotTrajectory": "Computes launch velocity for a target point and flight time",
    "MatchPhase": "Phases of a match (Intro, Cinematic, PreServe, Rally, Settling, BetweenPoints, BetweenGames, MatchOver); Timeout remains in the enum, with SinglesRules.TakeTimeout, only for protocol compatibility and is no longer entered",
    "MatchCamera": "Follows the local player behind the baseline and smooths position and rotation",
    "CourtFollowView": "Shared framing rule for local and online matches: keeps the player's feet above the bottom HUD band and the far baseline in view",
    "HudActorClearance": "Moves the shot-cue panel so that it never covers the player",
    # bot
    "IAdaptiveBotContext": "Information the bot needs about ball, court and opponent, independent of local or network play",
    "LocalAdaptiveBotContext": "Bot context for a local bot match",
    "NetworkBotContext": "Bot context for a ranked bot on the dedicated server",
    "BhTreeBot": "Adaptive bot driven by the Unity Behavior graph; tier chosen from Elo",
    "BotPolicy": "Maps a tier to reaction time, accuracy, aggression and error rate",
    "BotTierProfile": "Parameter set of one bot tier",
    "LocalBot": "Fixed-level bot (Easy, Medium, Hard) with deterministic settings",
    "Bot Behavior (Unity Behavior graph)": "Behaviour tree asset: position, choose shot, hit, recover",
    "BotContactChance": "Probability that the bot reaches and returns a ball",
    # online
    "MultiplayerApp": "Online flow controller: lobby, search, ready check, loading, match, reconnection and results",
    "FlowState": "States of the online flow (Menu, WaitingRoom, Delay, Searching, Connecting, InMatch, Reconnecting, Results)",
    "PhotonSession": "Starts and joins Fusion sessions in shared or client mode and handles disconnects",
    "PhotonRoomState": "Networked room state: roster, phase and host",
    "NetworkMatch": "Networked match state replicated from the authority: score, phase and ball",
    "NetworkPlayer": "Networked player: input, position and animation",
    "PickleballRules": "Rules engine used by the network match (singles and doubles)",
    "MatchRoster": "Teams, slots, bot flags and mode of a match",
    "PlayMultiplayerPresenter": "Online lobby screen: singles or doubles room, room code, search timer, invite button and ready state",
    "LobbyFriendsSidebar": "Collapsible friends column of the lobby: paged list of friends who can be invited, search and send invitation",
    # dedicated
    "DedicatedWorker": "Main loop of the dedicated server: lease, admission, heartbeats, telemetry, replay and result",
    "DedicatedAdmission": "Validates HMAC tickets and admits players to the session",
    "DedicatedController": "Player endpoint /api/Game/dedicated/ticket: reserves the slot and returns the signed connection ticket",
    "DedicatedWorkerController": "Service-key endpoints /api/Server/* used only by the dedicated server: poll, ready, heartbeat, admission, operation acknowledgement, telemetry, replay chunks, result and release",
    "GameController": "REST endpoints for profile, wallet, catalogue, purchase and claims",
    "IDedicatedServerService": "Contract of dedicated-server coordination",
    "DedicatedServerService": "Allocates the slot, signs tickets, records heartbeats and settles results",
    "DedicatedLeaseRepository": "Redis lease of the single server slot with expiry",
    "DedicatedLeaseMonitor": "Background worker that releases expired leases and interrupts orphan matches",
    # coaching
    "QuickCoachingRules": "Client-side quick tip shown when offline or before the AI result arrives",
    "CoachingRuntime": "Uploads telemetry, polls the analysis job and shows the coaching popup",
    "ShotTelemetry": "Per-shot record: timing, contact, landing, position and errors",
    "AiCoachingController": "REST endpoints for telemetry upload and coaching results",
    "IHybridCoachingService": "Contract of telemetry ingestion and coaching retrieval",
    "HybridCoachingService": "Stores telemetry, creates analysis jobs and returns results",
    "HybridAiWorker": "Sends pending jobs to the AI service with retries and stores the result",
    "main.py": "FastAPI entry point: /v1/analyze-session and health",
    "engine.analyze()": "Rule-based analysis producing findings, summary and recommendations",
    "TierPredictor": "Loads the approved model and predicts the tier with a confidence threshold",
    # progress
    "PlayerProgressionMenu": "Progress, missions and history popups",
    "OfflineSocialMenu": "Friends and leaderboard drawer",
    "ShopScreen": "Shop scene: list, preview and purchase",
    "ReplayView": "Replay viewer: download and playback controls",
    "OnlineSocialController": "REST endpoints for friends, blocking and leaderboard",
    "OnlineReplaysController": "REST endpoints for replay upload and download",
    "IOnlineGameService": "Contract of progression, economy and match use cases",
    "IOnlineSocialService": "Contract of social use cases",
    "IMatchReplayService": "Contract of replay storage",
    "OnlineSocialService": "Friend requests, friendships, blocks and leaderboard cache",
    "GameRepository": "EF Core queries and locks for matches, progression and economy",
    "PickleVerseContext": "EF Core DbContext mapping the 51 tables",
    # admin
    "AuthContext": "Admin web session state and role check",
    "CMS page": "Lesson, drill, item and mission forms",
    "Users page": "Account search and actions",
    "Moderation page": "Reports, sanctions and appeals",
    "Matches page": "Live matches and stop dialog",
    "Analytics page": "Charts for the selected date range",
    "Axios client (ApiResponse unwrap)": "HTTP client that adds the JWT and unwraps the ApiResponse envelope",
    "ContentController": "Admin endpoints for lessons and catalogue",
    "ProgressResetController": "Admin endpoints for reset preview and execution",
    "AdminController": "Admin endpoints for users, roles, sanctions and e-mail activation",
    "AdminModerationController": "Admin endpoints for reports, appeals and match stop",
    "LiveMatchesController": "Admin endpoints for live and finished matches",
    "AnalyticsController": "Admin endpoints for analytics summary and error heat map",
    "ExpertReviewController": "Admin endpoints for expert labelling and dataset export",
    "IContentService": "Contract of content management",
    "IProgressResetService": "Contract of progress reset",
    "IModerationService": "Contract of moderation use cases",
    "IAnalyticsService": "Contract of analytics queries",
    "IExpertReviewService": "Contract of expert review and export",
}

SECTIONS = [
    {"num": "3.1", "title": "3.1 Account and Authentication", "short": "account and authentication", "cls": "r4_cls_account",
     "cls_caption": "Class diagram – account and authentication",
     "intro": "This feature group implements FR-01 to FR-05. The Unity AccountScreen calls AccountService, which uses ApiClient to reach AuthController. AuthService performs the rules of BR-01 to BR-05 through IUnitOfWork and sends e-mails through IEmailSender.",
     "seqs": [("r4_seq_register", "Register and verify e-mail", "The visitor registers; the service hashes the password, stores the HMAC of a six-digit code and queues the e-mail. The alternative fragments show a confirmation mismatch, a registered e-mail, a valid code and an invalid or expired code."),
              ("r4_seq_login", "Sign in", "The service checks the password hash and the account state. A wrong password, an unverified e-mail and a restricted account return different results; an active account receives the JWT and refresh token, after which the client loads the profile."),
              ("r4_seq_reset_pwd", "Reset password", "The reset request always returns the same message; the mail queue sends the code only for a verified account. A valid code changes the hash and revokes every refresh token.")]},
    {"num": "3.2", "title": "3.2 Gameplay Core and Ball Physics", "short": "gameplay core", "cls": "r4_cls_gameplay",
     "cls_caption": "Class diagram – gameplay core (bot match)",
     "intro": "The gameplay core implements FR-11 to FR-13. BotMatch drives the match phases. Each MatchActor reads its intent from ShotInput (player) or a bot. PaddleContact accepts the hit, ShotTrajectory computes the launch velocity, BallFlight advances the ball and SinglesRules decides the outcome. The rule and physics classes have no Unity dependencies, so the same code runs in the client, in the dedicated server and in EditMode unit tests. The camera follows the common sports-game convention of a view from behind the player's own baseline: MatchCamera and the online camera both call CourtFollowView, which keeps the player's feet above the bottom HUD band (shot cue on PC, touch controls on Android) and widens the view slightly so that the far baseline stays visible; HudActorClearance moves the shot cue if it would still cover the player (defect D-03).",
     "params": [["Simulation step", "120 Hz (1/120 s)", "BallFlight fixed step, independent of frame rate"],
                ["Gravity", "9.81 m/s²", "Ball flight"],
                ["Bounce restitution", "0.73", "Vertical speed kept after a bounce"],
                ["Ball radius", "0.037 m", "Line calls use the ball edge"],
                ["Court half width / half length / kitchen", "3.048 / 6.7056 / 2.1336 m", "SinglesRules.Court, official dimensions"],
                ["Contact window", "±65 ms", "PaddleContact; outside the window the swing misses"],
                ["Perfect contact", "≤ 0.145 m and ±30 ms", "Higher speed and accuracy"],
                ["Serve timer", "10 s", "Fault ServeTimeout"],
                ["Contact delay per shot", "serve 0.80 s, dink 0.367 s, drop 0.533 s, smash 0.50 s, volley/reset 0.233 s, drive 0.433 s", "ShotIntent.ContactDelay: time from release to the animation's contact frame"],
                ["Camera height / distance / field of view", "5.3 m / 4.25 m / 56°", "CourtFollowView: position behind the player's baseline"],
                ["Feet line", "25 % of screen height on PC, 32 % on Android", "CourtFollowView.DesktopFeetLine / TouchFeetLine: the feet are never drawn lower"],
                ["Extra field of view", "up to 8°", "Added only when needed to keep the far baseline and the opponent's head in view"]],
     "params_caption": "Gameplay and physics parameters"},
    {"num": "3.3", "title": "3.3 Training and Offline Synchronisation", "short": "training", "cls": "r4_cls_training",
     "cls_caption": "Class diagram – training and synchronisation",
     "intro": "Training implements FR-06 to FR-10. TrainingDirector runs a drill locally with the gameplay core and evaluates it with TrainingEngine. Online, the attempt is sent at once; offline, OfflineTrainingStore keeps it until the device reconnects. On the server OnlineGameService re-evaluates the attempt with the same engine and returns a receipt.",
     "seqs": [("r4_seq_training_online", "Practise a drill online", "The server first checks that the lesson is published and unlocked, then records each trial; at the end it applies stars and the first-pass reward in one transaction."),
              ("r4_seq_offline_sync", "Synchronise offline attempts", "Each queued attempt is sent with its request ID. A new and valid attempt is applied, an already applied one is answered DUPLICATE, and a stale or invalid one is REJECTED.")]},
    {"num": "3.4", "title": "3.4 Bot AI", "short": "bot AI", "cls": "r4_cls_bot",
     "cls_caption": "Class diagram – bots",
     "intro": "The bot design implements FR-13 and BR-31. LocalBot provides three fixed levels. BhTreeBot executes a Unity Behavior graph whose decisions (position, shot choice, aggression, deliberate errors) are parameterised by BotPolicy and a BotTierProfile selected from the opponent's Elo. The IAdaptiveBotContext interface hides whether the bot plays locally or on the dedicated server, so the same bot is used for practice and for the ranked bot fallback.",
     "params": [["Fixed levels", "Easy, Medium, Hard", "LocalBot: fixed reaction and accuracy"],
                ["Adaptive tiers", "Elo < 500 beginner; < 2000 intermediate; ≥ 2000 advanced", "BotPolicy tier selection"],
                ["Ranked bot Elo", "own + 120 (own − 100 after 5 losses)", "EloPolicy.BotElo (BR-26)"],
                ["Ranked bot accuracy", "min(100, 25 + (Elo − 100) / 120) %", "EloPolicy.BotAccuracy"]],
     "params_caption": "Bot parameters",
     "seqs": [("r4_seq_bot_match", "Play a bot match", "The loop fragment repeats rallies until one side wins two games; at the end the client reports the result to GameMatchesController, which settles rewards.")]},
    {"num": "3.5", "title": "3.5 Online Matchmaking and Netcode", "short": "online play", "cls": "r4_cls_online",
     "cls_caption": "Class diagram – online client",
     "intro": "Online play implements FR-14 to FR-18. MultiplayerApp is a state machine over FlowState; PlayMultiplayerPresenter and LobbyFriendsSidebar form the lobby screen. Unity Gaming Services code from the earlier prototype (files guarded by PICKLEVERSE_LEGACY_NGO) is not compiled; the live path uses only Photon Fusion and the Web API. Ranked singles, quick doubles (unrated 2v2, filled from solo players and parties of two, no bot fallback) and private rooms share the same ready check and dedicated-server allocation. PhotonSession connects to Fusion; NetworkMatch and NetworkPlayer replicate state from the dedicated server, which is the only authority. Table below gives the netcode design: clients send input only, predict their own movement and reconcile with the server frame; remote players are interpolated.",
     "params": [["Authority", "Dedicated server (Fusion server mode)", "Shared mode is kept only for local development without a server"],
                ["Client input", "DedicatedInput {Movement, Running, Sequence}", "Sent every tick; shot commands travel as reliable messages (JSON, at most 16 KB) and are judged on the server"],
                ["Local prediction", "Own player only", "The client applies its input at once and keeps the unacknowledged moves in a sequence-numbered list"],
                ["Server frame", "PlayerFrame {position, velocity, sequence, motionEpoch, movement, running}", "Networked state of each player; sequence acknowledges the last input applied"],
                ["Reconciliation", "Replay unacknowledged moves from the server state", "Acknowledged moves are dropped; the visible correction is smoothed and clamped to 1.5 m; a new motionEpoch (serve reset, teleport) forces a hard reset"],
                ["Movement integration", "120 Hz sub-steps", "Same step as the authority, so prediction and server agree"],
                ["Remote players and ball", "Interpolated in Render", "Fusion snapshot interpolation"],
                ["Lag compensation", "Disabled", "Hits are judged on the server's own state"],
                ["Tick rate", "Fusion project default", "No explicit override in the project settings"],
                ["Protocol version", "18", "Client and server must match or the connection is refused"],
                ["Reconnection", "client 30 s; server keeps the slot 30 s; API interrupts after 35 s without heartbeat (heartbeat every 8 s)", "MultiplayerSettings.reconnectSeconds, NetworkMatch reservations, OnlineMatch settings (BR-28)"]],
     "params_caption": "Netcode design parameters",
     "seqs": [("r4_seq_ranked", "Find a ranked or quick doubles match", "The client polls the search. For singles the alternatives show a human opponent inside the Elo window and the bot fallback after 180 seconds. For quick doubles the captain sends the party members; the API fills two sides of two from parties and solo players, the match is unrated and there is no bot fallback. In both modes all players must accept, and a decline or timeout closes the offer."),
              ("r4_seq_invite", "Invite a friend to a room", "The invitation is rejected when the friend is not in the lobby, is blocked or the room is full; otherwise the friend polls and accepts it."),
              ("r4_seq_dedicated", "Allocate the dedicated server and play", "The server waits for work on /api/Server/poll. When the slot is busy the client waits. When the slot is reserved the API signs tickets; the client polls every 500 ms until the server is ready; during play the server sends heartbeats and at the end telemetry, replay and the result.")],
     "extra_cls": [("r4_cls_dedicated", "Class diagram – dedicated server coordination", "dedicated server")]},
    {"num": "3.6", "title": "3.6 AI Coaching", "short": "AI coaching", "cls": "r4_cls_coaching",
     "cls_caption": "Class diagram – AI coaching",
     "intro": "AI coaching implements FR-19 and NFR-05/NFR-06. Telemetry is uploaded by CoachingRuntime (drills and bot matches) or DedicatedWorker (online matches). HybridCoachingService stores it and creates an analysis job; HybridAiWorker calls the FastAPI service, which runs the rule engine and, when approved, the tier model.",
     "params": [["Model features", "8", "mean_abs_timing, late_rate, early_rate, kitchen_rate, momentum_rate, target_rate, recovery_rate, input_error_rate"],
                ["Tiers", "beginner, intermediate, advanced", "tier.py"],
                ["Minimum confidence / opportunities", "0.65 / 20", "Below these values no tier is returned"],
                ["Finding acceptance", "precision ≥ 0.80, recall ≥ 0.70", "calibration.py, NFR-06"],
                ["Model approval", "real data, ≥ 10 holdout players per tier, macro-F1 ≥ 0.70 and ≥ baseline + 0.05", "train.py"],
                ["Recommendations", "at most 3", "engine.py"]],
     "params_caption": "AI coaching parameters",
     "seqs": [("r4_seq_coaching", "Analyse a session", "The worker marks the job processing and calls the service; on success the result is stored and the job completed, on failure the job is retried and finally marked failed.")]},
    {"num": "3.7", "title": "3.7 Progress, Economy, Social and Replays", "short": "progress, economy and social", "cls": "r4_cls_progress",
     "cls_caption": "Class diagram – progress, economy, social and replays",
     "intro": "This group implements FR-20 to FR-26. The client screens share ProgressionRuntime. On the server, OnlineGameService settles rewards and purchases, OnlineSocialService manages friends and the leaderboard, and IMatchReplayService stores replays; GameRepository performs the EF Core queries and row locks inside the unit of work.",
     "seqs": [("r4_seq_purchase", "Purchase a cosmetic", "The command ID makes the purchase idempotent: an already processed request returns the stored receipt; otherwise ownership and balance are checked and the debit, ledger entry and ownership are written in one transaction.")]},
    {"num": "3.8", "title": "3.8 Administration and Moderation", "short": "administration", "cls": "r4_cls_admin",
     "cls_caption": "Class diagram – administration web and admin API",
     "intro": "Administration implements FR-27 to FR-33. Every admin controller requires the Admin role; services write an AdminAudits record in the same transaction as the change.",
     "seqs": [("r4_seq_moderation", "Report, sanction and appeal", "A player reports; the administrator accepts the report and applies a sanction; the restricted player appeals; the administrator accepts or declines the appeal."),
              ("r4_seq_reset", "Reset player progress", "The dialog previews the impact; execution is refused when the revision changed after the preview."),
              ("r4_seq_publish", "Publish a lesson", "Publishing is refused when the expected version changed; otherwise the lesson becomes PUBLISHED and a new catalogue version is produced."),
              ("r4_seq_stop", "Stop a live match", "The stop request is stored as a dedicated operation; the server reads it with the next heartbeat, ends the match as interrupted and acknowledges.")]},
]

API_INTRO = ("The Web API exposes 129 REST endpoints in 28 controller classes (24 source files). The endpoint list below is extracted from the "
             "controller attributes by build/api_extract.py, so routes and access levels are those of the code on 12/11/2026. The column "
             "Used by shows which program calls each route (game client, dedicated game server or admin web), found by searching the client "
             "sources; code compiled only for the old Netcode prototype is ignored. Access: Anonymous (no token), Player (any signed-in "
             "account with game scope), Player, Restricted (also allowed for a restricted account with appeals scope), Admin (role Admin), "
             "Service key (dedicated server only).")
API_LEGACY = ("24 endpoints are marked Not called. They belong to the first resource-style controllers (Achievements, Economy, Friends, "
              "Matches, Missions, Profiles, Replays, Training) and to three AiCoaching routes; the game now uses the command-style /api/Game "
              "routes for the same data. They still require authentication and pass through the same services, but they are not tested by "
              "the clients and are recorded as issue I-12 of the project tracking workbook: they will be removed or documented as public API before release.")
API_RULES = [
    ["Base path and format", "/api/<Controller>/...; JSON (UTF-8); enum values serialised as strings"],
    ["Envelope", "Success: ApiResponse<T> {success: true, message, data}. Error: ApiResponse {success: false, message, code, errors}"],
    ["Status codes", "400 validation, 401 missing or expired token, 403 role or scope not allowed, 404 not found, 409 conflict or stale version, 429 rate limit, 503 dependency unavailable, 500 internal (no details)"],
    ["Authentication", "Bearer JWT (HS256) with claims sub/NameIdentifier, email, role (Player, Admin or Restricted), access_scope (game or appeals) and auth_version; refresh tokens rotate"],
    ["Rate limiting", "Policy auth: 20 requests per minute per IP on authentication endpoints; rejection returns 429 with Retry-After: 60"],
    ["Idempotency", "Commands carry a request/command ID stored in GameCommands or receipts"],
    ["Service calls", "Dedicated server: service key header and HMAC-signed tickets; AI service: X-AI-Key header"],
    ["Documentation", "OpenAPI document generated at runtime with the ApiResponse schema"],
]


SECURITY = [
    ["Passwords", "BCrypt hash with salt; 6–128 characters; never logged"],
    ["Tokens", "JWT HS256, 2-hour access token; refresh token 14 days stored as SHA-256 hash, rotated on use; auth_version in the token is incremented to revoke all sessions (password reset, ban)"],
    ["Codes", "OTP and reset codes stored as HMAC, 10-minute expiry, 5 attempts, 60-second resend cooldown"],
    ["Authorisation", "Role-based ([Authorize(Roles = \"Admin\")]) plus access scope: restricted accounts receive scope appeals and can call only moderation endpoints; resource ownership is checked in services (own matches, replays, telemetry)"],
    ["Transport", "HTTPS with TLS 1.2+ behind the reverse proxy. Since 13/11/2026 the staging API and admin web are served only over HTTPS on port 443 (Let's Encrypt certificate, TLS 1.3; port 80 redirects and port 8080 is closed) and the game client uses https://tad-min.io.vn (commit be556a1; Report 5, D-18 fixed)"],
    ["Secrets", "Read from environment variables and the server .env file. Commit be556a1 (13/11/2026) emptied the development credentials in appsettings.json and removed the fallback JWT key from Program.cs: the API refuses to start without a key of at least 32 bytes. The published images are also built from sanitised settings (deploy/api/sanitize_settings.py). The old values remain in the Git history and must be rotated (Report 5, D-17 partly fixed)"],
    ["Abuse protection", "Rate limiting on authentication; server-authoritative online results; idempotent commands; validation of telemetry ownership; HMAC tickets bind a player to a match and slot"],
    ["CORS", "AdminWebPolicy allows only the admin web origins; production serves the admin web and API under the same origin through the reverse proxy"],
    ["Data protection", "Other players never receive e-mail addresses; audit log for administrative actions; since be556a1 the deployment takes a PostgreSQL dump before every release and verifies it with pg_restore (kept under .deploy/backups on the host); there is no scheduled daily backup or off-host copy yet (Report 2, risk 11)"],
    ["Data at rest", "Passwords are BCrypt hashes, refresh tokens SHA-256 hashes and codes HMACs. Database files, backups and replays are not encrypted with AES-256 as the proposal stated; disk encryption of the production host is planned and the deviation is recorded in Report 1 (LI-10)"],
    ["Error handling", "GlobalExceptionHandler maps AppException types to status codes and hides internal details of unexpected exceptions"],
]

ACTIVITIES = [
    ("r4_act_w01.png", "W01 – Learn and sync a drill (owner: Trần Nguyễn Hiếu Thuận)", "The player signs in or opens offline training; a beginner is sent to the tutorial; the player practises a drill; the attempt is applied online or queued offline and synchronised when the network returns."),
    ("r4_act_w02.png", "W02 – Play a bot and get a tip (owner: Nguyễn Gia Huy)", "The player sets up a bot match and plays rallies until a side wins; the API settles the result, the AI service analyses the telemetry and the player reads the tip and opens the recommended drill."),
    ("r4_act_w03.png", "W03 – Play online and save the result (owner: Nguyễn Văn Thành Đạt)", "The player creates a private room, starts quick doubles (alone or with a party of two) or searches a ranked singles match; only ranked singles has the bot fallback after 180 seconds; all players must accept; the dedicated server runs the match unless it is stopped or the lease is lost, and the API saves the result once."),
    ("r4_act_w04.png", "W04 – Review a report and publish a lesson, part 1: report, sanction and appeal (owner: Huỳnh Trung Kiên)", "A player reports an opponent; the administrator reviews and may sanction; the restricted player may appeal; the administrator decides the appeal."),
    ("r4_act_content.png", "W04 – Review a report and publish a lesson, part 2: publish a lesson (owner: Huỳnh Trung Kiên)", "The administrator edits and publishes a lesson; the API checks the expected version and that prerequisites are published; clients receive the new catalogue version."),
]

STATES = [
    ("r4_sm_account.png", "SM-01 Account status", "ACTIVE, SUSPENDED and BANNED; suspensions end by expiry or revocation, and an accepted appeal revokes the sanction."),
    ("r4_sm_email.png", "SM-02 E-mail verification code", "Pending, verified, expired and locked after 5 wrong attempts; a resend creates a new pending code."),
    ("r4_sm_training.png", "SM-03 Training attempt and receipt", "Briefing, practice, result; the receipt is APPLIED, DUPLICATE or REJECTED."),
    ("r4_sm_offline.png", "SM-04 Offline queue item", "Queued, sending, applied, duplicate or rejected; network failure returns the item to the queue."),
    ("r4_sm_botmatch.png", "SM-05 Match phase (local match)", "Intro, Cinematic, PreServe, Rally, Settling, BetweenPoints, BetweenGames and MatchOver. Time-outs were removed from play, so the Timeout value of the enum is never entered."),
    ("r4_sm_search.png", "SM-06 Matchmaking search", "SEARCHING ends in MATCHED, BOT (ranked singles fallback only), CANCELLED or TIMEOUT; a declined offer returns the other player to SEARCHING."),
    ("r4_sm_room.png", "SM-07 Online room phase", "idle, searching, assigning-bot, ready, loading, playing, interrupted, closed and rejected."),
    ("r4_sm_match.png", "SM-08 Game match", "PREPARING, PLAYING, FINISHED and CANCELLED; interruption and administrator stop end the match without Elo."),
    ("r4_sm_lease.png", "SM-09 Dedicated server lease", "queued, starting, ready, finishing, pending and busy; an expired lease releases the slot."),
    ("r4_sm_lesson.png", "SM-10 Lesson publication", "DRAFT, PUBLISHED and UNPUBLISHED."),
    ("r4_sm_report.png", "SM-11 Player report", "PENDING, CHECKING, ACCEPTED and DECLINED."),
    ("r4_sm_appeal.png", "SM-12 Sanction appeal", "PENDING, CHECKING, ACCEPTED (sanction revoked) and DECLINED."),
    ("r4_sm_aijob.png", "SM-13 AI analysis job", "pending, processing, completed and failed with retries."),
    ("r4_sm_friend.png", "SM-14 Friend request", "PENDING, ACCEPTED and DECLINED; blocking removes the friendship."),
]
