"""Report 3 - Software Requirement Specification (sections 1 to 3.1; FRs in r3_fr.py; NFRs and appendix in r3_tail.py)."""
from report_lib import Report
import r3_fr
import r3_tail

CHANGES = [
    ("27/10/2026", "A", "Nguyễn Gia Huy", "v0.1 – Feature list F01–F18, actors and workflows prepared for the Review 2 commitment (BM04)."),
    ("06/11/2026", "A", "Nguyễn Văn Thành Đạt", "v0.2 – First SRS on the official template: 23 use cases, FR-01 to FR-23, 10 NFRs."),
    ("07/11/2026", "M", "Nguyễn Văn Thành Đạt", "v0.3 – Linked the Review 2 feature list to the requirements; added business rules and API messages."),
    ("11/11/2026", "M", "Nguyễn Gia Huy", "v1.0 – Rewritten after the internal review: corrected context diagram; six actors; 68 use cases in eight UML use case diagrams; screen flows from the real scenes; screen authorization matrix; conceptual ERD; 33 functional requirements with real screenshots, data fields, validation and exception flows; gameplay, economy and authentication rules; measurable NFRs; message list in the product language."),
    ("11/11/2026", "M", "Nguyễn Gia Huy", "v1.1 – Appendix A: workflow and BM04 owner added for each Review 2 feature."),
    ("12/11/2026", "M", "Nguyễn Gia Huy", "v1.2 – Revised after the second internal review: time-outs removed (FR-12, BR-15, Bot Match screen); quick doubles added to UC-19, FR-14 and the non-screen functions; reconnection rule BR-28 and FR-17 aligned with the code; accuracy history, streak rewards and phone controls described; Menu, Online Lobby and Bot Match screens and their screenshots updated; screenshot references clarified."),
    ("13/11/2026", "M", "Nguyễn Gia Huy", "v1.3 – Communication interfaces: the staging environment serves the Web API and the admin web over HTTPS since 13/11/2026."),
    ("17/11/2026", "M", "Nguyễn Gia Huy", "v1.4 – Updated to main @ 96ff8e9 and the Review 1 comments: use cases UC-69 to UC-76 and the use case diagram P4 part 3; FR-34 Social Feed and Posts and FR-35 Direct Messages and Whispers; match detail in FR-24; display modes and quit in FR-05; ranked doubles with bot fill in UC-19, FR-14, FR-18, BR-22, BR-23 and BR-26; XP curve n × 10 in FR-20 and BR-33; business rules BR-52 to BR-57; messages MSG66 to MSG78; social screens, authorisation, non-screen functions and conceptual ERD; 59 tables and 147 endpoints."),
    ("18/11/2026", "M", "Nguyễn Gia Huy", "v1.5 – Rules aligned with the implemented behaviour after the defect review (Report 5, D-28 and D-30): BR-28 and FR-17 – a player who leaves or does not return within the 30-second reconnection window forfeits (loss, no Tokenpad or XP), and only an administrator stop or a lost dedicated server ends a match as interrupted; BR-32 – custom room matches give no XP or Tokenpad."),
    ("19/11/2026", "M", "Nguyễn Gia Huy", "v1.6 – Interface labels checked against the build: Online Lobby join button THAM GIA, new screen Match Summary (THOÁT TRẬN, VỀ PHÒNG CHỜ, VỀ TRANG CHỦ); MSG70 and the feed use case updated after the fix of D-22 for Windows; Vietnamese labels explained in English (Table of interface labels in §5.4); conceptual ERD redrawn in crow's-foot notation, the notation of the logical and physical ERDs; use case overview drawn with packages and the main use cases; non-human actors drawn as «actor» rectangles."),
    ("25/11/2026", "M", "Nguyễn Gia Huy", "v1.7 – Updated to main @ adcb4e2 (changes of 18–25/11/2026): membership (actor PayOS, UC-77, UC-78, FR-36, BR-58 to BR-61, MSG79 to MSG83, screens and authorisation) and its locks in FR-13, FR-14 and FR-15; one device per account (FR-02, BR-05, BR-62, MSG84); ready check after the match-found intro (FR-14, BR-25); host migration and position swap (UC-79, BR-63); replay uploaded during the match and sealed before the result (FR-16, FR-18, BR-64); coaching panel and referee calls (FR-13, FR-19, MSG30); lessons with several drills (FR-31, BR-47); replay opened from the admin web (UC-80, FR-30, BR-65, MSG85); Elite Token source (BR-36); protocol 22, 60 tables and 160 endpoints; surrender with confirmation instead of the court button that left at once (UC-81, FR-17, BR-66, MSG88 to MSG90; Report 5, D-34); FR-34 – the Windows build can choose a video."),
]

