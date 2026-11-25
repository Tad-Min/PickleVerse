"""Report 2 - Project Management Plan."""
import collections

from report_lib import Report, AI_USAGE
import r5_cases as R5

CHANGES = [
    ("27/10/2026", "A", "Nguyễn Gia Huy", "v0.1 – Work packages, schedule and risks summarised for the Review 2 design brief (BM01, BM04)."),
    ("06/11/2026", "A", "Nguyễn Văn Thành Đạt", "v0.2 – First complete Report 2 on the official template for Review 3."),
    ("07/11/2026", "M", "Nguyễn Văn Thành Đạt", "v0.3 – Added weekly effort, package readiness and review checks."),
    ("11/11/2026", "M", "Nguyễn Gia Huy", "v1.0 – Revised after the internal review: two-level WBS with complexity and effort; quality targets in the template format; risks with owner and trigger; actual iterations; dated milestones with actual dates; RACI; training plan; configuration management that matches the repository."),
    ("11/11/2026", "M", "Nguyễn Gia Huy", "v1.1 – RACI: workflow owners and supporting members aligned with BM04; note on implementation effort from the repository history."),
    ("12/11/2026", "M", "Nguyễn Gia Huy", "v1.2 – Revised after the second internal review: effort per member against capacity with corrective action; CI/CD status (never passed, manual releases) and new risk 13; secrets and commit-message practice described as they are; endpoint and test-case counts updated; workflow names and BM04 approval status as in the form; daily backup replaced by the real pre-deployment dump."),
    ("13/11/2026", "M", "Nguyễn Gia Huy", "v1.3 – Updated after the fixes of 12–13/11/2026 (main @ d9200cc): the CI/CD pipeline passes and deploys staging (risk 13 closed, history of the 35 runs corrected); HTTPS on staging and credentials removed from the settings file; verified database dump before each deployment (risk 11); commit figures at d9200cc; iteration and milestone notes updated."),
    ("17/11/2026", "M", "Nguyễn Gia Huy", "v1.4 – Updated to main @ 96ff8e9 (17/11/2026) and the Review 1 comments: WBS items 4.6, 7.6, 10.4, 10.5 and 14.4 added for the work of 15–17/11/2026 and the effort overrun stated; membership subscription recorded as change request CR-01; new risks 14 (subscription), 15 (user content without moderation) and 16 (late scope); risks 2, 3, 12 and 13 updated (public repository, game-server pool, Android protocol, 53 pipeline runs and the skipped deploy job); Iteration 4 closed and Iteration 5 progress; pull-request practice described as it is; commit figures at 96ff8e9."),
    ("19/11/2026", "M", "Nguyễn Gia Huy", "v1.5 – AI Usage Declaration added to §6.3 (Table 12); Review 2 status from BM04 V1.0 (the team did not attend, conclusion Re-review); acceptance testing described as the four workflows with a main and an exception scenario each; diagrams kept in one tool (PlantUML) after the outdated draw.io file was removed."),
    ("19/11/2026", "M", "Nguyễn Gia Huy", "v1.6 – Corrected two statements: the estimate is above the team capacity (it was described as fitting it), and the admin web code is attributed as in the repository history (most of it committed by Nguyễn Văn Thành Đạt); WBS owners are the members responsible for each item."),
    ("25/11/2026", "M", "Nguyễn Gia Huy", "v1.7 – Updated to main @ adcb4e2 (25/11/2026): WBS items 4.7, 7.7, 7.8, 8.4, 12.7, 14.5, 14.6 and group 16 (FE-11 Membership) added and the overrun recalculated; CR-01 implemented, risk 14 rewritten and new risks 17 (schema changes are not deployed), 18 (real payment on staging) and 19 (payment checks outside the pipeline); risks 1, 2, 10, 12, 13 and 16 updated; Iteration 5 progress; first pull requests and their limits; attribution of the admin web corrected to the present repository history; commit, pipeline, table and endpoint figures at adcb4e2."),
]

