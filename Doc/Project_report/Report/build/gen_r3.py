"""Report 3 - Software Requirement Specification (sections 1 to 3.1; FRs in r3_fr.py; NFRs and appendix in r3_tail.py)."""
from report_lib import Report
import r3_fr
import r3_tail

CHANGES = [
    ("27/10/2026", "A", "Nguyễn Gia Huy", "v0.1 – Feature list F01–F18, actors and workflows prepared for the Review 2 commitment (BM04)."),
    ("06/11/2026", "A", "Nguyễn Văn Thành Đạt", "v0.2 – First SRS on the official template: 23 use cases, FR-01 to FR-23, 10 NFRs."),
    ("07/11/2026", "M", "Nguyễn Văn Thành Đạt", "v0.3 – Linked the Review 2 feature list to the requirements; added business rules and API messages."),
    ("11/11/2026", "M", "Nguyễn Gia Huy", "v1.0 – Rewritten after the internal review: corrected context diagram; six actors; 68 use cases in eight UML use case diagrams; screen flows from the real scenes; screen authorization matrix; conceptual ERD; 33 functional requirements with real screenshots, data fields, validation and exception flows; gameplay, economy and authentication rules; measurable NFRs; message list in the product language."),
]

r = Report("Report3_Software Requirement Specification.docx", "Report 3 – Software Requirement Specification", CHANGES)
r.h1("II. Software Requirement Specification")

# ------------------------------------------------------------------ 1. Product overview
r.h2("1. Product Overview")
r.p("PickleVerse is a new 3D pickleball game that teaches beginners the rules and core techniques of pickleball and lets them practise against bots and other players online. Release 1.0 consists of five deployable parts that together form the system under specification: the Unity game client for Windows and Android, the Unity dedicated game server that runs online matches with server authority, the ASP.NET Core Web API with its PostgreSQL database and Redis cache, the Python AI coaching service, and the React administration web application. The Web API hosts the background workers for matchmaking, dedicated-server leases, AI analysis and moderation expiry.")
r.p("Figure 1 shows the context of release 1.0. The system exchanges data with two human actors and three external systems. Photon Cloud provides the real-time session and relay service used by the game client and the dedicated server; Gmail SMTP delivers one-time passwords; S3-compatible object storage keeps match replays. Real-money payment, social login and push notifications are outside the boundary of this release.")
r.figure("r3_context.png", "Context diagram of PickleVerse release 1.0", width_cm=16)
r.table(["External entity", "Type", "Data exchanged with PickleVerse"], [
    ["Player", "Human (Windows PC or Android phone)", "Sends account data, game input, training attempts, reports and appeals; receives game state, rewards, rankings, coaching tips and replays"],
    ["Administrator", "Human (web browser)", "Sends user, content, match and moderation commands; receives dashboards, live match status, reports, appeals and analytics"],
    ["Photon Cloud", "External real-time service", "Receives session registration and input/state packets from the client and the dedicated server; returns relayed packets and connection events"],
    ["Gmail SMTP", "External e-mail service", "Receives verification and password-reset messages to deliver to the player's mailbox"],
    ["S3-compatible object storage", "Storage service", "Stores replay manifests and compressed replay chunks; returns them for the replay viewer and moderation"],
], widths=[3.4, 3.6, 9.0], size=9.5, first_col_bold=True, caption="External entities of the context diagram")

