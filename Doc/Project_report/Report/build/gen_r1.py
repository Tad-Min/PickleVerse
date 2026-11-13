"""Report 1 - Project Introduction."""
from report_lib import Report, PROJECT_NAME

CHANGES = [
    ("27/10/2026", "A", "Nguyễn Gia Huy", "v0.1 – Project introduction drafted from the capstone proposal for the Review 2 design brief (BM01)."),
    ("06/11/2026", "A", "Nguyễn Văn Thành Đạt", "v0.2 – First complete Report 1 on the official template for Review 3."),
    ("07/11/2026", "M", "Nguyễn Văn Thành Đạt", "v0.3 – Clarified the project code, the product scope and the target users."),
    ("11/11/2026", "M", "Nguyễn Gia Huy", "v1.0 – Revised after the internal review: completed the team table; rewrote the background, existing systems, business opportunity and vision; aligned FE-01 to FE-10 with the SRS; recorded the deviations from the proposal; added references."),
    ("11/11/2026", "M", "Nguyễn Gia Huy", "v1.1 – Team table: workflow owners aligned with the Review 2 commitment BM04 (W01 Thuận, W02 Huy, W03 Đạt, W04 Kiên)."),
    ("12/11/2026", "M", "Nguyễn Gia Huy", "v1.2 – Revised after the second internal review: workflow names and BM04 approval status as in the form; quick doubles, accuracy history and streak rewards described as implemented; new limitations LI-10 (no encryption at rest) and LI-11 (Android performance target and device testing); privacy note for mobile numbers; references [2]–[4] completed."),
    ("13/11/2026", "M", "Nguyễn Gia Huy", "v1.3 – Staging addresses after the HTTPS switch of 13/11/2026; LI-11 updated with the Android build of 12/11/2026."),
]

r = Report("Report1_Project Introduction.docx", "Report 1 – Project Introduction", CHANGES)

r.h1("II. Project Introduction")
r.h2("1. Overview")
r.h3("1.1 Project Information")
r.table(
    ["Item", "Value"],
    [
        ["Project name (English)", PROJECT_NAME],
        ["Project name (Vietnamese)", "PickleVerse – Game Huấn Luyện Kỹ Thuật và Thi Đấu Pickleball Nhiều Người Chơi Tích Hợp AI"],
        ["Project code", "PKV"],
        ["Group name", "GFA26SE111"],
        ["Software type", "3D multiplayer sports game for Windows desktop and Android, supported by a web API, a real-time dedicated game server, an AI coaching service and an administration web application"],
        ["Duration", "14 weeks of development, from 07/09/2026 (Week 1) to 13/12/2026 (Week 14); final defense in Week 15 (14–20/12/2026)"],
        ["Source repository", "https://github.com/Tad-Min/PickleVerse (private; access is granted to the supervisor and the review committee on request)"],
        ["Staging environment", "Admin web: https://tad-min.io.vn – Web API: https://tad-min.io.vn/api (HTTPS since 13/11/2026; HTTP on port 8080 before) – Dedicated game server: tad-min.io.vn, UDP 3669"],
    ],
    widths=[4.5, 11.5], first_col_bold=True, caption="Project information")

r.h3("1.2 Project Team")
r.p("The team consists of four software engineering students supervised by one lecturer. Every member owns one end-to-end review workflow, as committed in the Review 2 form BM04 (v1.0 of 07/11/2026, reviewers DucDNM2 and TruongLV11; the form records that it has not yet been approved at Review 2), and one or more technical areas; the leader additionally coordinates planning, meetings and submissions. Workflow ownership is separate from implementation effort: according to the repository history, Nguyễn Gia Huy and Nguyễn Văn Thành Đạt implemented the largest share of the code (Report 2, §4).")
r.table(
    ["Full Name", "Student ID", "Role", "Main responsibility", "Email", "Mobile"],
    [
        ["Lại Đức Hùng", "–", "Supervisor", "Supervises scope, schedule and report quality", "Hungld5@fe.edu.vn", "–"],
        ["Huỳnh Trung Kiên", "SE184454", "Leader", "Planning and tracking; admin web application; music and sound effects; owner of workflow W04 (review a report and publish a lesson)", "kienht7704@gmail.com", "–"],
        ["Nguyễn Văn Thành Đạt", "SE184450", "Member", "Web API and database; online multiplayer and dedicated server; AI coaching service; CI/CD; owner of workflow W03 (play online and save the result); support for W02", "nguyenvanthanhdat44@gmail.com", "–"],
        ["Nguyễn Gia Huy", "SE184439", "Member", "Unity gameplay, ball physics and rules; training and tutorial; bot behaviour; 3D characters, animation, courts and in-game UI; owner of workflow W02 (play a bot and get a tip); support for W01 and W03", "huyngse184439@fpt.edu.vn", "–"],
        ["Trần Nguyễn Hiếu Thuận", "SE184471", "Member", "Unity user interface (login, loading, settings); audience and character assets; test lead; owner of workflow W01 (learn and sync a drill); support for W04", "hieuthuan833@gmail.com", "–"],
    ],
    widths=[3.1, 1.8, 1.9, 5.2, 3.6, 1.2], size=9, caption="Project team")