# Two-level WBS: (id, name, complexity, man-days, owner)
WBS = [
    ("1", "Project management and documentation", [
        ("1.1", "Planning, scheduling and weekly tracking", "Medium", 5, "Kiên"),
        ("1.2", "Report 1 and Report 2", "Simple", 3, "Huy, Kiên"),
        ("1.3", "Report 3 – Software Requirement Specification", "Complex", 8, "Huy"),
        ("1.4", "Report 4 – Software Design Document", "Complex", 7, "Đạt"),
        ("1.5", "Reports 5–7, user guides and defense slides", "Medium", 5, "Huy, Thuận"),
    ]),
    ("2", "Foundation and architecture", [
        ("2.1", "Unity project, scenes and assembly structure", "Simple", 3, "Huy"),
        ("2.2", "Layered Web API, EF Core model and database schema (60 tables)", "Complex", 9, "Đạt"),
        ("2.3", "Docker Compose environment with PostgreSQL, Redis and S3 storage", "Medium", 4, "Đạt"),
        ("2.4", "CI/CD pipeline (GitHub Actions, Docker Hub, SSH deployment)", "Medium", 4, "Đạt"),
    ]),
    ("3", "Gameplay core", [
        ("3.1", "Ball flight and bounce simulation at 120 Hz", "Complex", 8, "Huy"),
        ("3.2", "Paddle contact, shot input and seven shot kinds", "Complex", 7, "Huy"),
        ("3.3", "Singles rules engine (scoring, serve, faults, non-volley zone)", "Complex", 7, "Huy"),
        ("3.4", "Character movement and animation controller", "Medium", 5, "Huy"),
        ("3.5", "Match flow, HUD, intro cinematic and result screen", "Simple", 3, "Thuận"),
    ]),
    ("4", "FE-01 Account and onboarding", [
        ("4.1", "Registration and OTP email verification", "Medium", 4, "Đạt"),
        ("4.2", "Sign in, sign out, JWT access and refresh tokens", "Medium", 4, "Đạt"),
        ("4.3", "Password recovery", "Simple", 2, "Thuận"),
        ("4.4", "Profile, character and onboarding level", "Simple", 2, "Thuận"),
        ("4.5", "Settings: audio, graphics, key binding, touch layout", "Medium", 4, "Thuận"),
        ("4.6", "PC display modes, window frame and quit buttons (added 16/11/2026)", "Simple", 2, "Huy"),
        ("4.7", "One device per account, session check and seat takeover in a match (added 21/11/2026)", "Medium", 3, "Đạt"),
    ]),
    ("5", "FE-02 Tutorial and technique training", [
        ("5.1", "Interactive tutorial (17 steps)", "Medium", 5, "Huy"),
        ("5.2", "Lesson and drill framework with star scoring", "Complex", 7, "Huy"),
        ("5.3", "Content of 15 lessons and 12 techniques", "Medium", 5, "Huy"),
        ("5.4", "Offline training and one-time synchronisation", "Complex", 6, "Đạt"),
    ]),
    ("6", "FE-03 Bot practice", [
        ("6.1", "Fixed-level bot (Easy, Medium, Hard)", "Medium", 5, "Huy"),
        ("6.2", "Adaptive behaviour-tree bot with Elo tiers", "Complex", 7, "Đạt"),
        ("6.3", "Bot match setup, court and lighting options, rewards", "Simple", 3, "Thuận"),
    ]),
    ("7", "FE-04 Online matches", [
        ("7.1", "Photon Fusion session, lobby and room codes", "Complex", 7, "Đạt"),
        ("7.2", "Ranked matchmaking, ready check and bot fallback", "Complex", 7, "Đạt"),
        ("7.3", "Dedicated server build, signed tickets, lease and result", "Complex", 9, "Đạt"),
        ("7.4", "State synchronisation, input prediction and reconnection", "Complex", 9, "Đạt"),
        ("7.5", "Elo rating and match result persistence", "Medium", 4, "Đạt"),
        ("7.6", "Ranked doubles with bot fill and the dedicated-server pool (added 15–17/11/2026)", "Complex", 5, "Đạt"),
        ("7.7", "Match-found intro, ready check screen and online match summary (added 19–25/11/2026)", "Medium", 5, "Huy"),
        ("7.8", "Host migration, position swap, replay upload during the match and outcome cinematic (added 22–24/11/2026)", "Complex", 6, "Đạt"),
    ]),
    ("8", "FE-05 AI coaching", [
        ("8.1", "Telemetry capture, local spool and upload", "Medium", 5, "Đạt"),
        ("8.2", "FastAPI rule engine and gated tier model", "Complex", 7, "Đạt"),
        ("8.3", "Hybrid analysis worker, analysis jobs and coaching panel", "Medium", 6, "Huy"),
        ("8.4", "Analysis after a bot match, new coaching panel and referee calls (added 25/11/2026)", "Simple", 2, "Huy"),
    ]),
    ("9", "FE-06 Progress, rewards and shop", [
        ("9.1", "XP, levels, technique mastery and progress dashboard", "Medium", 5, "Huy"),
        ("9.2", "Daily and weekly missions, achievements, streak rewards", "Medium", 5, "Đạt"),
        ("9.3", "Currencies, shop and collection", "Medium", 5, "Huy"),
    ]),
    ("10", "FE-07 Social, history and ranking", [
        ("10.1", "Friends, blocking and room invitations", "Medium", 4, "Đạt"),
        ("10.2", "Match history, replay recording and replay viewer", "Complex", 6, "Đạt"),
        ("10.3", "Elo leaderboard with cache", "Simple", 2, "Kiên"),
        ("10.4", "Social feed, video posts, direct messages and whispers (Review 1, added 15/11/2026)", "Complex", 7, "Đạt"),
        ("10.5", "Match detail with game scores and error statistics (added 15/11/2026)", "Simple", 2, "Đạt"),
    ]),
    ("11", "FE-08 Reports, restrictions and appeals", [
        ("11.1", "Player report after a match", "Simple", 2, "Kiên"),
        ("11.2", "Restricted access, decision view and appeal", "Medium", 4, "Kiên"),
    ]),
    ("12", "FE-09 Administration web application", [
        ("12.1", "Admin authentication, layout and protected routing", "Simple", 2, "Kiên"),
        ("12.2", "User management, sanctions and progress reset", "Complex", 6, "Kiên"),
        ("12.3", "Content management: lessons, references, cosmetics, missions", "Medium", 6, "Kiên"),
        ("12.4", "Live match monitoring and stopping", "Medium", 4, "Đạt"),
        ("12.5", "Moderation of player reports and appeals", "Medium", 3, "Kiên"),
        ("12.6", "Analytics dashboard", "Medium", 3, "Kiên"),
        ("12.7", "Admin web redesign, lessons with several drills and replay launch in the game (added 20–22/11/2026)", "Medium", 5, "Kiên"),
    ]),
    ("13", "FE-10 Expert review", [
        ("13.1", "Shot labelling and dataset export", "Medium", 4, "Đạt"),
    ]),
    ("14", "Art, audio and interface assets", [
        ("14.1", "Characters, 20 animation clips, courts and stadium", "Complex", 8, "Huy"),
        ("14.2", "Music and sound effects", "Simple", 2, "Kiên"),
        ("14.3", "Front-end screens, loading artwork and Vietnamese fonts", "Medium", 5, "Thuận"),
        ("14.4", "Golden-hour district around the courts and night lighting (added 15/11/2026)", "Medium", 3, "Huy"),
        ("14.5", "Art direction: stylised shader, materials, courtside kit, lighting, atmosphere and geometry budget (added 24–25/11/2026)", "Complex", 7, "Huy"),
        ("14.6", "Gemstone and Dragon Ruby paddles with ball effects; six NCS music tracks (added 24–25/11/2026)", "Medium", 4, "Huy"),
    ]),
    ("15", "Testing and release", [
        ("15.1", "Unit tests (Unity EditMode, Python) and coverage", "Medium", 5, "Thuận"),
        ("15.2", "Integration and system testing on staging", "Medium", 5, "Thuận"),
        ("15.3", "Acceptance tests, playtests and defect fixing", "Medium", 6, "All"),
        ("15.4", "Staging deployment and release package", "Simple", 2, "Đạt"),
    ]),
    ("16", "FE-11 Membership (CR-01)", [
        ("16.1", "Membership orders, PayOS payment, mode locks and membership panel (added 22/11/2026)", "Complex", 6, "Đạt"),
    ]),
]

r = Report("Report2_Project Management Plan.docx", "Report 2 – Project Management Plan", CHANGES)
r.h1("II. Project Management Plan")
r.h2("1. Overview")
r.h3("1.1 Scope & Estimation")
r.p("The scope of version 1.0 is the eleven major features FE-01 to FE-11 defined in Report 1, plus the technical work that the features depend on: the gameplay core, the networking layer, art and audio assets, deployment and testing. Table 1 decomposes this scope into a two-level work breakdown structure (WBS). Each function is classified as Simple, Medium or Complex and estimated in man-days, where one man-day is eight hours of work by one member.")
rows, sub = [], []
total = 0
for wid, name, items in WBS:
    s = sum(i[3] for i in items)
    total += s
    sub.append(len(rows))
    rows.append(["**%s**" % wid, "**%s**" % name, "", "**%d**" % s, ""])
    for iid, iname, cx, md, owner in items:
        rows.append([iid, iname, cx, str(md), owner])
