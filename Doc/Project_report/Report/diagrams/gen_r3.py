"""PlantUML sources for Report 3 (SRS): context, use cases, screen flows, conceptual ERD."""
from diagram_lib import write_all

D = {}

D["r3_context"] = r"""
left to right direction
skinparam rectangle {
  RoundCorner 12
}
actor "Player\n(PC / Android)" as Player
actor "Administrator" as Admin
rectangle "<b>PickleVerse System</b>\n\nUnity game client, Web API,\nadmin web, AI coaching service,\ndedicated game server,\nPostgreSQL and Redis" as SYS #F5F9FC
rectangle "Photon Cloud\n(Fusion session and relay)" as PH
rectangle "Gmail SMTP\n(email delivery)" as SMTP
rectangle "S3-compatible\nobject storage" as S3

Player --> SYS : account data, game input,\ntraining runs, reports, appeals
SYS --> Player : game state, rewards, rankings,\ncoaching tips, replays
Admin --> SYS : user, content and\nmoderation commands
SYS --> Admin : dashboards, live matches,\nreports and appeals
SYS --> PH : session registration,\ninput and state packets
PH --> SYS : relayed packets,\nconnection events
SYS --> SMTP : verification and\npassword-reset codes
SYS --> S3 : replay manifests and chunks
S3 --> SYS : stored replay data
"""

D["r3_uc_overview"] = r"""
left to right direction
actor Visitor
actor Player
actor "Restricted Player" as RP
actor Administrator as Admin
actor "Email Service" as ES
actor "Photon Cloud" as PC
Player -up-|> Visitor
RP -up-|> Visitor
rectangle "PickleVerse System" {
  usecase "P1 Account and Onboarding\n(UC-01 to UC-09)" as P1
  usecase "P2 Learning and Coaching\n(UC-10 to UC-18)" as P2
  usecase "P3 Online Match\n(UC-19 to UC-27)" as P3
  usecase "P4 Progress, Economy and Social\n(UC-28 to UC-44)" as P4
  usecase "P5 Account Restriction\n(UC-45 to UC-46)" as P5
  usecase "P6 Admin: Users and Moderation\n(UC-47 to UC-55)" as P6
  usecase "P7 Admin: Content\n(UC-56 to UC-61)" as P7
  usecase "P8 Admin: Monitoring and Analytics\n(UC-62 to UC-68)" as P8
}
Visitor -- P1
Player -- P2
Player -- P3
Player -- P4
RP -- P5
Admin -- P6
Admin -- P7
Admin -- P8
P1 -- ES
P3 -- PC
"""

D["r3_uc_p1_account"] = r"""
left to right direction
actor Visitor
actor Player
actor "Email Service" as ES
Player -up-|> Visitor
rectangle "P1 Account and Onboarding" {
  usecase "UC-01 Register Account" as UC01
  usecase "UC-02 Verify Email" as UC02
  usecase "UC-03 Sign In" as UC03
  usecase "UC-04 Reset Password" as UC04
  usecase "UC-05 Sign Out" as UC05
  usecase "UC-06 Choose Experience Level" as UC06
  usecase "UC-07 Update Player Name" as UC07
  usecase "UC-08 Select Character" as UC08
  usecase "UC-09 Configure Game Settings" as UC09
}
Visitor -- UC01
Visitor -- UC02
Visitor -- UC03
Visitor -- UC04
Player -- UC05
Player -- UC06
Player -- UC07
Player -- UC08
Player -- UC09
UC01 ..> UC02 : <<include>>
UC02 -- ES
UC04 -- ES
"""

D["r3_uc_p2_learning"] = r"""
left to right direction
actor Player
rectangle "P2 Learning and Coaching" {
  usecase "UC-10 Complete Tutorial" as UC10
  usecase "UC-11 Browse Lessons" as UC11
  usecase "UC-12 Practise Drill" as UC12
  usecase "UC-13 Practise Drill Offline" as UC13
  usecase "UC-14 Synchronise Offline Results" as UC14
  usecase "UC-15 View Training Progress" as UC15
  usecase "UC-16 Play Bot Match" as UC16
  usecase "UC-17 View Coaching Summary" as UC17
  usecase "UC-18 View Drill Recommendation" as UC18
}
Player -- UC10
Player -- UC11
Player -- UC12
Player -- UC14
Player -- UC15
Player -- UC16
Player -- UC17
UC12 ..> UC11 : <<include>>
UC13 .> UC12 : <<extend>>
UC18 .> UC17 : <<extend>>
"""

