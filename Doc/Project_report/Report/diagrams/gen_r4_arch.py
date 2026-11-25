"""PlantUML sources for Report 4: architecture (UML component diagram), deployment, packages, logical and physical ERDs."""
from diagram_lib import write_all
import schema_lib

D = {}

D["r4_architecture"] = r"""
skinparam componentStyle uml2
component "Unity Game Client\n(Windows, Android)" as Client
component "Admin Web\n(React SPA)" as Web
component "HTTPS Reverse Proxy\n(Caddy)" as Proxy
component "Web API\n(ASP.NET Core 10, SocialHub)" as API
component "Background Workers\n(hosted in the Web API process)" as Workers
component "AI Coaching Service\n(Python FastAPI)" as AI
component "Dedicated Game Server\n(Unity Linux headless,\none container per match)" as DS
component "PostgreSQL 16" as PG <<database>>
component "Redis 7.4" as RD <<database>>
component "S3-compatible Storage" as S3 <<external>>
component "Photon Cloud" as Photon <<external>>
component "Gmail SMTP" as SMTP <<external>>
component "PayOS" as PAY <<external>>
interface "HTTPS\n(REST JSON, SignalR)" as IHttps
interface "Web API\n/api (JWT)" as IApi
interface "Server API\n(service key)" as ISrv
interface "Fusion match session\n(UDP 3669+, signed ticket)" as IGame
interface "Fusion lobby\nand relay" as IPhoton
interface "Analyze API\n(X-AI-Key)" as IAI
interface "SQL\n(Npgsql EF Core)" as ISql
interface "Redis\nprotocol" as IRedis
interface "S3 API" as IS3
interface "SMTP\n(STARTTLS)" as ISmtp
interface "PayOS API\n(HMAC-SHA256)" as IPay
interface "Webhook\n(signed)" as IHook
Proxy -up- IHttps
Client -down-( IHttps
Web -down-( IHttps
API -up- IApi
Proxy -down-( IApi
API -right- ISrv
DS -left-( ISrv
DS -up- IGame
Client -down-( IGame
Photon -down- IPhoton
Client -( IPhoton
DS -up-( IPhoton
AI -up- IAI
Workers -down-( IAI
PG -up- ISql
API -down-( ISql
Workers -down-( ISql
RD -up- IRedis
API -down-( IRedis
S3 -up- IS3
API -down-( IS3
SMTP -up- ISmtp
API -down-( ISmtp
PAY -up- IPay
API -down-( IPay
API -down- IHook
PAY -up-( IHook
Workers .up.> API : <<hosted in>>
"""

D["r4_deployment"] = r"""
skinparam linetype ortho
node "Player PC\n<<Windows 10+>>" as PC {
  artifact "PickleVerse.exe\n(Unity 6000.6.2f1, Mono)" as WinClient
}
node "Player phone\n<<Android 8.0+ (API 26), ARM64>>" as Phone {
  artifact "PickleVerse.apk\n(IL2CPP)" as Apk
}
node "Admin workstation" as AdminPC {
  artifact "Web browser" as Browser
}
node "tad-min.io.vn\n<<Ubuntu 24.04, Docker Engine>>" as Host {
  node "Caddy reverse proxy (host)\n:80 redirect, :443 TLS" as HostNginx
  node "web container\nnginx + React build\n127.0.0.1:3000" as WebC
  node "backend-api container\nASP.NET Core 10, SignalR, ffprobe\n127.0.0.1:8080" as ApiC
  node "ai-service container\nPython 3.12 FastAPI\n:8000 (internal)" as AiC
  node "game-server containers (× PKV_GAME_REPLICAS)\nUnity Linux Dedicated Server\nUDP 3669–4168 → :3669" as GameC
  node "postgres container\nPostgreSQL 16\n:5432 (internal)" as PgC
  node "redis container\nRedis 7.4\n:6379 (internal)" as RdC
  node "s3-storage container\nS3-compatible\n:9000 (internal)" as S3C
}
cloud "Photon Cloud\n(Fusion 2.1.3)" as Photon
cloud "Gmail SMTP\n:587" as Smtp
cloud "PayOS\n(api-merchant.payos.vn,\npay.payos.vn)" as PayOs
node "GitHub Actions\n<<CI/CD: plan, checks, build,\npublish, deploy>>" as CI
actor "Developer" as Dev
node "Docker Hub\ntadminm/pickelverse" as Hub

Browser --> HostNginx : HTTPS
WinClient --> HostNginx : HTTPS JSON
Apk --> HostNginx : HTTPS JSON
HostNginx --> ApiC : /api (incl. WebSocket hub),\n/health/ready
HostNginx --> WebC : other paths
WinClient --> Photon : UDP
Apk --> Photon : UDP
WinClient --> GameC : UDP
Apk --> GameC : UDP
GameC --> ApiC : HTTP (service key)
GameC --> Photon : UDP
ApiC --> PgC : TCP 5432
ApiC --> RdC : TCP 6379
ApiC --> S3C : HTTP 9000
ApiC --> AiC : HTTP 8000
ApiC --> Smtp : SMTP STARTTLS
ApiC --> PayOs : HTTPS (signed requests)
PayOs --> HostNginx : HTTPS webhook
WinClient --> PayOs : payment page (browser)
Dev --> CI : push to main
CI --> Hub : push changed images\n<component>-<commit>-<run>-<attempt>
CI --> Host : SSH: verified pg_dump,\nimage digests, docker compose up
Hub --> Host : pull by digest
"""

