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
class MatchCamera <<MonoBehaviour>> {
  + player : MatchActor
  + DesiredPosition : Vector3
  + DesiredRotation : Quaternion
  + SnapToPlayer() : void
}
class CourtFollowView <<static>> {
  + DesktopFeetLine = 0.25
  + TouchFeetLine = 0.32
  + Position(player, end, doubles) : Vector3
  + Frame(camera, player, end, doubles, feetLine, out rotation, out fov) : void
  + KeepFeetAbove(rotation, camera, feet, fov, feetLine) : Quaternion
  + Apply(view, player, end, doubles, dt) : void
}
class HudActorClearance <<MonoBehaviour>> {
  + Attach(panel, actor) : HudActorClearance
}
MatchCamera --> MatchActor : follows
MatchCamera ..> CourtFollowView
HudActorClearance ..> MatchActor : keeps the shot cue clear of
note bottom of CourtFollowView : Feet stay at or above 25% of the screen\nheight on PC and 32% on Android; the view\nwidens by up to 8 degrees to keep the far\nbaseline in the frame.
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
  Delay
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
class SurrenderDesk {
  + Ask(player) : void
  + Agree(player) : void
  + Refuse(player) : void
  + Disconnected(player) : void
  + Tick() : void
  + IsOpen(team) : bool
}
class SurrenderPanel <<MonoBehaviour>> {
  + Current : View
  + Ask(court) : void
  + Cancel() : void
  + Refresh(court, hidden) : void
}
NetworkMatch "1" *-- "1" PickleballRules
NetworkMatch "1" *-- "0..1" SurrenderDesk : state authority
SurrenderPanel ..> NetworkMatch : surrender messages
NetworkMatch "1" o-- "2..4" NetworkPlayer
NetworkMatch --> MatchRoster
MultiplayerApp --> MatchRoster
class PlayMultiplayerPresenter <<MonoBehaviour>> {
  + CanInviteFriends : bool
  + ShowInvite() : void
  - Refresh() : void
}
class LobbyFriendsSidebar <<MonoBehaviour>> {
  + IsOpen : bool
  + Page : InvitableFriendsPage
  + Open(focusSearch) : void
  + Fetch(append) : Task
  + SendAsync(friend) : Task
}
PlayMultiplayerPresenter --> MultiplayerApp : room and queue commands
PlayMultiplayerPresenter "1" *-- "1" LobbyFriendsSidebar
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
  class DedicatedWorkerController <<ApiController>> {
    + Poll()
    + Ready()
    + Heartbeat()
    + Acknowledge()
    + AuthorizeAdmission()
    + Complete()
    + Release()
    + Telemetry(lease)
    + Replay(lease)
    + Chunk(lease, index)
  }
  class DedicatedController <<ApiController>> {
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
DedicatedWorker ..> DedicatedWorkerController : HTTP /api/Server (service key)
DedicatedWorker ..> DedicatedAdmission
DedicatedController --> IDedicatedServerService
DedicatedWorkerController --> IDedicatedServerService
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
  class AuthContext {
    + login(email, password) : Promise
    + logout() : void
  }
  class CmsPage <<page>> {
    + saveDraft(id, draft, version) : Promise
    + publish(id) : Promise
    + unpublish(id) : Promise
  }
  class UsersPage <<page>> {
    + search(query) : Promise
    + previewReset(player, groups) : Promise
    + resetProgress(player, request) : Promise
  }
  class ModerationPage <<page>> {
    + reviewReport(request) : Promise
    + reviewAppeal(id, request) : Promise
  }
  class MatchesPage <<page>> {
    + stopMatch(match, reason) : Promise
    + stopStatus(match) : Promise
  }
  class AnalyticsPage <<page>> {
    + load() : Promise
  }
  class "Axios client\n(ApiResponse unwrap)" as Axios
}
package "Web API" {
  class ContentController <<ApiController>> {
    + List() / Get(id)
    + Create(request) / Save(id, request)
    + AddDrill(id, request) / SaveDrill(id, drillId, request)
    + DeleteDrill(id, drillId, expectedVersion)
    + Publish(id, request) / Unpublish(id, request)
  }
  class ReplayLaunchController <<ApiController>> {
    + Issue(match) : ReplayLaunchLink
    + Redeem(input) : ReplayLaunchView
    + Read(match) / Chunk(match, index)
  }
  class ProgressResetController <<ApiController>> {
    + Preview(player, request) : IActionResult
    + Reset(player, request) : IActionResult
  }
  class AdminController <<ApiController>> {
    + GetUsers(query) : IActionResult
    + AssignRole(request) : IActionResult
    + BanUser(request) : IActionResult
    + UnbanUser(request) : IActionResult
    + ManualVerifyUserEmail(user) : IActionResult
    + GetReports(query) : IActionResult
    + ReviewReport(request) : IActionResult
  }
  class AdminModerationController <<ApiController>> {
    + Sanctions(player) : IActionResult
    + Appeals(query) : IActionResult
    + Review(id, request) : IActionResult
    + Stop(match, request) : IActionResult
    + StopOperation(match) : IActionResult
  }
  class LiveMatchesController <<ApiController>> {
    + Live() : IActionResult
  }
  class AnalyticsController <<ApiController>> {
    + Summary() : IActionResult
    + Errors() : IActionResult
    + Report(from, to) : IActionResult
  }
  class ExpertReviewController <<ApiController>> {
    + Sessions(query) : IActionResult
    + Evidence(session) : IActionResult
    + Review(batch, request) : IActionResult
    + TierDataset() : IActionResult
    + CoachingDataset() : IActionResult
  }
  interface IContentService {
    + Lessons() / Get(lessonId)
    + CreateLesson(request) / SaveLesson(lessonId, request)
    + AddDrill / SaveDrill / DeleteDrill
    + Publish(lessonId, request, publish)
  }
  interface IReplayLaunchService {
    + Issue(admin, authVersion, match)
    + Redeem(code)
    + Read(token, match) / Chunk(token, match, index)
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
ModerationPage --> Axios
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
Axios ..> ReplayLaunchController : HTTPS JSON + JWT
ReplayLaunchController --> IReplayLaunchService
ProgressResetController --> IProgressResetService
AdminModerationController --> IModerationService
LiveMatchesController ..> IModerationService
AnalyticsController --> IAnalyticsService
ExpertReviewController --> IExpertReviewService
"""

# ---------------------------------------------------------------- sequence diagrams

# ---------------------------------------------------------------- activity diagrams
# ---------------------------------------------------------------- state machines
D["r4_cls_social"] = r"""
skinparam classAttributeIconSize 0
package "Unity client" {
  class SocialLobbyMenu <<MonoBehaviour>> {
    + OpenFeed() : void
    + OpenInbox() : void
    + OpenDirect(player, name, friend) : void
    - Compose(post) : void
    - SavePost(token) : Task
    - SendDirect() : Task
    - PlayVideo(post, token) : Task
  }
  class SocialLobbyConnection {
    + Ready : bool
    + Run() : Task
    + Invoke(method, args) : Task
  }
  class SocialTransport <<static>> {
    + UploadVideo(file, progress, token) : Task<Guid>
    + ConnectLobby(token) : Task<ClientWebSocket>
  }
}
package "Web API" {
  class SocialContentController <<ApiController>> {
    + Feed(before) : SocialPage<SocialPostItem>
    + Create(input) / Edit(post, input) / Delete(post)
    + Like(post, input) / Comment(post, input)
    + Upload() : SocialMediaReceipt
    + Playback(post) / Video(post, playback)
    + Inbox(offset) / Messages(conversation, before)
    + Read(conversation, input) / Send(input)
  }
  class SocialHub <<SignalR Hub>> {
    + Presence() : Task
    + Whisper(recipient, body) : Task
  }
  interface ISocialContentService
  interface ISocialDirectService
  interface ISocialMediaService
  interface ISocialSessionService
  class SocialContentService
  class SocialDirectService
  class SocialMediaService {
    + MaxVideoBytes = 250000000
    + Upload(actor, stream, length, token)
    + Open(viewer, post, range, token)
    + Cleanup(token)
  }
  class SocialSessionService
  class SocialPresenceRepository <<Redis>>
  class SocialMediaCleanup <<BackgroundService>>
  interface IUnitOfWork
  class GameRepository
}
SocialLobbyMenu --> SocialLobbyConnection
SocialLobbyMenu --> SocialTransport
SocialLobbyMenu ..> SocialContentController : HTTPS JSON
SocialTransport ..> SocialContentController : MP4 upload
SocialLobbyConnection ..> SocialHub : WebSocket (SignalR JSON)
SocialContentController --> ISocialContentService
SocialContentController --> ISocialDirectService
SocialContentController --> ISocialMediaService
SocialContentController --> ISocialSessionService
SocialHub --> ISocialDirectService
SocialHub --> ISocialSessionService
SocialContentService ..|> ISocialContentService
SocialDirectService ..|> ISocialDirectService
SocialMediaService ..|> ISocialMediaService
SocialSessionService ..|> ISocialSessionService
SocialSessionService --> SocialPresenceRepository
SocialMediaCleanup --> ISocialMediaService
SocialContentService --> IUnitOfWork
SocialDirectService --> IUnitOfWork
SocialMediaService --> IUnitOfWork
IUnitOfWork --> GameRepository
"""

D["r4_cls_membership"] = r"""
skinparam classAttributeIconSize 0
package "Unity client" {
  class MembershipPanel <<MonoBehaviour>> {
    + Show(locked) : void
    - Buy() : Task
    - Render() : void
  }
  class ModeSelectionMenu <<MonoBehaviour>> {
    - RequireMembership() : bool
    - RefreshMembership() : void
  }
  class ProgressionRuntime {
    + HasMembership : bool
    + CurrentMembership : MembershipOffer
    + ReadMembershipAsync(token) : Task<MembershipOffer>
    + CheckoutMembershipAsync(token) : Task<MembershipOffer>
  }
}
package "Web API" {
  class MembershipController <<ApiController>> {
    + Read() : MembershipOffer
    + Checkout(token) : MembershipOffer
    + Refresh(token) : MembershipOffer
    + Webhook(body, token) : ApiResponse
  }
  class GameMatchesController <<ApiController>> {
    - MemberAction(action, otherPlayers)
  }
  interface IMembershipService {
    + Read(player) : MembershipOffer
    + Checkout(player, token) : MembershipOffer
    + Refresh(player, token) : MembershipOffer
    + ReceiveWebhook(body, token) : void
    + Require(players) : void
  }
  class MembershipService {
    + PriceVnd = 49000
    + EliteTokenBonus = 3000
    - Reconcile(order, token)
    - Apply(order, payment)
  }
  interface IPayOsGateway {
    + Create(orderCode, amount, expiresAt, token) : PayOsCheckout
    + Read(orderCode, token) : PayOsPayment
    + VerifyWebhook(body) : PayOsNotification
  }
  class PayOsGateway {
    - Send(method, path, body, token) : JsonElement
    - Verify(data, signature) : bool
  }
  class MembershipOrder <<entity>> {
    + OrderCode : long
    + Status : string
    + ExpiresAt : DateTime
    + PaidAt : DateTime?
  }
  interface IUnitOfWork
  class GameRepository
}
MembershipPanel --> ProgressionRuntime
ModeSelectionMenu --> ProgressionRuntime
ModeSelectionMenu ..> MembershipPanel : opens
ProgressionRuntime ..> MembershipController : HTTPS JSON
MembershipController --> IMembershipService
GameMatchesController --> IMembershipService : Require
MembershipService ..|> IMembershipService
MembershipService --> IPayOsGateway
PayOsGateway ..|> IPayOsGateway
MembershipService --> IUnitOfWork
MembershipService ..> MembershipOrder
IUnitOfWork --> GameRepository
"""

from r4_behaviour import SEQ, ACT, SM  # noqa: E402
D.update(SEQ)
D.update(ACT)
D.update(SM)

if __name__ == "__main__":
    print(write_all(D))
