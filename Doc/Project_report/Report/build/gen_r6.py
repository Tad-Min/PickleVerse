"""Report 6 - Release Package & User Guides."""
from report_lib import Report

CHANGES = [
    ("11/11/2026", "A", "Nguyễn Gia Huy", "v1.0 – First release package description, installation guides (player, server, development) and user manual organised by the four review workflows, with real screenshots of the game and the admin web."),
    ("12/11/2026", "M", "Nguyễn Gia Huy", "v1.1 – Revised after the second internal review: Android controls corrected; time-outs removed; quick doubles and the lobby friends column added; workflow names as in BM04; deployment section states that the pipeline has not yet passed and staging is released manually; package counts and screenshots updated; Android build marked as not yet tested on a device."),
    ("13/11/2026", "M", "Nguyễn Gia Huy", "v1.2 – Updated after the fixes of 12–13/11/2026 (main @ d9200cc): the deployment section describes the pipeline as it runs and the new deployment script with its database dump; HTTPS address of the staging API and admin web with the Caddy reverse proxy; Android APK of 12/11/2026 with its limitation; package counts for the documents of 13/11/2026."),
]

r = Report("Report6_Software User Guides.docx", "Report 6 – Software User Guides", CHANGES)
r.h1("II. Release Package & User Guides")

# ================================================================== 1
r.h2("1. Deliverable Package")
r.p("Table %d lists the items of the release package of PickleVerse 1.0. Source code and documents are versioned in the Git repository https://github.com/Tad-Min/PickleVerse; this package corresponds to the commit main @ d9200cc (13/11/2026) for the software and to the documents of 13/11/2026 (Reports 1–5 v1.3, Report 6 v1.2 and Report 7 assembled from them). Since main @ 5963501 (11/11/2026) the commits repaired the delivery pipeline, moved the client and the server to HTTPS and removed the credentials from the settings file; the gameplay code is unchanged. The final release will be tagged v1.0 at milestone M08." % (r.tbl_no + 1))
r.table(["No.", "Deliverable Item", "Description"], [
    ["1", "Project Schedule / Tracking", "Doc/Project_report/Report/PickleVerse_Project_Schedule.xml (MS Project XML: 82 tasks, 4 resources, 61 assignments, % complete as of 13/11/2026) and PickleVerse_Project_Tracking.xlsx (sheets WBS, Backlog, Issues, Defects, Q&A)"],
    ["2", "Project Backlog", "Sheet Backlog of the tracking workbook: 21 product and iteration backlog items for Iterations 4 to 6 with priority, owner, estimate and status"],
    ["3", "Source Codes", "Unity client and dedicated server: My project/ (Unity 6000.6.2f1, 13 runtime assemblies); Web API: src/Backend (ASP.NET Core .NET 10, 4 projects); AI coaching service: src/AiService (Python 3.12, FastAPI); admin web: src/Frontend (React 18, TypeScript, Vite); tests: src/Testing; deployment: deploy/, docker-compose*.yml, .github/workflows/dockerhub.yml"],
    ["4", "Database Script(s)", "src/Database.sql – single script with the schema (51 tables, 71 foreign keys) and seed data (roles, currencies, 15 lessons, 12 techniques, 3 paddles, 17 missions, 10 achievements, 10 ranked bot accounts) executed in one transaction"],
    ["5", "Final Report Document", "Report7_Final Project Report.docx, which consolidates Reports 1–6; the individual Reports 1–6 (.docx) and their PDF exports in Doc/Project_report/Report/pdf"],
    ["6", "Test Cases Document", "Report5_Test Documentation.docx, Report5_Test Report.xlsx (118 cases with the results of Round 1 and of the CI run of 13/11/2026) and Report5_Unit Test.xlsx (10 functions, 66 decision-table cases); evidence files in Doc/Project_report/Report/evidence"],
    ["7", "Defects List", "Sheet Defects of the tracking workbook (21 defects with severity, owner and status: 7 fixed and awaiting retest, 1 accepted as designed and 13 open, one of them partly fixed); also listed in the Test Report workbook"],
    ["8", "Issues List", "Sheet Issues of the tracking workbook (15 issues with priority, owner, open and close dates; 4 closed)"],
    ["9", "Slide", "Defense presentation – prepared for milestone M10 (Week 15, 14–20/12/2026)"],
    ["10", "Server images", "Docker Hub repository tadminm/pickelverse with four images per release: api-, ai-, server- (Unity Linux dedicated server) and web-<release>. Since 13/11/2026 the pipeline builds the images that a push changes, publishes them with the tag <commit>-<run>-<attempt> and deploys all four by digest; the four earlier staging releases (08–10/11/2026) were built by hand and tagged <component>-manual-<yyyymmdd-hhmmss>"],
    ["11", "Game client builds", "Windows 64-bit build (PickleVerse.exe, Mono) and Android build (PickleVerse.apk, IL2CPP, ARM64) produced from the Unity project for the release; the scene list and settings are defined in My project/ProjectSettings. An Android APK was built on 12/11/2026 from main @ 5963501 (190.5 MB, signed with the debug key); it predates the HTTPS switch and has not been tested on a physical device, so the release APK has to be built again from main @ d9200cc (Report 1, LI-11; Report 5, D-21)"],
    ["12", "Design sources", "PlantUML sources and rendered diagrams (Doc/Project_report/Report/diagrams), the editable draw.io file PickleVerse_UseCase_ConceptualERD.drawio and the report build scripts (Doc/Project_report/Report/build)"],
], widths=[1.0, 3.6, 11.4], size=8.5, first_col_bold=False, caption="Deliverable package of PickleVerse 1.0")