D["r4_pkg_backend"] = r"""
skinparam packageStyle folder
left to right direction
package "PickleVerse.API" as API {
  package "Controllers (31 classes)\nand Hubs (SocialHub)" as Ctl
  package "Filters and middleware\n(ApiResponse envelope,\nexception handler, scope)" as Flt
  package "Program (DI, JWT, CORS,\nrate limiter, Redis)" as Prog
}
package "PickleVerse.Services" as SVC {
  package "Interfaces" as SI
  package "Implementations\n(incl. MembershipService, PayOsGateway,\nReplayLaunchService)" as SImpl
  package "Workers\n(HybridAiWorker,\nDedicatedLeaseMonitor,\nSocialMediaCleanup)" as SW
  package "Configuration\n(ServerSettings, PayOsSettings)" as SCfg
  package "Rules\n(TrainingEngine, Elo)" as SRules
}
package "PickleVerse.Repositories" as REPO {
  package "Context\n(PickleVerseContext)" as RCtx
  package "UnitOfWork" as RUow
  package "Implementations" as RImpl
  package "Game\n(GameRepository partials)" as RGame
  package "Caching\n(Redis repositories)" as RCache
}
package "PickleVerse.Entities" as ENT {
  package "Models (EF entities)" as EM
  package "DTOs and Contracts" as ED
  package "Enums" as EE
  package "Configuration\n(RedisSettings, DedicatedServerSettings)" as EC
  package "Exceptions\n(AppException, ApiResponse)" as EX
}
API ..> SVC : <<use>>
API ..> ENT : <<use>>
SVC ..> REPO : <<use>>
SVC ..> ENT : <<use>>
REPO ..> ENT : <<use>>
"""

D["r4_pkg_unity"] = r"""
skinparam packageStyle rectangle
left to right direction
package "PickleVerse.FrontEnd" as FrontEnd
package "PickleVerse.Multiplayer" as Multiplayer
package "PickleVerse.LocalMatch" as LocalMatch
package "PickleVerse.Rules" as Rules
package "PickleVerse.Progression" as Progression
package "PickleVerse.Services" as Services
package "PickleVerse.Entites" as Entites
package "PickleVerse.Shop" as Shop
package "PickleVerse.Social" as Social
package "PickleVerse.Replays" as Replays
package "PickleVerse.Settings" as Settings
package "PickleVerse.Audio" as Audio
package "PickleVerse.CharacterMotion" as CharacterMotion
package "PickleVerse.ArtDirection" as ArtDirection
package "PickleVerse.VFX" as VFX
package "Photon Fusion 2.1.3" as Fusion <<external>>
package "Unity Behavior 1.0.16" as Behavior <<external>>
FrontEnd ..> Multiplayer
FrontEnd ..> LocalMatch
FrontEnd ..> Progression
FrontEnd ..> Shop
FrontEnd ..> Social
FrontEnd ..> Replays
FrontEnd ..> Services
Multiplayer ..> LocalMatch
Multiplayer ..> Rules
Multiplayer ..> Progression
Multiplayer ..> Services
Multiplayer ..> Fusion
LocalMatch ..> Rules
LocalMatch ..> Progression
LocalMatch ..> Settings
LocalMatch ..> Audio
LocalMatch ..> CharacterMotion
LocalMatch ..> Behavior
LocalMatch ..> ArtDirection
Multiplayer ..> ArtDirection
ArtDirection ..> Settings
VFX ..> LocalMatch
VFX ..> Shop
Progression ..> Services
Progression ..> Entites
Progression ..> Replays
Shop ..> Progression
Social ..> Progression
Replays ..> Services
Replays ..> Settings
Audio ..> Settings
"""

