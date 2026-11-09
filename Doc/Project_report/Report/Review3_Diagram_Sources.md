# PickleVerse Review 3 diagram sources

The labels and links match the selected figures in Reports 3 and 4. The source baseline is `df5f671`. The activities describe supported workflows. A diagram does not record a completed staging run.

## Report 3 Figure 1 Product context

```mermaid
flowchart LR
  N_p["Player"]
  N_g["Unity game"]
  N_b["PickleVerse services"]
  N_a["Admin"]
  N_w["Admin web app"]
  N_s["Photon and email services"]
  N_p --> N_g
  N_g --> N_b
  N_a --> N_w
  N_w --> N_b
  N_b --> N_s
```

## Report 3 Figure 2 Player use cases

```mermaid
flowchart LR
  N_actor["Player"]
  N_u0["UC-03 First steps"]
  N_u1["UC-04 Learn rules"]
  N_u2["UC-05 Play drill"]
  N_u3["UC-06 Play bot"]
  N_u4["UC-07 / 08 / 09 Play online"]
  N_u5["UC-10 / 11 Read tips"]
  N_u6["UC-12 to 15 Progress and friends"]
  N_u7["UC-20 / 21 Send a report or appeal"]
  N_u8["UC-22 Train offline"]
  N_actor --> N_u0
  N_actor --> N_u1
  N_actor --> N_u2
  N_actor --> N_u3
  N_actor --> N_u4
  N_actor --> N_u5
  N_actor --> N_u6
  N_actor --> N_u7
  N_actor --> N_u8
```

## Report 3 Figure 3 Visitor use cases

```mermaid
flowchart LR
  N_actor["Visitor"]
  N_u0["UC-01 Create and verify an account"]
  N_u1["UC-02 Sign in or reset a password"]
  N_actor --> N_u0
  N_actor --> N_u1
```

## Report 3 Figure 4 Admin use cases

```mermaid
flowchart LR
  N_actor["Admin"]
  N_u0["UC-16 Admin sign-in"]
  N_u1["UC-17 / 18 Accounts and content"]
  N_u2["UC-19 Matches and analytics"]
  N_u3["UC-20 / 21 Review reports and appeals"]
  N_u4["UC-23 Review shot data"]
  N_actor --> N_u0
  N_actor --> N_u1
  N_actor --> N_u2
  N_actor --> N_u3
  N_actor --> N_u4
```

## Report 3 Figure 5 Game screen flow

```mermaid
flowchart LR
  N_auth["Login and Email Code"]
  N_load["Loading"]
  N_home["Menu"]
  N_first["First Steps / Tutorial"]
  N_training["Training / Bot Match"]
  N_online["Online Room / Court"]
  N_result["Result / Coaching"]
  N_more["Profile / Social / Rewards"]
  N_auth --> N_load
  N_load --> N_home
  N_load --> N_first
  N_home --> N_training
  N_home --> N_online
  N_training --> N_result
  N_online --> N_result
  N_home --> N_more
```

## Report 3 Figure 6 Admin screen flow

```mermaid
flowchart LR
  N_login["Admin Login"]
  N_home["Dashboard"]
  N_users["Users / Content"]
  N_matches["Matches / Moderation"]
  N_data["Analytics / Coaching"]
  N_login --> N_home
  N_home --> N_users
  N_home --> N_matches
  N_home --> N_data
```

## Report 3 Figure 7 Main data groups

```mermaid
flowchart LR
  N_u["Users / PlayerProfiles"]
  N_t["TrainingSessions"]
  N_m["GameMatchPlayers"]
  N_l["Lessons / Drills"]
  N_g["GameMatches"]
  N_s["AiTelemetrySessions"]
  N_u --> N_t
  N_u --> N_m
  N_u --> N_s
  N_t --> N_l
  N_m --> N_g
```

## Report 4 Figure 1 Runtime components