rows.append(["", "**Total Estimated Effort (man-days)**", "", "**%d**" % total, ""])
sub.append(len(rows) - 1)
r.table(["#", "WBS Item", "Complexity", "Est. Effort (man-days)", "Owner"], rows,
        widths=[1.2, 9.4, 2.0, 2.0, 2.2], size=9, subheader_rows=sub, caption="Work breakdown structure and effort estimation")
counts = {"Simple": 0, "Medium": 0, "Complex": 0}
for _, _, items in WBS:
    for i in items:
        counts[i[2]] += 1
ADDED = ("4.6", "4.7", "7.6", "7.7", "7.8", "8.4", "10.4", "10.5", "12.7", "14.4", "14.5", "14.6", "16.1")
added = sum(i[3] for _, _, items in WBS for i in items if i[0] in ADDED)
r.p("The estimate covers %d functions (%d Simple, %d Medium and %d Complex) and totals **%d man-days**. The available capacity is four members × 14 weeks × 5 working days = 280 man-days. The plan of 11/11/2026 used exactly this capacity, without reserve: Complex networking items were estimated from the actual effort of the October prototypes, and AI coding assistants are used for boilerplate code and first drafts of documents, every output being reviewed by the member who owns the item. The items %s were added for the work of 15–25/11/2026, including the social feature and the membership asked for at Review 1; their %d man-days are estimated from the size of their commits and must be confirmed by the owners. They bring the plan **%d man-days over capacity**. What is deferred to absorb the overrun has to be decided at the planning of Iteration 6; the first candidate remains FE-10 (expert review), which is not part of the core workflows." % (sum(counts.values()), counts["Simple"], counts["Medium"], counts["Complex"], total, ", ".join(ADDED), added, total - 280))
NAMES = [("Kiên", "Huỳnh Trung Kiên"), ("Đạt", "Nguyễn Văn Thành Đạt"), ("Huy", "Nguyễn Gia Huy"), ("Thuận", "Trần Nguyễn Hiếu Thuận")]
load = collections.Counter()
for _, _, items in WBS:
    for _, _, _, md, owner in items:
        people = [k for k, _ in NAMES] if owner == "All" else [x.strip() for x in owner.split(",")]
        for p in people:
            load[p] += md / len(people)
CAP = 14 * 5
r.p("The total is above the team capacity, and Table %d shows that the load per member is also uneven. The owner of an item in Table 1 is the member responsible for it. Each member has 14 × 5 = %d man-days." % (r.tbl_no + 1, CAP))
r.table(["Member", "Estimated effort of owned items (man-days)", "Capacity (man-days)", "Load"],
        [[full, "%.1f" % load[k], str(CAP), "%.0f%%" % (load[k] * 100.0 / CAP)] for k, full in NAMES] +
        [["**Total**", "**%.0f**" % sum(load.values()), "**%d**" % (CAP * 4), "**%.0f%%**" % (sum(load.values()) * 100.0 / (CAP * 4))]],
        widths=[5.0, 4.6, 3.2, 3.2], size=9.5, subheader_rows=(4,), caption="Estimated load per member")
r.p("Nguyễn Văn Thành Đạt and Nguyễn Gia Huy carry about %.0f%% and %.0f%% of their capacity, while Huỳnh Trung Kiên and Trần Nguyễn Hiếu Thuận carry about %.0f%% and %.0f%%. Until Iteration 4 the two overloaded members worked beyond the planned hours, which is one reason why documents were late. **Corrective action for Iterations 5 and 6:** new work is assigned first to Kiên and Thuận – system and acceptance testing, test data and seed accounts, the remaining UI defects, the admin web and the documents – while Đạt and Huy review it and fix the defects in the modules only they know; pair-programming sessions on the API and Photon Fusion (risk 1) let the other members take over online defects. The load is re-checked at each iteration planning in the WBS sheet of the tracking workbook." % tuple(load[k] * 100.0 / CAP for k in ("Đạt", "Huy", "Kiên", "Thuận")))

r.h3("1.2 Project Objectives")
r.p("The overall objective is to deliver, by Week 14 (13/12/2026), a deployed and tested version of PickleVerse in which a beginner can learn the rules, train the core techniques with graded drills and feedback, and play bot and online matches, while administrators manage accounts, content, matches and moderation from the web application. The quality, time and cost objectives are measurable as follows.")
r.p("**Quality.** Table %d states the planned test coverage" % (r.tbl_no + 1) + " and the expected number of defects at each testing stage. The defect estimate (about 120 defects in total) is based on the defects already found in the October prototypes and the 10/11/2026 playtest; it is used to judge whether a stage is finding fewer defects than expected, which usually means that testing is too shallow.")
r.table(["#", "Testing Stage", "Test Coverage", "No. of Defects", "% of Defect", "Notes"], [
    ["1", "Reviewing", "100% of reports, diagrams and database changes are reviewed by at least one other member", "20", "17%", "Document and design review before each submission"],
    ["2", "Unit Test", "100% of public methods in the rule, scoring, Elo, input and economy classes; ≥70% line coverage of core gameplay assemblies", "30", "25%", "Unity EditMode and Python pytest; run before every merge"],
    ["3", "Integration Test", "All 160 API endpoints exercised at least once; all database write paths", "25", "21%", "IntegrationChecks harness and API contract checks in the CI validate job (sixteen check groups on 25/11/2026; the groups membership and replay-launch exist but are not in the pipeline yet, risk 19)"],
    ["4", "System Test", "100%% of the %d test cases in Report 5 executed on staging" % len(R5.CASES), "35", "29%", "Includes network, device and AI quality tests"],
    ["5", "Acceptance Test", "8 demo scenarios covering workflows W01–W04", "10", "8%", "Executed with the supervisor before the final review"],
    ["", "**Total**", "", "**120**", "**100%**", ""],
], widths=[0.8, 2.6, 6.0, 1.7, 1.6, 4.3], size=9, subheader_rows=(5,), caption="Quality objectives by testing stage")
r.p("In addition, the release criteria for version 1.0 are: at least 90% of system test cases passed, no open Critical or High defect, at least 50% of test cases executed automatically, and every functional requirement covered by at least one passed test case.")
r.p("**Milestone timeliness.** Up to 11/11/2026, six milestones (M01–M06) were due and one of them (M06) was delivered on time, a timeliness of 17%. Reports 1–4 were first submitted on 06/11/2026 because the team prioritised the playable prototype in Weeks 5–8 (§2.1). The target for the remaining milestones M07–M10 is 100% on time, and the corrective actions are: one document owner per report, a weekly tracking meeting with the leader, and submission one day before each deadline.")
md = {i[0]: i[3] for _, _, items in WBS for i in items}
feat = sum(v for k, v in md.items() if 2 <= int(k.split(".")[0]) <= 14 or k.startswith("16."))
test15 = sum(v for k, v in md.items() if k.startswith("15."))
alloc = [("Requirements", "1.2, 1.3", md["1.2"] + md["1.3"]), ("Design", "1.4 + 15% of items 2–14 and 16", md["1.4"] + 0.15 * feat),
         ("Coding", "70% of items 2–14 and 16", 0.70 * feat), ("Testing", "15% of items 2–14 and 16 + item 15", 0.15 * feat + test15),
         ("Project management and documentation", "1.1, 1.5", md["1.1"] + md["1.5"])]