# ================================================================== 2
r.h2("2. Installation Guides")
r.h3("2.1 System Requirements")
r.table(["Component", "Requirement"], [
    ["Player PC", "Windows 10 or 11, 64-bit; DirectX 11 graphics card; recommended Intel Core i5 (10th generation) or equivalent, GTX 1650 or equivalent, 16 GB RAM (reference device of NFR-03); keyboard and mouse; broadband Internet for online features; outbound UDP allowed for Photon and port 3669"],
    ["Player phone", "Android 8.0 (API level 26) or later, ARM64 processor; touch screen; Wi-Fi or 4G connection for online features"],
    ["Administrator", "Latest Google Chrome or Microsoft Edge on a desktop screen of at least 1366 × 768; an account with the Admin role"],
    ["Server host", "Linux amd64 (Ubuntu 24.04 used for staging) with Docker Engine and Docker Compose v2 that supports `up --wait-timeout`; recommended 4 vCPU and 8 GB RAM; the game-server container is limited to 2 CPUs and 2 GB RAM by default; open ports 80/tcp (admin web), 8080/tcp (Web API) and 3669/udp (dedicated game server)"],
    ["External services", "Photon Cloud application for Fusion 2.1.3 (App ID in the Fusion settings of the Unity project); a Gmail account with an app password for SMTP (STARTTLS, port 587)"],
    ["Developer PC", "Unity Hub with Unity 6000.6.2f1 (Windows, Linux Dedicated Server and Android build support); .NET 10 SDK; Python 3.12; Node.js 22 with npm; Docker Desktop; Git; Java 17 only for rendering the report diagrams"],
], widths=[3.2, 12.8], size=8.5, first_col_bold=True, caption="System requirements")