r = Report("Report3_Software Requirement Specification.docx", "Report 3 – Software Requirement Specification", CHANGES)
r.h1("II. Software Requirement Specification")

# ------------------------------------------------------------------ 1. Product overview
r.h2("1. Product Overview")
r.p("PickleVerse is a new 3D pickleball game that teaches beginners the rules and core techniques of pickleball and lets them practise against bots and other players online. Release 1.0 consists of five deployable parts that together form the system under specification: the Unity game client for Windows and Android, the Unity dedicated game server that runs online matches with server authority, the ASP.NET Core Web API with its PostgreSQL database and Redis cache, the Python AI coaching service, and the React administration web application. The Web API hosts the background workers for matchmaking, dedicated-server leases, AI analysis, moderation expiry and the clean-up of unused social videos, and a SignalR hub that carries lobby whispers and new-message notifications. It also sells the lifetime membership through the payment provider PayOS.")
r.p("Figure 1 shows the context of release 1.0. The system exchanges data with two human actors and four external systems. Photon Cloud provides the real-time session and relay service used by the game client and the dedicated server; Gmail SMTP delivers one-time passwords; S3-compatible object storage keeps match replays and the videos of social posts; PayOS hosts the payment page of the membership and confirms payments. Apart from this one purchase, real-money payment is outside the boundary of this release, as are social login and push notifications.")
r.figure("r3_context.png", "Context diagram of PickleVerse release 1.0", width_cm=16)
r.table(["External entity", "Type", "Data exchanged with PickleVerse"], [
    ["Player", "Human (Windows PC or Android phone)", "Sends account data, game input, training attempts, reports and appeals; receives game state, rewards, rankings, coaching tips and replays"],
    ["Administrator", "Human (web browser)", "Sends user, content, match and moderation commands; receives dashboards, live match status, reports, appeals and analytics"],
    ["Photon Cloud", "External real-time service", "Receives session registration and input/state packets from the client and the dedicated server; returns relayed packets and connection events"],
    ["Gmail SMTP", "External e-mail service", "Receives verification and password-reset messages to deliver to the player's mailbox"],
    ["S3-compatible object storage", "Storage service", "Stores replay manifests, compressed replay chunks and the MP4 videos of social posts; returns them for the replay viewer, the video player and moderation"],
    ["PayOS", "External payment provider", "Receives the signed payment request of the membership (order code, 49,000 VND, return and cancel address); shows the payment page to the player in the browser; sends a signed webhook and answers queries about the order status"],
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
    ["7", "PayOS", "Secondary actor (payment provider) that hosts the payment page of the membership and confirms to the Web API that an order has been paid, cancelled or has expired."],
], widths=[0.8, 3.2, 12.0], size=9.5, caption="Actors")

