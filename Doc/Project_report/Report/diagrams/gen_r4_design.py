"""PlantUML sources for Report 4: class, sequence, activity and state machine diagrams."""
from diagram_lib import write_all

D = {}

# ---------------------------------------------------------------- class diagrams
D["r4_cls_account"] = r"""
skinparam classAttributeIconSize 0
package "Unity client" {
  class AccountScreen <<MonoBehaviour>> {
    - panel : AccountPanelView
    + SignIn() : void
    + Register() : void
    + VerifyCode() : void
    + RequestPasswordReset() : void
  }
  class AccountService {
    + Profile : BackendPlayerProfile
    + IsSignedIn : bool
    + AccessScope : SessionAccessScope
    + IsRestricted : bool
    + RegisterAsync(email, password, confirm) : Task
    + VerifyEmailAsync(email, code) : Task<ApiMessage>
    + ResendOtpAsync(email) : Task<ResendOtpResponse>
    + LoginAsync(email, password) : Task
    + ForgotPasswordAsync(email) : Task<ResendOtpResponse>
    + ResetPasswordAsync(email, code, password) : Task<ApiMessage>
    + LogoutAsync() : Task
  }
  class ApiClient {
    + SendAsync<T>(method, path, body, token) : Task<T>
  }
}
package "Web API" {
  class AuthController <<ApiController>> {
    + Register(RegisterRequest) : IActionResult
    + VerifyEmail(VerifyEmailRequest) : IActionResult
    + Login(LoginRequest) : IActionResult
    + RefreshToken(RefreshRequest) : IActionResult
    + ResendOtp(ResendOtpRequest) : IActionResult
    + ForgotPassword(ForgotPasswordRequest) : IActionResult
    + ResetPassword(ResetPasswordRequest) : IActionResult
    + Logout() : IActionResult
  }
  interface IAuthService {
    + RegisterAsync(RegisterRequest) : Task<AuthResponse>
    + VerifyEmailAsync(VerifyEmailRequest) : Task<bool>
    + ResendOtpAsync(ResendOtpRequest) : Task<ResendOtpResponse>
    + LoginAsync(LoginRequest) : Task<AuthResponse>
    + RefreshTokenAsync(RefreshRequest) : Task<AuthResponse>
    + LogoutAsync(Guid userId) : Task<bool>
    + ForgotPasswordAsync(ForgotPasswordRequest) : Task<ResendOtpResponse>
    + ResetPasswordAsync(ResetPasswordRequest) : Task
    + ValidateSessionAsync(Guid userId, int version, string scope) : Task<bool>
  }
  class AuthService {
    - unitOfWork : IUnitOfWork
    - emailSender : IEmailSender
    - settings : ServerSettings
    - HashRefreshToken(token) : string
  }
  interface IEmailSender {
    + SendAsync(to, subject, body) : Task
  }
  class SmtpEmailSender
  interface IUnitOfWork {
    + Users : IUserRepository
    + Profiles : IPlayerProfileRepository
    + SaveChangesAsync() : Task<int>
    + BeginTransactionAsync() : Task
    + CommitTransactionAsync() : Task
  }
}
AccountScreen --> AccountService
AccountService --> ApiClient
ApiClient ..> AuthController : HTTP JSON
AuthController --> IAuthService
AuthService ..|> IAuthService
AuthService --> IUnitOfWork
AuthService --> IEmailSender
SmtpEmailSender ..|> IEmailSender
"""

D["r4_cls_training"] = r"""
skinparam classAttributeIconSize 0
package "Unity client" {
  class TrainingDirector <<MonoBehaviour>> {
    + Phase : TrainingPhase
    + Lesson : TrainingDrill
    + Session : TrainingAttempt
    + Catalogue() : void
    + Select(id : string) : void
    + StartLesson(retry : bool) : void
    + Advance(dt, mask, movement, run, tap) : void
    + CommitTouchShot(mask : int) : void
    + SaveActive(force : bool) : void
  }
  enum TrainingPhase {
    Catalogue
    Briefing
    Practice
    Recovery
    Finished
  }
  class TrainingEngine <<static>> {
    + Unlocked(profile, drill) : bool
    + Start(profile, id, retry, catalog) : TrainingAttempt
    + Complete(profile, attempt, catalog) : bool
    + Recommended(profile, catalog) : string
  }
  class ProgressionRuntime {
    + Snapshot : OnlineGameSnapshot
    + IsOffline : bool
    + PendingOperations : int
    + StartTrainingAsync(id, retry) : Task<OnlineGameSnapshot>
    + SaveTrainingAsync(attempt, completed) : Task<OnlineGameSnapshot>
    + TryEnterOffline() : bool
    + SyncOfflineAsync() : Task
  }
  class OfflineTrainingStore {
    + Owner : string
    + PendingCount : int
    + Start(code, retry) : OnlineGameSnapshot
    + Save(attempt, completed) : OnlineGameSnapshot
    + UploadBatch() : List<TrainingAttempt>
    + Accept(OnlineTrainingSyncResult) : void
  }
  class TrainingAttempt {
    + id : string
    + lessonId : string
    + trials : int
    + successes : int
    + stars : int
    + catalogVersion : string
    + progressEpoch : long
  }
}
package "Web API" {
  class GameTrainingController <<ApiController>> {
    + Start(StartTrainingRequest) : GameSnapshot
    + Checkpoint(TrainingCheckpointRequest) : GameSnapshot
    + Complete(TrainingCompleteRequest) : GameSnapshot
    + SyncTraining(TrainingSyncRequest) : TrainingSyncResult
  }
  interface IGameTrainingService {
    + StartTraining(player, request) : Task<GameSnapshot>
    + UpdateTraining(player, request, completed) : Task<GameSnapshot>
    + SyncTraining(player, request) : Task<TrainingSyncResult>
  }
  class OnlineGameService {
    - unitOfWork : IUnitOfWork
    - catalog : IGameCatalogService
  }
  interface IGameCatalogService {
    + Load() : Task<GameCatalog>
  }
}
TrainingDirector --> TrainingPhase
TrainingDirector --> ProgressionRuntime
TrainingDirector ..> TrainingEngine
ProgressionRuntime --> OfflineTrainingStore
OfflineTrainingStore "1" o-- "0..*" TrainingAttempt
ProgressionRuntime ..> GameTrainingController : HTTP JSON
GameTrainingController --> IGameTrainingService
OnlineGameService ..|> IGameTrainingService
OnlineGameService --> IGameCatalogService
"""