r.h3("2.2 Installation Instruction")
r.h4("2.2.1 Install the game on Windows")
r.numbered([
    "Copy the Windows release folder (PickleVerse_Windows) to the PC, for example to C:\\Games\\PickleVerse. The folder contains PickleVerse.exe, PickleVerse_Data and the Unity runtime files; keep them together.",
    "Run PickleVerse.exe. When Windows Defender Firewall asks, allow the game on private networks so that UDP traffic to Photon and to the game server is possible.",
    "The Login screen opens (Figure %d). The first sign-in needs an Internet connection; afterwards training can also be started offline from the same PC." % (r.fig_no + 1),
])
r.figure("01_login.png", "First screen after installation – Login", width_cm=13)
r.h4("2.2.2 Install the game on Android")
r.numbered([
    "Copy PickleVerse.apk to the phone or download it from the release location shared by the team.",
    "In Settings → Security (or Apps → Special access), allow installing apps from the file manager or browser used to open the APK.",
    "Open the APK and select Install. Start PickleVerse from the app drawer; the game runs in landscape mode with touch controls (joystick and shot buttons).",
])
r.p("Note: on 13/11/2026 the Android build has not yet been installed and tested on a physical phone; these steps are the standard Android procedure and will be confirmed in test Round 2 (TC-N10). The APK built on 12/11/2026 still contains the former http:// address and forbids cleartext HTTP, so it cannot sign in; use an APK built from main @ d9200cc or later, which calls https://tad-min.io.vn (Report 5, D-21). The offline training mode does not need the server.")
r.h4("2.2.3 Deploy the server (CI/CD pipeline and manual release)")
r.p("The deployment is automatic: every push to the main branch runs the GitHub Actions workflow \"Build, publish and deploy PickleVerse\" (.github/workflows/dockerhub.yml) with the stages below. **Current status:** the pipeline passes since 13/11/2026 (runs 27, 28 and 30), and run 30 deployed the staging release that is running now. The 29 other runs did not pass: configuration failures on 05/11/2026, the billing lock of the GitHub account from 06/11 to 11/11, failures of the dedicated-server build on 11–12/11, one failed FastAPI test and two failed deployment steps. A push that changes only documents builds and deploys nothing, and a commit whose message contains [skip ci] does not start the workflow at all. If the pipeline is unavailable, the four images can still be built on a developer PC, pushed to Docker Hub with the tag <component>-manual-<yyyymmdd-hhmmss> and started on the host by hand with the same .env file, as was done on 08–10/11/2026.")
r.numbered([
    "**Plan** – reads the release that is deployed on the host and compares it with the pushed commit to select the checks and the images that the changed files need (deploy/ci/plan-release.py).",
    "**Checks** – as selected by the plan: the twelve IntegrationChecks groups against PostgreSQL 16 and Redis 7.4 service containers, the FastAPI tests and the type check of the admin web. The job \"Validate\" passes only when every selected check passed.",
    "**Build Unity Linux Dedicated Server** – builds the dedicated server with GameCI (unity-builder) using the Unity licence stored in the repository secrets.",
    "**Publish** – removes the credentials from the API settings files, builds and pushes the changed images among api, ai, server and web to Docker Hub tadminm/pickelverse with the tag <component>-<commit>-<run>-<attempt>, then starts the four image digests with Docker Compose on the runner and checks the health endpoints (smoke test).",
    "**Deploy** – over SSH (deploy/ci/ssh-deploy.sh): locks the release on the host, takes a PostgreSQL dump into .deploy/backups and checks it with pg_restore, copies docker-compose.linux.yml and src/Database.sql to the deployment root, writes the four image digests into the host .env file, runs `docker compose up -d --wait` and records the deployed release.",
])
r.p("Before the first deployment the administrator of the host prepares the deployment root and creates the host `.env` file from deploy/linux/runtime.env.example and installs Caddy with deploy/linux/Caddyfile, which serves the public HTTPS address, forwards /api and /health/ready to the API and every other path to the admin web. The variables are listed in Table %d; secret values are never committed to the repository. The workflow itself needs the repository secrets SSH_HOST, SSH_PORT, SSH_PASSWORD, SSH_KNOWN_HOSTS, DOCKERHUB_TOKEN, UNITY_EMAIL, UNITY_PASSWORD and UNITY_LICENSE (or UNITY_SERIAL), and the variables PKV_DEPLOY_ROOT (or PKV_DATA_ROOT), SSH_USER and DOCKERHUB_USERNAME of the GitHub environment \"development\"." % (r.tbl_no + 1))
r.table(["Variable", "Purpose"], [
    ["PKV_API_IMAGE, PKV_AI_IMAGE, PKV_WEB_IMAGE, PKV_GAME_SERVER_IMAGE", "Image references by digest; the deploy job writes them"],
    ["PKV_DATA_ROOT", "Absolute path of the host data directories (PostgresData, RedisData, S3Data, AiService/models, GameServerData)"],
    ["POSTGRES_USER, POSTGRES_PASSWORD", "User name and password of the PostgreSQL database"],
    ["JWT_KEY", "Signing key of the access tokens (at least 32 random characters)"],
    ["SMTP_USERNAME, SMTP_APP_PASSWORD, SMTP_FROM_EMAIL", "Gmail account, app password and sender address for OTP e-mails"],
    ["S3_ACCESS_KEY, S3_SECRET_KEY", "Credentials of the S3-compatible replay storage"],
    ["AI_SERVICE_KEY", "Shared key between the Web API worker and the AI coaching service (X-AI-Key)"],
    ["PKV_GAME_SERVER_KEY", "Service key used by the dedicated server to call the Web API"],
    ["API_PORT, WEB_PORT", "Local ports of the API (8080) and the admin web (3000); both are bound to 127.0.0.1 and published by Caddy"],
    ["PGADMIN_EMAIL, PGADMIN_PASSWORD, PGADMIN_BIND, PGADMIN_PORT", "Account and local address of the pgAdmin container (127.0.0.1:15050)"],
    ["POSTGRES_PORT, POSTGRES_MAX_POOL_SIZE, REDIS_PORT, S3_PORT", "Internal ports and the database connection pool size"],
    ["PKV_GAME_CPUS, PKV_GAME_MEMORY", "Resource limits of the dedicated game server (default 2 CPUs, 2 GB)"],
    ["AI_COACHING_CALIBRATION_SHA256", "Hash of the approved coaching calibration file; the service refuses a different file"],
], widths=[6.0, 10.0], size=8.5, caption="Server environment variables (host .env)")
r.h4("2.2.4 Initialise the database and create the first administrator")
r.numbered([
    "On first start, the PostgreSQL container runs src/Database.sql on the empty data volume. It creates all 51 tables and the seed data in one transaction. An existing volume is never re-initialised; use a new volume name to start from an empty database in development.",
    "To use an existing PostgreSQL server instead, create an empty database and run `psql -v ON_ERROR_STOP=1 -d <database> -f src/Database.sql` once.",
    "The script creates no administrator account. Register a normal account in the game, verify its e-mail, then grant it the Admin role once with SQL: `UPDATE Users SET RoleId = (SELECT RoleId FROM Roles WHERE RoleName = 'Admin') WHERE Email = '<e-mail>';`. Further administrators are assigned from the Users page of the admin web (FR-29).",
    "Check the deployment: `GET https://<host>/health/ready` must return HTTP 200 with the status ready, and https://<host>/ must open the admin web and accept the administrator account. Ports 8080 and 3000 are bound to 127.0.0.1 and must not answer from another computer.",
])
r.h4("2.2.5 Set up a local development environment")
r.numbered([
    "Clone the repository and copy `.env.example` to `.env`; fill JWT_KEY, the SMTP values and AI_SERVICE_KEY.",
    "Start the back-end services with `docker compose up -d --build`. The development stack exposes PostgreSQL on 127.0.0.1:15432, pgAdmin on 15050, S3 storage on 19000, Redis on 16379 and the Web API on 8080; the AI service is reachable only inside the Docker network.",
    "Admin web: in src/Frontend run `npm install` and `npm run dev`, then open http://localhost:5173.",
    "Unity: open the folder My project with Unity 6000.6.2f1 from Unity Hub. Select Assets/Resources/BackendApiSettings.asset and set **Base Url** to the API address (http://localhost:8080 for local development; the committed setting is https://tad-min.io.vn since 13/11/2026). Open the scene Assets/Scenes/UI/Login.unity and press Play.",
    "Tests: run the Unity EditMode tests in Window → General → Test Runner (or `unity command test run --mode editor` with the Unity CLI), `python -m pytest -q` in src/AiService, and `dotnet run --project src/Testing/Backend` for the integration checks.",
])

