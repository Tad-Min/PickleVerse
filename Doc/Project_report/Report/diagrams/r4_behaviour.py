"""Behavioural diagrams of Report 4 (UML 2.5): sequence, activity and state machine diagrams.

Rules applied (capstone guideline, section 9):
- Sequence: lifelines are instances (:Class) drawn as boundary, control or entity; every message is an operation of the
  receiving class as named in the class diagrams and the source code, with its arguments; alt/opt/loop fragments carry
  guards in square brackets; execution specifications (activation bars) show when an object is processing.
  Client-to-controller messages travel as HTTPS requests; the routes are listed in Report 4, §3.9.
- Activity: decision nodes carry no text, every outgoing flow has a guard in square brackets and [else] closes the set.
- State machine: transitions are written trigger [guard] / effect; state names are the status values stored.
"""

SEQ_HEAD = "autonumber\nskinparam responseMessageBelowArrow true\nskinparam sequenceParticipant underline\nhide footbox\n"

SEQ = {}

SEQ["r4_seq_register"] = SEQ_HEAD + r"""
actor Visitor
boundary ":AccountScreen" as UI
control ":AccountService" as AS
control ":AuthController" as C
control ":AuthService" as S
entity ":IUserRepository" as UR
entity ":IPlayerProfileRepository" as PR
entity ":IUnitOfWork" as U
control ":SmtpEmailSender" as M
Visitor -> UI : Register()
activate UI
UI -> AS : RegisterAsync(email, password, confirm)
activate AS
AS -> C : Register(request)
activate C
C -> S : RegisterAsync(request)
activate S
S -> UR : GetByEmailAsync(email)
alt [confirmation differs from the password]
  S --> C : error password_confirmation_mismatch
else [email already registered]
  S --> C : error email_exists
else [else]
  S -> U : BeginTransactionAsync()
  S -> UR : PrepareCreate(user)
  S -> PR : AddProfileAsync(profile)
  S -> UR : AddVerificationEmailAsync(verification)
  S -> U : CommitTransactionAsync()
  S -> M : SendVerificationCodeAsync(email, code)
  S --> C : AuthResponse (verification required)
end
deactivate S
C --> AS : ApiResponse
deactivate C
AS --> UI : registered
deactivate AS
UI --> Visitor : Email Code screen
Visitor -> UI : VerifyCode()
UI -> AS : VerifyEmailAsync(email, code)
activate AS
AS -> C : VerifyEmail(request)
activate C
C -> S : VerifyEmailAsync(request)
activate S
S -> UR : LockUserAsync(email)
S -> UR : GetLatestVerificationEmailAsync(user)
alt [code valid, not expired and fewer than 5 failed attempts]
  S -> U : SaveChangesAsync()
  S --> C : true
else [else]
  S --> C : error invalid_code
end
deactivate S
C --> AS : ApiResponse
deactivate C
AS --> UI : result
deactivate AS
deactivate UI
"""

SEQ["r4_seq_login"] = SEQ_HEAD + r"""
actor Visitor
boundary ":AccountScreen" as UI
control ":AccountService" as AS
control ":AuthController" as C
control ":AuthService" as S
entity ":IUserRepository" as UR
entity ":GameRepository" as G
control ":GameController" as GC
Visitor -> UI : SignIn()
activate UI
UI -> AS : LoginAsync(email, password)
activate AS
AS -> C : Login(request)
activate C
C -> S : LoginAsync(request)
activate S
S -> UR : GetByEmailAsync(email)
alt [wrong email or password]
  S --> C : error invalid_credentials
else [email not verified]
  S --> C : error email_not_verified
else [else]
  S -> G : EffectiveRestriction(user)
  S -> S : AuthVersion + 1
  S -> UR : RevokeAllRefreshTokensAsync(user)
  S -> UR : AddRefreshTokenAsync(hash)
  S -> G : RecordLogin(user)
  S --> C : AuthResponse (access 2 h, refresh 14 d, scope game or appeals)
end
deactivate S
C --> AS : ApiResponse
deactivate C
AS --> UI : session
deactivate AS
opt [scope = game]
  UI -> GC : State()
  activate GC
  GC --> UI : GameSnapshot (profile, catalog, inventory)
  deactivate GC
end
UI --> Visitor : Menu, Decisions and Appeals or error message
deactivate UI
loop [signed in, every 3 s]
  AS -> C : Session()
  activate C
  alt [the account signed in on another device]
    C --> AS : 401
    AS -> UI : Clear(MSG84), Login screen
  else [else]
    C --> AS : session active
  end
  deactivate C
end
"""

SEQ["r4_seq_reset_pwd"] = SEQ_HEAD + r"""
actor Visitor
boundary ":AccountScreen" as UI
control ":AccountService" as AS
control ":AuthController" as C
control ":AuthService" as S
entity ":IUserRepository" as UR
control ":PasswordRecoveryMailQueue" as Q
control ":SmtpEmailSender" as M
Visitor -> UI : RequestPasswordReset()
activate UI
UI -> AS : ForgotPasswordAsync(email)
activate AS
AS -> C : ForgotPassword(request)
activate C
C -> S : ForgotPasswordAsync(request)
activate S
S -> UR : GetByEmailAsync(email)
opt [account exists]
  S -> UR : SavePasswordResetAsync(challenge)
  S -> Q : TryEnqueue(email, code)
end
S --> C : same reply for known and unknown email
deactivate S
C --> AS : ApiResponse
deactivate C
AS --> UI : code sent
deactivate AS
Q ->> M : SendPasswordResetCodeAsync(email, code)
Visitor -> UI : RequestPasswordReset()
UI -> AS : ResetPasswordAsync(email, code, password)
activate AS
AS -> C : ResetPassword(request)
activate C
C -> S : ResetPasswordAsync(request)
activate S
S -> UR : GetPasswordResetAsync(user)
alt [code valid and fewer than 5 failed attempts]
  S -> UR : RevokeAllRefreshTokensAsync(user)
  S --> C : success (new BCrypt hash, AuthVersion + 1)
else [else]
  S --> C : error invalid_reset_code
end
deactivate S
C --> AS : ApiResponse
deactivate C
AS --> UI : result
deactivate AS
deactivate UI
"""