D["r4_cls_gameplay"] = r"""
skinparam classAttributeIconSize 0
class BotMatch <<MonoBehaviour>> {
  + human : MatchActor
  + bot : MatchActor
  + Rules : SinglesRules
  + Phase : MatchPhase
  + BallPosition : Vector3
  + BallVelocity : Vector3
  + TickInput(mask, cancel) : void
  + RequestShot(id, intent) : bool
  + StepSimulation(dt, move, run) : void
  + PredictContact(end, bounce, out point, out delay) : bool
  + Timeout() : void
  + Restart() : void
}
class MatchActor <<MonoBehaviour>> {
  + playerId : int
  + end : int
  + Intent : ShotIntent
  + Swinging : bool
  + InKitchen : bool
  + Swing(intent) : bool
  + CanContact(...) : bool
  + ConsumeContact() : void
  + Feet() : CourtPoint
}
class SinglesRules {
  + Server : int
  + Points : int[2]
  + Games : int[2]
  + Live : bool
  + LastFault : Fault
  + PrepareServe(end) : void
  + Serve(player, left, right, contact, elapsed) : Fault
  + Hit(player, contact, feetInKitchen) : Fault
  + Bounce(point) : Fault
  + CommitRally() : void
  + NextGame() : void
  + TakeTimeout(player) : bool
}
class ShotInput {
  + Selection : string
  + CommittedMask : int
  + Tick(mask, cancel, serve, out intent) : bool
  + Cancel() : void
}
class ShotIntent <<struct>> {
  + kind : ShotKind
  + power : bool
  + cross : bool
  + ContactDelay : float
}
enum ShotKind {
  Drive
  Dink
  Volley
  Drop
  Smash
  Reset
  Serve
}
class PaddleContact <<static>> {
  + TimingWindow = 0.065
  + PerfectWindow = 0.030
  + PaddleRadius = 0.145
  + TryEvaluate(...) : bool
  + Grade(distance, timing) : HitGrade
}
class BallFlight <<static>> {
  + Gravity = 9.81
  + Restitution = 0.73
  + Launch(from, target, apex) : Vector3
  + Integrate(ref p, ref v, dt) : void
  + Rebound(ref p, ref v) : void
}
class ShotTrajectory <<static>> {
  + Target(actor, shot, ball, serverX) : Vector3
  + Launch(shot, ball, target) : Vector3
}
enum MatchPhase {
  Intro
  Cinematic
  PreServe
  Rally
  Settling
  BetweenPoints
  BetweenGames
  Timeout
  MatchOver
}
BotMatch "1" *-- "2" MatchActor
BotMatch "1" *-- "1" SinglesRules
BotMatch "1" *-- "1" ShotInput
BotMatch --> MatchPhase
BotMatch ..> PaddleContact
BotMatch ..> BallFlight
BotMatch ..> ShotTrajectory
ShotInput ..> ShotIntent
ShotIntent --> ShotKind
MatchActor --> ShotIntent
"""

D["r4_cls_bot"] = r"""
skinparam classAttributeIconSize 0
interface IAdaptiveBotContext {
  + Actor : MatchActor
  + BallPosition : Vector3
  + BallVelocity : Vector3
  + Incoming : bool
  + Serving : bool
  + Stroke : int
  + PredictContact(bounce, out point, out delay, height, beforeBounce) : bool
  + RequestShot(intent) : bool
}
class LocalAdaptiveBotContext
class NetworkBotContext
class BhTreeBot <<MonoBehaviour>> {
  + graph : BehaviorGraph
  + Elo : int
  + Tier : int
  + ReactionSeconds : float
  + AimError : float
  + Initialize(context, elo, policy, seed) : void
  + Step(dt, out move, out run) : void
  + Wait() : bool
  + Serve() : bool
  + Shot(kind) : bool
  + Recover() : bool
}
class BotPolicy {
  + tiers : BotTierProfile[]
  + Profile(elo) : BotTierProfile
}
class BotTierProfile {
  + minimumElo : int
  + reactionSeconds : float
  + aimError : float
  + aggressiveness : float
}
class LocalBot {
  + difficulty : int
  + Step(...) : void
  + Reset() : void
}
class "Bot Behavior\n(Unity Behavior graph)" as Graph <<asset>>
class BotContactChance {
  + Resolve(random, elo, stroke) : bool
}
LocalAdaptiveBotContext ..|> IAdaptiveBotContext
NetworkBotContext ..|> IAdaptiveBotContext
BhTreeBot --> IAdaptiveBotContext
BhTreeBot --> BotPolicy
BotPolicy "1" *-- "3" BotTierProfile
BhTreeBot --> Graph : ticks
BhTreeBot ..> BotContactChance
note right of LocalBot : Fixed difficulty bot used by\nEasy, Medium and Hard levels.
note bottom of BhTreeBot : Adaptive bot used by the fourth level\nand by ranked bot fallback. Tier from Elo:\n< 500, 500 to 1999, >= 2000.
"""