r.p("Mobile numbers are kept in the team's internal contact list and shared with the supervisor directly; they are not printed in this report because the report is distributed to reviewers outside the team.")

r.h2("2. Product Background")
r.p("Pickleball has become one of the fastest-growing recreational sports in Vietnam. Local press reported that the number of regular players nearly doubled during 2024 to about 30,000 [1], that more than 200 new courts opened in Hanoi alone in the first seven months of 2024 [2], and that Vietnam was listed among the ten fastest-growing pickleball nations by registered-player growth on the DUPR rating platform [3]. The sport attracts office workers, university students and older adults because the court is small and the basic strokes are easy to start with.")
r.p("The growth, however, is not matched by structured learning. Most newcomers learn by imitating other players on the court and receive no feedback on their technique. Qualified coaches are scarce and private lessons are expensive, so beginners often reinforce incorrect fundamentals – an overhand or late serve, dinks that float too high, volleys taken inside the non-volley zone (the \"kitchen\") – which are difficult to correct later. Casual players also find it hard to meet partners of a similar level, and they cannot practise when a court is unavailable because of weather, cost or schedule. Recent reports that some courts in central Hanoi are now under-used [4] suggest that players who do not progress tend to drop the sport.")
r.p("The project idea was proposed by the team members, who are themselves beginner and casual players, and was registered as a student-initiated capstone topic under the supervision of Mr. Lại Đức Hùng. The proposal identifies Vietnamese beginners and casual players as the primary users and pickleball clubs, academies and community sport centres as potential organisational customers. PickleVerse addresses the problems above with a 3D game in which a player learns the rules and controls, repeats short graded drills, receives technique feedback computed from in-game telemetry, and applies the skills in matches against bots or other players.")