# ================================================================== 3
r.h2("3. User Manual")
r.h3("3.1 Overview")
r.p("PickleVerse has two applications. Players use the game client on Windows or Android to learn the rules, train techniques, play bot and online matches, receive AI coaching and follow their progress. Administrators use the admin web application to manage accounts, training content, matches, moderation, analytics and expert labelling. Figure %d shows how a player moves between the screens of the game; the four sections that follow describe the four end-to-end workflows committed at Review 2, each owned by one team member." % (r.fig_no + 1))
r.figure("r3_screenflow_game.png", "Screens of the game client and the navigation between them", width_cm=16, max_height_cm=17)
r.table(["Workflow", "Purpose", "Main screens", "Owner (BM04)"], [
    ["W01 – Learn and sync a drill", "Create an account, learn the rules and practise a drill online or offline", "Login, Register, Tutorial, Training Academy, Drill Result", "Trần Nguyễn Hiếu Thuận"],
    ["W02 – Play a bot and get a tip", "Play a singles match against a bot and read the AI coaching", "Mode Selection, Bot Match Setup, Bot Match, Match Result, Coaching", "Nguyễn Gia Huy"],
    ["W03 – Play online and save the result", "Play ranked singles, quick doubles or a private room with friends on the dedicated server; the result is saved once", "Online Lobby, Ready Check, Multiplayer Court, History", "Nguyễn Văn Thành Đạt"],
    ["W04 – Review a report and publish a lesson (administrator)", "Handle reports and appeals and publish lessons; also manage users, matches and analytics", "Admin web: Dashboard, Users, Moderation, CMS, Matches, Analytics", "Huỳnh Trung Kiên"],
], widths=[4.0, 5.2, 4.4, 2.4], size=8.5, caption="Workflows of the user manual")
r.p("**Controls.** Table %d summarises the default controls. Keys can be changed in Settings → Controls; the size, opacity, left-handed mode and position of the touch controls are set in the same tab. During a match the shot cue under the player shows the keys of the shot being prepared, and the camera keeps the player above the cue (PC) or the touch controls (phone)." % (r.tbl_no + 1))
r.table(["Action", "PC (keyboard)", "Android (touch)"], [
    ["Move / run", "W, A, S, D / hold Left Shift", "Drag the joystick on the left / hold the CHẠY button on the right"],
    ["Drive (and serve)", "J – hold and release", "Hold and release DRIVE"],
    ["Dink", "K – hold and release", "Hold and release DINK"],
    ["Volley", "L – hold and release", "Hold and release VOLLEY"],
    ["Drop / Smash / Reset", "J+K / J+L / K+L", "While holding, drag up: DINK → DROP, DRIVE → SMASH, VOLLEY → RESET"],
    ["Add power / aim cross-court", "Hold U / hold I while releasing the shot", "While holding, drag right (LỰC) / drag left (CHÉO); for the serve drag DRIVE left or right"],
    ["Cancel the prepared shot", "Backspace", "Drag down (HỦY)"],
    ["Settings / help / confirm", "Esc / F1 / Enter", "CÀI ĐẶT button / help button / tap"],
], widths=[4.0, 5.0, 7.0], size=8.5, caption="Default controls")
r.p("The shot is decided when the first shot key is released, so press the key early and release it just before the ball reaches the paddle. A release that is too early or too late produces the feedback \"Bạn thả nút hơi sớm/muộn\".")