```mermaid
flowchart LR
  N_g["Unity game"]
  N_r["Photon Shared rooms"]
  N_s["Unity Dedicated server"]
  N_w["React admin web"]
  N_a["ASP.NET Core API"]
  N_i["Python FastAPI"]
  N_p["PostgreSQL"]
  N_c["Redis"]
  N_f["S3 replay storage"]
  N_e["SMTP email service"]
  N_g -->|"Lobby"| N_r
  N_g -->|"Live play"| N_s
  N_g -->|"Ticket and JSON"| N_a
  N_w -->|"JSON"| N_a
  N_s -->|"Lease and result"| N_a
  N_a -->|"Analysis"| N_i
  N_a --> N_p
  N_a --> N_c
  N_a --> N_f
  N_a -->|"Email codes"| N_e
```

## Report 4 Figure 2 Backend project references

```mermaid
flowchart LR
  N_a["API"]
  N_s["Services"]
  N_r["Repositories"]
  N_e["Entities"]
  N_a --> N_s
  N_a --> N_e
  N_s --> N_r
  N_s --> N_e
  N_r --> N_e
```

## Report 4 Figure 3 Unity assembly references

```mermaid
flowchart LR
  N_f["FrontEnd"]
  N_m["Multiplayer"]
  N_l["LocalMatch"]
  N_p["Progression"]
  N_s["Services"]
  N_r["Rules"]
  N_f --> N_m
  N_f --> N_p
  N_m --> N_s
  N_m --> N_l
  N_l --> N_r
  N_p --> N_s
```

## Report 4 Figure 4 Admin web and AI modules

```mermaid
flowchart LR
  N_p["Admin feature pages"]
  N_a["Admin API modules"]
  N_c["Axios client"]
  N_f["FastAPI main"]
  N_r["Rules engine"]
  N_m["Request models"]
  N_t["Tier and calibration"]
  N_p --> N_a
  N_a --> N_c
  N_f --> N_r
  N_r --> N_m
  N_f --> N_t
```

## Report 4 Figure 5 Account and training ERD

```mermaid
flowchart TB
  N_r["Roles"]
  N_u["Users"]
  N_p["PlayerProfiles"]
  N_l["Lessons"]
  N_j["LessonDrills"]
  N_t["TrainingSessions"]
  N_k["Techniques"]
  N_d["Drills"]
  N_s["PlayerLessonDrills"]
  N_r -->|"1 to many"| N_u
  N_u -->|"1 to 0 or 1"| N_p
  N_l -->|"1 to many"| N_j
  N_d -->|"1 to many"| N_j
  N_k -->|"1 to many"| N_d
  N_j -->|"1 to many"| N_t
  N_p -->|"1 to many"| N_t
  N_j --> N_s
  N_p --> N_s
```

## Report 4 Figure 6 Match and safety ERD

```mermaid
flowchart TB
  N_p["PlayerProfiles"]
  N_m["GameMatches"]
  N_q["MatchScore"]
  N_j["GameMatchPlayers"]
  N_e["ratingledger"]
  N_r["MatchHistoryReplay"]
  N_n["UserSanctions"]
  N_a["SanctionAppeals"]
  N_d["DedicatedOperations"]
  N_p --> N_j
  N_m --> N_j
  N_m --> N_e
  N_p --> N_e
  N_m -->|"1 to many"| N_q
  N_m --> N_r
  N_n -->|"1 to 0 or 1"| N_a
  N_m --> N_d
```

## Report 4 Figure 7 Coaching ERD

```mermaid
flowchart TB
  N_p["PlayerProfiles"]
  N_s["AiTelemetrySessions"]
  N_t["AiTelemetryParts"]
  N_c["AiCoachingProfiles"]
  N_j["AiAnalysisJobs"]
  N_r["ExpertReviews"]
  N_m["AiCoachingSessions"]
  N_u["Users (reviewer)"]
  N_p --> N_s
  N_s --> N_t
  N_s --> N_j
  N_s --> N_r
  N_u --> N_r
  N_p --> N_c
  N_p --> N_m
```