SEQ["r4_seq_training_online"] = SEQ_HEAD + r"""
actor Player
boundary ":TrainingDirector" as TD
control ":ProgressionRuntime" as PR
control ":GameTrainingController" as C
control ":OnlineGameService" as S
entity ":GameRepository" as G
Player -> TD : StartLesson(false)
activate TD
TD -> PR : StartTrainingAsync(id, retry)
activate PR
PR -> C : Start(request)
activate C
C -> S : StartTraining(player, request)
activate S
S -> G : CatalogSnapshot()
alt [lesson locked or not published]
  S --> C : error lesson_not_available
else [else]
  S -> G : StartTraining(player, attempt)
  S --> C : GameSnapshot with the active attempt
end
deactivate S
C --> PR : ApiResponse
deactivate C
PR --> TD : attempt
deactivate PR
loop [trial < number of trials]
  Player -> TD : Advance(dt, mask, movement, run, tap)
  TD -> TD : SaveActive(false)
  TD -> PR : SaveTrainingAsync(attempt, false)
  activate PR
  PR -> C : Checkpoint(request)
  PR --> TD : snapshot
  deactivate PR
end
TD -> PR : SaveTrainingAsync(attempt, true)
activate PR
PR -> C : Complete(request)
activate C
C -> S : UpdateTraining(player, request, true)
activate S
S -> G : TrainingAttempt(player, id)
S -> G : UpdateTraining(attempt)
S -> G : RecordLesson(player, lesson, stars)
S -> G : GrantOnce(player, firstPassReward)
S --> C : GameSnapshot (stars recomputed, BR-11)
deactivate S
C --> PR : ApiResponse
deactivate C
PR --> TD : stars and reward
deactivate PR
TD --> Player : result screen
deactivate TD
"""

SEQ["r4_seq_offline_sync"] = SEQ_HEAD + r"""
actor Player
boundary ":TrainingDirector" as TD
control ":ProgressionRuntime" as PR
entity ":OfflineTrainingStore" as OS
control ":GameTrainingController" as C
control ":OnlineGameService" as S
entity ":GameRepository" as G
Player -> PR : TryEnterOffline()
activate PR
PR -> OS : Start(code, retry)
PR --> Player : cached catalog
deactivate PR
Player -> TD : StartLesson(false)
activate TD
TD -> OS : Save(attempt, true)
activate OS
OS --> TD : snapshot (stored on the device)
deactivate OS
deactivate TD
== Network available again ==
PR -> PR : SyncOfflineAsync()
activate PR
PR -> OS : UploadBatch()
PR -> C : SyncTraining(request)
activate C
C -> S : SyncTraining(player, request)
activate S
loop [attempt in batch (at most 50)]
  S -> G : AttemptIdUsed(player, attempt)
  S -> G : LearningEpoch(player)
  alt [new attempt and current epoch]
    S -> G : AddSyncedTraining(attempt)
    S -> G : GrantOnce(player, reward)
    S -> S : receipt APPLIED
  else [attempt already applied]
    S -> S : receipt DUPLICATE
  else [else]
    S -> S : receipt REJECTED with reason
  end
end
S --> C : TrainingSyncResult
deactivate S
C --> PR : ApiResponse
deactivate C
PR -> OS : Accept(result)
deactivate PR
"""

SEQ["r4_seq_bot_match"] = SEQ_HEAD + r"""
actor Player
boundary ":ModeSelectionMenu" as MS
boundary ":BotMatch" as BM
control ":BhTreeBot" as Bot
control ":SinglesRules" as R
control ":ProgressionRuntime" as PR
control ":GameMatchesController" as C
Player -> MS : StartBot()
activate MS
MS -> BM : load BotMatch scene (difficulty, time of day, venue)
deactivate MS
activate BM
BM -> PR : StartBotAsync(kind, requestId)
activate PR
PR -> C : StartBot(request)
PR --> BM : match id
deactivate PR
loop [no player has won two games]
  Player -> BM : movement and shot keys
  BM -> Bot : Step(dt)
  activate Bot
  Bot --> BM : move, run
  deactivate Bot
  BM -> R : Hit(player, contact, feetInKitchen)
  activate R
  R --> BM : fault or rally continues
  deactivate R
  BM -> R : CommitRally()
end
BM -> PR : FinishBotAsync(result, requestId)
activate PR
PR -> C : FinishBot(request)
PR --> BM : snapshot with rewards
deactivate PR
BM --> Player : result screen and Xem phân tích button
deactivate BM
"""

SEQ["r4_seq_ranked"] = SEQ_HEAD + r"""
actor Player
boundary ":MultiplayerApp" as App
control ":GameMatchesController" as C
control ":OnlineGameService" as S
entity ":RedisGameState" as R
entity ":DedicatedLeaseRepository" as L
Player -> App : FindAsync()
activate App
App -> C : Search(request)
activate C
C -> S : BeginRankedSearch(player, request)
activate S
S -> R : Begin(entry)
S --> C : RankedSearchTicket (SEARCHING)
deactivate S
deactivate C
loop [ticket is SEARCHING]
  App -> C : Poll(request)
  activate C
  C -> S : PollRankedSearch(player, request)
  activate S
  S -> R : Candidates(partition)
  alt [two compatible sides inside the Elo window]
    S -> R : Compare(before, after)
    S --> C : MATCHED (roster)
  else [singles and 180 s passed]
    S --> C : FALLBACK
  else [doubles, 180 s passed and client protocol >= 20]
    S -> S : AssignRankedBot(player, request)
    S --> C : MATCHED (roster with ranked bots)
  else [else]
    S --> C : SEARCHING
  end
  deactivate S
  C --> App : ticket
  deactivate C
end
opt [singles fallback]
  App -> C : Bot(request)
  activate C
  C -> S : AssignRankedBot(player, request)
  C --> App : BOT (virtual ranked account)
  deactivate C
end
App -> C : OfferOnline(request)
activate C
C -> S : OfferOnline(player, request)
activate S
S -> L : Reserve(job, limit, seconds)
alt [no free game-server container]
  S --> C : WAITING_CAPACITY
else [else]
  S --> C : ticket with a 10 s ready deadline
end
deactivate S
C --> App : OnlineMatchTicket
deactivate C
Player -> App : AcceptMatchAsync()
alt [all required humans accepted]
  App -> C : StartOnline(request)
  activate C
  C -> S : StartOnline(player, request)
  S -> L : Activate(match, seconds)
  C --> App : authorityMode = dedicated
  deactivate C
else [else]
  App --> Player : offer closed
end
deactivate App
"""