D["r4_cls_online"] = r"""
skinparam classAttributeIconSize 0
class MultiplayerApp <<MonoBehaviour>> {
  + State : FlowState
  + Mode : PlayMode
  + Room : PhotonSession
  + Match : PhotonSession
  + Roster : MatchRoster
  + FindAsync() : Task
  + CancelSearchAsync() : Task
  + CreateCustomAsync() : Task
  + JoinRoomAsync(code) : Task
  + AcceptMatchAsync() : Task
  + DeclineMatchAsync() : Task
  - ConnectDedicatedAsync(roster) : Task
}
enum FlowState {
  Menu
  WaitingRoom
  Searching
  Connecting
  InMatch
  Reconnecting
  Results
}
class PhotonSession {
  + Runner : NetworkRunner
  + Lobby : PhotonRoomState
  + Data : PhotonRoomData
  + IsDedicated : bool
  + StartAsync(session, player, region, data) : Task
  + JoinAsync(session, player) : Task
  + StartDedicatedAsync(session, region, roster, port, admission) : Task
  + JoinDedicatedAsync(session, region, player, token, roster) : Task
  + LeaveAsync() : Task
}
class PhotonRoomState <<NetworkBehaviour>> {
  + Data : PhotonRoomData
  + TryBeginRankedFallback() : bool
  + CompleteRankedFallback(bot) : void
}
class NetworkMatch <<NetworkBehaviour>> {
  + ScoreA : int
  + ScoreB : int
  + GamesA : int
  + GamesB : int
  + Finished : bool
  + Rules : PickleballRules
  + Roster : MatchRoster
}
class NetworkPlayer <<NetworkBehaviour>> {
  + Identity : string
  + Team : int
  + Actor : MatchActor
  + Simulate() : void
  + CaptureDedicatedInput() : DedicatedInput
  + CommitTouchShot(mask) : void
}
class PickleballRules {
  + mode : PlayMode
  + score : int[2]
  + games : int[2]
  + Serve(...) : bool
  + Hit(...) : bool
  + Bounce(...) : void
  + Fault(...) : void
  + SettleRally() : void
  + Forfeit(team) : void
}
class MatchRoster {
  + matchId : string
  + mode : PlayMode
  + members : List<RoomMember>
}
MultiplayerApp --> FlowState
MultiplayerApp "1" o-- "0..2" PhotonSession : Room / Match
PhotonSession "1" *-- "1" PhotonRoomState
PhotonSession ..> NetworkMatch : spawns
NetworkMatch "1" *-- "1" PickleballRules
NetworkMatch "1" o-- "2..4" NetworkPlayer
NetworkMatch --> MatchRoster
MultiplayerApp --> MatchRoster
"""

D["r4_cls_dedicated"] = r"""
skinparam classAttributeIconSize 0
package "Dedicated game server (Unity, UNITY_SERVER)" {
  class DedicatedWorker <<MonoBehaviour>> {
    - serviceKey : string
    - job : Allocation
    + Record(player, shot) : void
    + SubmitResults(json) : void
    + Abort() : void
    - Loop() : Task
    - Heartbeat(lease) : Task
  }
  class DedicatedAdmission <<static>> {
    + Verify(token, secret, match, lease, now) : Claims
  }
}
package "Web API" {
  class DedicatedController <<ApiController>> {
    + Ready()
    + Heartbeat()
    + Acknowledge()
    + AuthorizeAdmission()
    + Complete()
    + Release()
    + Telemetry(lease)
    + Replay(lease)
  }
  class GameController <<ApiController>> {
    + Ticket(MatchId) : DedicatedTicket
  }
  interface IDedicatedServerService {
    + Authenticate(key) : bool
    + Ticket(player, match) : Task<DedicatedTicket>
    + Poll(worker) : Task<Allocation>
    + Renew(lease) : Task<HeartbeatReply>
    + AuthorizeAdmission(lease, token) : Task<bool>
    + Complete(lease, result) : Task
    + Release(lease) : Task
    + InterruptExpired() : Task
    + LiveMatches() : Task<List<LiveMatchDto>>
  }
  class DedicatedServerService
  class DedicatedLeaseRepository {
    + Current() : Task<DedicatedLease>
    + Claim(worker, seconds) : Task<bool>
    + Renew(lease, seconds) : Task<bool>
    + Release(lease) : Task
  }
  class DedicatedLeaseMonitor <<BackgroundService>>
}
DedicatedWorker ..> DedicatedController : HTTP (service key)
DedicatedWorker ..> DedicatedAdmission
GameController --> IDedicatedServerService
DedicatedController --> IDedicatedServerService
DedicatedServerService ..|> IDedicatedServerService
DedicatedServerService --> DedicatedLeaseRepository
DedicatedLeaseMonitor --> IDedicatedServerService
note bottom of DedicatedLeaseRepository : One Redis key "slot" holds the only\nactive allocation (single-slot design).
"""

D["r4_cls_coaching"] = r"""
skinparam classAttributeIconSize 0
package "Unity client" {
  class QuickCoachingRules <<static>> {
    + RallyTip(playerFault, fault) : string
    + Tip(shots, start) : string
  }
  class CoachingRuntime {
    + Summary : CoachingSummary
    + Begin(sessionId, type) : CoachingSession
    + Checkpoint() : void
    + Close() : void
    + LoadSummary(sessionId) : Task<CoachingSummary>
  }
  class ShotTelemetry {
    + shotId : string
    + kind : string
    + technique : string
    + outcome : string
    + timingError : float?
    + assisted : bool
    + kitchen : bool?
  }
}
package "Web API" {
  class AiCoachingController <<ApiController>> {
    + Telemetry(TelemetryBatch)
    + Summary(sessionId)
  }
  interface IHybridCoachingService {
    + Ingest(player, batch) : Task<TelemetryAck>
    + Summary(player, session) : Task<CoachingSummary>
    + BuildRequest(job) : Task<AnalyzeRequest>
  }
  class HybridCoachingService
  class HybridAiWorker <<BackgroundService>> {
    - ClaimJob() : Task<AnalysisJob>
    - Analyze(job) : Task
  }
}
package "AI Coaching Service (FastAPI)" {
  class "main.py" as Main {
    + POST /v1/analyze-session
    + GET /ready
  }
  class "engine.analyze()" as Engine
  class TierPredictor {
    + status : str
    + predict(request) : dict
  }
}
CoachingRuntime "1" o-- "0..*" ShotTelemetry
CoachingRuntime ..> AiCoachingController : HTTP JSON
AiCoachingController --> IHybridCoachingService
HybridCoachingService ..|> IHybridCoachingService
HybridAiWorker --> IHybridCoachingService
HybridAiWorker ..> Main : HTTP + X-AI-Key
Main --> Engine
Main --> TierPredictor
"""