## Report 4 Figure 8 Report and appeal logical ERD

```mermaid
flowchart TB
  N_p["PlayerProfiles"]
  N_g["GameMatches"]
  N_r["ReportPlayerInMatch"]
  N_u["Users"]
  N_s["UserSanctions"]
  N_a["SanctionAppeals"]
  N_p -->|"Reporter and target"| N_r
  N_g -->|"Match"| N_r
  N_u -->|"Reviewer"| N_r
  N_u -->|"Account and actor"| N_s
  N_g -->|"Report match"| N_s
  N_u -->|"Account and reviewer"| N_a
  N_s -->|"0 or 1 appeal"| N_a
```

## Report 4 Figure 9 Progress and reward logical ERD

```mermaid
flowchart TB
  N_p["PlayerProfiles"]
  N_c["PlayerCurrencies"]
  N_r["ProgressRewardGrants"]
  N_o["PlayerCosmetics"]
  N_y["Currencies"]
  N_i["Cosmetics"]
  N_p --> N_c
  N_p -->|"One per grant key"| N_r
  N_p --> N_o
  N_y --> N_c
  N_i --> N_o
```

## Report 4 Figure 10 Friend and block logical ERD

```mermaid
flowchart TB
  N_u["Users"]
  N_f["FriendRequests"]
  N_r["Friendships"]
  N_b["BlockedUsers"]
  N_u -->|"Sender and receiver"| N_f
  N_u -->|"Two friends"| N_r
  N_u -->|"User and blocked user"| N_b
```

## Report 4 Figure 11 Account and first steps classes

```mermaid
flowchart LR
  N_ui["AccountScreen"]
  N_api["AuthController"]
  N_svc["IAuthService"]
  N_impl["AuthService"]
  N_uow["IUnitOfWork"]
  N_ui -->|"HTTP"| N_api
  N_api --> N_svc
  N_impl -->|"implements"| N_svc
  N_impl --> N_uow
```

## Report 4 Figure 12 Account and first steps sequence

```mermaid
sequenceDiagram
  participant P0 as AccountScreen
  participant P1 as AuthController
  participant P2 as AuthService
  participant P3 as Data / email
  P0->>P1: Send verified login details
  P1->>P2: LoginAsync
  P2->>P3: Read account and session state
  P3->>P2: Return account state
  P2->>P1: Return token or access error
  P1->>P0: Load the game or show error
```

## Report 4 Figure 13 Training and offline sync classes

```mermaid
flowchart LR
  N_t["TrainingDirector"]
  N_p["ProgressionRuntime"]
  N_c["GameTrainingController"]
  N_s["IGameTrainingService"]
  N_o["OnlineGameService"]
  N_u["IUnitOfWork"]
  N_t --> N_p
  N_p -->|"HTTP"| N_c
  N_c --> N_s
  N_o -->|"implements"| N_s
  N_o --> N_u
```

## Report 4 Figure 14 Training and offline sync sequence

```mermaid
sequenceDiagram
  participant P0 as Unity training
  participant P1 as Saved queue
  participant P2 as Training API
  participant P3 as OnlineGameService
  P0->>P1: Save finished offline run
  P1->>P2: Send runs after reconnect
  P2->>P3: SyncTraining
  P3->>P3: Check epoch, run ID and result
  P3->>P2: Return per-run receipt
  P2->>P1: Keep retry items, clear accepted items
```

## Report 4 Figure 15 Online rooms and dedicated play classes

```mermaid
flowchart LR
  N_p["PhotonSession"]
  N_w["DedicatedWorker"]
  N_a["DedicatedServerService"]
  N_d["DedicatedAdmission"]
  N_u["IUnitOfWork"]
  N_r["DedicatedLeaseRepository"]
  N_w -->|"API calls"| N_a
  N_w -->|"ticket check"| N_d
  N_a --> N_r
  N_a --> N_u
```