SEQ["r4_seq_dedicated"] = SEQ_HEAD + r"""
boundary ":MultiplayerApp" as Cl
control ":DedicatedController" as DC
control ":DedicatedWorkerController" as WC
control ":DedicatedServerService" as S
entity ":DedicatedLeaseRepository" as L
control ":DedicatedWorker" as W
control ":OnlineGameService" as G
W -> WC : Poll(request)
activate WC
WC -> S : Poll(worker)
activate S
S -> L : Poll(worker, seconds)
S --> WC : DedicatedAllocation (match, roster, lease)
deactivate S
WC --> W : allocation
deactivate WC
activate W
W -> W : StartDedicatedAsync(session, region, roster, port, admission)
W -> WC : Ready(request)
loop [allocation not ready]
  Cl -> DC : Ticket(matchId)
  activate DC
  DC -> S : Ticket(player, match)
  DC --> Cl : starting
  deactivate DC
end
Cl -> DC : Ticket(matchId)
activate DC
DC --> Cl : ready + HMAC connection token
deactivate DC
Cl -> W : JoinDedicatedAsync(session, region, player, token, roster)
W -> WC : AuthorizeAdmission(request)
loop [match running]
  W -> WC : Heartbeat(live status, sessions)
  activate WC
  WC -> S : Renew(lease, sessions)
  WC --> W : HeartbeatReply (stop command, expired sessions)
  deactivate WC
  opt [a session has expired]
    W -> W : ExpireSessions(reply) / disconnect the old device
  end
  W -> WC : ReplayBatch(lease, up to 4 chunks)
end
W -> WC : ReplayBatch(lease, last chunks, complete)
W -> WC : Telemetry(lease, batch)
W -> WC : Complete(results, replayChunkCount)
activate WC
WC -> S : Complete(lease, results, replayChunkCount)
activate S
S -> G : CompleteDedicated(match, results, replayChunkCount)
S -> L : Release(lease, worker)
S --> WC : saved once
deactivate S
WC --> W : ok
deactivate WC
deactivate W
"""

SEQ["r4_seq_surrender"] = SEQ_HEAD + r"""
actor Player
boundary ":SurrenderPanel\n(player)" as PA
control ":NetworkMatch\n(match server)" as S
control ":SurrenderDesk" as D
boundary ":SurrenderPanel\n(partner)" as PB
actor Partner
Player -> PA : Đầu hàng
activate PA
PA -> S : AskToSurrender() : surrender-ask
activate S
S -> D : Ask(player)
activate D
alt [the partner is offline or the team asked less than 15 s ago]
  D --> S : Closed(player, reason)
  S --> PA : surrender-end
else [else]
  D --> S : Opened(player), Opened(partner)
  deactivate D
  S --> PA : surrender-open (20 s)
  S -> PB : surrender-open (20 s)
  activate PB
  deactivate S
  Player -> PA : ĐẦU HÀNG
  PA -> S : AgreeToSurrender() : surrender-agree
  activate S
  S -> D : Agree(player)
  activate D
  D --> S : Opened(both, agreed)
  deactivate D
  S --> PA : surrender-open (waiting for the partner)
  deactivate S
  alt [the partner confirms]
    Partner -> PB : ĐẦU HÀNG
    PB -> S : AgreeToSurrender() : surrender-agree
    activate S
    S -> D : Agree(partner)
    activate D
    D --> S : Surrendered(team, players)
    deactivate D
    S -> S : SurrenderTeam(team, players) / Rules.Forfeit(team), Finish()
    S --> PA : match finished
    S --> PB : match finished
    deactivate S
  else [the partner refuses, the player withdraws, 20 s pass or a connection is lost]
    PB -> S : RefuseToSurrender() : surrender-refuse
    activate S
    S -> D : Refuse(partner) / Tick() / Disconnected(player)
    activate D
    D --> S : Closed(both, reason)
    deactivate D
    S --> PA : surrender-end
    S --> PB : surrender-end
    deactivate S
  end
end
deactivate PB
deactivate PA
"""

SEQ["r4_seq_coaching"] = SEQ_HEAD + r"""
boundary ":ProgressionRuntime" as Src
control ":AiCoachingController" as C
control ":HybridCoachingService" as S
entity ":GameRepository" as G
control ":HybridAiWorker" as W
control ":AI coaching service\n(FastAPI)" as AI
Src -> C : Telemetry(batch)
activate C
C -> S : Ingest(player, batch)
activate S
S -> G : CreateTelemetry(session)
S -> G : AddTelemetryPart(batch, part)
opt [all parts of the session received]
  S -> G : QueueAnalysis(session)
end
S --> C : TelemetryReceipt
deactivate S
C --> Src : ACK
deactivate C
W -> G : ClaimAnalysis(worker, 60 s)
activate W
W -> S : BuildRequest(job)
W -> AI : analyze_session(request)
activate AI
AI --> W : findings, summary, recommendations, gated tier
deactivate AI
alt [analysis returned]
  W -> G : CompleteAnalysis(job, result)
else [else]
  W -> G : RetryAnalysis(job, backoff)
end
deactivate W
Src -> C : Summary(sessionId)
activate C
C -> S : Summary(player, session)
C --> Src : summary and recommended drill
deactivate C
"""