r.h3("2.2 Use Cases")
r.h4("2.2.1 Diagram(s)")
r.p("The 81 use cases are grouped into eight packages. Figure 2 gives the package overview; Figures 3 to 12 show each package with its actors and the «include» and «extend» relationships (package P4 is drawn in three parts for readability). Player and Restricted Player are specialisations of Visitor, so they inherit the visitor use cases.")
r.figure("r3_uc_overview.png", "Use case overview by package", width_cm=15.5)
for f, cap in [
    ("r3_uc_p1_account.png", "Use case diagram P1 – Account and Onboarding"),
    ("r3_uc_p2_learning.png", "Use case diagram P2 – Learning and Coaching"),
    ("r3_uc_p3_online.png", "Use case diagram P3 – Online Match"),
    ("r3_uc_p4a_social.png", "Use case diagram P4 (part 1) – History, Ranking and Friends"),
    ("r3_uc_p4b_economy.png", "Use case diagram P4 (part 2) – Missions, Achievements and Shop"),
    ("r3_uc_p4c_feed.png", "Use case diagram P4 (part 3) – Social Feed and Messages"),
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
    ("03", "Sign In", "Visitor", "The visitor signs in with e-mail and password and is taken to the menu with the saved profile; a session of the account on another device ends.", "FR-02"),
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
    ("15", "View Training Progress", "Player", "The player reviews passed lessons, stars, best and recent accuracy of each lesson, practice time, common errors and post-session feedback.", "FR-10"),
    ("16", "Play Bot Match", "Player", "The player configures and plays a singles match against a computer opponent; the adaptive opponent needs the membership.", "FR-11, FR-12, FR-13"),
    ("17", "View Coaching Summary", "Player", "After a drill or match the player reads the analysis of their shots and the main errors.", "FR-19"),
    ("18", "View Drill Recommendation", "Player", "The player opens a recommended drill that targets a detected weakness.", "FR-19"),
    ("19", "Find Online Match", "Player", "A player with membership searches a ranked singles match (opponent of similar Elo, or a bot after 180 seconds) or a ranked doubles match (2v2, alone or with a party of two; free seats are filled with ranked bots after 180 seconds).", "FR-14"),
    ("20", "Confirm Match Readiness", "Player", "After the match-found intro each paired player accepts the match within 10 seconds before it starts.", "FR-14"),
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
    ("56", "Edit Lesson Draft", "Administrator", "The administrator creates or edits a lesson and its drills and saves them as a draft.", "FR-31"),
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
    ("69", "Browse Social Feed", "Player", "The player reads the posts that he or she may see, newest first.", "FR-34"),
    ("70", "Write, Edit or Delete Post", "Player", "The player writes a post with text, a replay of an own match or an MP4 video, chooses its audience, and later edits or deletes it.", "FR-34"),
    ("71", "Like and Comment on Post", "Player", "The player likes a visible post and reads, writes or deletes comments.", "FR-34"),
    ("72", "Watch Shared Video or Replay", "Player", "The player plays the video of a post or opens its shared replay.", "FR-34"),
    ("73", "Send Direct Message", "Player", "The player sends a message to a friend, also when the friend is offline.", "FR-35"),
    ("74", "Read Inbox and Conversation", "Player", "The player reads the conversations with unread counters and the history of one conversation.", "FR-35"),
    ("75", "Whisper in the Lobby", "Player", "The player sends a short live message to another player who is in the lobby.", "FR-35"),
    ("76", "View Match Detail", "Player", "The player opens a finished match from the history and reads the score of each game and the error statistics of each player.", "FR-24"),
    ("77", "View Membership Offer", "Player", "The player opens the membership panel from the menu or from a locked mode and sees the price, the benefits and the state of an open order.", "FR-36"),
    ("78", "Buy Membership", "Player, PayOS", "The player pays the membership on the PayOS page; the system activates it and credits the Elite Token once the payment is confirmed.", "FR-36"),
    ("79", "Swap Position in a Room", "Player", "In a custom room a player asks another player to swap positions; the other player agrees or declines.", "FR-15"),
    ("80", "Open Replay in the Game", "Administrator", "The administrator opens the replay of a reported match in the installed game with a one-time ticket.", "FR-30"),
    ("81", "Surrender Match", "Player", "The player gives up a running online match and confirms; in doubles the human partner has to confirm as well, and then their team loses the match.", "FR-17"),
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
    ("FE-01", "Login", "E-mail and password fields with a show/hide control, the Sign in button, links to e-mail verification, password recovery and registration, an Offline training button when a profile is cached on the device, and the THOÁT GAME button in the top-right corner; after a sign-in on another device it shows why the session ended (MSG84)."),
    ("FE-01", "Quit Confirmation [dialog]", "\"Thoát PickleVerse?\" with Ở lại and Thoát game; opened from the Login screen and from the × button of the window frame (PC)."),
    ("FE-01", "Window Frame (PC, windowed mode)", "Title bar with the PICKLEVERSE wordmark, drag area, – (minimise) and × (quit) buttons, and a rounded blue border; the interface below moves under the title bar."),
    ("FE-01", "Register", "E-mail, password and password confirmation fields with inline validation and the Create account button."),
    ("FE-01", "Email Verification (OTP)", "Six-digit code field, Verify and Resend code buttons, and a link back to sign-in."),
    ("FE-01", "Forgot / Reset Password", "Step 1 asks for the e-mail address; step 2 asks for the reset code, the new password and its confirmation."),
    ("FE-01", "Loading", "Animated artwork while the profile, wallet, content catalogue and settings are loaded."),
    ("FE-01", "Experience Choice [popup]", "Asked once at the first sign-in: \"Bạn đã quen với pickleball chưa?\" with Beginner and Experienced options."),
    ("FE-01", "Menu (Lobby)", "3D court with the player's character; name tag with level, rank and Elo; Tokenpad and Elite Token balances; male/female selector; day/night toggle; music player; Play button (Enter); the GÓI THÀNH VIÊN button (THÀNH VIÊN once it is active); one row of seven tiles – Progress, Decisions and Appeals (\"!\"), History, Missions, Collection, Shop and Settings; friend drawer on the left edge with the BẢNG TIN (feed) and TIN NHẮN (messages) buttons."),
    ("FE-01", "Settings [overlay]", "Tabs Overview (with Sign out and Thoát game), Audio, Graphics, Controls (key binding and touch layout) and, on PC, Khung hình (Cửa sổ, Toàn màn hình, Không viền and three window sizes); Restore defaults, Undo and Apply and save buttons."),
    ("FE-02", "Tutorial", "Seventeen steps on the court; each step has a title, an explanation and a demonstration, with Continue, Replay and Later buttons."),
    ("FE-02", "Mode Selection", "Four cards – Bot match, Online match, Training and Create room – and a Tutorial strip; the Online match and Create room cards carry a lock badge until the membership is active."),
    ("FE-01", "Music Room [overlay]", "Playlist of ten tracks with their disc, play/pause, previous and next, volume shortcut and the NCS badge in the top-right corner (copyright-free music)."),
    ("FE-11", "Membership [popup]", "Heading GÓI THÀNH VIÊN, the reason when it was opened from a locked mode, the price, \"Thanh toán một lần · Vĩnh viễn\", three benefits, a status line and the button MUA NGAY / MỞ THANH TOÁN / ĐÃ KÍCH HOẠT."),
    ("FE-11", "Payment Return (web page)", "Page of the web application to which PayOS sends the browser after the payment; it asks the player to go back to the game and activates nothing by itself."),
    ("FE-02", "Training Academy", "Lesson catalogue with group tabs (All, Basic, Control, Advanced, Doubles); lesson briefing with target, reward and star rules; practice HUD with shot counter, timing feedback and key hints."),
    ("FE-02", "Drill Result [popup]", "Stars, accuracy (hits in target / shots), mean timing error, practice time, most frequent error with a tip, and Retry, Next lesson and Lesson list buttons."),
    ("FE-02", "Training Progress [popup]", "Passed lessons out of 15, next lesson, post-session feedback, practice time, most frequent error, and for each lesson the stars, best accuracy and the four most recent accuracy results (\"Gần đây\") with a Retry button."),
    ("FE-03", "Bot Match Setup", "Difficulty (Easy, Medium, Hard, Adaptive – shown as \"Tự động\", locked without membership), time of day (day, night) and court (professional, outdoor), with a summary line and the Enter court button."),
    ("FE-03", "Bot Match", "Court seen from behind the player's baseline with both players; score panel with serving side and serve timer; rule hint; referee call after each rally (red MẤT LƯỢT, green BẠN GHI ĐIỂM or ĐẾN LƯỢT BẠN GIAO BÓNG); shot cue under the player with the key combination (touch buttons on phones); Settings (Esc) and help (F1)."),
    ("FE-03", "Match Result [popup]", "Winner, game scores, Tokenpad and XP earned, Elo change for ranked matches, and Play again, Mode selection and View analysis buttons."),
    ("FE-05", "Coaching Summary [popup]", "Header HUẤN LUYỆN VIÊN AI; four figures – LƯỢT ĐÁNH, CÚ CHUẨN, TỈ LỆ CHUẨN, LỖI GHI NHẬN; the scrolling list LỖI CẦN SỬA with every fault, its frequency per technique and one piece of advice; up to three drills under BÀI TẬP ĐỀ XUẤT with LUYỆN NGAY; while the analysis runs the panel refreshes itself, and after 90 seconds it offers THỬ LẠI."),
    ("FE-04", "Online Lobby", "Singles 1v1 and Doubles 2v2 tabs, room code with Copy, Join by code, roster slots with Invite buttons, a collapsible friends column listing online friends who can be invited, a position button on the card of each other player in a custom room, and the Find match / Cancel button with an elapsed-time counter."),
    ("FE-04", "Ready Check [overlay]", "Match-found intro (about 4 seconds: paddles, the VS ball and the two sides with their players) followed by the cards of the players, a 10-second countdown and the Accept and Cancel buttons; cards show who has accepted."),
    ("FE-04", "Multiplayer Court", "Online match on the dedicated server with score, connection state, reconnecting overlay and leave control."),
    ("FE-04", "Match Summary [overlay]", "Outcome cinematic with the XEM TỔNG KẾT button, then the summary card PICKLEVERSE / TỔNG KẾT TRẬN ĐẤU: outcome, players, Elo change with the rating before and after, current rank, game scores and XP, and the buttons VỀ PHÒNG CHỜ (back to the room or the online lobby) and VỀ TRANG CHỦ (back to the menu)."),
    ("FE-07", "Match History [popup]", "Level card and the last 20 matches with opponent, mode, Vietnam time, result, game score, Elo change, and Report and Replay buttons; selecting a match opens Match Detail."),
    ("FE-07", "Match Detail [popup]", "Kind, mode, series score, Elo change and end time; tab Tỉ số with the score of each game for team A and team B; tab Thông số with the errors per technique and game of each player; Làm mới and Quay lại."),
    ("FE-07", "Replay Viewer", "Playback of a recorded match with play/pause, a seek bar and speeds from 0.5× to 2×."),
    ("FE-06", "Missions [popup]", "Daily and weekly tabs, mission list with progress and reward, set bonus, daily streak, reset countdown, and Claim buttons."),
    ("FE-07", "Friends and Leaderboard [drawer]", "Friends tab with search by name or ID and sub-tabs Friends, Find friends and Blocked, NHẮN TIN (friend) or THÌ THẦM (other player) on each row; BẢNG TIN and TIN NHẮN buttons; Leaderboard tab with own position."),
    ("FE-07", "Social Feed [overlay]", "Header with Bảng tin, Hộp thư, Bạn bè and ×; posts with author, time, audience, text, Video and Replay buttons, Thích and Bình luận counters, Sửa and Xóa on own posts; Viết bài, Làm mới and Xem thêm."),
    ("FE-07", "Post Editor", "Nội dung bài đăng (2,000 characters), Ai có thể xem (Cộng đồng, Chỉ mình tôi, Bạn bè, Bạn bè ngoại trừ) with Chọn người ngoại trừ, Chọn MP4 / Bỏ video, Chọn replay / Bỏ replay, Đăng bài (Lưu bài) and Quay lại."),
    ("FE-07", "Comments", "Comments of one post with author and Xóa; Viết bình luận field and Gửi."),
    ("FE-07", "Inbox and Conversation", "Conversations with name, last message and unread count; message history with Xem thêm; Nhắn tin cho … field and Gửi; read-only note after unfriending or blocking."),
    ("FE-07", "Whispers", "Latest whisper of each sender with Mở; whisper thread with the Thì thầm trong sảnh field and Gửi."),
    ("FE-07", "Video Player [overlay]", "Video of a post with Phát / Dừng and Đóng video."),
    ("FE-06", "Shop", "Featured and all items, item preview, price in Tokenpad and/or Elite Token, a panel that describes the ball effect of the Dragon Ruby paddle, wallet balances and the Buy button."),
    ("FE-06", "Collection", "Owned paddles with preview and the Equip button."),
    ("FE-08", "Decisions and Appeals [popup]", "List of sanctions on the account with type, reason and expiry; appeal form and appeal status."),
    ("FE-08", "Report Player [dialog]", "Opened from a history entry: reported player, match, reason field and Send button."),
    ("FE-09", "Admin Login", "Account or e-mail and password fields and the Authenticate button; only Admin accounts are accepted."),
    ("FE-09", "Dashboard", "Total users, daily active users, matches played, online time, pending reports, technique-error heat map and quick links."),
    ("FE-09", "Users", "Search and status filter; table of player and e-mail, role, status, Elo, play time and creation date; actions to assign a role, sanction, verify e-mail and reset progress."),
    ("FE-09", "Progress Reset [dialog]", "Selection of LEARNING, RANKING, MISSIONS and ECONOMY groups, an impact preview and the Reset button."),
    ("FE-09", "Content (CMS)", "Lesson manager with the drills of each lesson (add, edit, delete, draft, publish, unpublish), shop item form, mission form and technique references."),
    ("FE-09", "Matches", "Live matches refreshed every 5 seconds with score and connection state, match history, and a Stop match dialog that requires a reason."),
    ("FE-09", "Moderation", "Reports, sanctions and appeals with match evidence, replay download and Xem replay (opens the match in the installed game), decision form and decision reason."),
    ("FE-09", "Analytics", "Date range in Vietnam time; daily active players, retention cohorts, popular lessons with pass rate, technique-error heat map and matchmaking health."),
    ("FE-10", "Expert Review", "Training sessions with shot-by-shot evidence, level and error labels, data group (training, calibration, holdout) and export."),
]
r.table(["#", "Feature", "Screen", "Description"], [[str(i + 1), a, b, c] for i, (a, b, c) in enumerate(SCREENS)],
        widths=[0.8, 1.5, 3.6, 10.1], size=8.5, caption="Screen descriptions")