# ------------------------------------------------------------------ 2. User requirements
r.h2("2. User Requirements")
r.h3("2.1 Actors")
r.table(["#", "Actor", "Description"], [
    ["1", "Visitor", "A person who uses the game client without being signed in. A visitor can register, verify an e-mail address, sign in, reset a forgotten password and, on a device that has a cached profile, start offline training."],
    ["2", "Player", "A signed-in user with the Player role and an active account. A player inherits every use case of a visitor and can learn, train, play bot and online matches, view progress, use the shop and the social features, and report other players."],
    ["3", "Restricted Player", "A player whose account is suspended or banned. After signing in, a restricted player is limited to viewing the sanction decision and submitting one appeal for it."],
    ["4", "Administrator", "A user with the Admin role who signs in to the administration web application to manage accounts, content, matches, moderation and analytics, and to label coaching sessions as an expert reviewer."],
    ["5", "Email Service", "Secondary actor (Gmail SMTP) that delivers verification and password-reset codes."],
    ["6", "Photon Cloud", "Secondary actor that hosts the real-time sessions through which the game client, other players and the dedicated server exchange match packets."],
], widths=[0.8, 3.2, 12.0], size=9.5, caption="Actors")

r.h3("2.2 Use Cases")
r.h4("2.2.1 Diagram(s)")
r.p("The 68 use cases are grouped into eight packages. Figure 2 gives the package overview; Figures 3 to 11 show each package with its actors and the «include» and «extend» relationships (package P4 is drawn in two parts for readability). Player and Restricted Player are specialisations of Visitor, so they inherit the visitor use cases.")
r.figure("r3_uc_overview.png", "Use case overview by package", width_cm=15.5)
for f, cap in [
    ("r3_uc_p1_account.png", "Use case diagram P1 – Account and Onboarding"),
    ("r3_uc_p2_learning.png", "Use case diagram P2 – Learning and Coaching"),
    ("r3_uc_p3_online.png", "Use case diagram P3 – Online Match"),
    ("r3_uc_p4a_social.png", "Use case diagram P4 (part 1) – History, Ranking and Friends"),
    ("r3_uc_p4b_economy.png", "Use case diagram P4 (part 2) – Missions, Achievements and Shop"),
    ("r3_uc_p5_restricted.png", "Use case diagram P5 – Account Restriction"),
    ("r3_uc_p6_admin_users.png", "Use case diagram P6 – Admin: Users and Moderation"),
    ("r3_uc_p7_admin_content.png", "Use case diagram P7 – Admin: Content"),
    ("r3_uc_p8_admin_monitor.png", "Use case diagram P8 – Admin: Monitoring and Analytics"),
]:
    r.figure(f, cap, width_cm=14.5, max_height_cm=19)