D["r3_uc_p3_online"] = r"""
left to right direction
actor Player
actor "Photon Cloud" as PC
rectangle "P3 Online Match" {
  usecase "UC-19 Find Online Match" as UC19
  usecase "UC-20 Confirm Match Readiness" as UC20
  usecase "UC-21 Create Private Room" as UC21
  usecase "UC-22 Join Room by Code" as UC22
  usecase "UC-23 Invite Friend to Room" as UC23
  usecase "UC-24 Respond to Room Invitation" as UC24
  usecase "UC-25 Play Online Match" as UC25
  usecase "UC-26 Reconnect to Match" as UC26
  usecase "UC-27 Leave Match" as UC27
}
Player -- UC19
Player -- UC21
Player -- UC22
Player -- UC24
Player -- UC25
UC19 ..> UC20 : <<include>>
UC23 .> UC21 : <<extend>>
UC26 .> UC25 : <<extend>>
UC27 .> UC25 : <<extend>>
UC21 -- PC
UC25 -- PC
"""

D["r3_uc_p4a_social"] = r"""
left to right direction
actor Player
rectangle "P4 Progress, Economy and Social (part 1: history, ranking and friends)" {
  usecase "UC-28 View Match History" as UC28
  usecase "UC-29 Watch Match Replay" as UC29
  usecase "UC-30 Report Player" as UC30
  usecase "UC-31 View Leaderboard" as UC31
  usecase "UC-32 Send Friend Request" as UC32
  usecase "UC-33 Respond to Friend Request" as UC33
  usecase "UC-34 Remove Friend" as UC34
  usecase "UC-35 Block Player" as UC35
  usecase "UC-36 Unblock Player" as UC36
}
Player -- UC28
Player -- UC31
Player -- UC32
Player -- UC33
Player -- UC34
Player -- UC35
Player -- UC36
UC29 .> UC28 : <<extend>>
UC30 .> UC28 : <<extend>>
"""

D["r3_uc_p4b_economy"] = r"""
left to right direction
actor Player
rectangle "P4 Progress, Economy and Social (part 2: missions, achievements and shop)" {
  usecase "UC-37 View Missions and Streak" as UC37
  usecase "UC-38 Claim Mission Reward" as UC38
  usecase "UC-39 View Achievements" as UC39
  usecase "UC-40 Claim Achievement Reward" as UC40
  usecase "UC-41 Browse Shop" as UC41
  usecase "UC-42 Purchase Cosmetic" as UC42
  usecase "UC-43 View Collection" as UC43
  usecase "UC-44 Equip Cosmetic" as UC44
}
Player -- UC37
Player -- UC39
Player -- UC41
Player -- UC43
UC38 .> UC37 : <<extend>>
UC40 .> UC39 : <<extend>>
UC42 .> UC41 : <<extend>>
UC44 .> UC43 : <<extend>>
"""

D["r3_uc_p5_restricted"] = r"""
left to right direction
actor "Restricted Player" as RP
rectangle "P5 Account Restriction" {
  usecase "UC-45 View Sanction Decision" as UC45
  usecase "UC-46 Submit Appeal" as UC46
}
RP -- UC45
UC46 .> UC45 : <<extend>>
"""