# ---------------------------------------------------------------- W01
r.h3("3.2 Workflow W01 – Learn and Sync a Drill")
r.p("**Purpose.** A new player creates an account, learns the court, rules and controls with the tutorial, then practises the techniques with graded drills. Training also works offline after the first sign-in, and offline results are synchronised once when the game is online again. Figure %d shows the activity flow." % (r.fig_no + 1))
r.figure("r4_act_w01.png", "Workflow W01 – Learn and sync a drill", width_cm=14, max_height_cm=17)
r.h4("Step 1 – Register and verify the e-mail")
r.numbered([
    "On the Login screen select **Đăng ký** (Register).",
    "Enter the e-mail address, a password of at least 6 characters and the same password again, then select **TẠO TÀI KHOẢN**. Invalid input is shown in red under the form (Figure %d)." % (r.fig_no + 2),
    "Open the mailbox (also check the spam folder) and copy the six-digit code. Enter it on the verification page and select **XÁC THỰC**. If the code does not arrive, select **Gửi lại mã xác thực** after 60 seconds.",
])
r.figure("29_register.png", "Register screen", width_cm=12)
r.figure("32_register_validation.png", "Validation message on the Register screen", width_cm=12)
r.figure("30_verify_email.png", "E-mail verification with the six-digit code", width_cm=12)
r.h4("Step 2 – Sign in and choose the experience level")
r.numbered([
    "Enter the e-mail and password and select **ĐĂNG NHẬP**. Forgotten passwords are reset with **Quên mật khẩu** (Figure %d): enter the e-mail, then the code received and a new password." % (r.fig_no + 1),
    "At the first sign-in the game asks \"Bạn đã quen với pickleball chưa?\". Choose **Người mới** to start the tutorial or **Đã thành thạo** to go to the Menu. The choice cannot be changed later.",
])
r.figure("31_forgot_password.png", "Forgot password screen", width_cm=12)
r.h4("Step 3 – Complete the tutorial")
r.p("The tutorial has 17 short steps about the court, faults, the two-bounce rule, the non-volley zone and the shot keys. Read each step, select **TIẾP TỤC** (Enter) to continue, **XEM LẠI** to replay the demonstration or **ĐỂ SAU** to leave and finish later from Mode Selection.")
r.figure("33_tutorial.png", "Tutorial – step 1 of 17", width_cm=13)
r.figure("34_tutorial_step.png", "Tutorial – fault when the ball does not cross the net", width_cm=13)
r.h4("Step 4 – Practise a drill")
r.numbered([
    "From the Menu select **CHƠI NGAY** → **Luyện tập**. The academy lists 15 lessons in the groups Basic, Control, Advanced and Doubles; locked lessons open after their prerequisite is passed.",
    "Select **LUYỆN TẬP** on a lesson to read the briefing: target (for example 6 of 8 serves), first-pass reward and the star rules. Select **BẮT ĐẦU**.",
    "Hit the shots shown by the coach panel. After the last shot the result shows the number of successful shots, the accuracy, the mean timing error, the practice time and the most frequent error with a tip. Stars: 1 star when the target is reached, 2 stars at 80%, 3 stars at 90% with timing ≤ 0.16 s.",
    "Select **LUYỆN LẠI** to retry, **BÀI TIẾP THEO** for the next lesson, or **DANH SÁCH BÀI** for the catalogue. A failed attempt costs nothing.",
])
r.figure("08_lesson_catalog.png", "Lesson catalogue", width_cm=13)
r.figure("04_training_entry.png", "Lesson briefing with target and star rules", width_cm=13)
r.figure("07_training_finished.png", "Drill result", width_cm=13)
r.h4("Step 5 – Train offline and synchronise")
r.numbered([
    "Without a connection, open the game and select **Luyện tập ngoại tuyến** on the Login screen. This button appears only on a device where the account has signed in before.",
    "Practise as in Step 4. The banner \"Đang luyện tập ngoại tuyến · n mục chờ đồng bộ\" shows how many results wait for synchronisation; bot and online matches and the shop are disabled.",
    "When the Internet is available again, select **Đăng nhập** on the banner. Pending results are sent and applied once; **Chi tiết** shows rejected items, if any.",
])
r.figure("02_menu_offline.png", "Menu in offline training mode", width_cm=13)
r.figure("03_progression.png", "Training progress after practising", width_cm=13)