r.p("**Cost (allocated effort).** The %d man-days are distributed over the project activities as shown in Table %d." % (total, r.tbl_no + 1) + " Feature and technical items include their own design (15%), coding (70%) and unit testing (15%); documentation items are counted under requirements, design or project management.")
r.table(["Activity", "Source in the WBS", "Effort (man-days)", "Share"],
        [[a, b, "%.0f" % v, "%.1f%%" % (v * 100.0 / total)] for a, b, v in alloc] + [["**Total**", "", "**%d**" % total, "**100%**"]],
        widths=[5.0, 6.0, 2.6, 2.4], size=9.5, subheader_rows=(5,), caption="Allocated effort by activity")

r.h3("1.3 Project Risks")
r.p("Risks are reviewed in the weekly tracking meeting. A risk that occurs is closed in this table and recorded as an issue in the Issues sheet of the project tracking workbook.")
r.table(["#", "Risk Description", "Impact", "Possibility", "Trigger", "Response Plans", "Owner"], [
    ["1", "Key-person dependency: most of the networking, backend and AI code has been written by one member, so the online features stall if that member is unavailable.", "High", "Medium", "Owner unavailable for more than 3 days, or one member has more than 60% of the commits in an iteration", "Each member owns and demonstrates one workflow; pair-programming sessions on Fusion and the API; handover notes in Doc/; code walkthrough at the end of each iteration", "Kiên"],
    ["2", "Leaked credentials: the SMTP password and JWT signing key were committed in appsettings.json. They were removed on 13/11/2026 (be556a1), but the repository is public, so the old values can be read by anyone in the Git history until they are rotated. Since 22/11/2026 the server also holds PayOS keys (environment variables only).", "High", "High", "Secret found in a tracked file or in the history of the public repository (occurred; see issue I-03)", "Rotate the exposed values on staging and record the rotation; keep secrets only in environment variables and the server .env file; add a secret scan to CI", "Đạt"],
    ["3", "Too few dedicated game-server containers limit concurrent online matches. Since 15/11/2026 each container runs one match and the number of containers is a setting (PKV_GAME_REPLICAS, default 1); the host's CPU and memory limit how many can run.", "High", "Medium", "\"Server busy\" responses during tests with more than one match", "Set the replica count for the playtests and the demo; measure CPU and memory per container; load-test the API separately (NFR-04)", "Đạt"],
    ["4", "The Photon free plan limits concurrent users, which can block group playtests or the demo.", "Medium", "Medium", "Connection refused when more than 20 clients connect", "Use the dedicated server path for matches; schedule playtests; upgrade the plan before the defense if needed", "Đạt"],
    ["5", "Unity tests are not run in GitHub Actions, so automation evidence depends on manual Unity CLI runs. The workflow defines a GameCI build of the dedicated server with a Unity licence but no test step.", "Medium", "High", "CI shows no Unity test step", "Add a GameCI unity-test-runner EditMode step that reuses the licence secrets of the server build (the pipeline passes since 13/11/2026); until then attach Unity CLI result files with the commit hash", "Thuận"],
    ["6", "Resetting the development database deletes test accounts and data.", "Medium", "Medium", "Schema change that requires a reset", "Keep seed accounts and content in Database.sql; back up with pg_dump before a reset; re-run seed checks", "Đạt"],
    ["7", "Documents or code produced with AI assistants contain statements that do not match the implementation.", "High", "Medium", "A reviewer finds a claim without evidence in code or tests", "The owner of each section verifies it against code and test results; cross-document consistency check before submission", "Huy"],
    ["8", "Too little real player data to validate the AI tier model.", "Medium", "High", "Fewer than 200 labelled sessions by Week 12", "Keep the tier model gated; use rule-based findings; collect labels with the expert review tool during playtests", "Đạt"],
    ["9", "High latency or packet loss between Vietnamese players and the single staging host degrades matches.", "Medium", "Medium", "Round-trip time above 100 ms or rubber-banding reported in playtests", "Measure RTT in each playtest; tune interpolation; reconnection within 30 seconds", "Đạt"],
    ["10", "Schedule slip and scope creep (milestones M01–M05 were late).", "High", "High", "A milestone is not ready two days before its date", "Freeze scope after Review 3; weekly tracking; defer FE-10 first", "Kiên"],
    ["11", "The staging host fails and data is lost (single server, no replica).", "High", "Low", "Host unreachable or disk failure", "Since be556a1 (13/11/2026) the deployment takes a pg_dump before every release and checks it with pg_restore, but the dumps stay on the same host and there is no daily job: add a daily pg_dump with off-host copies (PB-21); redeploy from the Docker Hub images; keep a local Docker environment as fallback", "Đạt"],
    ["12", "The game does not reach the frame-rate target on mid-range Android phones, or the Android build fails on a real device (APKs were built on 12/11 and 14/11/2026 but none has been installed on a phone; the golden-hour district of 15/11/2026 and the art pass of 24–25/11/2026 add geometry and effects to the courts).", "Medium", "Medium", "Average below 30 FPS on the test phone, or a crash in the smoke test TC-N10", "Rebuild the APK from the current main (protocol 22); borrow a reference phone; profile with the Unity Profiler; quality presets; reduce stadium detail and shadows on mobile", "Huy"],
    ["13", "The CI/CD pipeline does not run, so builds, checks and deployments depend on manual steps. Occurred from 05/11 to 13/11/2026: 30 of the 35 runs of dockerhub.yml did not pass (two configuration failures on 05/11; 18 runs did not start while the GitHub account was locked for a billing issue from 06/11 to 11/11; six dedicated-server build failures on 11–12/11; two failed deployment steps, one failed FastAPI test and one cancelled manual run on 13/11). Closed on 13/11/2026: runs 27, 28, 30, 33 and 34 passed every job (issue I-11). In 9 passing runs of 13–16/11/2026 the deploy job was skipped (defect D-23, fixed on 18/11 in 291542a). Of the 51 runs since 18/11/2026 33 passed, 15 were cancelled because a newer push replaced them and 3 failed (two on 20–21/11, run 79 on 22/11); run 103 of 25/11/2026 deployed main @ adcb4e2.", "High", "Low (was High)", "A push to main without a green run, or a green run without deployment", "Done: the leader had the billing lock lifted on 11/11/2026 and Đạt repaired the dedicated-server build and the deployment script on 12–13/11/2026. The deploy job runs after every passing run since 291542a (D-23). A red run is fixed before the next merge; the manual release script stays as the fallback", "Kiên"],
    ["14", "The membership (CR-01, implemented on 22/11/2026) puts real-money payment in front of online play: without it a player cannot search a match, open a room or play the adaptive bot. The refund procedure, the terms of sale, the treatment of minors and the tax question are not settled, and there is no admin view of orders.", "High", "Medium", "A player pays and is not activated, or asks for a refund; the supervisor or the committee rejects the paywall of a core feature", "Confirm price, benefits and refund procedure with the supervisor; write the terms of sale and show them before the payment; add an order list for administrators (PB-22); keep seeded member accounts for the reviews and the defense so that W03 can always be demonstrated", "Kiên"],
    ["15", "User-generated content (posts, videos, comments, messages, whispers) can contain abuse, and on 17/11/2026 it cannot be reported by players or removed by administrators; only blocking hides it.", "Medium", "Medium", "A player complains about a post or a message", "Add reporting of posts and messages and an admin removal function to the moderation workflow (PB-25); until then administrators can sanction the account", "Kiên"],
    ["16", "Scope added late (social feature on 15/11, ranked doubles on 17/11, single device and admin redesign on 20–22/11, membership on 22/11, art pass and paddle effects on 24–25/11) leaves less time for system and acceptance testing.", "High", "High (occurred)", "System test coverage below 50% at the end of Round 2", "Freeze features from 25/11/2026; only defects, balance and tests until the release; test the added features first in Round 2; defer FE-10 if needed", "Kiên"],
    ["17", "Schema changes are not deployed: the pipeline copies Database.sql, which is used only for an empty database, and checks three older objects. Since 22/11/2026 the code needs new columns (Drills.Config, LessonDrills.SortOrder) and the table MembershipOrders, and since 25/11/2026 two rows for the Dragon Ruby paddle.", "High", "High", "HTTP 500 on profile, lessons or membership after a deployment that passed", "Recreate or patch the staging database by hand in the same step as the deployment and record it; extend the schema check in ssh-deploy.sh to the new objects so that the deployment stops instead of failing at run time (PB-28)", "Đạt"],
    ["18", "PayOS runs against its production address on staging, so a test payment moves real money and a defect could charge a player without activation.", "High", "Medium", "An order is PAID at PayOS but the player is not a member", "Pay only with team accounts; compare the PayOS statement with MembershipOrders after each test; the client can refresh an order at any time; keep PKV_PAYOS_ENABLED=false outside the test sessions if the keys are the team's own", "Đạt"],
    ["19", "The check groups membership and replay-launch (PayOS signature, amount, partial payment, idempotent credit, mode permissions, one-time ticket) exist in IntegrationChecks but are not in the list of groups that the pipeline runs.", "High", "Medium", "A change breaks the payment path and the pipeline stays green", "Add both groups to the validate job (PB-27); run them by hand before each release until then", "Đạt"],
], widths=[0.9, 3.8, 1.6, 2.0, 2.6, 3.7, 1.4], size=8.5, caption="Project risks")