r.h4("2.2.2 Descriptions")
UC = [
    ("01", "Register Account", "Visitor", "The visitor creates an account with an e-mail address and a password; the system sends a six-digit code to that address.", "FR-01"),
    ("02", "Verify Email", "Visitor, Email Service", "The visitor enters the code received by e-mail to activate the account, or asks for a new code.", "FR-01"),
    ("03", "Sign In", "Visitor", "The visitor signs in with e-mail and password and is taken to the menu with the saved profile.", "FR-02"),
    ("04", "Reset Password", "Visitor, Email Service", "The visitor receives a reset code by e-mail and sets a new password; all existing sessions are revoked.", "FR-03"),
    ("05", "Sign Out", "Player", "The player ends the session on the device.", "FR-02"),
    ("06", "Choose Experience Level", "Player", "At the first sign-in the player states whether they are new to pickleball, which decides whether the tutorial starts.", "FR-04"),
    ("07", "Update Player Name", "Player", "The player changes the display name shown to other players.", "FR-04"),
    ("08", "Select Character", "Player", "The player chooses the male or female character used in matches.", "FR-04"),
    ("09", "Configure Game Settings", "Player", "The player adjusts audio, graphics, key bindings and the touch layout.", "FR-05"),
    ("10", "Complete Tutorial", "Player", "The player follows the 17-step tutorial on the court, rules and controls.", "FR-06"),
    ("11", "Browse Lessons", "Player", "The player views the lesson catalogue by group, with stars and lock status.", "FR-07"),
    ("12", "Practise Drill", "Player", "The player plays the drill of an unlocked lesson and receives a score of 0 to 3 stars.", "FR-08"),
    ("13", "Practise Drill Offline", "Player", "Without a connection, the player practises a cached drill; the result is stored for later synchronisation.", "FR-09"),
    ("14", "Synchronise Offline Results", "Player", "When online again, the stored offline results are sent and applied exactly once.", "FR-09"),
    ("15", "View Training Progress", "Player", "The player reviews passed lessons, practice time, common errors and post-session feedback.", "FR-10"),
    ("16", "Play Bot Match", "Player", "The player configures and plays a singles match against a computer opponent.", "FR-11, FR-12, FR-13"),
    ("17", "View Coaching Summary", "Player", "After a drill or match the player reads the analysis of their shots and the main errors.", "FR-19"),
    ("18", "View Drill Recommendation", "Player", "The player opens a recommended drill that targets a detected weakness.", "FR-19"),
    ("19", "Find Ranked Match", "Player", "The player joins the ranked singles queue and is paired with an opponent of similar Elo, or with a bot after 180 seconds.", "FR-14"),
    ("20", "Confirm Match Readiness", "Player", "Each paired player accepts the match within 10 seconds before it starts.", "FR-14"),
    ("21", "Create Private Room", "Player, Photon Cloud", "The player creates a singles or doubles room identified by a room code.", "FR-15"),
    ("22", "Join Room by Code", "Player", "The player joins a friend's room by typing its code.", "FR-15"),
    ("23", "Invite Friend to Room", "Player", "The host invites an online friend into the room.", "FR-15"),
    ("24", "Respond to Room Invitation", "Player", "The invited player accepts or declines the invitation.", "FR-15"),
    ("25", "Play Online Match", "Player, Photon Cloud", "The players play a match whose rules and score are decided by the dedicated server.", "FR-16"),
    ("26", "Reconnect to Match", "Player", "After a disconnection the player rejoins the same match within the reconnection window.", "FR-17"),
    ("27", "Leave Match", "Player", "The player leaves a match before it ends and accepts the consequences.", "FR-17"),
    ("28", "View Match History", "Player", "The player views the last 20 matches with result, score and Elo change.", "FR-24"),
    ("29", "Watch Match Replay", "Player", "The player watches the replay of one of their own matches with pause, seek and speed control.", "FR-24"),
    ("30", "Report Player", "Player", "The player reports an opponent from a finished match with a reason.", "FR-27"),
    ("31", "View Leaderboard", "Player", "The player views players ordered by Elo and their own position.", "FR-26"),
    ("32", "Send Friend Request", "Player", "The player searches by name or ID and sends a friend request.", "FR-25"),
    ("33", "Respond to Friend Request", "Player", "The player accepts or declines a received request.", "FR-25"),
    ("34", "Remove Friend", "Player", "The player removes a friend from the friend list.", "FR-25"),
    ("35", "Block Player", "Player", "The player blocks another player, which removes friendship and prevents requests and invitations.", "FR-25"),
    ("36", "Unblock Player", "Player", "The player removes a block.", "FR-25"),
    ("37", "View Missions and Streak", "Player", "The player views daily and weekly missions, their progress and the daily streak.", "FR-21"),
    ("38", "Claim Mission Reward", "Player", "The player claims the reward of a completed mission or mission set once.", "FR-21"),
    ("39", "View Achievements", "Player", "The player views achievement progress.", "FR-21"),
    ("40", "Claim Achievement Reward", "Player", "The player claims the reward of a completed achievement once.", "FR-21"),
    ("41", "Browse Shop", "Player", "The player views cosmetic items with their prices and currencies.", "FR-22"),
    ("42", "Purchase Cosmetic", "Player", "The player buys an item with in-game currency.", "FR-22"),
    ("43", "View Collection", "Player", "The player views owned cosmetic items.", "FR-23"),
    ("44", "Equip Cosmetic", "Player", "The player equips an owned paddle, which is then shown in matches.", "FR-23"),
    ("45", "View Sanction Decision", "Restricted Player", "The restricted player reads the restriction type, reason and expiry.", "FR-28"),
    ("46", "Submit Appeal", "Restricted Player", "The restricted player submits one written appeal against a sanction.", "FR-28"),
    ("47", "Sign In to Admin Portal", "Administrator", "The administrator signs in to the web application; only the Admin role is accepted.", "FR-29"),
    ("48", "Search User Accounts", "Administrator", "The administrator searches and filters accounts by text and status.", "FR-29"),
    ("49", "Assign Role", "Administrator", "The administrator changes the role of an account between Player and Admin.", "FR-29"),
    ("50", "Verify Email Manually", "Administrator", "The administrator activates an account whose verification e-mail could not be delivered.", "FR-29"),
    ("51", "Apply Sanction", "Administrator", "The administrator suspends or bans an account with a reason, optionally from a report.", "FR-29, FR-30"),
    ("52", "Revoke Sanction", "Administrator", "The administrator lifts an active sanction with a reason.", "FR-29"),
    ("53", "Reset Player Progress", "Administrator", "The administrator previews and resets selected progress groups of a player.", "FR-29"),
    ("54", "Review Player Report", "Administrator", "The administrator examines a report with match evidence and accepts or declines it.", "FR-30"),
    ("55", "Review Appeal", "Administrator", "The administrator accepts or declines an appeal; acceptance revokes the related sanction.", "FR-30"),
    ("56", "Edit Lesson Draft", "Administrator", "The administrator creates or edits a lesson and saves it as a draft.", "FR-31"),
    ("57", "Publish Lesson", "Administrator", "The administrator publishes the saved draft so that new attempts use it.", "FR-31"),
    ("58", "Unpublish Lesson", "Administrator", "The administrator hides a published lesson from players.", "FR-31"),
    ("59", "Manage Technique References", "Administrator", "The administrator maintains the reference values used to judge each technique.", "FR-31"),
    ("60", "Add Cosmetic Item", "Administrator", "The administrator adds an item to the shop with type, price and currency.", "FR-31"),
    ("61", "Add Mission", "Administrator", "The administrator creates a daily or weekly mission with target and reward.", "FR-31"),
    ("62", "Monitor Live Matches", "Administrator", "The administrator watches active matches with score and connection status refreshed every 5 seconds.", "FR-32"),
    ("63", "Stop Live Match", "Administrator", "The administrator stops a dedicated-server match with a reason.", "FR-32"),
    ("64", "View Match Records", "Administrator", "The administrator browses finished and interrupted matches.", "FR-32"),
    ("65", "View Dashboard Summary", "Administrator", "The administrator sees today's key figures and items that need action.", "FR-32"),
    ("66", "View Analytics Report", "Administrator", "The administrator views active users, retention, lesson pass rates, technique errors and matchmaking health for a date range.", "FR-32"),
    ("67", "Review Coaching Session", "Administrator", "Acting as expert, the administrator inspects shot evidence of a training session and labels the player level and errors.", "FR-33"),
    ("68", "Export Labelled Dataset", "Administrator", "The administrator exports the labelled sessions used to evaluate the AI coaching quality.", "FR-33"),
]
r.table(["ID", "Use Case", "Actors", "Use Case Description", "FR"],
        [["UC-" + a, b, c, d, e] for a, b, c, d, e in UC],
        widths=[1.4, 3.6, 2.6, 7.0, 1.8], size=8.5, caption="Use case descriptions")