# ---------------------------------------------------------------- W02
r.h3("3.3 Workflow W02 – Play a Bot and Get a Tip")
r.p("**Purpose.** The player applies the techniques in a singles match against a computer opponent and receives AI coaching based on the shots of the match. Figure %d shows the flow." % (r.fig_no + 1))
r.figure("r4_act_w02.png", "Workflow W02 – Play a bot and get a tip", width_cm=14, max_height_cm=17)
r.h4("Step 1 – Set up the match")
r.numbered([
    "In the Menu select **CHƠI NGAY** → **Đấu với bot**.",
    "Choose the difficulty (Dễ, Vừa, Khó, or **Tự động**, the adaptive option that follows your Elo), the time of day (Ban ngày / Ban đêm) and the court (Chuyên nghiệp / Ngoài trời), then select **VÀO SÂN**.",
])
r.figure("16_mode_selection.png", "Mode selection", width_cm=13)
r.figure("17_bot_setup.png", "Bot match setup", width_cm=13)
r.h4("Step 2 – Play the match")
r.numbered([
    "After the introduction, serve within 10 seconds: hold J (DRIVE on a phone) behind the baseline and release to serve diagonally over the non-volley zone. The hint on the left and the shot cue under the player show what to press.",
    "Let the return and the third shot bounce (two-bounce rule); afterwards volley with L only outside the non-volley zone. Faults appear as a banner, for example \"Lỗi • Bóng ra ngoài\".",
    "A game is won at 11 points with a 2-point lead and the match at two games; in the third game the players change ends at 6 points. There are no time-outs; Esc opens the settings and F1 the help.",
])
r.figure("18_botmatch_intro.png", "Bot match – introduction", width_cm=13)
r.figure("19_botmatch_rally.png", "Bot match – serving; the shot cue under the player shows J + I (cross-court serve)", width_cm=13)
r.h4("Step 3 – Read the result and the coaching")
r.numbered([
    "The result shows the winner and the game score. A win gives 100 Tokenpad and 10 XP, a loss 50 Tokenpad and 5 XP; practice bot matches do not change Elo.",
    "Select **Xem phân tích** to open the coaching popup: number of shots, contacts and serves, the main errors and up to three recommended drills. Select **Luyện ngay** to open the recommended lesson.",
    "Open **LỊCH SỬ** in the Menu to see the match in the history list.",
])
r.figure("20_botmatch_victory.png", "Match result", width_cm=13)
r.figure("21_ai_analysis.png", "AI coaching summary with a recommended drill", width_cm=13)
r.figure("12_history.png", "Match history", width_cm=13)

# ---------------------------------------------------------------- W03
r.h3("3.4 Workflow W03 – Play Online and Save the Result")
r.p("**Purpose.** The player plays against other people: a ranked singles match that changes Elo, an unrated quick doubles match, or a private singles or doubles room with friends. Online matches run on the dedicated server, which decides the score, and the result is saved once. Figure %d shows the flow." % (r.fig_no + 1))
r.figure("r4_act_w03.png", "Workflow W03 – Play online and save the result", width_cm=14, max_height_cm=17)
r.h4("Step 1 – Find a ranked match")
r.numbered([
    "Select **CHƠI NGAY** → **Đấu online** → **ĐẤU ĐƠN 1v1**, then **Tìm trận**. The lobby shows \"Đang tìm đối thủ trực tuyến…\" and the waiting time; **Hủy** stops the search.",
    "When an opponent with a similar Elo is found, accept within 10 seconds. If nobody is found within 180 seconds, a ranked bot is offered instead.",
    "If the dedicated server is busy, the lobby shows \"Máy chủ đang bận • Đang chờ lượt thi đấu…\" and continues automatically when the server is free.",
])
r.figure("28_searching.png", "Searching for a singles opponent (layout preview rendered in the Unity Editor)", width_cm=13)
r.h4("Step 2 – Play quick doubles or with friends in a private room")
r.numbered([
    "Quick doubles: select **ĐẤU ĐÔI 2v2** in the online lobby and **Tìm trận**, alone or after a friend has joined your party. The game fills two teams of two with other searching players; there is no bot, and the match does not change Elo. If no teams are found, the search ends with \"Hết thời gian tìm trận. Hãy thử lại.\"",
    "Private room: select **Tạo phòng** in Mode Selection and choose 1v1 or 2v2. Share the room code with **SAO CHÉP**, or open the friends column on the right of the lobby and invite a friend who is in the lobby.",
    "Friends type the code in **Mã phòng** and select **THAM GIA BẰNG MÃ**, or accept the invitation. The host starts the match. Private matches do not change Elo.",
])
r.figure("27_online_lobby.png", "Online lobby – doubles room with the room code and the friends column (layout preview rendered in the Unity Editor with sample friend data)", width_cm=13)
r.figure("13_social.png", "Friends drawer used to find and invite friends", width_cm=13)
r.h4("Step 3 – Play, reconnect and see the result")
r.numbered([
    "Play as in a bot match. If the connection drops, the game reconnects automatically for up to 30 seconds and you continue with the server score.",
    "If you leave or cannot reconnect in time, the match ends as interrupted and gives no Elo or reward.",
    "At the end the result shows the Elo change for ranked matches. The match appears in **LỊCH SỬ**; from there you can watch the replay or report a player (**Tố cáo**).",
])
r.figure("10_multiplayercourt_editmode.png", "Multiplayer court", width_cm=13)
r.figure("14_leaderboard.png", "Elo leaderboard", width_cm=13)