r.h2("2. Management Approach")
r.h3("2.1 Project Process")
r.p("The team follows an iterative and incremental process with two-week iterations, adapted from Scrum for a student team. The leader acts as Scrum Master and product owner on behalf of the supervisor; each member owns a technical area and one end-to-end workflow. Every iteration starts with planning from the product backlog in the tracking workbook and ends with a demonstration to the team and, at review weeks, to the supervisor. Figure 1 shows the cycle.")
r.figure("r2_process.png", "Iterative development process used by the team", width_cm=15)
r.p("**Definition of Done.** A backlog item is done when (1) the code is merged into the main branch, (2) its unit tests pass and the existing EditMode and Python suites still pass, (3) the related API, database and UI changes are consistent with Database.sql and the API client, (4) it works on the staging server, and (5) the SRS, SDD and test cases are updated.")
r.p("The actual iterations, reconstructed from the repository history, are listed in Table %d." % (r.tbl_no + 1) + " The architecture changed three times, which explains why the early milestones were late: the online layer moved from a Netcode for GameObjects prototype to Photon Fusion in shared mode and then to an authoritative dedicated server, and player data moved from local JSON files to the Web API and PostgreSQL.")
r.table(["Iteration", "Weeks and dates", "Main results", "Status"], [
    ["It-0 Inception", "W1–W2, 07/09–20/09", "Proposal and team set-up; repository created (first commit 23/08); Unity project initialised (17/09)", "Done"],
    ["It-1 Prototype", "W3–W4, 21/09–04/10", "Unity project structure and camera; 3D character models generated and rigged (03/10)", "Done"],
    ["It-2 Single-player core", "W5–W6, 05/10–18/10", "20 animation clips; BotMatch singles with rules (07/10); mode selection; music and SFX (09/10); shop and collection (10/10); tutorial (11/10); training mode (12/10); history, missions, Elo and ranks (11/10); friends and leaderboard (12/10); replay and streaks (13/10); Netcode for GameObjects prototype (07/10–21/10)", "Done"],
    ["It-3 Online and backend", "W7–W8, 19/10–01/11", "Loading scene and touch controls; Web API and database (20/10); move to Photon Fusion (20/10); Unity–API integration (21–23/10); admin web (22–24/10); OTP email (24/10); doubles and custom rooms (25/10); Review 2 package (27/10); AI service and Unity Behavior bot (27/10); normalised schema and ApiResponse refactor (31/10); login scene (01/11)", "Done"],
    ["It-4 Dedicated server and coaching", "W9–W10, 02/11–15/11", "Dedicated server, accounts and coaching (03/11); Docker deployment (04/11); CI/CD workflow written (05–06/11; first passing run on 13/11 – risk 13); admin tools and response training (06/11); reports v0.2 (06/11); manual staging releases (08–10/11); reconnect and offline fixes (10/11); playtest (10/11); 515 EditMode tests passed (11/11); UI and camera fixes D-03, D-04, D-05, D-07, D-19 and 516 EditMode tests passed (11/11); reports v1.0–v1.2 (11–12/11); pipeline repaired with the first automatic deployment, HTTPS for the staging API and admin web, credentials removed from the settings file, Android APK built, reports v1.3 (12–13/11); social feed, direct messages and whispers, match detail and the dedicated-server pool (15/11, after Review 1); golden-hour district and night lighting (15/11)", "Done"],
    ["It-5 Stabilisation", "W11–W12, 16/11–29/11", "Done by 17/11: PC display modes, window frame and quit buttons; restyled offline and moderation dialogs; ranked doubles with bot fill, protocol 20 and the new XP curve (17/11); 15 IntegrationChecks groups pass in CI (run 52); reports v1.4. Done 18–25/11: defect-fix pass and retest (EV-17); Committee 1.1 documents; match-found ready check (19/11); admin web redesign and lesson–drill model (20–22/11); one device per account and netcode fixes (21–22/11); membership with PayOS and replay launch (22/11); host migration, position swap, replay upload during the match (23/11); outcome cinematic (24/11); art direction, new summary, coaching fix, referee calls, music, Gemstone and Dragon Ruby effects (24–25/11). Open: rotation of the exposed credentials; daily off-host database backup; Unity tests in CI; schema check in the deployment (risk 17); payment checks in CI (risk 19); Android device test; system and load tests; Review 3", "In progress"],
    ["It-6 Release", "W13–W14, 30/11–13/12", "Acceptance tests and demo scenarios; release package; final updates of Reports 6 and 7 (v1.0 written on 11/11/2026); defense slides and rehearsal (W15)", "Planned"],
], widths=[3.0, 3.2, 8.4, 1.6], size=9, caption="Iterations and actual progress")