# ------------------------------------------------------------------ 3. Functional requirements
r.h2("3. Functional Requirements")
r.h3("3.1 System Functional Overview")
r.h4("3.1.1 Screens Flow")
r.p("Figure %d shows the screens of the game client and how a player moves between them; Figure %d shows the administration web application. Rounded white boxes marked [popup], [dialog], [drawer] or [overlay] are windows opened above another screen; the other boxes are full scenes or pages." % (r.fig_no + 1, r.fig_no + 2))
r.figure("r3_screenflow_game.png", "Screen flow of the game client", width_cm=16.5, max_height_cm=21)
r.figure("r3_screenflow_admin.png", "Screen flow of the administration web application", width_cm=14)

r.h4("3.1.2 Screen Descriptions")
SCREENS = [
    ("FE-01", "Login", "E-mail and password fields with a show/hide control, the Sign in button, links to e-mail verification, password recovery and registration, and an Offline training button when a profile is cached on the device."),
    ("FE-01", "Register", "E-mail, password and password confirmation fields with inline validation and the Create account button."),
    ("FE-01", "Email Verification (OTP)", "Six-digit code field, Verify and Resend code buttons, and a link back to sign-in."),
    ("FE-01", "Forgot / Reset Password", "Step 1 asks for the e-mail address; step 2 asks for the reset code, the new password and its confirmation."),
    ("FE-01", "Loading", "Animated artwork while the profile, wallet, content catalogue and settings are loaded."),
    ("FE-01", "Experience Choice [popup]", "Asked once at the first sign-in: \"Bạn đã quen với pickleball chưa?\" with Beginner and Experienced options."),
    ("FE-01", "Menu (Lobby)", "3D court with the player's character; name tag with level, rank and Elo; Tokenpad and Elite Token balances; male/female selector; music player; Play button; buttons for Progress, History, Missions, Collection, Shop and Settings; friend drawer; Decisions and Appeals button."),
    ("FE-01", "Settings [overlay]", "Tabs Overview (with Sign out), Audio, Graphics and Controls (key binding and touch layout); Restore defaults, Undo and Apply and save buttons."),
    ("FE-02", "Tutorial", "Seventeen steps on the court; each step has a title, an explanation and a demonstration, with Continue, Replay and Later buttons."),
    ("FE-02", "Mode Selection", "Four cards – Bot match, Online match, Training and Create room – and a Tutorial strip."),
    ("FE-02", "Training Academy", "Lesson catalogue with group tabs (All, Basic, Control, Advanced, Doubles); lesson briefing with target, reward and star rules; practice HUD with shot counter, timing feedback and key hints."),
    ("FE-02", "Drill Result [popup]", "Stars, accuracy (hits in target / shots), mean timing error, practice time, most frequent error with a tip, and Retry, Next lesson and Lesson list buttons."),
    ("FE-02", "Training Progress [popup]", "Passed lessons out of 15, next lesson, post-session feedback, practice time, most frequent error, and per-lesson best result with a Retry button."),
    ("FE-03", "Bot Match Setup", "Difficulty (Easy, Medium, Hard, Adaptive), time of day (day, night) and court (professional, outdoor), with a summary line and the Enter court button."),
    ("FE-03", "Bot Match", "Court with both players, score and serving side, serve timer, fault banners, time-out control, key hints and the pause/settings overlay."),
    ("FE-03", "Match Result [popup]", "Winner, game scores, Tokenpad and XP earned, Elo change for ranked matches, and Play again, Mode selection and View analysis buttons."),
    ("FE-05", "Coaching Summary [popup]", "Counts of shots, contacts and serves; main errors with their frequency; up to three recommended drills with a Practise now link."),
    ("FE-04", "Online Lobby", "Room code field, Join by code button, Singles 1v1 and Doubles 2v2 buttons, roster slots, friend invitation list and the Find match / Cancel button with an elapsed-time counter."),
    ("FE-04", "Ready Check [popup]", "Match found message with Accept and Decline buttons and a 10-second countdown."),
    ("FE-04", "Multiplayer Court", "Online match on the dedicated server with score, connection state, reconnecting overlay and leave control."),
    ("FE-07", "Match History [popup]", "Level card and the last 20 matches with opponent, mode, Vietnam time, result, game score, Elo change, and Report and Replay buttons."),
    ("FE-07", "Replay Viewer", "Playback of a recorded match with play/pause, a seek bar and speeds from 0.5× to 2×."),
    ("FE-06", "Missions [popup]", "Daily and weekly tabs, mission list with progress and reward, set bonus, daily streak, reset countdown, and Claim buttons."),
    ("FE-07", "Friends and Leaderboard [drawer]", "Friends tab with search by name or ID and sub-tabs Friends, Find friends and Blocked; Leaderboard tab with own position."),
    ("FE-06", "Shop", "Featured and all items, item preview, price in Tokenpad and/or Elite Token, wallet balances and the Buy button."),
    ("FE-06", "Collection", "Owned paddles with preview and the Equip button."),
    ("FE-08", "Decisions and Appeals [popup]", "List of sanctions on the account with type, reason and expiry; appeal form and appeal status."),
    ("FE-08", "Report Player [dialog]", "Opened from a history entry: reported player, match, reason field and Send button."),
    ("FE-09", "Admin Login", "Account or e-mail and password fields and the Authenticate button; only Admin accounts are accepted."),
    ("FE-09", "Dashboard", "Total users, daily active users, matches played, online time, pending reports, technique-error heat map and quick links."),
    ("FE-09", "Users", "Search and status filter; table of player and e-mail, role, status, Elo, play time and creation date; actions to assign a role, sanction, verify e-mail and reset progress."),
    ("FE-09", "Progress Reset [dialog]", "Selection of LEARNING, RANKING, MISSIONS and ECONOMY groups, an impact preview and the Reset button."),
    ("FE-09", "Content (CMS)", "Lesson manager (draft, publish, unpublish), drill form, shop item form, mission form and technique references."),
    ("FE-09", "Matches", "Live matches refreshed every 5 seconds with score and connection state, match history, and a Stop match dialog that requires a reason."),
    ("FE-09", "Moderation", "Reports, sanctions and appeals with match evidence and replay download, decision form and decision reason."),
    ("FE-09", "Analytics", "Date range in Vietnam time; daily active players, retention cohorts, popular lessons with pass rate, technique-error heat map and matchmaking health."),
    ("FE-10", "Expert Review", "Training sessions with shot-by-shot evidence, level and error labels, data group (training, calibration, holdout) and export."),
]
r.table(["#", "Feature", "Screen", "Description"], [[str(i + 1), a, b, c] for i, (a, b, c) in enumerate(SCREENS)],
        widths=[0.8, 1.5, 3.6, 10.1], size=8.5, caption="Screen descriptions")