r.h2("3. Existing Systems")
r.p("The team studied three products that either simulate pickleball or help players improve their technique. They were selected because together they cover the three ideas that PickleVerse combines: an accessible pickleball game, a realistic training simulator, and AI-based technique analysis.")
r.h3("3.1 Pickleball Smash")
r.label("Link", "https://www.xbox.com/games/store/pickleball-smash/9N4DL8WL5DV2 [5]")
r.label("Overview", "An arcade pickleball game developed by 1PXL Games and published by GameMill Entertainment on 24 October 2023 for Nintendo Switch, PlayStation 4/5, Xbox One/Series X|S and PC (Steam) [5], [6].")
r.label("Actors", "Player (single player against the computer, or up to four players sharing one console).")
r.label("Main features", "Singles and doubles matches against computer opponents; local multiplayer; mini-games that each practise one shot; several playable characters and courts.")
r.label("Pros", "Simple controls and short sessions; mini-games show that a single activity can teach a single skill; available on many platforms.")
r.label("Cons", "Arcade physics rather than real pickleball technique; no lesson structure, scoring of technique or feedback; local multiplayer requires all players on the same console; no Vietnamese interface; no mobile version.")
r.h3("3.2 Eleven Pickleball")
r.label("Link", "https://www.meta.com/experiences/eleven-pickleball [7]")
r.label("Overview", "A virtual-reality pickleball simulator by For Fun Labs, the studio behind Eleven Table Tennis, released for Meta Quest headsets [7].")
r.label("Actors", "Player (single player against AI, or online against other players).")
r.label("Main features", "Physics-based paddle control with motion controllers; a programmable ball machine whose speed, spin and placement can be set for each shot; matches against AI bots; online 1v1 and 2v2 matches.")
r.label("Pros", "Realistic ball physics and paddle motion; the ball machine supports deliberate repetition of one shot; real online play.")
r.label("Cons", "Requires a VR headset, which few Vietnamese beginners own; no guided curriculum or star scoring; no technique diagnosis or recommendation; English interface.")
r.h3("3.3 PB Vision")
r.label("Link", "https://apps.apple.com/us/app/pb-vision/id6467020610 [8]")
r.label("Overview", "A mobile and web service that analyses videos of real pickleball games with computer vision and machine learning [8], [9].")
r.label("Actors", "Player (uploads a recorded game and reviews the analysis); club (records courts with an installed camera).")
r.label("Main features", "Shot speed, placement and error statistics; rally and shot breakdowns; heat maps and highlights; skill ratings.")
r.label("Pros", "Feedback is based on the player's real games; detailed statistics; integrates with club court cameras.")
r.label("Cons", "A full game must be filmed from a fixed angle with all four court corners visible; feedback arrives after the game, not during practice; no practice content when no court is available; advanced analysis is a paid service.")
r.h3("3.4 Comparison")
r.p("Table 3 compares the three products with PickleVerse against the criteria that matter to the target users.")
r.table(
    ["Criterion", "Pickleball Smash", "Eleven Pickleball", "PB Vision", "PickleVerse"],
    [
        ["Device needed", "Console or PC", "VR headset", "Phone and camera, real court", "Windows PC or Android phone"],
        ["Structured lessons with graded drills", "No (mini-games only)", "No (free ball machine)", "No", "Yes – tutorial and 15 lessons scored 0–3 stars"],
        ["Technique feedback", "No", "No", "Yes, after a filmed game", "Yes, after each drill and match, from in-game telemetry"],
        ["Practice without a court or partner", "Yes", "Yes", "No", "Yes, including offline training"],
        ["Online play and rating", "No online play", "Yes", "Rating only", "Yes – ranked singles with Elo, custom singles and doubles rooms"],
        ["Vietnamese interface", "No", "No", "No", "Yes"],
        ["Administration tools for an organisation", "No", "No", "Club integration", "Yes – user, content, match, moderation and analytics tools"],
    ],
    widths=[3.6, 2.8, 2.8, 3.0, 4.0], size=9, first_col_bold=True, caption="Comparison of existing systems with PickleVerse")

r.h2("4. Business Opportunity")
r.p("The comparison shows a gap between entertainment games, which are accessible but do not teach technique, and analysis tools, which give useful feedback but require a real court, a recorded game and in many cases payment. A Vietnamese beginner who wants to learn the fundamentals at home, at low cost and in Vietnamese currently has no suitable product.")
r.p("PickleVerse targets this gap. It runs on hardware the users already own (a Windows PC or an Android phone), teaches the official rules through a tutorial and graded drills, and turns each drill and match into feedback without any camera. Because the feedback is computed from the game's own telemetry, the cost of a coaching session is close to zero, which makes the product attractive to price-sensitive students and young workers. Online ranked matches with an Elo rating and a leaderboard keep players engaged after the learning phase and help them find opponents of a similar level, which is a common complaint in local player communities.")
r.p("For organisations, the administration web application lets a club or academy manage accounts, publish its own lessons and technique references, monitor matches and handle player reports. This creates a business-to-business direction described in the proposal: clubs could use PickleVerse as an inexpensive digital training and community tool for new members. Cosmetic items are already bought with an in-game currency; real-money payment through local wallets such as MoMo or VNPay is a post-capstone option and is excluded from this project (LI-02).")

r.h2("5. Software Product Vision")
r.p("**For** Vietnamese pickleball beginners and casual players **who** want to learn the rules and fundamental strokes without a coach, a partner or a court, **PickleVerse** is a 3D pickleball game for Windows and Android **that** teaches the rules through an interactive tutorial, trains each core technique with graded drills, gives technique feedback computed from gameplay telemetry, and lets players apply their skills in matches against adaptive bots and other players online. **Unlike** arcade pickleball games, which do not teach technique, and video-analysis services, which need a filmed game on a real court, **our product** combines practice, feedback and competition in one Vietnamese-language application that runs on devices the players already own, and gives clubs a web dashboard to manage their members and training content.")