r.h3("2.2 Quality Management")
r.label("Defect prevention", "Coding rules are written in AGENTS.md and enforced in review: the layered backend architecture (API → Services → Repositories → Entities), access to data only through the unit of work, ApiResponse<T> envelopes, a single schema source (src/Database.sql) and no secrets in source files. Gameplay rules are implemented in engine-independent C# classes so that they can be unit-tested.")
r.label("Reviewing", "Every report section is written by its owner and reviewed by one other member against the code and test results before submission (Table 9). Code is still committed directly to the main branch: the rule that every change goes through a pull request with one reviewer and a passing CI run, planned from Iteration 5, was used only on 21/11/2026 and without a second reviewer (§6.2).")
r.label("Unit testing", "Unity EditMode tests (730 tests in 12 assemblies, all passed on 25/11/2026 on adcb4e2 with the fixes of D-33 and D-34, EV-18) cover the rules, scoring, Elo, input, economy, replay and multiplayer protocol classes; pytest covers the AI service. The suite is run with the Unity CLI before each merge and the result file is stored with the commit hash.")
r.label("Integration testing", "The IntegrationChecks console program and the API contract checks verify the API, the database writes and the dedicated-server ticket flow against a PostgreSQL and Redis instance. They run in the CI pipeline for every push that changes the backend, the Unity scripts or the AI service; they run sixteen check groups, including single session, room invitations, ranked doubles and social content, and their results are linked to test cases in Report 5 (EV-08, EV-11, EV-19). Two more groups, membership and replay-launch, are run by hand (risk 19).")
r.label("System testing", "The %d test cases in Report 5" % len(R5.CASES) + " are executed on the staging server for each package milestone; defects are logged in the tracking workbook with severity and retested before closing.")
r.label("Acceptance testing", "The four workflows W01–W04 of the Review 2 commitment are run end to end with the supervisor, each with its main and its exception scenario (eight demo scenarios, DS-01a to DS-04b), and the results are recorded in Report 5 (TC-W01 to TC-W04).")

r.h3("2.3 Training Plan")
r.table(["Training Area", "Participants", "When, Duration", "Waiver Criteria"], [
    ["Git and GitHub flow (branches, pull requests, reviews)", "All members", "W1, 2 hours; refresher W11, 1 hour", "Mandatory"],
    ["Unity 6, C# gameplay scripting, Input System", "Thuận, Kiên", "W2–W3, 12 hours", "Has completed a Unity game project before"],
    ["Photon Fusion 2 and dedicated server builds", "Huy, Thuận", "W8–W9, 8 hours (taught by Đạt)", "Has run and debugged a networked match on the staging server"],
    ["ASP.NET Core Web API, EF Core and PostgreSQL", "Kiên, Huy", "W6–W7, 8 hours (taught by Đạt)", "Has implemented an API endpoint with a repository and an EF Core query"],
    ["React 18, TypeScript and React Query", "Kiên, Thuận", "W7, 6 hours", "Has built a React application with TypeScript"],
    ["Python FastAPI and scikit-learn", "Đạt, Huy", "W8, 4 hours", "Has deployed a FastAPI service"],
    ["Docker, Docker Compose and GitHub Actions", "All members", "W9, 4 hours", "Can start the local stack and read container logs"],
    ["Blender and character animation import", "Huy, Thuận", "W4, 6 hours", "Has rigged and imported a character into Unity"],
    ["UML 2.5 modelling with PlantUML", "All members", "W3, 3 hours", "Mandatory"],
], widths=[5.6, 2.8, 4.0, 4.6], size=9, caption="Training plan")

r.h2("3. Project Deliverables")
r.p("Week 1 starts on Monday 07/09/2026; each milestone is due on the Sunday of its week. Actual dates are taken from the repository history and submission records.")
r.table(["#", "Deliverable", "Due Date", "Actual Date", "Status", "Notes"], [
    ["M01", "Report 1 – Project Introduction", "13/09/2026", "06/11/2026", "Done, late", "Proposal registered before Week 1; v1.0 on 11/11/2026"],
    ["M02", "Report 2 – Project Management Plan, schedule, tracking workbook", "20/09/2026", "06/11/2026", "Done, late", "v1.0 on 11/11/2026"],
    ["M03", "Report 3 – Software Requirement Specification", "27/09/2026", "06/11/2026", "Done, late", "v1.0 on 11/11/2026"],
    ["M04", "Report 4 – Software Design Document; Report 5 test plan", "11/10/2026", "06/11/2026", "Done, late", "v1.0 on 11/11/2026"],
    ["M05", "Software Package 1 – single-player core, training, first online build (Review 2)", "25/10/2026", "27/10/2026", "Done, 2 days late", "Review 2 forms BM01–BM04; BM04 V1.0 (07/11/2026) records the conclusion Re-review – the team did not attend the Review 2 session and received no comments"],
    ["M06", "Software Package 2 – Web API, admin web, AI service, dedicated server, CI/CD", "08/11/2026", "06/11/2026", "Done, on time", "Staging on tad-min.io.vn; the CI/CD workflow was written on time but first passed on 13/11/2026, so the releases of 08–10/11 were manual (risk 13)"],
    ["M07", "Software Package 3 – defect fixes, system and load tests (Review 3)", "22/11/2026", "–", "In progress", "Report 5 results due with this package; the social feature, ranked doubles, the membership and the art pass were added on 15–25/11/2026"],
    ["M08", "Full software package with test results and open defects", "06/12/2026", "–", "Planned", ""],
    ["M09", "Report 6 – Software User Guides", "13/12/2026", "11/11/2026", "Done (v1.0)", "Installation, player and administrator guides; updated with the release build"],
    ["M10", "Report 7 – Final Project Report, release package and defense", "20/12/2026", "–", "In progress", "Report 7 v1.0 on 11/11/2026; slides and defense in Week 15"],
], widths=[1.0, 5.6, 2.0, 2.0, 2.2, 4.2], size=9, caption="Project deliverables and milestones")
r.p("Figure 2 places the iterations and milestones on the calendar; the red line marks the status date of the schedule (25/11/2026).")
r.figure("r2_gantt.png", "Master schedule: iterations and milestones", width_cm=16.5)