D["r4_pkg_web_ai"] = r"""
skinparam packageStyle folder
left to right direction
package "Admin Web (React 18 + TypeScript + Vite)" as WEB {
  package "features" as F {
    package "auth" as Fa
    package "dashboard" as Fd
    package "users" as Fu
    package "content" as Fc
    package "matches" as Fm
    package "moderation" as Fmo
    package "analytics\n(incl. expert review)" as Fan
    package "membership\n(payment return page)" as Fme
  }
  package "api\n(client.ts, adminApi.ts, lessonApi.ts,\ncoachingApi.ts, replayLaunchApi.ts)" as WApi
  package "context\n(AuthContext)" as WCtx
  package "components/layout\n(AdminLayout, ProtectedRoute,\nSidebar)" as WLay
  package "types" as WT
}
package "AI Coaching Service (Python 3.12 FastAPI)" as AIS {
  package "main.py\n(endpoints, X-AI-Key)" as Am
  package "engine.py\n(rule findings, summary,\nrecommendations)" as Ae
  package "models.py\n(request schema)" as Amo
  package "tier.py\n(gated tier model)" as At
  package "calibration.py" as Ac
}
F ..> WApi
F ..> WT
WLay ..> WCtx
WCtx ..> WApi
Am ..> Ae
Am ..> At
Ae ..> Amo
At ..> Amo
Ae ..> Ac
"""

T = schema_lib.parse()

GROUPS = {
    "r4_erd_account": (["Roles", "Users", "RefreshTokens", "VerificationEmail", "PasswordResetChallenges", "PlayerProfiles", "PlayerLoginDays", "PlayerActivityDays"], []),
    "r4_erd_social": (["FriendRequests", "Friendships", "BlockedUsers"], ["Users"]),
    "r4_erd_training": (["Techniques", "TechniqueReferences", "Lessons", "Drills", "LessonDrills", "ContentCatalogs", "PlayerLessonDrills", "TrainingSessions", "PlayerTechniqueMastery", "PlayerTechniquesErrors", "GameCommands"], ["PlayerProfiles", "GameMatches"]),
    "r4_erd_match": (["GameMatches", "GameMatchPlayers", "MatchScore", "ratingledger", "MatchHistoryReplay", "MatchmakingSearches", "DedicatedOperations"], ["PlayerProfiles"]),
    "r4_erd_economy": (["MembershipOrders", "Currencies", "PlayerCurrencies", "CurrencyTransactions", "Cosmetics", "CosmeticPrices", "PlayerCosmetics", "Missions", "PlayerMissions", "Achievements", "PlayerAchievements", "ProgressRewardGrants"], ["PlayerProfiles"]),
    "r4_erd_moderation": (["ReportPlayerInMatch", "UserSanctions", "SanctionAppeals", "AdminAudits"], ["Users", "PlayerProfiles", "GameMatches"]),
    "r4_erd_feed": (["SocialMedia", "SocialPosts", "SocialPostExclusions", "SocialPostLikes", "SocialPostComments", "SocialConversations", "SocialConversationMembers", "SocialMessages"], ["PlayerProfiles", "GameMatches"]),
    "r4_erd_coaching": (["aitelemetrysessions", "aitelemetryparts", "aianalysisjobs", "aicoachingsessions", "aicoachingprofiles", "ExpertReviews", "CoachingDemoSessions"], ["PlayerProfiles", "Users"]),
}

covered = set()
for key, (focus, refs) in GROUPS.items():
    D[key] = schema_lib.erd(T, focus, refs)
    D[key.replace("r4_erd_", "r4_lerd_")] = schema_lib.erd(T, focus, refs, physical=False)
    covered.update(focus)
missing = sorted(set(T) - covered)
assert not missing, "Tables missing from ERDs: %s" % missing

if __name__ == "__main__":
    print(write_all(D))
    print("tables:", len(T), "columns:", sum(len(t["columns"]) for t in T.values()), "fks:", sum(len(t["fks"]) for t in T.values()))