D["r4_cls_progress"] = r"""
skinparam classAttributeIconSize 0
package "Unity client" {
  class ProgressionRuntime {
    + Snapshot : OnlineGameSnapshot
    + ReloadAsync() : Task
    + CommandAsync(path, payload) : Task<OnlineGameSnapshot>
    + ClaimAsync(key) : Task
  }
  class PlayerProgressionMenu <<MonoBehaviour>>
  class OfflineSocialMenu <<MonoBehaviour>>
  class ShopScreen <<MonoBehaviour>>
  class ReplayView <<MonoBehaviour>>
}
package "Web API" {
  class GameController <<ApiController>> {
    + State() : GameSnapshot
    + Catalog() : GameCatalog
    + Claim(ClaimRequest) : GameSnapshot
    + Purchase(PurchaseRequest) : GameSnapshot
    + Equip(EquipRequest) : GameSnapshot
    + Onboarding(OnboardingRequest) : GameSnapshot
  }
  class OnlineSocialController <<ApiController>> {
    + Read(view) : OnlineSocialPage
    + Act(SocialAction) : void
  }
  class OnlineReplaysController <<ApiController>>
  interface IOnlineGameService {
    + Read(player) : Task<GameSnapshot>
    + Claim(player, request) : Task<GameSnapshot>
    + Purchase(player, request) : Task<GameSnapshot>
    + Equip(player, request) : Task<GameSnapshot>
    + Onboarding(player, request) : Task<GameSnapshot>
  }
  interface IOnlineSocialService {
    + Read(player, view, query) : Task<OnlineSocialPage>
    + Act(player, action) : Task
  }
  interface IMatchReplayService
  class OnlineGameService
  class OnlineSocialService
  interface IUnitOfWork
  class GameRepository
  class PickleVerseContext <<DbContext>>
}
PlayerProgressionMenu --> ProgressionRuntime
ShopScreen --> ProgressionRuntime
OfflineSocialMenu ..> OnlineSocialController : HTTP
ReplayView ..> OnlineReplaysController : HTTP
ProgressionRuntime ..> GameController : HTTP JSON
GameController --> IOnlineGameService
OnlineSocialController --> IOnlineSocialService
OnlineReplaysController --> IMatchReplayService
OnlineGameService ..|> IOnlineGameService
OnlineSocialService ..|> IOnlineSocialService
OnlineGameService --> IUnitOfWork
OnlineSocialService --> IUnitOfWork
IUnitOfWork --> GameRepository
GameRepository --> PickleVerseContext
"""

D["r4_cls_admin"] = r"""
skinparam classAttributeIconSize 0
package "Admin Web (React)" {
  class AuthContext
  class "CMS page" as CmsPage
  class "Users page" as UsersPage
  class "Moderation page" as ModPage
  class "Matches page" as MatchesPage
  class "Analytics page" as AnalyticsPage
  class "Axios client\n(ApiResponse unwrap)" as Axios
}
package "Web API" {
  class ContentController <<ApiController>>
  class ProgressResetController <<ApiController>>
  class AdminController <<ApiController>>
  class AdminModerationController <<ApiController>>
  class LiveMatchesController <<ApiController>>
  class AnalyticsController <<ApiController>>
  class ExpertReviewController <<ApiController>>
  interface IContentService {
    + Lessons() : Task<List<LessonSummary>>
    + Get(id) : Task<LessonDraft>
    + Save(actor, id, draft, expectedVersion) : Task<LessonDraft>
    + Publish(actor, id, publish) : Task<LessonDraft>
  }
  interface IProgressResetService {
    + Preview(player, groups) : Task<ResetPreview>
    + Execute(actor, player, request) : Task<ResetReceipt>
  }
  interface IModerationService {
    + Apply(actor, request) : Task<SanctionDecision>
    + Revoke(actor, sanction, reason) : Task
    + ReviewReport(actor, request) : Task
    + SubmitAppeal(player, request) : Task<SanctionAppeal>
    + ReviewAppeal(actor, id, request) : Task<SanctionAppeal>
    + StopMatch(actor, match, reason) : Task<DedicatedOperationRecord>
  }
  interface IAnalyticsService {
    + Summary() : Task<AdminAnalyticsResponse>
    + Errors() : Task<List<HeatmapDto>>
    + Report(from, to) : Task<AnalyticsReport>
  }
  interface IExpertReviewService {
    + Sessions() : Task
    + Evidence(batch) : Task
    + Review(actor, batch, request) : Task
    + Export() : Task
  }
}
CmsPage --> Axios
UsersPage --> Axios
ModPage --> Axios
MatchesPage --> Axios
AnalyticsPage --> Axios
AuthContext --> Axios
Axios ..> ContentController : HTTPS JSON + JWT
Axios ..> AdminController
Axios ..> AdminModerationController
Axios ..> LiveMatchesController
Axios ..> AnalyticsController
Axios ..> ExpertReviewController
Axios ..> ProgressResetController
ContentController --> IContentService
ProgressResetController --> IProgressResetService
AdminModerationController --> IModerationService
LiveMatchesController ..> IModerationService
AnalyticsController --> IAnalyticsService
ExpertReviewController --> IExpertReviewService
"""

# ---------------------------------------------------------------- sequence diagrams
SEQ_HEAD = "autonumber\nskinparam responseMessageBelowArrow true\n"

D["r4_seq_register"] = SEQ_HEAD + r"""
actor Visitor
participant "AccountScreen" as UI
participant "AuthController" as C
participant "AuthService" as S
database "PostgreSQL" as DB
participant "SmtpEmailSender" as M
Visitor -> UI : enter email, password, confirmation
UI -> C : POST /api/Auth/register
C -> S : RegisterAsync(request)
S -> S : validate password (6-128) and confirmation
alt confirmation mismatch
  S --> C : 400 password_confirmation_mismatch
else email already registered
  S -> DB : find user by email
  S --> C : 400 email_exists
else valid
  S -> DB : insert Users (Status=ACTIVE) and PlayerProfiles
  S -> DB : insert VerificationEmail (HMAC code, expires in 10 min)
  S -> M : send 6-digit code
  S --> C : AuthResponse (verification required)
end
C --> UI : ApiResponse
UI --> Visitor : show Email Code screen
Visitor -> UI : enter code
UI -> C : POST /api/Auth/verify-email
C -> S : VerifyEmailAsync(request)
alt code valid and attempts < 5
  S -> DB : mark verified
  S --> C : true
else invalid or expired
  S --> C : 400 invalid code
end
C --> UI : result
"""