r.h2("4. Responsibility Assignments")
r.p("D~Do; R~Review; S~Support; I~Informed; <blank>~Omitted. Every row has exactly one D. Each member owns one end-to-end workflow, which that member demonstrates at the reviews; the workflow owners and supporting members follow the Review 2 commitment form BM04 (V1.0 of 07/11/2026; the team did not attend the Review 2 session and the form records the conclusion Re-review). Workflow ownership is an accountability for acceptance, not a measure of implementation effort: the technical rows show the member responsible for each part, and the repository history shows that Nguyễn Gia Huy and Nguyễn Văn Thành Đạt carried the largest share of the code (Table 2). Huỳnh Trung Kiên is the D for the admin web and the owner of workflow W04; after the redesign of 20–22/11/2026 about 62% of the present admin web source lines are his (3,067 of 4,984 lines, git blame at adcb4e2) and 38% were written by Nguyễn Văn Thành Đạt.")
M = ["KienHT SE184454", "DatNVT SE184450", "HuyNG SE184439", "ThuanTNH SE184471"]
r.table(["Responsibility"] + M, [
    ["Project planning and tracking", "D", "R", "S", "S"],
    ["Report 1 – Project Introduction", "R", "S", "D", "I"],
    ["Report 2 – Project Management Plan", "D", "S", "R", "I"],
    ["Report 3 – SRS: overview, use cases, screens", "R", "S", "D", "S"],
    ["Report 3 – SRS: functional and non-functional requirements", "S", "R", "D", "S"],
    ["Report 4 – SDD: architecture, database, API, security", "I", "D", "R", "S"],
    ["Report 4 – SDD: gameplay, bot and client design", "I", "R", "D", "S"],
    ["Report 5 – Test plan, test cases and test report", "R", "S", "S", "D"],
    ["Report 6 – User guides", "R", "S", "D", "S"],
    ["Report 7 – Final report", "R", "R", "D", "S"],
    ["Defense slides and presentation", "D", "S", "S", "S"],
    ["Gameplay core, training and tutorial", "I", "R", "D", "S"],
    ["Online matches, dedicated server and netcode", "I", "D", "R", "S"],
    ["Web API, database and AI coaching service", "S", "D", "R", "I"],
    ["Admin web application", "D", "R", "I", "S"],
    ["Unity user interface, settings and loading", "S", "I", "R", "D"],
    ["3D art, animation and courts", "I", "I", "D", "S"],
    ["Music and sound effects", "D", "I", "R", "I"],
    ["CI/CD and staging deployment", "I", "D", "S", "R"],
    ["Workflow W01 – Learn and sync a drill (F01–F04, F13, F14)", "R", "I", "S", "D"],
    ["Workflow W02 – Play a bot and get a tip (F05, F06, F11)", "R", "S", "D", "I"],
    ["Workflow W03 – Play online and save the result (F07–F10, F12)", "R", "D", "S", "I"],
    ["Workflow W04 – Review a report and publish a lesson (F15–F18)", "D", "R", "I", "S"],
], widths=[6.4, 2.4, 2.4, 2.4, 2.4], size=9, caption="Responsibility assignment matrix (RACI)")

r.h2("5. Project Communications")
r.table(["Communication Item", "Who/ Target", "Purpose", "When, Frequency", "Type, Tool, Method(s)"], [
    ["Supervisor meeting", "Team and Mr. Lại Đức Hùng", "Demonstrate progress, agree on scope changes, record decisions and answers", "Weekly, 60 minutes; extra meeting before each review", "Online or on-campus meeting; minutes and decisions in the Q&A sheet"],
    ["Iteration planning", "All members", "Select backlog items, estimate and assign owners", "First day of each two-week iteration", "Meeting; Iteration backlog in the tracking workbook"],
    ["Progress check", "All members", "Report done work, next work and blockers", "Twice a week (Tuesday and Friday evening)", "Group chat and short call"],
    ["Iteration review and retrospective", "All members, supervisor at review weeks", "Demonstrate the increment; decide improvements", "Last day of each iteration", "Meeting; notes in the tracking workbook"],
    ["Code review", "Author and one reviewer", "Check correctness, tests and architecture rules", "Each pull request (from Iteration 5)", "GitHub pull request with CI result"],
    ["Defect and issue updates", "Owner of the defect or issue", "Keep status, severity and owner current", "When status changes; reviewed weekly", "Tracking workbook (Defects and Issues sheets)"],
    ["Report submission", "Leader", "Submit reports and packages", "One day before each deadline", "FPT University LMS"],
], widths=[3.2, 3.0, 4.0, 3.0, 3.4], size=9, caption="Project communications")