r.h4("3.1.3 Screen Authorization")
r.p("X means that the role may open the screen or perform the activity. Visitor is a user who has not signed in; Restricted Player is a signed-in player with a suspended or banned account. The Web API enforces the same rules: a missing or expired token returns HTTP 401, and a valid token without the required role or scope returns HTTP 403.")
AUTH = [
    ("Login, Register, Email Verification, Forgot Password", "X", "", "", ""),
    ("    Start offline training from a cached profile", "X", "X", "", ""),
    ("Menu, Settings, Mode Selection, Tutorial", "", "X", "", ""),
    ("    Update name, select character, sign out", "", "X", "", ""),
    ("Training Academy, Drill Result, Training Progress", "", "X", "", ""),
    ("Bot Match Setup, Bot Match, Match Result, Coaching Summary", "", "X", "", ""),
    ("Online Lobby, Ready Check, Multiplayer Court", "", "X", "", ""),
    ("    Create room, invite friend, start a custom match (host only)", "", "X", "", ""),
    ("Match History, Replay Viewer", "", "X", "", ""),
    ("    Query own matches and replays only", "", "X", "", ""),
    ("    Report a player of an own finished match", "", "X", "", ""),
    ("Missions, Shop, Collection", "", "X", "", ""),
    ("    Claim rewards, purchase and equip own items", "", "X", "", ""),
    ("Friends and Leaderboard", "", "X", "", ""),
    ("Decisions and Appeals", "", "X", "X", ""),
    ("    Submit an appeal for an own sanction", "", "", "X", ""),
    ("Admin Login", "", "", "", "X"),
    ("Dashboard, Analytics", "", "", "", "X"),
    ("Users", "", "", "", "X"),
    ("    Query all accounts", "", "", "", "X"),
    ("    Assign role, verify e-mail, apply and revoke sanctions (not own account)", "", "", "", "X"),
    ("    Preview and reset player progress", "", "", "", "X"),
    ("Content (CMS)", "", "", "", "X"),
    ("    Add and update lessons, references, items and missions; publish and unpublish", "", "", "", "X"),
    ("Matches", "", "", "", "X"),
    ("    Stop a live dedicated-server match", "", "", "", "X"),
    ("Moderation", "", "", "", "X"),
    ("    Decide reports and appeals", "", "", "", "X"),
    ("Expert Review", "", "", "", "X"),
    ("    Save expert labels and export the dataset", "", "", "", "X"),
]
r.table(["Screen / Activity", "Visitor", "Player", "Restricted Player", "Administrator"], [list(a) for a in AUTH],
        widths=[9.0, 1.6, 1.6, 2.0, 2.0], size=8.5, caption="Screen authorization")

