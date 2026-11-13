"""PlantUML sources for Report 4: architecture, deployment, packages and logical ERDs."""
from diagram_lib import write_all
import schema_lib

D = {}

D["r4_architecture"] = r"""
skinparam componentStyle uml2
actor Player
actor Administrator as Admin
node "Player device" {
  [Unity Game Client] as Client
}
node "Browser" {
  [Admin Web (React SPA)] as Web
}
cloud "Photon Cloud" as Photon
node "Linux host (Docker Compose)" {
  [HTTPS reverse proxy\n(Caddy)] as Nginx
  [Web API\nASP.NET Core 10] as API
  [Background workers\nHybridAiWorker, DedicatedLeaseMonitor] as Workers
  [AI Coaching Service\nPython FastAPI] as AI
  [Dedicated Game Server\nUnity Linux headless] as DS
  database "PostgreSQL 16" as PG
  database "Redis 7.4" as RD
  database "S3-compatible storage" as S3
}
cloud "Gmail SMTP" as SMTP
Player --> Client
Admin --> Web
Web --> Nginx : HTTPS
Nginx --> API : /api (JSON, JWT)
Client --> Nginx : HTTPS REST JSON, JWT
Client --> Photon : Fusion Shared lobby (UDP)
Client --> DS : Fusion client, signed ticket (UDP 3669)
DS --> Photon : session registration
DS --> API : lease, heartbeat, result\n(service key)
API --> PG : Npgsql EF Core
API --> RD : queue, leases, presence, cache
API --> S3 : replay manifest and chunks
API --> SMTP : OTP email (STARTTLS)
Workers --> AI : POST /v1/analyze-session\n(X-AI-Key)
Workers ..> PG
API ..> Workers : hosted in API process
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
  node "backend-api container\nASP.NET Core 10\n127.0.0.1:8080" as ApiC
  node "ai-service container\nPython 3.12 FastAPI\n:8000 (internal)" as AiC
  node "game-server container\nUnity Linux Dedicated Server\n:3669/udp" as GameC
  node "postgres container\nPostgreSQL 16\n:5432 (internal)" as PgC
  node "redis container\nRedis 7.4\n:6379 (internal)" as RdC
  node "s3-storage container\nS3-compatible\n:9000 (internal)" as S3C
}
cloud "Photon Cloud\n(Fusion 2.1.3)" as Photon
cloud "Gmail SMTP\n:587" as Smtp
node "GitHub Actions\n<<CI/CD: plan, checks, build,\npublish, deploy>>" as CI
actor "Developer" as Dev
node "Docker Hub\ntadminm/pickelverse" as Hub

Browser --> HostNginx : HTTPS
WinClient --> HostNginx : HTTPS JSON
Apk --> HostNginx : HTTPS JSON
HostNginx --> ApiC : /api, /health/ready
HostNginx --> WebC : other paths
WinClient --> Photon : UDP
Apk --> Photon : UDP
WinClient --> GameC : UDP 3669
Apk --> GameC : UDP 3669
GameC --> ApiC : HTTP (service key)
GameC --> Photon : UDP
ApiC --> PgC : TCP 5432
ApiC --> RdC : TCP 6379
ApiC --> S3C : HTTP 9000
ApiC --> AiC : HTTP 8000
ApiC --> Smtp : SMTP STARTTLS
Dev --> CI : push to main
CI --> Hub : push changed images\n<component>-<commit>-<run>-<attempt>
CI --> Host : SSH: verified pg_dump,\nimage digests, docker compose up
Hub --> Host : pull by digest
"""

D["r4_pkg_backend"] = r"""
skinparam packageStyle folder
left to right direction
package "PickleVerse.API" as API {
  package "Controllers (28 classes)" as Ctl
  package "Filters and middleware\n(ApiResponse envelope,\nexception handler, scope)" as Flt
  package "Program (DI, JWT, CORS,\nrate limiter, Redis)" as Prog
}
package "PickleVerse.Services" as SVC {
  package "Interfaces" as SI
  package "Implementations" as SImpl
  package "Workers\n(HybridAiWorker,\nDedicatedLeaseMonitor)" as SW
  package "Configuration\n(ServerSettings)" as SCfg
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
  }
  package "api\n(client.ts, adminApi.ts,\ncoachingApi.ts)" as WApi
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
    "r4_erd_economy": (["Currencies", "PlayerCurrencies", "CurrencyTransactions", "Cosmetics", "CosmeticPrices", "PlayerCosmetics", "Missions", "PlayerMissions", "Achievements", "PlayerAchievements", "ProgressRewardGrants"], ["PlayerProfiles"]),
    "r4_erd_moderation": (["ReportPlayerInMatch", "UserSanctions", "SanctionAppeals", "AdminAudits"], ["Users", "PlayerProfiles", "GameMatches"]),
    "r4_erd_coaching": (["aitelemetrysessions", "aitelemetryparts", "aianalysisjobs", "aicoachingsessions", "aicoachingprofiles", "ExpertReviews", "CoachingDemoSessions"], ["PlayerProfiles", "Users"]),
}

covered = set()
for key, (focus, refs) in GROUPS.items():
    D[key] = schema_lib.erd(T, focus, refs)
    covered.update(focus)
missing = sorted(set(T) - covered)
assert not missing, "Tables missing from ERDs: %s" % missing

if __name__ == "__main__":
    print(write_all(D))
    print("tables:", len(T), "columns:", sum(len(t["columns"]) for t in T.values()), "fks:", sum(len(t["fks"]) for t in T.values()))