## Report 4 Figure 16 Online rooms and dedicated play sequence

```mermaid
sequenceDiagram
  participant P0 as Unity client
  participant P1 as API
  participant P2 as Dedicated Worker
  participant P3 as PostgreSQL
  P0->>P1: Request ticket for the started match
  P1->>P0: Waiting state while the worker starts
  P2->>P1: Claim lease and report Ready
  P0->>P1: Poll ticket every 500 ms until Ready
  P1->>P0: Ready with signed token
  P0->>P2: Join with the token
  P2->>P1: Check admission and lease
  P2->>P2: Run rallies and track scores
  P2->>P1: Send trusted completion
  P1->>P3: Save the outcome once
```

## Report 4 Figure 17 Bot and coaching classes

```mermaid
flowchart LR
  N_q["QuickCoachingRules"]
  N_c["CoachingRuntime"]
  N_h["HybridCoachingService"]
  N_b["HybridAiWorker"]
  N_a["FastAPI analyzer"]
  N_u["IUnitOfWork"]
  N_c -->|"API flow"| N_h
  N_h --> N_u
  N_b -->|"HTTP"| N_a
  N_b --> N_h
```

## Report 4 Figure 18 Bot and coaching sequence

```mermaid
sequenceDiagram
  participant P0 as Unity / Dedicated
  participant P1 as Coaching API
  participant P2 as HybridAiWorker
  participant P3 as FastAPI
  P0->>P1: Upload allowed session data
  P1->>P1: Check owner and queue the batch
  P2->>P1: Claim ready analysis job
  P2->>P3: Analyze with X-AI-Key
  P3->>P2: Return rules or gated model output
  P2->>P1: Store completed result
  P0->>P1: Read summary and suggestion
```

## Report 4 Figure 19 Progress, items and social classes

```mermaid
flowchart LR
  N_p["ProgressionRuntime"]
  N_a["Game API"]
  N_s["OnlineGameService"]
  N_u["IUnitOfWork"]
  N_r["GameRepository"]
  N_c["PickleVerseContext"]
  N_p -->|"HTTP"| N_a
  N_a -->|"service contract"| N_s
  N_s --> N_u
  N_u --> N_r
  N_r --> N_c
```

## Report 4 Figure 20 Progress, items and social sequence

```mermaid
sequenceDiagram
  participant P0 as Game client
  participant P1 as Game API
  participant P2 as Service
  participant P3 as Database
  P0->>P1: Request mission reward
  P1->>P2: Check player and claim
  P2->>P3: Read progress and saved command
  P2->>P3: Save one claim and reward
  P2->>P1: Return updated game state
  P1->>P0: Show new balance
```

## Report 4 Figure 21 Content and account administration classes

```mermaid
flowchart LR
  N_w["Admin web"]
  N_c["ContentController"]
  N_s["ContentService"]
  N_r["ProgressResetController"]
  N_p["ProgressResetService"]
  N_u["IUnitOfWork"]
  N_w -->|"HTTP"| N_c
  N_w -->|"HTTP"| N_r
  N_c -->|"IContentService"| N_s
  N_r -->|"service"| N_p
  N_s --> N_u
  N_p --> N_u
```

## Report 4 Figure 22 Content and account administration sequence

```mermaid
sequenceDiagram
  participant P0 as Admin web
  participant P1 as ContentController
  participant P2 as ContentService
  participant P3 as PostgreSQL
  P0->>P1: Save draft with expected version
  P1->>P2: Check Admin and pass request
  P2->>P3: Check and save versioned content
  P0->>P1: Publish lesson
  P1->>P2: Request a new catalog
  P2->>P3: Save published snapshot
  P1->>P0: Show current state
```