# ---------------------------------------------------------------- W04
r.h3("3.5 Workflow W04 – Review a Report and Publish a Lesson (Administrator)")
r.p("**Purpose.** An administrator keeps the community and the training content healthy: handles player reports and appeals, manages accounts and roles, publishes lessons, monitors matches and reads the analytics. Figures %d and %d show the moderation and content flows. Every administrative change is recorded in the audit log." % (r.fig_no + 1, r.fig_no + 2))
r.figure("r4_act_w04.png", "Workflow W04, part 1 – Report, sanction and appeal", width_cm=14, max_height_cm=17)
r.figure("r4_act_content.png", "Workflow W04, part 2 – Publish a lesson", width_cm=14, max_height_cm=17)
r.h4("Step 1 – Sign in to the admin web")
r.p("Open the admin web address (https://tad-min.io.vn on staging since 13/11/2026), enter the e-mail and password of an Admin account and select **Xác thực & Truy cập**. Accounts without the Admin role are rejected. The dashboard shows the number of users, today's active players, matches, online time and pending reports.")
r.figure("40_admin_login.jpg", "Admin web login", width_cm=12)
r.figure("41_admin_dashboard.png", "Admin dashboard", width_cm=14)
r.h4("Step 2 – Manage user accounts")
r.numbered([
    "Open **Quản lý người dùng**. Search by name, e-mail or ID and filter by status (ACTIVE, SUSPENDED, BANNED).",
    "Use the action icons of a row to assign a role, activate an e-mail manually, apply or revoke a sanction (a suspension needs a future expiry date and a reason of 3–2,000 characters) or reset player progress.",
    "Progress reset: select the groups LEARNING, RANKING, MISSIONS or ECONOMY, check the preview of the impact and confirm. Reset is refused while the player is searching or playing.",
])
r.figure("42_admin_users.png", "Users page (e-mail addresses hidden in this figure)", width_cm=14)
r.h4("Step 3 – Review reports and appeals")
r.numbered([
    "Open **Xử lý vi phạm**. Pending reports are listed with the reporter, the reported player, the match and the reason.",
    "Open a report, download the match evidence or replay, choose **Chấp nhận tố cáo** (optionally with a suspension or ban) or **Bác tố cáo**, write the reason and select **Lưu quyết định**.",
    "In the appeals section, review the statement of a restricted player. Accepting an appeal revokes only the sanction it concerns.",
])
r.figure("45_admin_moderation.png", "Reports, decisions and appeals", width_cm=14)
r.h4("Step 4 – Manage lessons and content")
r.numbered([
    "Open **Nội dung (CMS)**. The left list shows the 15 lessons with their status and version.",
    "Select a lesson to edit the code, name, technique template, group, number of shots, target, rewards, minimum level and target radius, then select **Lưu bản nháp**.",
    "Select **Xuất bản bản đã lưu** to publish the draft for new attempts, or **Gỡ xuất bản** to hide the lesson. Attempts already in progress keep their version.",
    "Use the other tabs to add a drill, publish a cosmetic item in the shop, or create a daily or weekly mission.",
])
r.figure("43_admin_cms.png", "Content management – lesson editor", width_cm=14)
r.h4("Step 5 – Monitor matches")
r.p("Open **Giám sát trận đấu**. **Trận đang hoạt động** lists live matches refreshed every 5 seconds with the score and the connection reported by the dedicated server. To stop a live match select **Dừng trận có lý do**, enter the reason and confirm; the match ends as interrupted without Elo or reward. **Lịch sử** shows finished matches with kind, games won and end time.")
r.figure("44_admin_matches.png", "Match monitoring – history", width_cm=14)
r.h4("Step 6 – Read analytics and label coaching sessions")
r.numbered([
    "Open **Thống kê & AI Telemetry**, choose the date range (Vietnam time, at most 366 days) and select **Làm mới**. The page shows registrations, daily players, completed matches, active hours, retention, popular lessons with pass rate, the technique-error heat map and matchmaking health.",
    "Open **Gán nhãn coaching** to act as an expert reviewer. Select a synchronised session, inspect the shot-by-shot evidence, set the observed level, the error labels and the data group, then save. **Xuất dataset** exports the labelled data used to evaluate the AI coaching.",
])
r.figure("46_admin_analytics.png", "Player and matchmaking analytics", width_cm=14)
r.figure("47_admin_expert.png", "Expert labelling of a training session", width_cm=14)