r.h4("3.1.3 Screen Authorization")
r.p("X means that the role may open the screen or perform the activity. Visitor is a user who has not signed in; Restricted Player is a signed-in player with a suspended or banned account. The Web API enforces the same rules: a missing or expired token returns HTTP 401, and a valid token without the required role or scope returns HTTP 403.")
AUTH = [
    ("Login, Register, Email Verification, Forgot Password", "X", "", "", ""),
    ("    Quit the game (Login, Settings, window frame)", "X", "X", "X", ""),
    ("    Start offline training from a cached profile", "X", "X", "", ""),
    ("Menu, Settings, Mode Selection, Tutorial", "", "X", "", ""),
    ("    Update name, select character, sign out", "", "X", "", ""),
    ("Training Academy, Drill Result, Training Progress", "", "X", "", ""),
    ("Bot Match Setup, Bot Match, Match Result, Coaching Summary", "", "X", "", ""),
    ("    Adaptive bot (membership required)", "", "X", "", ""),
    ("Online Lobby, Ready Check, Multiplayer Court, Match Summary (membership required)", "", "X", "", ""),
    ("    Create room, invite friend, start a custom match (host only)", "", "X", "", ""),
    ("    Ask for and answer a position swap (custom room)", "", "X", "", ""),
    ("Membership", "", "X", "", ""),
    ("    View the offer, buy and refresh the own order", "", "X", "", ""),
    ("Match History, Match Detail, Replay Viewer", "", "X", "", ""),
    ("    Query own matches and replays only", "", "X", "", ""),
    ("    Report a player of an own finished match", "", "X", "", ""),
    ("Missions, Shop, Collection", "", "X", "", ""),
    ("    Claim rewards, purchase and equip own items", "", "X", "", ""),
    ("Friends and Leaderboard", "", "X", "", ""),
    ("Social Feed, Post Editor, Comments, Video Player", "", "X", "", ""),
    ("    See only posts allowed by the audience; edit and delete own posts", "", "X", "", ""),
    ("Inbox, Conversation, Whispers", "", "X", "", ""),
    ("    Message friends only; whisper only to players in the lobby", "", "X", "", ""),
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
    ("    Open a match replay in the game with a one-time ticket", "", "", "", "X"),
    ("Expert Review", "", "", "", "X"),
    ("    Save expert labels and export the dataset", "", "", "", "X"),
]
r.table(["Screen / Activity", "Visitor", "Player", "Restricted Player", "Administrator"], [list(a) for a in AUTH],
        widths=[9.0, 1.6, 1.6, 2.0, 2.0], size=8.5, caption="Screen authorization")