SEQ["r4_seq_purchase"] = SEQ_HEAD + r"""
actor Player
boundary ":ShopScreen" as UI
control ":ProgressionRuntime" as PR
control ":GameController" as C
control ":OnlineGameService" as S
entity ":GameRepository" as G
Player -> UI : RequestPurchase(currency)
activate UI
Player -> UI : ConfirmPurchase()
UI -> PR : CommandAsync("purchase", request)
activate PR
PR -> C : Purchase(request)
activate C
C -> S : Purchase(player, request)
activate S
S -> G : Command(player, requestId)
alt [request already processed]
  S --> C : saved snapshot
else [else]
  S -> G : LockProfile(player)
  S -> G : Price(cosmetic, currency)
  alt [item already owned]
    S --> C : error already_owned
  else [balance below the price]
    S --> C : error insufficient_balance
  else [else]
    S -> G : SaveBalance(player, currency, -price)
    S -> G : AddCosmetic(player, cosmetic)
    S -> G : AddCommand(player, requestId)
    S --> C : updated snapshot
  end
end
deactivate S
C --> PR : ApiResponse
deactivate C
PR --> UI : balance and inventory
deactivate PR
deactivate UI
"""

SEQ["r4_seq_invite"] = SEQ_HEAD + r"""
actor Host
actor Friend
boundary ":MultiplayerApp" as App
control ":RoomInvitationsController" as C
control ":RoomInvitationService" as S
entity ":LobbyRepository" as R
entity ":GameRepository" as G
Host -> App : CreateCustomAsync()
activate App
App -> C : Presence(request)
activate C
C -> S : Touch(player, presence)
S -> R : RegisterRoom(presence, now, ttl)
deactivate C
App -> C : Friends()
activate C
C -> S : List(player)
S -> G : InvitableFriends(player)
C --> App : friends in the lobby
deactivate C
Host -> App : invite friend
App -> C : Send(request)
activate C
C -> S : Send(host, friend, room)
activate S
S -> G : CanInvite(host, friend)
alt [friend not in the lobby, blocked or room full]
  S --> C : error with reason
else [else]
  S -> R : StoreInvitation(host, friend, stored, now, 60 s)
  S --> C : invitation id
end
deactivate S
C --> App : ApiResponse
deactivate C
deactivate App
loop [every 3 s while in the lobby]
  Friend -> C : Presence(request)
  activate C
  C -> S : Touch(friend, presence)
  S -> R : InvitationIds(friend, now)
  C --> Friend : pending invitation
  deactivate C
end
Friend -> C : Respond(id, accept)
activate C
C -> S : Respond(friend, id, accept)
S -> R : Respond(friend, id, "ACCEPTED", now)
C --> Friend : room session and region
deactivate C
"""

SEQ["r4_seq_publish"] = SEQ_HEAD + r"""
actor Administrator as Admin
boundary ":CmsPage" as W
control ":ContentController" as C
control ":ContentService" as S
entity ":GameRepository" as G
Admin -> W : edit a drill of the lesson
activate W
W -> C : SaveDrill(id, drillId, draft, expectedVersion)
activate C
C -> S : SaveDrill(lessonId, drillId, request)
activate S
S -> G : Lock(lesson)
S -> G : ContentLesson(id)
alt [stored version differs from expectedVersion]
  S --> C : error 409 reload_before_saving
else [else]
  S --> C : AdminLessonContent (version + 1, HasChanges)
end
deactivate S
C --> W : ApiResponse
deactivate C
Admin -> W : Publish
W -> C : Publish(id, request)
activate C
C -> S : Publish(lessonId, request, true)
activate S
S -> G : ContentLessons()
alt [the lesson has no drill or a prerequisite is not published]
  S --> C : error 400
else [else]
  S -> S : copy DraftConfig to Config of every drill
  S -> G : AddContent(catalogSnapshot)
  S --> C : AdminLessonContent (PUBLISHED)
end
deactivate S
C --> W : ApiResponse
deactivate C
deactivate W
note over G : the next catalogue request of a game client\nreturns the new catalogue version
"""

SEQ["r4_seq_membership"] = SEQ_HEAD + r"""
actor Player
boundary ":MembershipPanel" as UI
control ":MembershipController" as C
control ":MembershipService" as S
entity ":GameRepository" as G
control ":PayOsGateway" as P
actor "PayOS" as Ext
Player -> UI : Buy()
activate UI
UI -> C : Checkout()
activate C
C -> S : Checkout(player)
activate S
S -> G : Lock(membership-checkout, player)
alt [the player is a member]
  S --> C : offer (active)
else [a pending order is still valid]
  S --> C : offer (same checkout URL)
else [else]
  S -> P : Create(orderCode, 49000, expiresAt)
  P -> Ext : signed payment request
  Ext --> P : paymentLinkId, checkoutUrl (signed)
  S -> G : AddMembershipOrder(PENDING)
  S --> C : offer (checkout URL, 15 min)
end
deactivate S
C --> UI : MembershipOffer
deactivate C
UI -> Ext : open the payment page in the browser
deactivate UI
Player -> Ext : pay
Ext -> C : Webhook(body)
activate C
C -> S : ReceiveWebhook(body)
activate S
S -> P : VerifyWebhook(body)
alt [signature invalid]
  S --> C : error 403
else [order known and not paid]
  S -> P : Read(orderCode)
  P -> Ext : order query
  Ext --> P : status, amount, amountPaid (signed)
  S -> G : Lock(membership-checkout, player)
  alt [PAID, amount matches, nothing remaining]
    S -> G : SaveBalance(+3000 Elite Token), order PAID
  else [partial payment]
    S --> C : error 409
  else [else]
    S -> G : order CANCELLED or EXPIRED
  end
end
deactivate S
C --> Ext : 200
deactivate C
Player -> UI : return to the game
UI -> C : Refresh()
activate C
C --> UI : MembershipOffer (active)
deactivate C
note over UI : Refresh also reads the order when no webhook has arrived\n(at most every 10 s per order)
"""