r.h4("3.1.4 Non-Screen Functions")
NSF = [
    ("FE-01", "Send verification and reset e-mails", "Generates a six-digit code, stores only its HMAC, and sends the message through Gmail SMTP (STARTTLS) from a background mail queue."),
    ("FE-01", "Refresh access token", "Exchanges a valid refresh token for a new access token (2 hours) and a rotated refresh token (14 days); revoked or reused tokens are rejected."),
    ("FE-01", "Record activity days", "Records each player's login day and active day (Vietnam time) and online presence in Redis for analytics and streaks."),
    ("FE-02", "Apply training results", "Validates each training attempt from its telemetry, applies stars and first-pass rewards, and returns a receipt APPLIED, DUPLICATE or REJECTED for each request ID."),
    ("FE-04", "Ranked matchmaking", "Keeps search tickets in a Redis queue, pairs tickets within the Elo window, creates the ready check, and assigns a ranked bot after 180 seconds."),
    ("FE-04", "Dedicated server allocation", "Issues HMAC-signed match tickets, leases the dedicated server slot, receives heartbeats every 8 seconds and releases expired leases (DedicatedLeaseMonitor)."),
    ("FE-04", "Settle match result", "Saves the game scores once, applies Elo for ranked matches, grants rewards and updates missions and achievements in one transaction."),
    ("FE-04", "Store replay", "Uploads the replay manifest and compressed chunks to object storage and links them to the match."),
    ("FE-05", "Ingest telemetry", "Receives shot telemetry parts from the client (or the dedicated server for online matches), stores them and creates an analysis job."),
    ("FE-05", "Hybrid AI worker", "Sends pending analysis jobs to the AI coaching service, stores the coaching result and retries failed jobs (HybridAiWorker)."),
    ("FE-05", "AI coaching service", "POST /v1/analyze-session: rule-based findings, summary, at most three drill recommendations and, when the gated model is approved, a tier estimate."),
    ("FE-06", "Mission cycle", "Opens new daily missions at 07:00 Vietnam time each day and new weekly missions each week, and updates the daily streak."),
    ("FE-07", "Leaderboard cache", "Caches the Elo leaderboard in Redis for 15 seconds."),
    ("FE-08", "Sanction expiry", "Restores suspended accounts whose sanction has expired (ModerationExpiryWorker)."),
    ("FE-09", "Analytics projection", "Aggregates daily active players, retention cohorts, lesson statistics and matchmaking events (AnalyticsProjectionWorker)."),
    ("FE-09", "Admin audit", "Writes an audit record for every administrative change with actor, action, target and time."),
]
r.table(["#", "Feature", "System Function", "Description"], [[str(i + 1), a, b, c] for i, (a, b, c) in enumerate(NSF)],
        widths=[0.8, 1.5, 3.8, 9.9], size=8.5, caption="Non-screen functions")