D["r3_uc_p6_admin_users"] = r"""
left to right direction
actor Administrator as Admin
rectangle "P6 Admin: Users and Moderation" {
  usecase "UC-47 Sign In to Admin Portal" as UC47
  usecase "UC-48 Search User Accounts" as UC48
  usecase "UC-49 Assign Role" as UC49
  usecase "UC-50 Verify Email Manually" as UC50
  usecase "UC-51 Apply Sanction" as UC51
  usecase "UC-52 Revoke Sanction" as UC52
  usecase "UC-53 Reset Player Progress" as UC53
  usecase "UC-54 Review Player Report" as UC54
  usecase "UC-55 Review Appeal" as UC55
}
Admin -- UC47
Admin -- UC48
Admin -- UC51
Admin -- UC52
Admin -- UC53
Admin -- UC54
Admin -- UC55
UC49 .> UC48 : <<extend>>
UC50 .> UC48 : <<extend>>
UC51 .> UC54 : <<extend>>
"""

D["r3_uc_p7_admin_content"] = r"""
left to right direction
actor Administrator as Admin
rectangle "P7 Admin: Content" {
  usecase "UC-56 Edit Lesson Draft" as UC56
  usecase "UC-57 Publish Lesson" as UC57
  usecase "UC-58 Unpublish Lesson" as UC58
  usecase "UC-59 Manage Technique References" as UC59
  usecase "UC-60 Add Cosmetic Item" as UC60
  usecase "UC-61 Add Mission" as UC61
}
Admin -- UC56
Admin -- UC57
Admin -- UC58
Admin -- UC59
Admin -- UC60
Admin -- UC61
"""

D["r3_uc_p8_admin_monitor"] = r"""
left to right direction
actor Administrator as Admin
rectangle "P8 Admin: Monitoring and Analytics" {
  usecase "UC-62 Monitor Live Matches" as UC62
  usecase "UC-63 Stop Live Match" as UC63
  usecase "UC-64 View Match Records" as UC64
  usecase "UC-65 View Dashboard Summary" as UC65
  usecase "UC-66 View Analytics Report" as UC66
  usecase "UC-67 Review Coaching Session" as UC67
  usecase "UC-68 Export Labelled Dataset" as UC68
}
Admin -- UC62
Admin -- UC64
Admin -- UC65
Admin -- UC66
Admin -- UC67
UC63 .> UC62 : <<extend>>
UC68 .> UC67 : <<extend>>
"""

D["r3_screenflow_game"] = r"""
hide empty description
skinparam state {
  BackgroundColor<<popup>> #FFFFFF
  BorderColor<<popup>> #777777
}
state "Login" as Login
state "Register" as Register
state "Email Code (OTP)" as OTP
state "Forgot / Reset Password" as Forgot
state "Loading" as Loading
state "Menu (Lobby)" as Menu
state "Experience Choice\n[popup]" as Choice <<popup>>
state "Tutorial" as Tutorial
state "Mode Selection" as Modes
state "Bot Match Setup" as BotSetup
state "Bot Match" as BotMatch
state "Training Academy" as Training
state "Drill Result\n[popup]" as DrillResult <<popup>>
state "Online Lobby" as Online
state "Ready Check\n[popup]" as Ready <<popup>>
state "Multiplayer Court" as Court
state "Match Result\n[popup]" as Result <<popup>>
state "Coaching Summary\n[popup]" as Coach <<popup>>
state "Training Progress\n[popup]" as Progress <<popup>>
state "Match History\n[popup]" as History <<popup>>
state "Replay Viewer" as Replay
state "Missions\n[popup]" as Missions <<popup>>
state "Friends and Leaderboard\n[drawer]" as Social <<popup>>
state "Shop" as Shop
state "Collection" as Collection
state "Settings\n[overlay]" as Settings <<popup>>
state "Decisions and Appeals\n[popup]" as Appeals <<popup>>

[*] --> Login
Login --> Register
Register --> OTP
Login --> OTP : unverified email
Login --> Forgot
Forgot --> Login
OTP --> Login
Login --> Loading : signed in
Login --> Menu : offline training
Login --> Appeals : restricted account
Loading --> Menu
Menu --> Choice : first sign-in
Choice --> Tutorial : Beginner
Choice --> Menu : Experienced
Menu --> Modes : Play
Modes --> Tutorial
Modes --> BotSetup
BotSetup --> BotMatch
BotMatch --> Result
Modes --> Training
Training --> DrillResult
DrillResult --> Coach
Modes --> Online
Online --> Ready : match found
Ready --> Court : all accepted
Court --> Result
Result --> Coach
Menu --> Progress
Menu --> History
History --> Replay
Menu --> Missions
Menu --> Social
Menu --> Shop
Menu --> Collection
Menu --> Settings
"""