SEQ["r4_seq_replay_launch"] = SEQ_HEAD + r"""
actor Administrator as Admin
boundary ":ReplayLaunchDialog" as W
control ":ReplayLaunchController" as C
control ":ReplayLaunchService" as S
entity ":ReplayLaunchRepository" as R
boundary ":WebReplayLauncher" as Game
Admin -> W : Xem replay
activate W
W -> C : Issue(match)
activate C
C -> S : Issue(admin, authVersion, match)
activate S
S -> R : Issue(code, access, 2 min)
S --> C : pickleverse://replay/{match}?code
deactivate S
C --> W : ReplayLaunchLink
deactivate C
W -> Game : open the link (confirmed in the browser)
deactivate W
activate Game
Game -> C : Redeem(code)
activate C
C -> S : Redeem(code)
activate S
S -> R : Exchange(code, view token, 2 h)
alt [expired or already used]
  S --> C : error 401 (MSG85)
else [else]
  S --> C : ReplayLaunchView (grant, metadata)
end
deactivate S
C --> Game : view token
deactivate C
loop [each chunk]
  Game -> C : Chunk(match, index)
  activate C
  C -> S : Chunk(token, match, index)
  activate S
  S -> S : Guard(admin active, same session)
  S --> C : ReplayChunkUpload
  deactivate S
  C --> Game : chunk
  deactivate C
end
Game --> Admin : Replay Viewer
deactivate Game
"""

SEQ["r4_seq_moderation"] = SEQ_HEAD + r"""
actor Player
actor "Restricted Player" as RP
actor Administrator as Admin
control ":MatchesController" as MC
control ":MatchService" as MS
entity ":IMatchRepository" as MR
control ":AdminController" as AC
control ":ModerationController" as PC
control ":AdminModerationController" as AMC
control ":ModerationService" as S
entity ":GameRepository" as G
Player -> MC : ReportPlayer(request)
activate MC
MC -> MS : ReportPlayerAsync(reporter, request)
activate MS
MS -> MR : GetMatchByIdAsync(match)
MS -> MR : AddReportAsync(report)
MS --> MC : true (report PENDING)
deactivate MS
MC --> Player : ApiResponse
deactivate MC
Admin -> AC : ReviewReport(request)
activate AC
AC -> S : ReviewReport(actor, request)
activate S
S -> G : LockReport(id)
opt [decision ACCEPTED with a sanction]
  S -> G : AddSanction(sanction)
  S -> G : AddAudit(actor, decision)
end
S --> AC : saved in one transaction
deactivate S
deactivate AC
RP -> PC : Decisions()
activate PC
PC --> RP : sanction and reason
deactivate PC
RP -> PC : Appeal(request)
activate PC
PC -> S : SubmitAppeal(player, request)
activate S
S -> G : AppealForSanction(sanction)
S -> G : AddAppeal(appeal)
S --> PC : SanctionAppeal (PENDING)
deactivate S
deactivate PC
Admin -> AMC : Review(id, request)
activate AMC
AMC -> S : ReviewAppeal(actor, id, request)
activate S
S -> G : Appeal(id)
alt [appeal ACCEPTED]
  S -> S : Revoke(actor, sanction, reason)
else [else]
  S -> G : AddAudit(actor, declined)
end
S --> AMC : SanctionAppeal
deactivate S
deactivate AMC
"""

SEQ["r4_seq_stop"] = SEQ_HEAD + r"""
actor Administrator as Admin
boundary ":MatchesPage" as W
control ":AdminModerationController" as C
control ":ModerationService" as S
entity ":GameRepository" as G
control ":DedicatedWorkerController" as WC
control ":DedicatedWorker" as DW
Admin -> W : stop match with a reason
activate W
W -> C : Stop(match, request)
activate C
C -> S : StopMatch(actor, match, reason)
activate S
S -> G : RequestStop(match, reason)
S --> C : DedicatedOperationRecord (not acknowledged)
deactivate S
deactivate C
DW -> WC : Heartbeat(request)
activate WC
WC --> DW : stop command
deactivate WC
activate DW
DW -> DW : end match as INTERRUPTED
DW -> WC : Acknowledge(request)
activate WC
WC -> G : AcknowledgeOperation(operation)
deactivate WC
deactivate DW
W -> C : StopOperation(match)
activate C
C --> W : acknowledged (no Elo change)
deactivate C
deactivate W
"""

SEQ["r4_seq_reset"] = SEQ_HEAD + r"""
actor Administrator as Admin
boundary ":UsersPage" as W
control ":ProgressResetController" as C
control ":ProgressResetService" as S
entity ":GameRepository" as G
Admin -> W : choose groups (LEARNING, RANKING, MISSIONS, ECONOMY)
activate W
W -> C : Preview(player, request)
activate C
C -> S : Preview(player, groups)
activate S
S -> G : ResetPreview(player, groups)
S --> C : ResetPreview with the profile revision
deactivate S
C --> W : impact preview
deactivate C
Admin -> W : confirm with a reason
W -> C : Reset(player, request)
activate C
C -> S : Execute(actor, player, request)
activate S
S -> G : Command(player, requestId)
alt [profile revision changed since the preview]
  S --> C : error 409 preview_again
else [else]
  S -> G : ResetLearning(player)
  S -> G : ResetMissions(player)
  S -> G : ResetCosmetics(player)
  S -> G : AddAudit(actor, reset)
  S --> C : ResetReceipt (same receipt on retry)
end
deactivate S
C --> W : ApiResponse
deactivate C
deactivate W
"""