## Report 4 Figure 23 Moderation and expert review classes

```mermaid
flowchart LR
  N_m["ModerationService"]
  N_d["DedicatedServerService"]
  N_w["ExpertReviewController"]
  N_u["IUnitOfWork"]
  N_e["ExpertReviewService"]
  N_m --> N_u
  N_d --> N_u
  N_w -->|"service contract"| N_e
  N_e --> N_u
```

## Report 4 Figure 24 Moderation and expert review sequence

```mermaid
sequenceDiagram
  participant P0 as Admin web
  participant P1 as Moderation API
  participant P2 as ModerationService
  participant P3 as Dedicated Worker
  P0->>P1: Request stop with reason
  P1->>P2: Check access and live match
  P2->>P2: Store stop operation
  P3->>P1: Send heartbeat
  P1->>P3: Return stop command
  P3->>P1: Acknowledge with lease
  P0->>P1: Read operation state
```

## Report 4 Figure 25 ACT-W01 Learn and sync a drill

```mermaid
flowchart LR
  N_a["Player<br/>Verify and sign in"]
  N_b["Game<br/>Open tutorial and drill"]
  N_c["Player<br/>Finish cached drill offline"]
  N_d["Game<br/>Save run on device"]
  N_e["API<br/>Check ID and version"]
  N_f["API<br/>Apply once or return receipt"]
  N_a --> N_b
  N_b --> N_c
  N_c --> N_d
  N_d -->|"Reconnect"| N_e
  N_e --> N_f
```

## Report 4 Figure 26 ACT-W02 Play a bot and get a tip

```mermaid
flowchart LR
  N_a["Player<br/>Pick level and mode"]
  N_b["Game<br/>Start bot match"]
  N_c["Player and bot<br/>Play rallies"]
  N_d["Game<br/>Show result and quick tips"]
  N_e["AI service<br/>Check usable data"]
  N_f["Game<br/>Show available summary"]
  N_a --> N_b
  N_b --> N_c
  N_c --> N_d
  N_d -->|"Allowed data"| N_e
  N_e --> N_f
```

## Report 4 Figure 27 ACT-W03 Play online and save the result

```mermaid
flowchart LR
  N_a["Players<br/>Find or join a room"]
  N_b["Players<br/>Accept the ready check"]
  N_c["API and worker<br/>Wait for Ready ticket"]
  N_x["Game<br/>Close old offer"]
  N_d["Dedicated server<br/>Run rallies"]
  N_e["API<br/>Save one result"]
  N_a --> N_b
  N_b -->|"All accept"| N_c
  N_b -->|"Decline"| N_x
  N_c -->|"Ready"| N_d
  N_d --> N_e
```

## Report 4 Figure 28 ACT-W04 Review a report and publish a lesson

```mermaid
flowchart LR
  N_a["Player<br/>Send eligible report"]
  N_b["Admin<br/>Review the report"]
  N_c["API<br/>Save one decision"]
  N_d["Admin<br/>Publish a draft"]
  N_e["API<br/>Check role and version"]
  N_f["Game<br/>Read published lesson"]
  N_a --> N_b
  N_b --> N_c
  N_d --> N_e
  N_e -->|"Valid Admin"| N_f
```

## Report 4 Figure 29 SM-01 Email verification

```mermaid
stateDiagram-v2
  state "Unverified" as S_u
  state "Verified" as S_v
  [*] --> S_u: Register
  S_u --> S_v: Valid code
```

## Report 4 Figure 30 SM-02 Training run and sync

```mermaid
stateDiagram-v2
  state "Active run" as S_a
  state "Saved offline" as S_b
  state "APPLIED" as S_c
  state "REJECTED" as S_d
  state "DUPLICATE" as S_e
  S_a --> S_b: Finish offline
  S_b --> S_c: Valid sync
  S_b --> S_d: Bad version
  S_c --> S_e: Repeat
```