D["r4_seq_login"] = SEQ_HEAD + r"""
actor Visitor
participant "AccountScreen" as UI
participant "SceneLoading" as SL
participant "AuthController" as C
participant "AuthService" as S
database "PostgreSQL" as DB
participant "GameController" as G
Visitor -> UI : email and password
UI -> SL : LoadPrepared(Menu)
SL -> C : POST /api/Auth/login
C -> S : LoginAsync(request)
S -> DB : read user, role, verification
alt wrong email or password
  S --> C : 401 invalid_credentials
  C --> UI : error, stay on Login
else email not verified
  S --> C : 401 email_not_verified
  C --> UI : open Email Code screen
else account restricted
  S -> DB : store refresh token (SHA-256)
  S --> C : tokens, scope = appeals
  C --> UI : open Decisions and Appeals
else active account
  S -> DB : store refresh token (SHA-256)
  S --> C : access token (2 h), refresh token (14 d), scope = game
  C --> SL : AuthResponse
  SL -> G : GET /api/Game/state
  G --> SL : GameSnapshot (profile, catalog, inventory)
  SL --> Visitor : reveal Menu after warm-up
end
"""

D["r4_seq_reset_pwd"] = SEQ_HEAD + r"""
actor Visitor
participant "AccountScreen" as UI
participant "AuthController" as C
participant "AuthService" as S
database "PostgreSQL" as DB
participant "PasswordRecoveryMailQueue" as Q
participant "Gmail SMTP" as M
Visitor -> UI : request password reset (email)
UI -> C : POST /api/Auth/forgot-password
C -> S : ForgotPasswordAsync(request)
S -> DB : upsert PasswordResetChallenges (HMAC code, 10 min)
S -> Q : enqueue reset email
S --> C : same reply for known and unknown email
Q -> M : send code
Visitor -> UI : code and new password
UI -> C : POST /api/Auth/reset-password
C -> S : ResetPasswordAsync(request)
alt valid code, < 5 failed attempts
  S -> DB : update password hash (BCrypt)
  S -> DB : revoke refresh tokens, AuthVersion + 1
  S --> C : success
else invalid or expired code
  S --> C : 400 invalid reset code
end
C --> UI : result
"""

D["r4_seq_training_online"] = SEQ_HEAD + r"""
actor Player
participant "TrainingDirector" as TD
participant "ProgressionRuntime" as PR
participant "GameTrainingController" as C
participant "OnlineGameService" as S
database "PostgreSQL" as DB
Player -> TD : choose lesson and press Start
TD -> PR : StartTrainingAsync(lesson)
PR -> C : POST /api/Game/training/start
C -> S : StartTraining(player, request)
S -> DB : check published lesson, prerequisites, level
alt locked or unpublished
  S --> C : 400 lesson not available
  C --> TD : show error
else allowed
  S -> DB : insert TrainingSessions (IsCurrent = true)
  S --> C : GameSnapshot with active attempt
  C --> TD : attempt
  loop each trial
    Player -> TD : shot input
    TD -> TD : contact, rule and target check
    TD -> PR : SaveTrainingAsync(attempt, false)
    PR -> C : POST /api/Game/training/checkpoint
  end
  TD -> PR : SaveTrainingAsync(attempt, true)
  PR -> C : POST /api/Game/training/complete
  C -> S : UpdateTraining(completed)
  S -> S : recompute stars (BR-11), ignore client rewards
  S -> DB : update PlayerLessonDrills, grant first-pass reward once
  S --> C : GameSnapshot
  C --> TD : result (stars, reward)
end
"""

D["r4_seq_offline_sync"] = SEQ_HEAD + r"""
actor Player
participant "TrainingDirector" as TD
participant "ProgressionRuntime" as PR
participant "OfflineTrainingStore" as OS
participant "GameTrainingController" as C
participant "OnlineGameService" as S
database "PostgreSQL" as DB
Player -> PR : Train offline (cached profile)
PR -> OS : Project() confirmed snapshot
Player -> TD : play cached drill
TD -> OS : Save(attempt, completed = true)
OS -> OS : persist JSON on device (atomic replace)
... later, network available ...
PR -> OS : UploadBatch() (max 50)
PR -> C : POST /api/Game/training/sync
C -> S : SyncTraining(player, request)
loop each attempt
  S -> DB : check owner, attempt id, catalog version, progress epoch
  alt new and valid
    S -> DB : apply result and reward once
    S --> C : receipt APPLIED
  else already applied
    S --> C : receipt DUPLICATE
  else stale epoch or invalid
    S --> C : receipt REJECTED (reason)
  end
end
C --> PR : TrainingSyncResult
PR -> OS : Accept(result) clears APPLIED and DUPLICATE items
"""

D["r4_seq_bot_match"] = SEQ_HEAD + r"""
actor Player
participant "ModeSelectionMenu" as MS
participant "BotMatch" as BM
participant "BhTreeBot / LocalBot" as Bot
participant "SinglesRules" as R
participant "ProgressionRuntime" as PR
participant "GameMatchesController" as C
Player -> MS : choose difficulty, time of day, venue
MS -> BM : load BotMatch scene
BM -> PR : StartBotAsync()
PR -> C : POST /api/Game/match/bot/start
C --> PR : match id
BM -> BM : intro cinematic (skippable)
loop each rally until a player wins 2 games
  Player -> BM : movement and shot keys
  BM -> Bot : Step(dt)
  Bot --> BM : movement and shot request
  BM -> R : Serve / Hit / Bounce
  R --> BM : Fault or continue
  BM -> R : CommitRally()
end
BM -> PR : FinishBotAsync(result)
PR -> C : POST /api/Game/match/bot/complete
C --> PR : snapshot with rewards
BM --> Player : result screen and coaching button
"""

D["r4_seq_ranked"] = SEQ_HEAD + r"""
actor Player
participant "MultiplayerApp" as App
participant "GameMatchesController" as C
participant "OnlineGameService\n(RankedSearch)" as S
database "Redis" as R
participant "Photon Cloud" as P
Player -> App : Find match (1v1)
App -> C : POST /api/Game/ranked/search
C -> S : BeginRankedSearch
S -> R : enqueue ticket (Elo window)
loop poll every few seconds
  App -> C : POST /api/Game/ranked/poll
  C -> S : PollRankedSearch
  S -> R : try pair compatible tickets
end
alt human opponent found
  S --> App : MATCHED (session, region, roster)
else no opponent within 180 s
  App -> C : POST /api/Game/ranked/bot
  C -> S : AssignRankedBot
  S --> App : BOT (virtual ranked account)
end
App -> P : start or join Photon match session
App -> C : POST /api/Game/match/online/offer
C --> App : ticket with ready deadline (10 s)
Player -> App : Accept
alt all required humans accept
  App -> C : POST /api/Game/match/online/start
  C --> App : authorityMode = dedicated
else someone declines or deadline passes
  App --> Player : offer closed, search again
end
"""