SEQ["r4_seq_social_post"] = SEQ_HEAD + r"""
actor Player
boundary ":SocialLobbyMenu" as UI
control ":SocialContentController" as C
control ":SocialMediaService" as M
control ":SocialContentService" as S
entity ":GameRepository" as G
entity ":StorageService" as S3
opt [video attached (Unity Editor and Windows build)]
  UI -> C : Upload()
  activate C
  C -> M : Upload(actor, stream, length, token)
  activate M
  alt [file too large or four uploads running]
    M --> C : error 413 or 429 (MSG68, MSG69)
  else [not MP4 H.264/AAC or longer than 15 min]
    M --> C : error 400 (MSG67)
  else [else]
    M -> G : AddSocialMedia(draft)
    M -> S3 : WriteVideo(key, input, cancellation)
    M --> C : SocialMediaReceipt (mediaId)
  end
  deactivate M
  C --> UI : ApiResponse
  deactivate C
end
Player -> UI : text, audience, replay; Đăng bài
activate UI
UI -> C : Create(input)
activate C
C -> S : Create(actor, input)
activate S
S -> G : LockPlayers(actor)
S -> G : MediaUsed(media)
S -> G : PostExclusionTargetsExist(actor, exclusions)
alt [invalid audience, replay or media]
  S --> C : error 400 (MSG66)
else [else]
  S -> G : AddSocialPost(post)
  S -> G : SetPostExclusions(post, exclusions)
  S --> C : SocialPostItem
end
deactivate S
C --> UI : ApiResponse
deactivate C
deactivate UI
"""

SEQ["r4_seq_message"] = SEQ_HEAD + r"""
actor "Player A" as A
boundary ":SocialLobbyMenu" as UI
control ":SocialContentController" as C
control ":SocialHub" as H
control ":SocialDirectService" as S
entity ":GameRepository" as G
entity ":SocialPresenceRepository" as R
actor "Player B" as B
group Direct message
  A -> UI : write message, send
  activate UI
  UI -> C : Send(input)
  activate C
  C -> S : Send(actor, input)
  activate S
  S -> G : LockPlayers(a, b)
  S -> G : MessageByClientId(a, clientMessageId)
  alt [clientMessageId already stored]
    S --> C : the first message (no duplicate)
  else [not friends, blocked or recipient inactive]
    S --> C : error 403 (MSG72)
  else [else]
    S -> G : SocialConversationFor(a, b)
    S -> G : AddSocialMessage(message)
    S -> H : MessageChanged(a, b)
    H ->> B : inbox refreshed (if online)
    S --> C : SocialMessageItem
  end
  deactivate S
  C --> UI : ApiResponse
  deactivate C
  deactivate UI
end
group Whisper (both players in the lobby)
  UI -> H : Whisper(recipient, body)
  activate H
  H -> R : Connections(recipient)
  H -> S : Whisper(sender, recipient, body)
  activate S
  alt [blocked or recipient not verified]
    S --> H : HubException (MSG73)
  else [recipient has no live connection]
    S --> H : HubException (MSG74)
  else [else]
    S --> H : allowed
    H ->> B : WhisperReceived (not stored)
  end
  deactivate S
  deactivate H
end
"""

# ---------------------------------------------------------------- activity diagrams
ACT = {}

ACT["r4_act_w01"] = r"""
|Player|
start
:Register and verify email;
:Sign in;
|Game client|
if () then ([first sign-in])
  :Show experience choice;
  |Player|
  if () then ([beginner])
    |Game client|
    :Open Tutorial;
    |Player|
    :Complete tutorial steps;
  else ([else])
  endif
else ([else])
endif
|Player|
:Select unlocked drill;
|Game client|
if () then ([network available])
  |Web API|
  :Check lesson is published and unlocked;
  :Create training attempt;
else ([else])
  |Game client|
  :Start attempt from cached catalog;
endif
|Player|
:Play all trials;
|Game client|
:Evaluate contact, rules and target;
if () then ([online])
  |Web API|
  :Recompute stars and grant reward once;
else ([else])
  |Game client|
  :Save completed run on device;
  :Upload run after reconnect;
  |Web API|
  if () then ([attempt already applied])
    :Return receipt DUPLICATE;
  else ([else])
    :Validate epoch and apply once;
  endif
endif
|Game client|
:Show stars, reward and tips;
stop
"""

ACT["r4_act_w02"] = r"""
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
  if () then ([fault])
    :Show the referee call\n(red for the side at fault, green for the other);
  else ([else])
  endif
  :Commit point;
repeat while () is ([no player has won two games]) not ([else])
|Web API|
:Save bot result and rewards;
|Game client|
:Upload session telemetry\n(after the result has been saved);
|AI coaching service|
:Analyse session with rules;
:Return findings, summary and drill recommendation;
|Game client|
if () then ([analysis ready within 90 s])
  :Show the coaching panel;
else ([else])
  :Offer "THỬ LẠI";
endif
|Player|
:Read the coaching summary\nand open a recommended drill;
stop
"""

ACT["r4_act_w03"] = r"""
|Player|
start
:Choose online play or a private room;
|Web API|
if () then ([every human player is a member])
else ([else])
  :Refuse with MSG79;
  |Player|
  :Open the membership panel (FR-36);
  stop
endif
|Player|
if () then ([private room])
  :Create room, invite friend\n(and swap positions by agreement);
elseif () then ([ranked doubles])
  :Search alone or with a party of two;
  |Web API|
  if () then ([two sides of humans within 180 s])
    :Build ranked 2v2 roster;
  else ([else])
    :Fill free seats with ranked bots;
  endif
else ([else])
  |Player|
  :Find ranked singles match;
  |Web API|
  if () then ([opponent found within 180 s])
    :Pair players by Elo;
  else ([else])
    :Assign ranked bot;
  endif
endif
|Player|
:Watch the match-found intro\nand accept the ready check (10 s);
|Web API|
if () then ([all required humans accepted])
  :Create match record (PLAYING);
  :Reserve a free game-server container\nand issue tickets;
else ([else])
  :Close offer;
  stop
endif
|Dedicated server|
:Verify ticket and admit players;
:Simulate rallies authoritatively\nand upload the replay in batches;
if () then ([stopped by an administrator or lease lost])
  :End match as INTERRUPTED\n(no Elo, no reward);
elseif () then ([a player left or did not return within 30 s,\nor every human of a team confirmed the surrender])
  :End match as a forfeit\n(the other team wins, BR-28, BR-66);
  :Seal the replay (at most 60 s)\nand send the final result;
else ([else])
  :Seal the replay (at most 60 s)\nand send the final result;
endif
|Web API|
:Save scores, Elo and rewards once\n(with or without a replay);
|Player|
:View result and history;
stop
"""