r.h2("6. Configuration Management")
r.h3("6.1 Document Management")
r.bullets([
    "All project documents are stored in the Git repository under `Doc/`: the proposal in `Doc/Proposal`, review forms in `Doc/Project_report/Review*`, and Reports 1–7 with the tracking workbook and schedule in `Doc/Project_report/Report`.",
    "File names follow the course template: `Report<n>_<Title>.docx`, `PickleVerse_Project_Tracking.xlsx` and `PickleVerse_Project_Schedule.xml`.",
    "Each document records its versions in the Record of Changes table: v0.x for drafts and v1.0, v1.1 and so on for reviewed versions submitted to the supervisor.",
    "All diagrams are written in one tool, PlantUML, following UML 2.5: the sources are in `Doc/Project_report/Report/diagrams/src` and are rendered to PNG at 200 dpi, so every figure can be regenerated and reviewed as text. An older draw.io drawing of the use cases and the conceptual ERD was removed on 19/11/2026 because it no longer matched the product.",
    "A document is submitted only after it has been reviewed by the member marked R in Table 9 and the leader; the PDF exported from the reviewed version is uploaded to the LMS.",
])
r.h3("6.2 Source Code Management")
r.bullets([
    "Repository: GitHub `Tad-Min/PickleVerse`, one repository for the Unity client (`My project/`), the backend and admin web (`src/`), deployment files (`deploy/`, Docker Compose files) and documents.",
    "Current practice: members commit directly to `main` and merge local work with merge commits; the coaching work used the branch `realtime-AI-response`. Pull requests #2 to #6 were opened and merged on 21/11/2026 by the repository owner without a second reviewer, and the commits after that day were pushed directly again; no release tags are used, so reviews are informal. On 25/11/2026 `main` has 379 commits (at adcb4e2; 236 by Nguyễn Gia Huy, 131 by Nguyễn Văn Thành Đạt, 10 by Huỳnh Trung Kiên and 2 by Trần Nguyễn Hiếu Thuận, account anhbathung) and no tags; the repository is public on GitHub.",
    "Planned practice (Iteration 5, tried on 21/11/2026 only): every change is made on a branch named `feature/<item-id>-<short-name>` or `fix/<defect-id>`, merged into `main` through a pull request with one reviewer and a passing CI run. Review packages will be tagged `review-3` and `v1.0`, and the Review 2 package will be tagged retrospectively at the commit of 27/10/2026.",
    "Commit messages follow the Conventional Commits style (`feat:`, `fix:`, `chore:`, `docs:`) and reference the backlog item or defect ID. The rule is not enforced: 195 of the 379 commits on `main` (51%) use the style. Once pull requests are used, the reviewer rejects a pull request whose commits do not follow it.",
    "Large binary sources (Blender work files, local Unity work folders) are excluded by `.gitignore`; only exported models, textures and animation clips are committed.",
    "Secrets: on staging, connection strings, the JWT signing key, the SMTP password, Photon keys and the dedicated-server service key are supplied through environment variables and the server `.env` file. Until 13/11/2026 the repository also contained development credentials in `appsettings.json` and a fallback JWT key in `Program.cs` (defect D-17, High). Commit be556a1 emptied these values and the API now refuses to start without a JWT key of at least 32 bytes; the published images are in addition built from sanitised settings (`deploy/api/sanitize_settings.py`). The old values are still in the Git history of the public repository, so they must be rotated on staging and the rotation recorded. The rule from Iteration 5 is that no secret is committed, and a secret scan will be added to the pipeline.",
    "Delivery: on each push to `main` the GitHub Actions workflow `dockerhub.yml` reads the release that is deployed, plans which checks and images the changed files need, runs the IntegrationChecks, the FastAPI tests and the admin web type check, builds the Unity Linux dedicated server, publishes the changed images to Docker Hub (`tadminm/pickelverse`, tags `<component>-<commit>-<run>-<attempt>`), smoke-tests the four image digests and deploys them to the staging host over SSH with Docker Compose after a verified database dump. The pipeline first passed on 13/11/2026; of the 51 runs since 18/11/2026 33 passed, 15 were cancelled by a newer push and 3 failed (risk 13). Until 18/11/2026 the deploy job was skipped when the FastAPI or admin web checks were not needed (defect D-23, fixed in 291542a); since then every passing run deploys, and run 103 deployed main @ adcb4e2 on 25/11/2026. Schema changes are not part of the deployment (risk 17). Before 13/11/2026 the staging stack was built and pushed by hand as four releases tagged `api-`, `ai-`, `web-` and `server-manual-<date-time>` (08/11–10/11/2026). A commit that changes only documents builds and deploys nothing; the team also marks such commits `[skip ci]`.",
])
r.h3("6.3 Tools & Infrastructures")
r.table(["Category", "Tools / Infrastructure"], [
    ["Technology", "Unity 6000.6.2f1 with C# (game client and dedicated server); Photon Fusion 2.1.3; Unity Behavior 1.0.16; ASP.NET Core on .NET 10 (Web API); Python 3.12 with FastAPI and scikit-learn (AI service); React 18, TypeScript 5, Vite 5 and React Query (admin web)"],
    ["Database", "PostgreSQL 16 (Npgsql Entity Framework Core); Redis 7.4 (matchmaking queue, leases, presence, replay tickets, cache); S3-compatible object storage (replays, videos); PayOS (payment provider of the membership)"],
    ["IDEs/Editors", "Unity Editor and Unity CLI, Visual Studio Code, Visual Studio, pgAdmin 4, Docker Desktop, Photon Dashboard"],
    ["Art and audio", "Blender (rigging and animation), Meshy AI (base character meshes), Figma (interface mock-ups)"],
    ["Diagramming", "PlantUML with Graphviz (all UML diagrams and ERDs)"],
    ["Documentation", "Microsoft Office (Word, Excel, Project-compatible XML), Markdown"],
    ["Version Control", "Git and GitHub (source code and documents)"],
    ["CI/CD and deployment", "GitHub Actions (plan, check, build, publish and deploy jobs; run 103 of 25/11/2026 is the 33rd passing run since 18/11/2026), Docker and Docker Compose, Docker Hub; staging host tad-min.io.vn (Ubuntu 24.04, Caddy reverse proxy with HTTPS, deploy/linux/Caddyfile)"],
    ["Testing", "Unity Test Framework (EditMode), Unity CLI, pytest, IntegrationChecks console harness, Swagger UI; k6 is planned for the API load test (NFR-04)"],
    ["Project management", "Tracking workbook (backlog, WBS, issues, defects, Q&A) and the MS Project XML schedule; GitHub for code and pull requests"],
    ["AI assistants", "OpenAI Codex, Anthropic Claude Code and Meshy AI, declared in Table %d" % (r.tbl_no + 2)],
], widths=[3.6, 12.4], size=9.5, first_col_bold=True, caption="Tools and infrastructure")

r.p("**AI Usage Declaration.** Table %d declares the AI tools used in the project, as required by the capstone guideline. Every output was reviewed by the member who used the tool before it was committed; generated code had to pass the existing automated tests, and figures quoted in the reports were taken from real runs (Report 5, §5.1)." % (r.tbl_no + 1))
r.table(["Tool · version", "Purpose", "Scope", "Member", "How the output was checked"],
        [list(row) for row in AI_USAGE], widths=[2.8, 3.6, 4.0, 1.8, 3.8], size=8.5, caption="AI Usage Declaration")

out = r.save("Report2_Project Management Plan.docx", "Report 2 – Project Management Plan")
print(out, "total", total, counts, "tables", r.tbl_no, "figs", r.fig_no)