r.h2("6. Project Scope & Limitations")
r.h3("6.1 Major Features")
r.p("The ten major features below define the scope of version 1.0. Report 3 refines each feature into use cases and functional requirements (FR-01 to FR-33), and the Review 2 commitment features F01–F18 are traced to them in Report 3, Appendix A.")
r.table(
    ["ID", "Feature", "Description"],
    [
        ["FE-01", "Account and onboarding", "Register with an email address, verify it with a six-digit one-time password (OTP), sign in and out, recover a forgotten password, edit the display name, choose a male or female character and choose a starting level (beginner or experienced) once. Configure audio, graphics, key bindings and the touch layout."],
        ["FE-02", "Tutorial and technique training", "A 17-step interactive tutorial on the court, the rules and the controls. Fifteen lessons in four groups (basic, control, advanced and doubles) train twelve techniques: serve, drive, dink, volley, power and cross-court modifiers, third-shot drop, reset, smash, footwork, approach, and doubles coverage and transition. Each drill is scored with 0–3 stars. Training also works offline after the first sign-in, and results are synchronised once when the game is online again."],
        ["FE-03", "Bot practice", "Singles matches against a computer opponent at three fixed levels (Easy, Medium, Hard) or in an adaptive mode whose behaviour-tree bot scales its shot choice, aggression and error rate to the player's Elo tier. The player chooses a professional or outdoor court and day or night lighting."],
        ["FE-04", "Online matches", "Ranked singles quick match that pairs players of similar Elo, with a 10-second ready check and a bot opponent after 180 seconds without a human match; quick doubles, an unrated 2v2 search for a solo player or a party of two that is filled only with human players; private singles and doubles rooms joined by room code or friend invitation. Matches run on an authoritative dedicated game server and support reconnection."],
        ["FE-05", "AI coaching", "After a drill or match, the AI coaching service analyses the shot telemetry and returns a summary, detected errors (for example a late serve or a dink that is too high) and at most three recommended drills."],
        ["FE-06", "Progress, rewards and shop", "Experience points and levels, lesson stars, best accuracy and the four most recent accuracy results of each lesson, technique mastery, a daily play streak (rewarded through the achievement for a 5-day streak and a weekly streak mission), daily and weekly missions, achievements, and the Tokenpad currency. Players buy and equip cosmetic paddles in the shop and view owned items in the collection."],
        ["FE-07", "Social, history and ranking", "Send, accept and decline friend requests; remove and block players; view match history and replays (pause, seek, 0.5×–2× speed); view the Elo leaderboard."],
        ["FE-08", "Reports, restrictions and appeals", "Report another player after a match. A restricted (suspended or banned) player can see the sanction decision and submit one appeal."],
        ["FE-09", "Administration web application", "Administrators manage accounts, roles and sanctions, reset player progress by group, manage lessons, technique references, cosmetics and missions, monitor and stop live matches, review player reports and appeals, and view analytics (active users, retention, popular lessons, technique errors and matchmaking health)."],
        ["FE-10", "Expert review of AI results", "Administrators acting as pickleball experts label recorded shots and export a labelled dataset. This tool is used to evaluate the AI coaching quality (Report 3, NFR-06) and is an addition to the proposal."],
    ],
    widths=[1.4, 3.6, 11.0], size=9.5, first_col_bold=True, caption="Major features of PickleVerse 1.0")