# ---------------------------------------------------------------- other functions
r.h3("3.6 Other Player Functions")
r.table(["Function", "How to use"], [
    ["Missions and streak", "Menu → NHIỆM VỤ: 7 daily and 10 weekly missions; select NHẬN THƯỞNG when a mission is complete and NHẬN THƯỞNG TỔNG for the set bonus. Missions renew at 07:00 Vietnam time; play at least one match per day to keep the streak. A 3-day streak completes a weekly mission and a 5-day streak unlocks an achievement."],
    ["Progress", "Menu → TIẾN TRÌNH: passed lessons, practice time, the most frequent error and, for each lesson, the stars, the best accuracy and the four most recent results (Gần đây)."],
    ["Shop", "Menu → CỬA HÀNG: preview a paddle and buy it with Tokenpad or Elite Token (if a price exists in that currency)."],
    ["Collection", "Menu → BỘ SƯU TẬP: owned paddles; select TRANG BỊ to use a paddle in matches."],
    ["Friends and leaderboard", "Friend drawer on the left of the Menu: search by name or ID, send or answer requests, remove or block players; the Bảng xếp hạng tab ranks players by Elo."],
    ["Replay", "LỊCH SỬ → replay button of a match: pause, seek and change speed from 0.5× to 2×. Only players of the match can watch it."],
    ["Report a player", "LỊCH SỬ → Tố cáo on a finished match: choose the player and enter the reason (up to 254 characters)."],
    ["Decisions and appeals", "Menu → \"!\" KHIẾU NẠI tile next to TIẾN TRÌNH: shows sanctions on the account; a restricted player can send one appeal (at least 10 characters) per sanction."],
    ["Settings", "Menu → CÀI ĐẶT: Tổng quan (sign out), Âm thanh, Đồ họa, Điều khiển (key binding and touch layout). Select ÁP DỤNG VÀ LƯU to keep the changes."],
], widths=[3.6, 12.4], size=8.5, first_col_bold=True, caption="Other player functions")
for f, cap in [("15_missions.png", "Missions and daily streak"), ("22_shop.png", "Shop"), ("23_collection.png", "Collection"), ("24_settings.png", "Settings"), ("25_settings_controls.png", "Settings – key binding")]:
    r.figure(f, cap, width_cm=12.5)

r.h3("3.7 Troubleshooting")
r.table(["Message or symptom", "Cause", "What to do"], [
    ["No verification e-mail", "The e-mail is delayed or in the spam folder; SMTP is unavailable", "Check the spam folder; select Gửi lại mã xác thực after 60 seconds; if it still fails, ask an administrator to activate the account manually"],
    ["\"Email chưa được xác thực…\"", "The account was registered but not verified", "Open Bạn cần xác thực email? and enter the code"],
    ["\"Email hoặc mật khẩu không chính xác.\"", "Wrong e-mail or password", "Check the input or use Quên mật khẩu"],
    ["\"Tài khoản đang bị hạn chế…\"", "The account is suspended or banned", "Read the decision and send an appeal from Quyết định & Khiếu nại"],
    ["\"Không kết nối được Photon…\"", "No Internet or UDP blocked by a firewall", "Check the connection and allow the game in the firewall"],
    ["\"Máy chủ đang bận • Đang chờ lượt thi đấu…\"", "The dedicated server is running another match", "Wait in the lobby; the match starts when the server is free"],
    ["Offline results not synchronised", "The device has not signed in since training offline", "Select Đăng nhập on the offline banner; check Chi tiết for rejected items"],
    ["\"Số dư tiền tệ không đủ…\"", "Not enough Tokenpad or Elite Token", "Earn Tokenpad by playing matches, lessons and missions"],
], widths=[4.8, 4.6, 6.6], size=8.5, caption="Troubleshooting")

out = r.save("Report6_Software User Guides.docx", "Report 6 – Software User Guides")
print(out, "tables", r.tbl_no, "figures", r.fig_no)