D["r3_screenflow_admin"] = r"""
hide empty description
state "Admin Login" as Login
state "Dashboard" as Dash
state "Users" as Users
state "Progress Reset\n[dialog]" as Reset
state "Content (CMS)" as CMS
state "Lesson Editor" as Lesson
state "Technique References" as Refs
state "Matches\n(live and history)" as Matches
state "Stop Match\n[dialog]" as Stop
state "Moderation\n(reports, sanctions, appeals)" as Mod
state "Analytics" as Analytics
state "Coaching Review" as Coach
[*] --> Login
Login --> Dash : Admin role
Dash --> Users
Users --> Reset
Dash --> CMS
CMS --> Lesson
CMS --> Refs
Dash --> Matches
Matches --> Stop
Dash --> Mod
Dash --> Analytics
Dash --> Coach
"""

D["r3_cerd_account"] = r"""
@startchen
entity ACCOUNT {
  AccountId <<key>>
  Email
  PasswordHash
  Status
}
entity ROLE {
  RoleId <<key>>
  RoleName
}
entity PLAYER_PROFILE {
  PlayerId <<key>>
  PlayerName
  Elo
  Level
  ExperienceChoice
}
entity VERIFICATION_CODE {
  CodeId <<key>>
  ExpiresAt
}
relationship HAS_ROLE {
}
relationship OWNS_PROFILE {
}
relationship VERIFIES {
}
relationship FRIEND_OF {
}
relationship BLOCKS {
}
ROLE -1- HAS_ROLE
HAS_ROLE -N- ACCOUNT
ACCOUNT -1- OWNS_PROFILE
OWNS_PROFILE -1- PLAYER_PROFILE
ACCOUNT -1- VERIFIES
VERIFIES -N- VERIFICATION_CODE
ACCOUNT -M- FRIEND_OF
FRIEND_OF -N- ACCOUNT
ACCOUNT -M- BLOCKS
BLOCKS -N- ACCOUNT
@endchen
"""

D["r3_cerd_training"] = r"""
@startchen
entity TECHNIQUE {
  TechniqueId <<key>>
  Name
}
entity LESSON {
  LessonId <<key>>
  Code
  PublishStatus
  ContentVersion
}
entity DRILL {
  DrillId <<key>>
  Code
}
entity PLAYER_PROFILE {
  PlayerId <<key>>
}
entity TRAINING_ATTEMPT {
  AttemptId <<key>>
  Stars
  Accuracy
  Completed
}
relationship CONTAINS {
}
relationship PRACTISES_SKILL {
}
relationship MAKES {
}
relationship USES {
}
relationship MASTERS {
  MasteryLevel
}
LESSON -M- CONTAINS
CONTAINS -N- DRILL
DRILL -N- PRACTISES_SKILL
PRACTISES_SKILL -1- TECHNIQUE
PLAYER_PROFILE -1- MAKES
MAKES -N- TRAINING_ATTEMPT
TRAINING_ATTEMPT -N- USES
USES -1- DRILL
PLAYER_PROFILE -M- MASTERS
MASTERS -N- TECHNIQUE
@endchen
"""

D["r3_cerd_match"] = r"""
@startchen
entity PLAYER_PROFILE {
  PlayerId <<key>>
}
entity MATCH {
  MatchId <<key>>
  Mode
  Status
  AuthorityMode
}
entity MATCH_GAME {
  GameNumber <<key>>
  TeamAScore
  TeamBScore
}
entity RATING_CHANGE {
  BeforeElo
  AfterElo
  Delta
}
entity REPLAY {
  ObjectKey
}
relationship PARTICIPATES {
  Team
  Outcome
}
relationship CONSISTS_OF {
}
relationship CHANGES_RATING {
}
relationship RECORDED_AS {
}
relationship RECEIVES {
}
PLAYER_PROFILE -M- PARTICIPATES
PARTICIPATES -N- MATCH
MATCH -1- CONSISTS_OF
CONSISTS_OF -N- MATCH_GAME
MATCH -1- CHANGES_RATING
CHANGES_RATING -N- RATING_CHANGE
PLAYER_PROFILE -1- RECEIVES
RECEIVES -N- RATING_CHANGE
MATCH -1- RECORDED_AS
RECORDED_AS -1- REPLAY
@endchen
"""