r.h4("3.1.5 Entity Relationship Diagram")
r.p("The conceptual ERD is drawn in Chen notation and split into six subject areas so that every diagram stays readable. Entities are business concepts rather than database tables: junction tables appear as relationships with their own attributes (for example PARTICIPATES carries Team and Outcome), and purely technical tables such as refresh tokens and telemetry parts are omitted. The logical design of all 51 tables is given in Report 4, §2.")
for f, cap in [
    ("r3_cerd_account.png", "Conceptual ERD – accounts, profiles and social relations"),
    ("r3_cerd_training.png", "Conceptual ERD – techniques, lessons and training"),
    ("r3_cerd_match.png", "Conceptual ERD – matches, ratings and replays"),
    ("r3_cerd_economy.png", "Conceptual ERD – currencies, cosmetics, missions and achievements"),
    ("r3_cerd_moderation.png", "Conceptual ERD – reports, sanctions and appeals"),
    ("r3_cerd_coaching.png", "Conceptual ERD – telemetry, coaching and expert review"),
]:
    r.figure(f, cap, width_cm=15, max_height_cm=17)
r.p("**Entities Description**", bold=True)
ENT = [
    ("ACCOUNT", "A registered user identified by e-mail; holds the password hash, status (ACTIVE, SUSPENDED, BANNED) and verification state."),
    ("ROLE", "Player or Admin; each account has exactly one role."),
    ("VERIFICATION_CODE", "A one-time code for e-mail verification or password reset, with expiry and remaining attempts."),
    ("PLAYER_PROFILE", "Game identity of an account: display name, character, level, XP, Elo, onboarding choice and statistics."),
    ("TECHNIQUE", "A pickleball technique to be learnt (serve, drive, dink, volley, drop, reset, smash, footwork, approach, doubles coverage and transition, modifiers)."),
    ("LESSON", "A unit of the curriculum with code, group, order, target, reward, prerequisite and publication status."),
    ("DRILL", "An exercise that practises one technique; a lesson contains one or more drills."),
    ("TRAINING_ATTEMPT", "One run of a drill by a player, with shots, hits, timing, stars and synchronisation receipt."),
    ("MATCH", "A bot, ranked, matched doubles or custom match with mode, kind, authority mode and status."),
    ("MATCH_GAME", "One game (1 to 3) of a match with the score of each team; a weak entity identified by match and game number."),
    ("RATING_CHANGE", "The Elo before, change and after of one player caused by one ranked match."),
    ("REPLAY", "The stored recording of a match (object key of its manifest)."),
    ("PLAYER_REPORT", "A complaint by one player about another player in a given match, with reason, status and decision."),
    ("SANCTION", "A suspension (with expiry) or ban applied to an account, with reason, actor and revocation data."),
    ("APPEAL", "A restricted player's written objection to one sanction, with status and decision."),
    ("CURRENCY", "In-game currency: Tokenpad (earned by playing) and Elite Token."),
    ("COSMETIC", "A purchasable or granted item (paddle, outfit, hat) with price and currency."),
    ("MISSION", "A daily or weekly goal with target and reward."),
    ("ACHIEVEMENT", "A long-term goal (wins, matches, points, streaks) with a one-time reward."),
    ("TELEMETRY_SESSION", "The shot-level data captured during a drill or match and uploaded for analysis."),
    ("COACHING_RESULT", "The AI analysis of a telemetry session: findings, summary, recommendations and optional tier."),
    ("EXPERT_REVIEW", "An expert's labels for a telemetry session (level, errors, data group) used to evaluate the AI."),
    ("ADMIN", "An account with the Admin role, shown as a separate box in the coaching diagram for readability."),
]
r.table(["#", "Entity", "Description"], [[str(i + 1), a, b] for i, (a, b) in enumerate(ENT)],
        widths=[0.8, 3.6, 11.6], size=9, caption="Entities description")

r3_fr.write(r)
r3_tail.write(r)
out = r.save("Report3_Software Requirement Specification.docx", "Report 3 – Software Requirement Specification")
print(out, "tables", r.tbl_no, "figures", r.fig_no)