ACT["r4_act_w04"] = r"""
|Player|
start
:Report a player from match history;
|Web API|
:Validate reporter and target;
:Store report as PENDING;
|Administrator|
:Review evidence and replay;
if () then ([report accepted])
  :Apply ban or suspension with reason;
  |Web API|
  :Restrict account and revoke sessions;
  |Restricted Player|
  :Sign in and read decision;
  if () then ([appeal submitted])
    :Write appeal statement;
    |Administrator|
    :Review appeal;
    if () then ([appeal accepted])
      |Web API|
      :Revoke sanction;
    else ([else])
      |Web API|
      :Keep sanction and record reason;
    endif
  else ([else])
  endif
else ([else])
  |Web API|
  :Close report as DECLINED;
endif
stop
"""

ACT["r4_act_content"] = r"""
|Administrator|
start
:Open lesson in CMS;
:Edit the drafts of its drills\n(target, reward, prerequisites);
|Web API|
if () then ([expected version matches])
  :Save draft and audit;
else ([else])
  :Return 409 conflict;
  |Administrator|
  :Reload draft;
  stop
endif
|Administrator|
:Publish lesson;
|Web API|
if () then ([at least one drill and all prerequisites published])
  :Create new catalog snapshot;
else ([else])
  :Reject publish;
  stop
endif
|Game client|
:Load new catalog version;
:Show lesson to eligible players;
stop
"""

# ---------------------------------------------------------------- state machines
SM = {}

SM["r4_sm_email"] = r"""
[*] --> Unverified : register / sendCode()
Unverified --> Unverified : submitCode [code invalid and attempts < 5] / countFailure()
Unverified --> Unverified : resendCode [60 s cooldown passed] / sendCode()
Unverified --> Verified : submitCode [code valid and not expired]
Unverified --> Verified : verifyManually [administrator]
Verified --> [*]
"""

SM["r4_sm_account"] = r"""
[*] --> ACTIVE : register
ACTIVE --> SUSPENDED : applySanction [type = suspension] / revokeSessions()
ACTIVE --> BANNED : applySanction [type = ban] / revokeSessions()
SUSPENDED --> BANNED : applySanction [type = ban]
SUSPENDED --> ACTIVE : expire [suspension end reached]
SUSPENDED --> ACTIVE : revokeSanction
SUSPENDED --> ACTIVE : reviewAppeal [accepted]
BANNED --> ACTIVE : revokeSanction
BANNED --> ACTIVE : reviewAppeal [accepted]
"""

SM["r4_sm_training"] = r"""
[*] --> Active : start [lesson unlocked and published] / createAttempt()
Active --> Active : checkpoint [trial played]
Active --> Abandoned : start [another attempt started]
Active --> Completed : complete [last trial played] / recomputeStars()
state Completed {
  [*] --> Passed : [successes >= required] / grantFirstPassRewardOnce()
  [*] --> Failed : [else]
}
Completed --> [*]
Abandoned --> [*]
"""

SM["r4_sm_offline"] = r"""
[*] --> SavedOnDevice : save [drill finished offline]
SavedOnDevice --> Uploading : syncOffline [network available]
Uploading --> APPLIED : syncResult [first valid attempt] / applyOnce()
Uploading --> DUPLICATE : syncResult [attempt already applied]
Uploading --> REJECTED : syncResult [stale epoch or invalid data]
Uploading --> SavedOnDevice : syncFailed / retryLater()
APPLIED --> Cleared : accept(result)
DUPLICATE --> Cleared : accept(result)
REJECTED --> KeptWithReason : accept(result) / showReason()
Cleared --> [*]
KeptWithReason --> [*]
"""

SM["r4_sm_botmatch"] = r"""
[*] --> Intro
Intro --> Cinematic : sceneLoaded
Cinematic --> PreServe : introEnded
Cinematic --> PreServe : skip
PreServe --> Rally : serve [legal serve]
PreServe --> Settling : serve [service fault]
PreServe --> Settling : serveTimer [10 s passed]
Rally --> Settling : ballDead [fault, out or double bounce]
Settling --> BetweenPoints : commitRally [game not won]
Settling --> BetweenGames : commitRally [game won and match not won]
Settling --> MatchOver : commitRally [second game won] / saveResult()
BetweenPoints --> PreServe : nextPoint
BetweenGames --> PreServe : nextGame / changeEnds()
MatchOver --> Cinematic : playAgain
MatchOver --> [*] : leave
"""

SM["r4_sm_search"] = r"""
[*] --> SEARCHING : beginRankedSearch / enqueueTicket()
SEARCHING --> MATCHED : poll [compatible sides found]
SEARCHING --> BOT : assignRankedBot [singles and 180 s passed]
SEARCHING --> MATCHED : poll [doubles, 180 s passed] / fillSeatsWithBots()
SEARCHING --> CANCELLED : cancelSearch
SEARCHING --> TIMEOUT : poll [ticket expired]
MATCHED --> [*]
BOT --> [*]
CANCELLED --> [*]
TIMEOUT --> [*]
"""