r.h4("3.1.4 Non-Screen Functions")
NSF = [
    ("FE-01", "Send verification and reset e-mails", "Generates a six-digit code, stores only its HMAC, and sends the message through Gmail SMTP (STARTTLS) from a background mail queue."),
    ("FE-01", "Refresh access token", "Exchanges a valid refresh token for a new access token (2 hours) and a rotated refresh token (14 days); revoked or reused tokens are rejected."),
    ("FE-01", "Check session", "Every 3 seconds, and when the game window gets the focus, the client asks the API whether its session is still the current one of the account (GET /api/Auth/session); a session replaced on another device is closed with MSG84."),
    ("FE-01", "Record activity days", "Records each player's login day and active day (Vietnam time) and online presence in Redis for analytics and streaks."),
    ("FE-02", "Apply training results", "Validates each training attempt from its telemetry, applies stars and first-pass rewards, and returns a receipt APPLIED, DUPLICATE or REJECTED for each request ID."),
    ("FE-04", "Matchmaking", "Keeps search tickets in a Redis queue. Ranked singles: pairs tickets within the Elo window and assigns a ranked bot after 180 seconds. Ranked doubles: fills two teams of two from solo players and parties of two in the same region and protocol version; after 180 seconds the free seats are filled with ranked bots. Both create the ready check; every human player must have the membership."),
    ("FE-04", "Dedicated server allocation", "Reserves a free game-server container for a confirmed match in a Redis queue (at most DedicatedServer:MaxConcurrentMatches), issues HMAC-signed match tickets, receives heartbeats every 8 seconds, reports players whose login has been replaced and releases expired leases (DedicatedLeaseMonitor)."),
    ("FE-04", "Settle match result", "Saves the game scores once, applies Elo for ranked matches, grants rewards and updates missions and achievements in one transaction."),
    ("FE-04", "Store replay", "Uploads the replay manifest and the compressed chunks in batches while the match is played, seals the recording at the end and links it to the match; the result is saved after the recording is sealed, or without a replay when that takes longer than 60 seconds."),
    ("FE-05", "Ingest telemetry", "Receives shot telemetry parts from the client (or the dedicated server for online matches), stores them and creates an analysis job."),
    ("FE-05", "Hybrid AI worker", "Sends pending analysis jobs to the AI coaching service, stores the coaching result and retries failed jobs (HybridAiWorker)."),
    ("FE-05", "AI coaching service", "POST /v1/analyze-session: rule-based findings, summary, at most three drill recommendations and, when the gated model is approved, a tier estimate."),
    ("FE-11", "Confirm membership payment", "Verifies the signature of the PayOS webhook, reads the order from PayOS (a redirect or a webhook alone is not a receipt), and activates the membership and credits the Elite Token once; a pending order is also checked when the player returns to the game."),
    ("FE-06", "Mission cycle", "Opens new daily missions at 07:00 Vietnam time each day and new weekly missions each week, and updates the daily streak."),
    ("FE-07", "Leaderboard cache", "Caches the Elo leaderboard in Redis for 15 seconds."),
    ("FE-07", "Lobby presence and live delivery", "SocialHub (SignalR over WebSocket) records which players are in the lobby (Redis, 45-second presence), delivers whispers and notifies both players of a new direct message."),
    ("FE-07", "Video validation and storage", "Spools an uploaded MP4 to disk, checks it with ffprobe (H.264 video, optional AAC audio, at most 15 minutes), stores it in object storage and grants 20-minute playback links."),
    ("FE-07", "Social video clean-up", "Every 10 minutes deletes videos that were removed or never attached to a post within 24 hours, and stale upload spools (SocialMediaCleanup)."),
    ("FE-08", "Sanction expiry", "Restores suspended accounts whose sanction has expired (ModerationExpiryWorker)."),
    ("FE-09", "Analytics projection", "Aggregates daily active players, retention cohorts, lesson statistics and matchmaking events (AnalyticsProjectionWorker)."),
    ("FE-09", "Replay launch ticket", "Issues a one-time ticket (2 minutes) that the game exchanges for a read-only view of one replay (2 hours); every read checks again that the issuing administrator is still active."),
    ("FE-09", "Admin audit", "Writes an audit record for every administrative change with actor, action, target and time."),
]
r.table(["#", "Feature", "System Function", "Description"], [[str(i + 1), a, b, c] for i, (a, b, c) in enumerate(NSF)],
        widths=[0.8, 1.5, 3.8, 9.9], size=8.5, caption="Non-screen functions")