r.h3("6.2 Limitations & Exclusions")
r.p("The following items are deliberately excluded from version 1.0, or deviate from the registered proposal. Each deviation is recorded with its reason so that requirement changes can be evaluated against it.")
r.table(
    ["ID", "Limitation or exclusion"],
    [
        ["LI-01", "Coaching uses in-game telemetry only. Analysis of real swing video, augmented reality and a separate coach portal are excluded."],
        ["LI-02", "Ranked seasons, tournaments, spectator mode, club management, voice or text chat and real-money payments are excluded. The Tokenpad currency is earned only by playing. The Elite Token currency exists in the data model but has no earning source in version 1.0, so items priced only in Elite Token cannot be bought."],
        ["LI-03", "Only Ranked Singles and Ranked Bot matches change the Elo rating (K-factor 32, floor 100). Doubles and private rooms are unrated. The proposal described an Elo update after every match; rating only one-to-one ranked results keeps the rating meaningful."],
        ["LI-04", "Offline mode covers training only, after at least one online sign-in on the device. Bot matches, online matches, the shop and the administration tools need a connection."],
        ["LI-05", "The proposal's \"self-assessment with a starting-tier classifier\" is implemented as a one-time choice between Beginner and Experienced. The trained tier model in the AI service is gated: its estimate is shown only when the model passes the accuracy target in NFR-06; otherwise the rule-based result is used."],
        ["LI-06", "Release platforms are Windows 10 or later and Android 8.0 (API 26) or later on ARM64. An iOS build is outside the delivered scope because the team has no Apple build machine and developer account; the project settings keep iOS 15.0 as the minimum for future work (the proposal named iOS 14)."],
        ["LI-07", "The staging deployment runs one dedicated game-server container, so one server-authoritative match can run at a time; when the slot is busy players are told that the server is busy. The proposal target of 500 concurrent matches requires a server pool and is treated as a scalability goal, verified by load testing of the API (Report 3, NFR-04)."],
        ["LI-08", "Technology deviations from the proposal: the web API uses ASP.NET Core (.NET 10) instead of Spring Boot or FastAPI because the team shares C# with Unity; real-time networking uses Photon Fusion 2 (shared lobby plus a dedicated server) instead of Netcode for GameObjects, after Netcode with Relay showed unstable connections during the October prototype; the game interface uses uGUI instead of UI Toolkit; ball flight uses a custom fixed-step simulation instead of PhysX for deterministic replay and server authority. The reasons are detailed in Report 4, §1.4."],
        ["LI-09", "The game interface is in Vietnamese only. The administration web application is in Vietnamese, and project documents are in English."],
        ["LI-10", "The proposal stated that stored data would be encrypted with AES-256. Version 1.0 protects secrets by one-way hashing instead (BCrypt for passwords, SHA-256 for refresh tokens, HMAC for verification and reset codes); the database files, backups and replay objects are not encrypted at rest. Disk encryption of the production host is planned before release (Report 4, §3.10)."],
        ["LI-11", "The proposal targeted 60 FPS on all devices. The SRS keeps 60 FPS for the reference PC but sets an average of 30 FPS for a mid-range Android phone (NFR-03), because mobile rendering removes HDR, MSAA and most shadows to stay within the phone's thermal budget; the game still requests up to 60 FPS on Android. The Android APK was built on 12/11/2026, but on 13/11/2026 it has not yet been measured or smoke-tested on a physical device (test cases TC-N03 and TC-N10 are pending in Report 5)."],
    ],
    widths=[1.4, 14.6], size=9.5, first_col_bold=True, caption="Limitations and exclusions")

r.h2("7. References")
r.references([
    "Tuoi Tre News, \"Rapid rise of pickleball in Vietnam marked by growing pains in organization,\" 15 Mar. 2025. https://tuoitrenews.vn/news/sports/20250315/rapid-rise-of-pickleball-in-vietnam-marked-by-growing-pains-in-organization/85232.html (accessed 11 Nov. 2026).",
    "T. Nga, \"Vietnam catches pickleball fever,\" VnExpress International, 23 Jul. 2024. https://e.vnexpress.net/news/life/trend/vietnam-catches-pickleball-fever-4771953.html (accessed 12 Nov. 2026).",
    "Voice of Vietnam (VOV), \"Vietnam among world's top 10 fastest-growing pickleball nations,\" 12 Oct. 2026. https://english.vov.vn/en/sports/vietnam-among-worlds-top-10-fastest-growing-pickleball-nations-post1340523.vov (accessed 12 Nov. 2026).",
    "Dan Tri International, \"Hanoi pickleball court owners face slump in demand,\" 8 Apr. 2026. https://dtinews.dantri.com.vn/print/20260408102840844.htm (accessed 12 Nov. 2026).",
    "Microsoft Store, \"Pickleball Smash.\" https://www.xbox.com/games/store/pickleball-smash/9N4DL8WL5DV2 (accessed 11 Nov. 2026).",
    "Gematsu, \"Pickleball Smash – release dates.\" https://www.gematsu.com/games/pickleball-smash (accessed 11 Nov. 2026).",
    "Meta Quest Store, \"Eleven Pickleball,\" For Fun Labs. https://www.meta.com/experiences/eleven-pickleball (accessed 11 Nov. 2026).",
    "Apple App Store, \"PB Vision,\" Pickleball Vision AI Inc. https://apps.apple.com/us/app/pb-vision/id6467020610 (accessed 11 Nov. 2026).",
    "Ministry of Sport, \"AI now assisting pickleball players improve their game with the launch of PB Vision.\" https://ministryofsport.com/ai-now-assisting-pickleball-players-improve-their-game-with-the-launch-of-pb-vision/ (accessed 11 Nov. 2026).",
    "GFA26SE111, \"PickleVerse – Capstone Project Register (proposal),\" FPT University, 2026.",
])

out = r.save("Report1_Project Introduction.docx", "Report 1 – Project Introduction")
print(out, "tables:", r.tbl_no, "figures:", r.fig_no)