## Report 4 Figure 31 SM-03 Bot match phases

```mermaid
stateDiagram-v2
  state "Intro" as S_i
  state "Cinematic" as S_c
  state "PreServe" as S_p
  state "BetweenPoints" as S_b
  state "Settling" as S_s
  state "Rally" as S_r
  state "BetweenGames" as S_g
  state "MatchOver" as S_m
  state "Timeout" as S_t
  S_i --> S_c: Start
  S_c --> S_p: Intro ends
  S_p --> S_r: Serve
  S_r --> S_s: Rally ends
  S_s --> S_b: Next point
  S_s --> S_g: Next game
  S_s --> S_m: Match won
  S_b --> S_p
  S_g --> S_p
  S_p --> S_t: Timeout
  S_t --> S_p: Resume
```

## Report 4 Figure 32 SM-04 Ranked search

```mermaid
stateDiagram-v2
  state "SEARCHING" as S_s
  state "MATCHED" as S_m
  state "BOT" as S_b
  state "CANCELLED" as S_c
  state "TIMEOUT" as S_t
  S_s --> S_m: Group found
  S_s --> S_b: Bot fallback
  S_s --> S_c: Cancel
  S_s --> S_t: Expired
```

## Report 4 Figure 33 SM-05 Saved match

```mermaid
stateDiagram-v2
  state "PREPARING" as S_p
  state "PLAYING" as S_y
  state "FINISHED" as S_f
  state "CANCELLED" as S_c
  S_p --> S_y: Accept and start
  S_p --> S_c: Cancel
  S_y --> S_f: Result or interruption
```

## Report 4 Figure 34 SM-06 Content publication

```mermaid
stateDiagram-v2
  state "DRAFT" as S_d
  state "PUBLISHED" as S_p
  state "UNPUBLISHED" as S_u
  S_d --> S_p: Publish
  S_p --> S_u: Unpublish
  S_u --> S_p: Publish again
```

## Report 4 Figure 35 SM-07 Report and appeal decision

```mermaid
stateDiagram-v2
  state "PENDING" as S_p
  state "CHECKING" as S_c
  state "ACCEPTED" as S_a
  state "DECLINED" as S_d
  S_p --> S_c: Review
  S_c --> S_a: Accept
  S_c --> S_d: Decline
  S_p --> S_a: Direct accept
  S_p --> S_d: Direct decline
```

## Report 4 Figure 36 SM-08 Account restriction

```mermaid
stateDiagram-v2
  state "ACTIVE" as S_a
  state "SUSPENDED" as S_s
  state "BANNED" as S_b
  S_a --> S_s: Suspend
  S_a --> S_b: Ban
  S_s --> S_a: Expire or revoke
  S_b --> S_a: Lift restriction
```

## Report 4 Figure 37 SM-09 AI analysis job

```mermaid
stateDiagram-v2
  state "pending" as S_p
  state "processing" as S_r
  state "completed" as S_c
  state "failed" as S_f
  S_p --> S_r: Claim job
  S_r --> S_c: Save result
  S_r --> S_p: Retry
  S_r --> S_f: Limit reached
```

## Report 4 Figure 38 SM-10 Friend request

```mermaid
stateDiagram-v2
  state "PENDING" as S_p
  state "ACCEPTED" as S_a
  state "DECLINED" as S_d
  S_p --> S_a: Accept
  S_p --> S_d: Decline
```

## Report 4 Figure 39 SM-11 Dedicated allocation

```mermaid
stateDiagram-v2
  state "queued" as S_q
  state "starting" as S_s
  state "ready" as S_r
  state "Lease released or expired" as S_e
  state "finishing" as S_f
  S_q --> S_s: Worker claim
  S_s --> S_r: Worker ready
  S_r --> S_f: Final result
  S_f --> S_e: Release
  S_s --> S_e: Expire
  S_r --> S_e: Expire or stop
```