D["r4_seq_dedicated"] = SEQ_HEAD + r"""
participant "Unity client" as Cl
participant "Web API\n(DedicatedServerService)" as API
database "Redis\n(lease slot)" as R
participant "DedicatedWorker\n(game server)" as W
database "PostgreSQL" as DB
W -> API : POST /dedicated/ready (poll for job)
Cl -> API : POST /api/Game/dedicated/ticket
API -> R : reserve slot for match
alt slot busy
  API --> Cl : busy (ranked waits, custom fails)
else slot reserved
  API --> Cl : starting
end
API --> W : allocation (match, roster, lease)
W -> W : StartDedicatedAsync (Fusion Server)
W -> API : heartbeat (state = ready)
loop every 500 ms until ready
  Cl -> API : ticket
end
API --> Cl : ready + HMAC connection token
Cl -> W : join session with token (UDP 3669)
W -> W : DedicatedAdmission.Verify(token)
W -> API : POST /dedicated/admission
loop during play
  W -> API : heartbeat (score, connections)
  API --> W : pending stop command, if any
end
W -> API : POST /dedicated/complete (result, telemetry, replay)
API -> DB : save GameMatches, MatchScore, ratingledger, rewards (one transaction)
API -> R : release lease
"""

D["r4_seq_coaching"] = SEQ_HEAD + r"""
participant "Unity client or\nDedicatedWorker" as Src
participant "AiCoachingController" as C
participant "HybridCoachingService" as S
database "PostgreSQL" as DB
participant "HybridAiWorker" as W
participant "AI Coaching Service\n(FastAPI)" as AI
Src -> C : POST /api/AiCoaching/telemetry (batch, parts)
C -> S : Ingest(player, batch)
S -> DB : store parts (idempotent by batch and part)
S -> DB : create aianalysisjobs (pending) when all parts arrive
C --> Src : ACK
W -> DB : claim job (lease 60 s)
W -> S : BuildRequest(job)
W -> AI : POST /v1/analyze-session (X-AI-Key)
AI -> AI : rule findings, summary, recommendations, gated tier
AI --> W : analysis result
alt success
  W -> DB : save aicoachingsessions, job completed
else failure
  W -> DB : retry with backoff (max 10 attempts)
end
Src -> C : GET /api/AiCoaching/summary?sessionId=...
C --> Src : summary and drill recommendation
"""

D["r4_seq_purchase"] = SEQ_HEAD + r"""
actor Player
participant "ShopScreen" as UI
participant "ProgressionRuntime" as PR
participant "GameController" as C
participant "OnlineGameService" as S
database "PostgreSQL" as DB
Player -> UI : select item and currency
UI -> PR : CommandAsync("purchase")
PR -> C : POST /api/Game/purchase (requestId)
C -> S : Purchase(player, request)
S -> DB : check GameCommands for requestId
alt request already processed
  S --> C : saved snapshot
else new request
  S -> DB : lock PlayerCurrencies
  alt already owned
    S --> C : 400 already owned
  else balance too low
    S --> C : 400 insufficient TOKENPAD / ELITE TOKEN
  else valid
    S -> DB : debit balance, insert CurrencyTransactions
    S -> DB : insert PlayerCosmetics, save GameCommands
    S --> C : updated snapshot
  end
end
C --> UI : new balance and inventory
"""

D["r4_seq_invite"] = SEQ_HEAD + r"""
actor Host
actor Friend
participant "MultiplayerApp" as App
participant "RoomInvitationsController" as C
participant "RoomInvitationService" as S
database "Redis" as R
Host -> App : create private room
App -> C : POST /api/Game/lobby/presence (room)
Host -> App : open invite list
App -> C : GET /api/Game/lobby/friends
C -> S : List(friends, presence)
S -> R : read presence leases (20 s)
Host -> App : invite friend
App -> C : POST /api/Game/lobby/invitations
C -> S : Send(host, friend, room)
alt friend not in lobby, blocked or room full
  S --> C : 400 / 409 with reason
else valid
  S -> R : store invitation (TTL 60 s)
end
loop every ~3 s
  Friend -> C : POST /api/Game/lobby/presence
  C --> Friend : pending invitation
end
Friend -> C : POST /lobby/invitations/{id}/respond (accept)
C -> S : Respond(accept)
S --> Friend : room session and region
Friend -> App : join Photon room
"""

D["r4_seq_publish"] = SEQ_HEAD + r"""
actor Administrator as Admin
participant "CMS page" as W
participant "ContentController" as C
participant "ContentService" as S
database "PostgreSQL" as DB
participant "GameCatalogService" as G
Admin -> W : edit lesson draft
W -> C : PUT /api/Admin/cms/lessons/{id} (draft, expectedVersion)
C -> S : Save(actor, id, draft, version)
alt version changed
  S --> C : 409 reload before saving
else valid
  S -> DB : update DraftConfig, ContentVersion + 1, AdminAudits
  S --> C : saved draft
end
Admin -> W : Publish
W -> C : POST /api/Admin/cms/lessons/{id}/publish
C -> S : Publish(actor, id, true)
S -> DB : check prerequisites published
S -> DB : PublishStatus = PUBLISHED, insert ContentCatalogs snapshot
S --> C : published lesson
note over G : Next GET /api/Game/catalog returns\nthe new catalog version to game clients
"""