r.h4("3.1.5 Entity Relationship Diagram")
r.p("The conceptual ERD is the first of the three ERD levels; the logical and the physical ERDs follow in Report 4, §2.1 and §2.2, and all three use crow's-foot notation. It is split into seven subject areas so that every diagram stays readable. Entities are business concepts rather than database tables, with their business attributes and the key above the line; many-to-many relationships keep their own attributes on the relationship line (for example participates carries Team and Outcome), and purely technical tables such as refresh tokens and telemetry parts are omitted. The logical design of all 60 tables is given in Report 4, §2.")
for f, cap in [
    ("r3_cerd_account.png", "Conceptual ERD – accounts, profiles and social relations"),
    ("r3_cerd_training.png", "Conceptual ERD – techniques, lessons and training"),
    ("r3_cerd_match.png", "Conceptual ERD – matches, ratings and replays"),
    ("r3_cerd_economy.png", "Conceptual ERD – currencies, cosmetics, missions and achievements"),
    ("r3_cerd_moderation.png", "Conceptual ERD – reports, sanctions and appeals"),
    ("r3_cerd_coaching.png", "Conceptual ERD – telemetry, coaching and expert review"),
    ("r3_cerd_feed.png", "Conceptual ERD – posts, videos and messages"),
]:
    r.figure(f, cap, width_cm=15, max_height_cm=17)