SM["r4_sm_room"] = r"""
state "assigning-bot" as assigning_bot
[*] --> searching : createSession
searching --> searching : hostLeft [a player remains] / promoteNextHost()
searching --> assigning_bot : beginRankedFallback [no opponent after 180 s]
assigning_bot --> searching : botAssignmentFailed
assigning_bot --> ready : completeRankedFallback(bot)
searching --> ready : playerJoined [all seats connected]
ready --> loading : accept [all humans accepted]
ready --> rejected : decline
ready --> rejected : readyDeadline [4 s intro + 10 s passed]
loading --> playing : courtLoaded
loading --> rejected : playerLeft
playing --> interrupted : authorityLost
playing --> closed : matchFinished
rejected --> [*]
interrupted --> [*]
closed --> [*]
"""

SM["r4_sm_match"] = r"""
[*] --> PREPARING : offerOnline / addMatch()
PREPARING --> PLAYING : startOnline [all required players accepted]
PREPARING --> CANCELLED : decline
PREPARING --> CANCELLED : startOnline [start failed]
PLAYING --> FINISHED : complete [trusted result] / saveScoresAndElo()
PLAYING --> FINISHED : complete [player forfeited or team surrendered] / saveForfeitLoss()
PLAYING --> FINISHED : stop [administrator stop or lease lost] / markInterrupted()
FINISHED --> FINISHED : complete [result already saved] / ignore()
FINISHED --> [*]
CANCELLED --> [*]
"""

SM["r4_sm_lease"] = r"""
state "waiting (WAITING_CAPACITY)" as waiting
[*] --> reserved : offerOnline [idle container and below MaxConcurrentMatches]
[*] --> waiting : offerOnline [all containers busy]
waiting --> reserved : containerFreed [first in the waiting queue]
waiting --> [*] : waitExpired [15 s passed]
reserved --> queued : startOnline [all humans accepted]
reserved --> Released : cancelOffer
reserved --> Released : workerLost
queued --> starting : poll / claimJob()
starting --> ready : ready
starting --> Released : leaseExpired
ready --> finishing : complete(result)
ready --> Released : leaseExpired
ready --> Released : acknowledgeStop
finishing --> Released : resultSaved / release()
Released --> [*]
"""

SM["r4_sm_lesson"] = r"""
[*] --> DRAFT : createLesson
DRAFT --> DRAFT : save [expected version matches] / incrementVersion()
DRAFT --> PUBLISHED : publish [prerequisites published] / createCatalogSnapshot()
PUBLISHED --> UNPUBLISHED : unpublish [no published dependants]
UNPUBLISHED --> PUBLISHED : publish [prerequisites published]
PUBLISHED --> PUBLISHED : save / keepLiveCopy()
"""

SM["r4_sm_report"] = r"""
[*] --> PENDING : reportPlayer [reporter took part in the match]
PENDING --> CHECKING : startReview
PENDING --> ACCEPTED : reviewReport [accepted] / applySanction()
PENDING --> DECLINED : reviewReport [declined]
CHECKING --> ACCEPTED : reviewReport [accepted] / applySanction()
CHECKING --> DECLINED : reviewReport [declined]
ACCEPTED --> [*]
DECLINED --> [*]
"""

SM["r4_sm_appeal"] = r"""
[*] --> PENDING : submitAppeal [one appeal per sanction]
PENDING --> CHECKING : startReview
PENDING --> ACCEPTED : reviewAppeal [accepted] / revokeSanction()
PENDING --> DECLINED : reviewAppeal [declined] / keepSanction()
CHECKING --> ACCEPTED : reviewAppeal [accepted] / revokeSanction()
CHECKING --> DECLINED : reviewAppeal [declined] / keepSanction()
ACCEPTED --> [*]
DECLINED --> [*]
"""

SM["r4_sm_aijob"] = r"""
[*] --> pending : queueAnalysis [all telemetry parts received]
pending --> processing : claimAnalysis / setLease(60 s)
processing --> completed : analysisReturned / completeAnalysis()
processing --> pending : analysisFailed [attempts < 10] / retryWithBackoff()
processing --> pending : leaseExpired
processing --> failed : analysisFailed [attempts = 10]
completed --> [*]
failed --> [*]
"""

SM["r4_sm_friend"] = r"""
[*] --> PENDING : sendFriendRequest [not blocked and not friends]
PENDING --> ACCEPTED : respond [accepted] / addFriendship()
PENDING --> DECLINED : respond [declined]
ACCEPTED --> [*]
DECLINED --> [*]
"""

SM["r4_sm_media"] = r"""
state "Draft (Ready = false)" as Draft
state "Ready" as Ready
state "Attached to a post" as Attached
state "Deleted" as Deleted
[*] --> Draft : upload [ffprobe accepts the file] / addSocialMedia()
Draft --> Ready : stored [object written]
Draft --> Deleted : storeFailed
Ready --> Attached : createPost [video attached]
Ready --> Removed : cleanup [not attached within 24 h]
Attached --> Deleted : deletePost
Attached --> Deleted : editPost [video replaced]
Deleted --> Removed : cleanup / deleteObject()
Removed --> [*]
"""

SM["r4_sm_membership"] = r"""
[*] --> PENDING : checkout [no valid pending order] / createPaymentLink()
PENDING --> PENDING : checkout [link valid] / returnSameLink()
PENDING --> PAID : reconcile [PAID, amount matches, nothing remaining] / creditEliteToken()
PENDING --> CANCELLED : reconcile [cancelled at PayOS, nothing paid]
PENDING --> EXPIRED : reconcile [expired at PayOS, nothing paid]
CANCELLED --> PAID : reconcile [paid late in full] / creditEliteToken()
EXPIRED --> PAID : reconcile [paid late in full] / creditEliteToken()
PAID --> [*]
CANCELLED --> [*]
EXPIRED --> [*]
"""


def _fragment_guards(body):
    """PlantUML prints the guard of alt/else/opt/loop in square brackets itself; written brackets would become links."""
    import re
    return re.sub(r"^(\s*(?:alt|else|opt|loop)) \[(.*)\]\s*$", r"\1 \2", body, flags=re.M)


SEQ = {k: _fragment_guards(v) for k, v in SEQ.items()}