D["r4_seq_moderation"] = SEQ_HEAD + r"""
actor Player
actor "Restricted Player" as RP
actor Administrator as Admin
participant "Web API" as API
participant "ModerationService" as S
database "PostgreSQL" as DB
Player -> API : POST /api/Matches/report (match, target, reason)
API -> DB : insert ReportPlayerInMatch (PENDING)
Admin -> API : POST /api/Admin/reports/review (ACCEPTED, sanction)
API -> S : ReviewReport(actor, request)
S -> DB : update report, insert UserSanctions, AdminAudits (one transaction)
S -> DB : Users.Status = SUSPENDED or BANNED, revoke sessions
RP -> API : POST /api/Auth/login
API --> RP : session with scope = appeals
RP -> API : GET /api/Moderation/decisions
RP -> API : POST /api/Moderation/appeals (statement)
API -> DB : insert SanctionAppeals (PENDING)
Admin -> API : POST /api/Admin/appeals/{id}/review (ACCEPTED or DECLINED)
API -> S : ReviewAppeal(actor, id, request)
alt accepted
  S -> DB : revoke sanction, Users.Status = ACTIVE
else declined
  S -> DB : keep sanction, store decision reason
end
"""

D["r4_seq_stop"] = SEQ_HEAD + r"""
actor Administrator as Admin
participant "Matches page" as W
participant "AdminModerationController" as C
participant "ModerationService" as S
database "PostgreSQL" as DB
participant "DedicatedWorker" as DW
Admin -> W : Stop match with reason
W -> C : POST /api/Admin/matches/{id}/stop
C -> S : StopMatch(actor, match, reason)
S -> DB : insert DedicatedOperations (durable command)
S --> C : operation (not acknowledged)
DW -> C : heartbeat
C --> DW : stop command
DW -> DW : end match, outcome INTERRUPTED
DW -> C : POST /dedicated/operations/ack (lease)
C -> DB : AcknowledgedAt = now, no Elo entry
W -> C : GET /api/Admin/matches/{id}/stop
C --> W : acknowledged
"""

D["r4_seq_reset"] = SEQ_HEAD + r"""
actor Administrator as Admin
participant "Progress Reset dialog" as W
participant "ProgressResetController" as C
participant "ProgressResetService" as S
database "PostgreSQL" as DB
Admin -> W : choose groups (LEARNING, RANKING, MISSIONS, ECONOMY)
W -> C : POST .../progress-reset/preview
C -> S : Preview(player, groups)
S -> DB : read current values and profile Revision
S --> W : impact preview + revision
Admin -> W : confirm with reason
W -> C : POST .../progress-reset (requestId, revision)
C -> S : Execute(actor, player, request)
alt revision changed
  S --> C : 409 preview again
else valid
  S -> DB : reset selected groups, bump epochs, AdminAudits
  S --> C : receipt (same receipt on retry)
end
"""

# ---------------------------------------------------------------- activity diagrams
D["r4_act_w01"] = r"""
|Player|
start
:Register and verify email;
:Sign in;
|Game client|
if (First sign-in?) then (yes)
  :Show experience choice;
  |Player|
  if (Beginner?) then (yes)
    |Game client|
    :Open Tutorial;
    |Player|
    :Complete tutorial steps;
  else (no)
  endif
else (no)
endif
|Player|
:Select unlocked drill;
|Game client|
if (Network available?) then (yes)
  |Web API|
  :Check lesson is published and unlocked;
  :Create training attempt;
else (no)
  |Game client|
  :Start attempt from cached catalog;
endif
|Player|
:Play all trials;
|Game client|
:Evaluate contact, rules and target;
if (Online?) then (yes)
  |Web API|
  :Recompute stars and grant reward once;
else (no)
  |Game client|
  :Save completed run on device;
  :Upload run after reconnect;
  |Web API|
  :Validate epoch and apply once;
endif
|Game client|
:Show stars, reward and tips;
stop
"""

D["r4_act_w02"] = r"""
|Player|
start
:Choose difficulty, time of day and venue;
|Game client|
:Load Bot Match and play intro;
repeat
  |Player|
  :Move and select shot;
  |Game client|
  :Bot decides movement and shot;
  :Apply singles rules to the rally;
  if (Fault?) then (yes)
    :Show quick rule tip;
  endif
  :Commit point;
repeat while (Match won by a player?) is (no)
->yes;
|Web API|
:Save bot result and rewards;
|Game client|
:Upload session telemetry;
|AI coaching service|
:Analyse session with rules;
:Return summary and drill recommendation;
|Player|
:Read coaching summary;
stop
"""

D["r4_act_w03"] = r"""
|Player|
start
if (Private room?) then (yes)
  :Create room and invite friend;
else (no)
  :Find ranked match;
  |Web API|
  if (Opponent found within 180 s?) then (yes)
    :Pair players by Elo;
  else (no)
    :Assign ranked bot;
  endif
endif
|Player|
:Accept ready check (10 s);
|Web API|
if (All required humans accepted?) then (yes)
  :Create match record (PLAYING);
  :Reserve dedicated slot and issue ticket;
else (no)
  :Close offer;
  stop
endif
|Dedicated server|
:Verify ticket and admit players;
:Simulate rallies authoritatively;
if (Stopped or lease lost?) then (yes)
  :End match as INTERRUPTED;
else (no)
  :Send final result;
endif
|Web API|
:Save scores, Elo and rewards once;
|Player|
:View result and history;
stop
"""

D["r4_act_w04"] = r"""
|Player|
start
:Report a player from match history;
|Web API|
:Validate reporter and target;
:Store report as PENDING;
|Administrator|
:Review evidence and replay;
if (Report accepted?) then (yes)
  :Apply ban or suspension with reason;
  |Web API|
  :Restrict account and revoke sessions;
  |Restricted Player|
  :Sign in and read decision;
  if (Submit appeal?) then (yes)
    :Write appeal statement;
    |Administrator|
    :Review appeal;
    if (Appeal accepted?) then (yes)
      |Web API|
      :Revoke sanction;
    else (no)
      |Web API|
      :Keep sanction and record reason;
    endif
  else (no)
  endif
else (no)
  |Web API|
  :Close report as DECLINED;
endif
stop
"""

D["r4_act_content"] = r"""
|Administrator|
start
:Open lesson in CMS;
:Edit draft (config, reward, prerequisites);
|Web API|
if (Expected version matches?) then (yes)
  :Save draft and audit;
else (no)
  :Return 409 conflict;
  |Administrator|
  :Reload draft;
  stop
endif
|Administrator|
:Publish lesson;
|Web API|
if (Prerequisites published?) then (yes)
  :Create new catalog snapshot;
else (no)
  :Reject publish;
  stop
endif
|Game client|
:Load new catalog version;
:Show lesson to eligible players;
stop
"""