r.p("**Entities Description**", bold=True)
ENT = [
    ("ACCOUNT", "A registered user identified by e-mail; holds the password hash, status (ACTIVE, SUSPENDED, BANNED) and verification state."),
    ("ROLE", "Player or Admin; each account has exactly one role."),
    ("VERIFICATION_CODE", "A one-time code for e-mail verification or password reset, with expiry and remaining attempts."),
    ("PLAYER_PROFILE", "Game identity of an account: display name, character, level, XP, Elo, onboarding choice and statistics."),
    ("TECHNIQUE", "A pickleball technique to be learnt (serve, drive, dink, volley, drop, reset, smash, footwork, approach, doubles coverage and transition, modifiers)."),
    ("LESSON", "A unit of the curriculum with code, order and publication status; it groups one or more drills that are published together."),
    ("DRILL", "An exercise that practises one technique; it holds the whole configuration of the exercise (target, reward, prerequisite, coach text), in a draft and a published version."),
    ("TRAINING_ATTEMPT", "One run of a drill by a player, with shots, hits, timing, stars and synchronisation receipt."),
    ("MATCH", "A bot, ranked, matched doubles or custom match with mode, kind, authority mode and status."),
    ("MATCH_GAME", "One game (1 to 3) of a match with the score of each team; a weak entity identified by match and game number."),
    ("RATING_CHANGE", "The Elo before, change and after of one player caused by one ranked match."),
    ("REPLAY", "The stored recording of a match (object key of its manifest)."),
    ("PLAYER_REPORT", "A complaint by one player about another player in a given match, with reason, status and decision."),
    ("SANCTION", "A suspension (with expiry) or ban applied to an account, with reason, actor and revocation data."),
    ("APPEAL", "A restricted player's written objection to one sanction, with status and decision."),
    ("CURRENCY", "In-game currency: Tokenpad (earned by playing) and Elite Token (credited with the membership)."),
    ("MEMBERSHIP_ORDER", "An order of the lifetime membership with the PayOS link, amount, bonus, status (PENDING, PAID, CANCELLED, EXPIRED) and payment time; a paid order makes the player a member."),
    ("COSMETIC", "A purchasable or granted item (paddle, outfit, hat) with price and currency."),
    ("MISSION", "A daily or weekly goal with target and reward."),
    ("ACHIEVEMENT", "A long-term goal (wins, matches, points, streaks) with a one-time reward."),
    ("TELEMETRY_SESSION", "The shot-level data captured during a drill or match and uploaded for analysis."),
    ("COACHING_RESULT", "The AI analysis of a telemetry session: findings, summary, recommendations and optional tier."),
    ("EXPERT_REVIEW", "An expert's labels for a telemetry session (level, errors, data group) used to evaluate the AI."),
    ("ADMIN", "An account with the Admin role, shown as a separate box in the coaching diagram for readability."),
    ("POST", "A message of a player in the social feed with text, audience and optional video and shared match replay."),
    ("VIDEO", "An uploaded MP4 file of a player; it is attached to at most one post."),
    ("COMMENT", "A comment of a player on a post."),
    ("CONVERSATION", "The direct-message thread between two players; each pair has at most one."),
    ("MESSAGE", "A direct message in a conversation, with the sender and the client message ID used for retries."),
]
r.table(["#", "Entity", "Description"], [[str(i + 1), a, b] for i, (a, b) in enumerate(ENT)],
        widths=[0.8, 3.6, 11.6], size=9, caption="Entities description")

r3_fr.write(r)
r3_tail.write(r)
out = r.save("Report3_Software Requirement Specification.docx", "Report 3 – Software Requirement Specification")
print(out, "tables", r.tbl_no, "figures", r.fig_no)