D["r3_cerd_moderation"] = r"""
@startchen
skinparam ranksep 18
skinparam nodesep 22
entity ACCOUNT {
  AccountId <<key>>
}
entity MATCH {
  MatchId <<key>>
}
entity PLAYER_REPORT {
  Reason
  Status
}
entity SANCTION {
  SanctionId <<key>>
  Restriction
  ExpiresAt
}
entity APPEAL {
  AppealId <<key>>
  Statement
  Status
}
relationship FILES {
}
relationship CONCERNS {
}
relationship OCCURS_IN {
}
relationship RESTRICTS {
}
relationship CONTESTS {
}
relationship REVIEWS {
  DecisionReason
}
relationship ISSUES {
}
ACCOUNT -1- FILES
FILES -N- PLAYER_REPORT
PLAYER_REPORT -N- CONCERNS
CONCERNS -1- ACCOUNT
PLAYER_REPORT -N- OCCURS_IN
OCCURS_IN -1- MATCH
SANCTION -N- RESTRICTS
RESTRICTS -1- ACCOUNT
APPEAL -1- CONTESTS
CONTESTS -1- SANCTION
ACCOUNT -1- REVIEWS
REVIEWS -N- PLAYER_REPORT
ACCOUNT -1- ISSUES
ISSUES -N- SANCTION
@endchen
"""

D["r3_cerd_economy"] = r"""
@startchen
entity PLAYER_PROFILE {
  PlayerId <<key>>
}
entity CURRENCY {
  CurrencyId <<key>>
  Name
}
entity COSMETIC {
  CosmeticId <<key>>
  ItemCode
}
entity MISSION {
  MissionId <<key>>
  Scope
  Target
}
entity ACHIEVEMENT {
  AchievementId <<key>>
  Target
}
relationship HOLDS {
  Balance
}
relationship PRICED_IN {
  Price
}
relationship OWNS {
  Equipped
}
relationship PROGRESSES {
  PeriodKey
  Progress
  Claimed
}
relationship EARNS {
  Progress
  Claimed
}
PLAYER_PROFILE -M- HOLDS
HOLDS -N- CURRENCY
COSMETIC -M- PRICED_IN
PRICED_IN -N- CURRENCY
PLAYER_PROFILE -M- OWNS
OWNS -N- COSMETIC
PLAYER_PROFILE -M- PROGRESSES
PROGRESSES -N- MISSION
PLAYER_PROFILE -M- EARNS
EARNS -N- ACHIEVEMENT
@endchen
"""

D["r3_cerd_coaching"] = r"""
@startchen
skinparam ranksep 18
skinparam nodesep 22
entity PLAYER_PROFILE {
  PlayerId <<key>>
}
entity TELEMETRY_SESSION {
  BatchId <<key>>
  SessionType
}
entity COACHING_RESULT {
  Summary
  Recommendations
}
entity EXPERT_REVIEW {
  ReviewId <<key>>
  Label
  Version
}
entity ADMIN {
  AccountId <<key>>
}
relationship UPLOADS {
}
relationship ANALYSED_INTO {
}
relationship REVIEWS {
}
relationship REVIEWED_BY {
}
PLAYER_PROFILE -1- UPLOADS
UPLOADS -N- TELEMETRY_SESSION
TELEMETRY_SESSION -1- ANALYSED_INTO
ANALYSED_INTO -1- COACHING_RESULT
TELEMETRY_SESSION -1- REVIEWS
REVIEWS -N- EXPERT_REVIEW
EXPERT_REVIEW -N- REVIEWED_BY
REVIEWED_BY -1- ADMIN
@endchen
"""

if __name__ == "__main__":
    print(write_all(D))