# ---------------------------------------------------------------- state machines
D["r4_sm_email"] = r"""
[*] --> Unverified : register
Unverified --> Unverified : invalid code [attempts < 5]
Unverified --> Unverified : resend code [cooldown 60 s passed]
Unverified --> Verified : valid code [not expired]
Unverified --> Verified : Admin verifies manually
Verified --> [*]
"""

D["r4_sm_account"] = r"""
[*] --> ACTIVE : account created
ACTIVE --> SUSPENDED : Admin applies suspension\n[expiry date set]
ACTIVE --> BANNED : Admin applies ban
SUSPENDED --> BANNED : Admin applies ban
SUSPENDED --> ACTIVE : expiry reached /\nsanction revoked /\nappeal accepted
BANNED --> ACTIVE : sanction revoked /\nappeal accepted
"""

D["r4_sm_training"] = r"""
[*] --> Active : start [lesson unlocked and published]
Active --> Active : checkpoint after trial
Active --> Abandoned : new attempt started
Active --> Completed : last trial played
state Completed {
  [*] --> Passed : successes >= required
  [*] --> Failed : successes < required
}
Completed --> [*]
Abandoned --> [*]
"""

D["r4_sm_offline"] = r"""
[*] --> SavedOnDevice : finish drill offline
SavedOnDevice --> Uploading : network available
Uploading --> APPLIED : first valid sync
Uploading --> DUPLICATE : same attempt already applied
Uploading --> REJECTED : stale epoch or invalid data
Uploading --> SavedOnDevice : request failed (retry)
APPLIED --> Cleared : receipt accepted
DUPLICATE --> Cleared : receipt accepted
REJECTED --> KeptWithReason : shown in sync details
Cleared --> [*]
KeptWithReason --> [*]
"""

D["r4_sm_botmatch"] = r"""
[*] --> Intro
Intro --> Cinematic : scene loaded
Cinematic --> PreServe : intro ends or skipped
PreServe --> Rally : legal serve
PreServe --> Settling : serve fault or 10 s timeout
PreServe --> Timeout : time-out requested [remaining > 0]
Timeout --> PreServe : resume or 60 s elapsed
Rally --> Settling : fault, out or double bounce
Settling --> BetweenPoints : point committed
Settling --> BetweenGames : game won
Settling --> MatchOver : second game won
BetweenPoints --> PreServe
BetweenGames --> PreServe : ends changed
MatchOver --> Cinematic : play again
MatchOver --> [*] : leave to Mode Selection
"""

D["r4_sm_search"] = r"""
[*] --> SEARCHING : search started
SEARCHING --> MATCHED : compatible player found
SEARCHING --> BOT : no player within 180 s
SEARCHING --> CANCELLED : player cancels
SEARCHING --> TIMEOUT : ticket expired
MATCHED --> [*]
BOT --> [*]
CANCELLED --> [*]
TIMEOUT --> [*]
"""

D["r4_sm_room"] = r"""
state "assigning-bot" as assigning_bot
[*] --> searching : match session created
searching --> assigning_bot : bot fallback started
assigning_bot --> searching : bot assignment failed
assigning_bot --> ready : bot seat filled
searching --> ready : all seats connected
ready --> loading : all humans accepted
ready --> rejected : decline or deadline passed
loading --> playing : court loaded
loading --> rejected : player left
playing --> interrupted : authority lost
playing --> closed : match finished
rejected --> [*]
interrupted --> [*]
closed --> [*]
"""

D["r4_sm_match"] = r"""
[*] --> PREPARING : offer created
PREPARING --> PLAYING : all required players accepted
PREPARING --> CANCELLED : declined or start failed
PLAYING --> FINISHED : trusted result saved\n(outcome WIN / LOSE)
PLAYING --> FINISHED : stop command or lease lost\n(outcome INTERRUPTED, no Elo)
FINISHED --> FINISHED : repeated result ignored
FINISHED --> [*]
CANCELLED --> [*]
"""

D["r4_sm_lease"] = r"""
[*] --> queued : ticket requested [slot free]
[*] --> busy : ticket requested [slot taken]
busy --> queued : slot released
queued --> starting : worker claims job
starting --> ready : worker heartbeat ready
starting --> Released : lease expired
ready --> finishing : final result received
ready --> Released : lease expired or stop acknowledged
finishing --> Released : result saved
Released --> [*]
"""

D["r4_sm_lesson"] = r"""
[*] --> DRAFT : lesson created
DRAFT --> DRAFT : save draft [version matches]
DRAFT --> PUBLISHED : publish [prerequisites published]
PUBLISHED --> UNPUBLISHED : unpublish [no published dependants]
UNPUBLISHED --> PUBLISHED : publish again
PUBLISHED --> PUBLISHED : save draft (live copy unchanged)
"""

D["r4_sm_report"] = r"""
[*] --> PENDING : participant reports player
PENDING --> CHECKING : Admin starts review
PENDING --> ACCEPTED : Admin accepts directly
PENDING --> DECLINED : Admin declines directly
CHECKING --> ACCEPTED : accept
CHECKING --> DECLINED : decline
ACCEPTED --> [*]
DECLINED --> [*]
note right of ACCEPTED : A sanction may be applied\nin the same transaction.
"""

D["r4_sm_appeal"] = r"""
[*] --> PENDING : restricted player submits appeal
PENDING --> CHECKING : Admin starts review
PENDING --> ACCEPTED : accept
PENDING --> DECLINED : decline
CHECKING --> ACCEPTED : accept
CHECKING --> DECLINED : decline
ACCEPTED --> [*] : sanction revoked
DECLINED --> [*] : sanction kept
"""

D["r4_sm_aijob"] = r"""
[*] --> pending : all telemetry parts received
pending --> processing : worker claims job (lease 60 s)
processing --> completed : analysis saved
processing --> pending : error [attempts < 10] / backoff
processing --> pending : lease expired
processing --> failed : error [attempts = 10]
completed --> [*]
failed --> [*]
"""

D["r4_sm_friend"] = r"""
[*] --> PENDING : request sent [not blocked, not friends]
PENDING --> ACCEPTED : receiver accepts
PENDING --> DECLINED : receiver declines
ACCEPTED --> [*] : friendship created
DECLINED --> [*]
"""

if __name__ == "__main__":
    print(write_all(D))
