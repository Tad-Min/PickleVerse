"""Report 5 - Software Test Documentation."""
import json
import os
from collections import Counter, OrderedDict

from report_lib import Report
import r5_cases as R

CHANGES = [
    ("27/10/2026", "A", "Nguyễn Gia Huy", "v0.1 – Test formula, workflows and demo scenarios prepared for the Review 2 commitment (BM04)."),
    ("06/11/2026", "A", "Nguyễn Văn Thành Đạt", "v0.2 – First test documentation: 116 planned cases, 36 unit checks, no results."),
    ("11/11/2026", "M", "Nguyễn Gia Huy", "v1.0 – Rewritten: scope with levels, entry and exit criteria; test types with objective, technique and completion criteria; tools with vendor and version; dated test milestones; 114 concrete test cases mapped to FR-01–FR-33; real results of 09–11/11/2026 (515 EditMode tests passed; 49 passed, 3 failed, 62 pending system cases); 18 defects; analysis against the release criteria."),
    ("11/11/2026", "M", "Nguyễn Gia Huy", "v1.1 – Human resources and workflow test cases: owners aligned with BM04."),
    ("12/11/2026", "M", "Nguyễn Gia Huy", "v1.2 – Revised after the second internal review: EditMode run of 516 tests on main @ 5963501 (EV-06) and Play Mode check of fixed defects (EV-07); defect statuses with fix commits; new defects D-19 and D-20; quick doubles case TC-F07-04; workflow names as in BM04; CI and Android status stated; counts in the text computed from the case list."),
    ("13/11/2026", "M", "Nguyễn Gia Huy", "v1.3 – Updated after the fixes of 12–13/11/2026 (main @ d9200cc): the GitHub Actions pipeline passes (EV-08), so 14 cases are now passed by the IntegrationChecks of its validate stage; transport and secret check EV-09 (D-18 fixed, D-17 partly fixed, TC-N09 still failed); Android APK built (EV-10) and defect D-21 found and fixed; D-20 accepted as designed; CI history corrected."),
    ("17/11/2026", "M", "Nguyễn Gia Huy", "v1.4 – Updated to main @ 96ff8e9 and the Review 1 comments: 9 new cases (display modes TC-F01-07/08, match detail TC-F11-04, social feed and messages TC-F19-01 to TC-F19-06); quick doubles case TC-F07-04 replaced by ranked doubles and passed by the CI check ranked-doubles; time-out transition and unit case removed; evidence EV-11 (CI run 52, 15 check groups), EV-12 (522 EditMode tests), EV-13 (Windows window test) and EV-14 (review of 17/11/2026); D-20 fixed by the new XP curve; new defects D-22 (video upload only in the Editor) and D-23 (skipped deploy job); traceability to FR-35."),
    ("18/11/2026", "M", "Nguyễn Gia Huy", "v1.5 – Results of the end-to-end run EV-15 (17–18/11/2026) on a Windows build of main @ 96ff8e9 with three player accounts and on the admin web: 19 more cases passed (rooms, human ranked 1v1, bot fallback timing, friends, messages and whispers, admin users, content, matches, analytics, appeals), 6 more failed (report from the match detail, MP4 upload, reconnection outcome, expert review message, workflow W04, room state SM-07); D-04, D-09, D-15, D-19 and D-20 closed after retest; new defects D-24 to D-28; expected result of TC-F18-02 corrected."),
    ("18/11/2026", "M", "Nguyễn Gia Huy", "v1.6 – Windows player rebuilt from main @ 96ff8e9 (no newer commit on GitHub) and the changes of b08f5c7 retested (EV-16): new cases TC-F08-04 (2v2 room by code, failed) and TC-F11-05 (replay, passed); ranked doubles with bot fill run in the client (TC-F07-04); new defects D-29 (guests stuck on the summary after a room match) and D-30 (no XP for custom matches against BR-32)."),
    ("18/11/2026", "M", "Nguyễn Gia Huy", "v1.7 – Defect fixes 291542a, 0cee928, 95ff4a4, 9557581 and 329810f on main and their retest on the Windows build and on staging (EV-17): D-29 diagnosed with an input probe and fixed; 10 more cases passed (TC-F06-03, TC-F08-04, TC-F09-03, TC-F17-01, TC-F19-05, TC-X01-02, TC-W04, TC-S07-02, TC-S11-01, TC-S11-02); TC-F09-03 and the SM-07 transition follow BR-28 v1.5; 18 defects closed; D-31 and D-32 added."),
]
EV_FILE = os.environ.get("PKV_EDITMODE_RESULTS")

r = Report("Report5_Test Documentation.docx", "Report 5 – Software Test Documentation", CHANGES)
r.h1("II. Testing Documentation")

# ------------------------------------------------------------------ 1
r.h2("1. Scope of Testing")
r.p("The target of test is PickleVerse release 1.0 as specified in Report 3: the ten features FE-01 to FE-10 with their functional requirements FR-01 to FR-35, the business rules BR-01 to BR-57, and the non-functional requirements NFR-01 to NFR-12. Testing covers the Unity game client (Windows build and Unity Editor), the dedicated game server, the Web API with its workers, the AI coaching service and the administration web application deployed on the staging server.")
r.p("**In scope.** All screens and non-screen functions of Report 3, the gameplay rules and physics parameters of Report 4 §3.2, online matchmaking and dedicated play, data integrity of rewards and results, role-based access, and the measurable quality attributes NFR-01 to NFR-12.")
r.p("**Out of scope.** iOS builds and real-money payment (Report 1, LI-02 and LI-06); load beyond the API capacity target and more simultaneous matches than the game-server containers started on staging (LI-07); third-party services themselves (Photon Cloud, Gmail SMTP), which are only tested through their integration points; and the tier model's accuracy until enough expert-labelled data exists (NFR-06).")
r.table(["Level", "In charge", "Inputs and time", "Focus", "Acceptance (exit) criteria"], [
    ["Unit", "Each module owner; review by Trần Nguyễn Hiếu Thuận", "Source code, BR-xx; before every merge", "Rules, scoring, Elo, input decoding, economy, offline store, matchmaking policies (Unity EditMode, pytest)", "All unit tests pass; every public rule method has at least one normal, abnormal and boundary case"],
    ["Integration", "Nguyễn Văn Thành Đạt", "API build, Database.sql seed, PostgreSQL and Redis; each package milestone", "API contracts, database writes, dedicated tickets and leases, training sync receipts, role and scope checks", "All IntegrationChecks pass on a fresh database; no contract difference between Unity, API and AI service"],
    ["System", "Trần Nguyễn Hiếu Thuận with the feature owners", "Staging deployment, test accounts and seed data; Rounds 1–3 (§3.3)", "Every FR through the user interface, state transitions, NFR measurements", "≥ 90% of system cases passed, all FRs covered by a passed case, no open Critical/High defect"],
    ["Acceptance", "Huỳnh Trung Kiên with the supervisor", "Release candidate, demo scenarios W01–W04", "End-to-end workflows of the four members", "All four workflows pass in front of the supervisor"],
], widths=[2.0, 3.0, 3.4, 4.2, 3.4], size=8.5, caption="Test levels: responsibility, inputs, focus and exit criteria")
r.p("**Constraints and assumptions.**")
r.bullets([
    "Each game-server container runs one match and staging starts PKV_GAME_REPLICAS containers (default 1); online cases that need two simultaneous matches need the replica count raised first.",
    "The GitHub Actions pipeline passes since 13/11/2026 (EV-08, EV-11) and runs the IntegrationChecks, the FastAPI tests and the admin web type check for the parts that a push changes, but it has no Unity test step. The Unity EditMode suite is therefore still run with the Unity CLI on a team PC and the result file is stored with the commit; its last run is EV-12 of 16/11/2026, before the changes of 16–17/11/2026 (b08f5c7 added EditMode tests that have not run yet). On main @ 96ff8e9 the test assemblies did not compile because of a duplicate reference (D-24); commit 291542a removed it and the Windows builds of 18/11/2026 compiled the synchronised tests, but the suite has not been run again.",
    "Android APKs were built on 12/11 and 14/11/2026 (EV-10), but no Android device has been used for testing yet. Both use protocol 18 while the servers use protocol 20 since 17/11/2026, so the Android cases (TC-N03, TC-N10) need a new APK from main @ 96ff8e9 and a reference device in Round 2. A Windows build of main @ 96ff8e9 (protocol 20) was made on 17/11/2026, used for the end-to-end run EV-15, and rebuilt from the same commit on 18/11/2026 for EV-16 (GitHub had no newer commit); the Windows builds of EV-17 contain the fixes of 18/11/2026, the last one the game code of main @ 329810f; the earlier Windows build of 16/11/2026 (EV-13) used protocol 19.",
    "Tests that need several players (rooms, friends, reports) require two or more accounts at the same time. In EV-15 and EV-17 three player windows of the Windows build ran on one PC, each signed in by the account owner; the tester never typed a password. Because the three players share the offline store of the PC, the offline case of EV-17 was run with the other two windows closed. Disconnections were simulated by suspending the player process, not by cutting the network of a device.",
    "The admin web moved to https://tad-min.io.vn on 13/11/2026 and was unreachable between the deployments of 01:18 and 14:34 that day; the admin web cases (TC-F15 to TC-F18, TC-X01, TC-W04) were run there on 17–18/11/2026 (EV-15).",
    "Cases run on staging create real data. The test data of EV-15 was removed or restored afterwards where the system allows it (posts, friendships, lesson text, names, the ban); audit records, match histories and messages are kept (evidence/e2e_run_20261117.md, §6).",
    "A dedicated test mailbox and seed accounts (for example a player at level 24 or with 9 passed lessons) must be added to Database.sql before Round 2.",
    "The Photon free plan limits concurrent users; group playtests are scheduled so that the limit is not reached.",
])

# ------------------------------------------------------------------ 2
r.h2("2. Test Strategy")
r.h3("2.1 Testing Types")
r.table(["Type", "Objective", "Technique", "Completion criteria"], [
    ["Functional", "Verify every FR and BR", "Black-box: equivalence partitions and boundary values from BR-xx; decision tables for scoring and Elo", "Each FR has ≥ 1 passed case; all Main cases passed"],
    ["Gameplay rules and physics", "Verify serve, faults, scoring, contact timing and bounce", "Automated EditMode tests on the engine-independent rule classes; scripted rallies", "All rule tests pass; every fault type has a test"],
    ["User interface and usability", "Verify layout, messages and learnability", "Checklist per screen against Report 3 §3.1 and the message list; moderated usability test (NFR-01)", "No overlapping or truncated elements; NFR-01 met"],
    ["Integration / API contract", "Verify API, database and service contracts", "IntegrationChecks console harness on a fresh database; shared JSON fixtures in contracts/", "All checks pass on the release commit"],
    ["Security", "Verify authentication, authorisation and secret handling", "Negative API tests with wrong roles and scopes, token expiry, rate limit; secret scan", "NFR-09 checklist satisfied; no secret in the repository"],
    ["Performance and load", "Verify NFR-02 to NFR-05", "Unity Profiler on reference devices; Fusion RTT statistics; k6 load test on the API", "All measured values within the NFR limits"],
    ["Network resilience", "Verify reconnection and interruption", "Disconnect a client for 20 s and 60 s; drop the dedicated server heartbeat", "Behaviour matches BR-28 and BR-29"],
    ["Compatibility", "Verify target platforms", "Install and smoke test on Windows 10/11 and Android 8+ (ARM64); admin web on Chrome and Edge", "NFR-10 met"],
    ["AI quality", "Verify coaching findings and the gated model", "Holdout evaluation against expert labels", "NFR-06 thresholds met, otherwise the model stays disabled"],
    ["Exploratory playtest and balance", "Find defects that scripted cases miss; tune bot difficulty", "Time-boxed sessions with a charter; win rate of players of known level against each bot level", "Each session logged; balance targets agreed with the supervisor"],
    ["Regression", "Keep fixed defects fixed", "Re-run the automated suites and the cases linked to closed defects in every round", "No reopened defect"],
], widths=[3.0, 3.6, 5.6, 3.8], size=8.5, caption="Testing types")
r.h3("2.2 Test Levels")
r.table(["Type of Tests", "Unit", "Integration", "System", "Acceptance"], [
    ["Functional", "X", "X", "X", "X"], ["Gameplay rules and physics", "X", "", "X", "X"], ["User interface and usability", "", "", "X", "X"],
    ["Integration / API contract", "", "X", "", ""], ["Security", "X", "X", "X", ""], ["Performance and load", "", "X", "X", ""],
    ["Network resilience", "X", "", "X", ""], ["Compatibility", "", "", "X", "X"], ["AI quality", "X", "", "X", ""],
    ["Exploratory playtest and balance", "", "", "X", "X"], ["Regression", "X", "X", "X", ""],
], widths=[6.0, 2.5, 2.5, 2.5, 2.5], size=9, caption="Test types per level")
r.h3("2.3 Supporting Tools")
r.table(["Purpose", "Tool", "Vendor/In-house", "Version"], [
    ["Unity unit tests (EditMode, NUnit)", "Unity Test Framework", "Unity Technologies", "1.8.0"],
    ["Run tests and capture screens from the command line", "Unity CLI with the Pipeline package", "Unity Technologies", "CLI 1.0.0-beta.17, Pipeline 0.9.0-exp.1"],
    ["Python unit tests", "pytest (with httpx)", "Open source", "8.x"],
    ["API and database integration checks", "IntegrationChecks console harness", "In-house", "main @ 96ff8e9, 15 check groups (run by the CI pipeline)"],
    ["Manual API calls", "Swagger UI", "Swashbuckle (open source)", "10.3.1"],
    ["API load test (NFR-04)", "k6", "Grafana Labs (open source)", "latest stable, planned"],
    ["Frame-rate measurement", "Unity Profiler", "Unity Technologies", "6000.6.2f1"],
    ["Network statistics", "Photon Fusion statistics and dashboard", "Exit Games", "Fusion 2.1.3"],
    ["Test case and result management", "Microsoft Excel (Report5 workbooks)", "Microsoft", "Microsoft 365"],
    ["Driving several Windows player windows (screenshots, clicks, key presses, process suspension)", "PowerShell script with Win32 calls", "In-house", "Used in EV-13 and EV-15"],
    ["Continuous integration", "GitHub Actions (plan, checks, build, publish and deploy jobs)", "GitHub", "Hosted runner ubuntu-24.04; 17 of 53 runs passed by 17/11/2026 (run 52 on main @ 96ff8e9)"],
], widths=[5.2, 4.4, 3.4, 3.0], size=9, caption="Supporting tools")

# ------------------------------------------------------------------ 3
r.h2("3. Test Plan")
r.h3("3.1 Human Resources")
r.table(["Worker/Doer", "Role", "Specific Responsibilities/Comments"], [
    ["Trần Nguyễn Hiếu Thuận", "Test lead", "Owns the test plan and workbooks; runs system rounds; tracks defects; tests the account, onboarding, offline and social screens (F01, F02, F12, F14); owner of workflow W01"],
    ["Nguyễn Gia Huy", "Tester, gameplay owner", "Unit tests of rules, physics, training and bot; playtests and balance sessions; runs the Unity EditMode suite; F03, F05, F11, F13; owner of workflow W02, support for W01 and W03"],
    ["Nguyễn Văn Thành Đạt", "Tester, backend and network owner", "IntegrationChecks, API security and load tests, online and dedicated cases (F04, F06–F10); owner of workflow W03, support for W02"],
    ["Huỳnh Trung Kiên", "Tester, leader", "Admin web cases (F15–F18), acceptance session with the supervisor, release decision; owner of workflow W04"],
    ["Mr. Lại Đức Hùng", "Supervisor", "Witnesses the acceptance scenarios and approves the release criteria"],
], widths=[4.0, 3.6, 8.4], size=9, caption="Human resources")
r.h3("3.2 Test Environment")
r.table(["Purpose", "Tool", "Provider", "Version"], [
    ["Staging server", "Ubuntu with Docker Engine and Docker Compose (API, AI service, dedicated server, PostgreSQL, Redis, S3 storage, web) behind a Caddy reverse proxy; API and admin web on https://tad-min.io.vn since 13/11/2026", "Team VPS tad-min.io.vn", "Ubuntu 24.04; images published by the CI run of main"],
    ["Game client under test", "Unity Editor and Windows build", "Unity Technologies", "6000.6.2f1"],
    ["Test PC", "Windows PC with keyboard and mouse", "Team member", "Windows 11 Home"],
    ["Android device", "Android phone (ARM64) for NFR-03 and NFR-10; APK of EV-10", "Team member", "Android 8.0 or later (to be recorded in Round 2)"],
    ["Real-time service", "Photon Cloud", "Exit Games", "Fusion 2.1.3"],
    ["Database and cache", "PostgreSQL, Redis", "Docker images postgres:16-alpine and redis:7.4-alpine", "16 / 7.4"],
    ["Admin web browser", "Google Chrome, Microsoft Edge", "Google, Microsoft", "Latest stable"],
], widths=[3.4, 6.2, 3.4, 3.0], size=8.5, caption="Test environment")
r.h3("3.3 Test Milestones")
r.table(["Milestone Task", "Start Date", "End Date"], [
    ["Round 1 – automated suites and first system pass on staging", "09/11/2026", "13/11/2026"],
    ["Prepare seed accounts, test mailbox and analytics seed", "16/11/2026", "18/11/2026"],
    ["Round 2 – all system cases; retest of the fixed defects and fix verification of D-01 to D-32; social feed and ranked doubles with several accounts (end-to-end run EV-15 on 17–18/11/2026)", "16/11/2026", "27/11/2026"],
    ["Android install, smoke and frame-rate test on a reference phone (NFR-03, NFR-10)", "16/11/2026", "27/11/2026"],
    ["Usability test with 10 students (NFR-01)", "23/11/2026", "27/11/2026"],
    ["Load, latency and frame-rate measurements (NFR-02 to NFR-05)", "23/11/2026", "29/11/2026"],
    ["Uptime probe for NFR-07 (14 days)", "30/11/2026", "13/12/2026"],
    ["Round 3 – regression on the release candidate", "30/11/2026", "06/12/2026"],
    ["Acceptance test W01–W04 with the supervisor", "07/12/2026", "11/12/2026"],
], widths=[10.0, 3.0, 3.0], size=9, caption="Test milestones")

# ------------------------------------------------------------------ 4
r.h2("4. Test Cases")
r.p("Unit test cases are documented in **Report5_Unit Test.xlsx** as decision tables (conditions, confirmations, type N/A/B and result for each UTCID) for ten core functions, each mapped to the NUnit test that implements it. Integration, system and acceptance test cases are documented in **Report5_Test Report.xlsx**, one sheet per feature group, with ID, description, procedure with concrete data, expected result, pre-conditions, three test rounds and a note that names the evidence or the reason why a case is pending.")
cnt = Counter(c["sheet"] for c in R.CASES)
r.p("The number of cases follows the Review 2 formula: 18 committed features × 3 cases (54) + 4 workflows + 42 state transitions (three per state machine SM-01 to SM-14) + 12 NFRs = 112 cases. Six cases are added: two for the expert review tool (FE-10), which is not in the Review 2 feature list, three for FR-03 to FR-05, which the Review 2 feature F01 did not detail, and one for doubles matchmaking (TC-F07-04), which F07 describes only in general, giving 118 cases. Version 1.4 adds nine cases for the changes of 15–17/11/2026 and the Review 1 comments – the PC display modes (TC-F01-07, TC-F01-08), the match detail (TC-F11-04) and the social feed and messages (TC-F19-01 to TC-F19-06). Version 1.6 adds two system cases for the 2v2 room and the replay changed in b08f5c7 (TC-F08-04, TC-F11-05), giving **%d cases**." % len(R.CASES))
r.table(["Sheet", "Scope", "Cases", "Unit", "Integration", "System", "Acceptance", "Automated"],
        [[s, name, str(cnt[s])] + [str(sum(1 for c in R.CASES if c["sheet"] == s and c["level"] == lv)) for lv in ("Unit", "Integration", "System", "Acceptance")] +
         [str(sum(1 for c in R.CASES if c["sheet"] == s and c["auto"] != "Manual"))] for s, name, _ in R.SHEETS] +
        [["**Total**", "", "**%d**" % len(R.CASES)] + ["**%d**" % sum(1 for c in R.CASES if c["level"] == lv) for lv in ("Unit", "Integration", "System", "Acceptance")] +
         ["**%d**" % sum(1 for c in R.CASES if c["auto"] != "Manual")]],
        widths=[2.6, 5.0, 1.2, 1.2, 1.6, 1.4, 1.6, 1.6], size=8.5, subheader_rows=(len(R.SHEETS),), caption="Test cases by sheet and level")
r.h3("4.1 Requirement Traceability")
trace = OrderedDict(("FR-%02d" % i, []) for i in range(1, 36))
for c in R.CASES:
    for tok in c["fr"].replace(";", ",").split(","):
        tok = tok.strip()
        if tok in trace:
            trace[tok].append(c)
rows = []
for fr, cs in trace.items():
    passed = sum(1 for c in cs if c["status"] == "Passed")
    rows.append([fr, ", ".join(c["id"] for c in cs) or "–", str(len(cs)), str(passed),
                 "Covered" if passed else ("Failed" if any(c["status"] == "Failed" for c in cs) else "Not yet covered")])
r.table(["Requirement", "Test cases", "Cases", "Passed", "Coverage status"], rows, widths=[2.0, 9.0, 1.4, 1.4, 2.2], size=8, caption="Traceability from functional requirements to test cases")
covered = sum(1 for row in rows if row[4] == "Covered")
missing = [row[0] for row in rows if row[4] != "Covered"]
nfr_cases = [c for c in R.CASES if c["sheet"] == "NFR"]
nfr_run = [c for c in nfr_cases if c["status"] != "Pending"]
r.p("%d of the 35 functional requirements are covered by at least one passed case after Round 1, the checks of 13–17/11/2026 (EV-08 to EV-13) and the end-to-end runs of 17–18/11/2026 (EV-15, EV-16) and the defect retest EV-17; %s have no passed case yet. NFR-01 to NFR-12 are traced one-to-one to TC-N01 to TC-N12: %d of the 12 have been executed (%s), none has passed." % (covered, ", ".join(missing), len(nfr_run), ", ".join("%s %s" % (c["id"], c["status"].lower()) for c in nfr_run) or "none"))

# ------------------------------------------------------------------ 5
r.h2("5. Test Reports")
r.h3("5.1 Evidence")
r.table(["ID", "Run", "Date", "Build / commit", "Result", "Location"], [
    ["EV-01", "Unity EditMode suite through the Unity CLI (Pipeline test run)", "11/11/2026", "main @ 7f8ee32", "515 tests, 515 passed, 0 failed, 6.19 s", "Doc/Project_report/Report/evidence/editmode_results_20261111.json"],
    ["EV-02", "Playtest on the staging server (offline training, signed-in menu, bot match, AI coaching, ranked 1v1 with bot fallback)", "10/11/2026", "main @ 82f6a7a client; staging images of 06/11/2026", "See Report5_Test Report.xlsx", "screenshots/01–28, evidence/playtest_log_20261110.md"],
    ["EV-03", "UI check of the register, verification, password-reset screens and the tutorial", "11/11/2026", "main @ 7f8ee32", "See TC-F01-03, TC-F02-01", "screenshots/29–34"],
    ["EV-04", "Match history of the team test accounts (two ranked 1v1 attempts)", "09/11/2026", "staging", "Both interrupted", "screenshots/12_history.png"],
    ["EV-05", "Team-recorded IntegrationChecks on disposable PostgreSQL databases (12 areas)", "06/11/2026", "df5f671", "Reported as passed; logs not in the repository, so not counted", "Review3_Evidence.md (file names and SHA-256)"],
    ["EV-06", "Unity EditMode suite through the Unity CLI after the UI and camera fixes (adds the camera framing test)", "11/11/2026", "main @ 5963501", "516 tests, 516 passed, 0 failed, 5.96 s", "Doc/Project_report/Report/evidence/editmode_results_20261111_5963501.json"],
    ["EV-07", "Unity Play Mode check of the fixed defects D-03, D-04, D-05, D-07 and D-19 (menu, mode selection, bot setup, bot match, training HUD)", "11/11/2026", "main @ 5963501", "All five behave as expected; to be retested on staging in Round 2", "screenshots/05, 11, 16, 17, 19"],
    ["EV-08", "GitHub Actions run 30 of the workflow \"Build, publish and deploy PickleVerse\": Backend integration checks (12 IntegrationChecks groups on PostgreSQL 16 and Redis 7.4), FastAPI tests, admin web type check, Unity Linux dedicated-server build, publish with smoke test, deployment over SSH. Runs 27 and 28 passed on main @ 1dfefe9", "13/11/2026 14:20–14:34", "main @ d9200cc", "All nine jobs and all their steps succeeded (14 min)", "evidence/ci_runs_20261113.json; github.com/Tad-Min/PickleVerse/actions/runs/43385071385"],
    ["EV-09", "Transport and secret check: HTTPS, redirects and open ports of the staging host; credential values in the tracked settings files. Run at 11:30 and again at 16:21 after commit be556a1 was deployed", "13/11/2026", "staging; main @ 1dfefe9 and d9200cc", "16:21: API and admin web only over HTTPS, port 8080 closed, settings files clean; old values remain in the Git history", "evidence/staging_check_20261113.md"],
    ["EV-10", "Android build with Unity batch mode and inspection of the APK manifest (aapt2, apksigner)", "12/11/2026", "main @ 5963501", "APK built (190.5 MB, IL2CPP, ARM64, API 26–36); not installed on a device; cleartext HTTP forbidden (D-21, fixed in be556a1)", "evidence/android_build_20261112.md"],
    ["EV-11", "GitHub Actions run 52: 15 IntegrationChecks groups (adds room-invitations, ranked-doubles and social-content), FastAPI tests, admin web type check, dedicated-server build, publish and deployment", "17/11/2026 13:04–13:20", "main @ 96ff8e9", "All jobs succeeded; staging runs this release", "evidence/ci_runs_20261117.json; github.com/Tad-Min/PickleVerse/actions/runs/44028526455"],
    ["EV-12", "Unity EditMode suite through the Unity CLI after the display-mode changes", "16/11/2026", "source committed as bc53722–1b64424", "522 tests, 522 passed, 0 failed, 19.05 s", "evidence/editmode_results_20261116_1b64424.json"],
    ["EV-13", "Windows build: display modes, window frame, minimise, drag and quit read with Win32 calls", "16/11/2026", "source of 1b64424", "All steps as expected (TC-F01-07)", "evidence/windows_window_test_20261116.md"],
    ["EV-14", "Review of the code, the CI history and staging for the reports v1.4", "17/11/2026", "main @ 96ff8e9", "D-22 and D-23 found; staging healthy, social API deployed", "evidence/review_20261117.md"],
    ["EV-17", "Defect fixes on main and their retest: D-29 diagnosis with an input probe, 2v2 room, report from the match detail with suspension and appeal, MP4 upload, forfeit after the reconnection window, offline analysis status, bot match screens, admin web fixes", "18/11/2026 15:20–21:35", "Windows builds with the fixes; staging release of 9557581", "10 more cases passed; 18 defects closed; D-31 and D-32 found", "evidence/bugfix_retest_20261118.md"],
    ["EV-16", "Rebuild of the Windows player and retest of b08f5c7: doubles room by code with team change, ranked doubles with bot fill, replay playback", "18/11/2026 08:24–09:02", "Windows build of main @ 96ff8e9 (rebuilt); staging release of run 52", "TC-F11-05 passed, TC-F08-04 failed; ranked doubles rated the human only; D-29 and D-30 found", "evidence/e2e_retest_20261118.md"],
    ["EV-15", "End-to-end run: three player windows of the Windows build (accounts signed in by their owners) and the admin web on staging – social functions, rooms, human ranked 1v1, bot fallback, reconnection, report, ban and appeal, lesson publication, analytics, expert review", "17/11/2026 20:35–21:45 and 18/11/2026 07:32–07:55", "Windows build of main @ 96ff8e9; staging release of run 52", "19 cases newly passed, 6 failed; D-04, D-09, D-15, D-19, D-20 closed; D-24 to D-28 found", "evidence/e2e_run_20261117.md; screenshots kept on the test PC"],
], widths=[1.2, 5.0, 1.8, 2.6, 2.6, 2.8], size=8, caption="Test evidence")
r.h3("5.2 Unit Test Results")
asm = OrderedDict()
if EV_FILE and os.path.exists(EV_FILE):
    res = json.load(open(EV_FILE, encoding="utf-8"))
    lst = json.load(open(os.path.join(os.path.dirname(EV_FILE), "editmode_list.json"), encoding="utf-8"))["data"]["result"]["Tests"]
    amap = {t["FullName"]: t["Assembly"] for t in lst}
    cmap = {t["FullName"].rsplit(".", 1)[0]: t["Assembly"] for t in lst}  # tests added after the list was exported
    nsmap = {t["FullName"].rsplit(".", 2)[0]: t["Assembly"] for t in lst}  # namespace of a class added after the export
    for x in res["results"]:
        cls = x["FullName"].split("(")[0].rsplit(".", 1)[0]
        a = amap.get(x["FullName"]) or cmap.get(cls) or nsmap.get(cls.rsplit(".", 1)[0], "?")
        a = a.replace("PickleVerse.", "")
        asm.setdefault(a, Counter())[x["Status"]] += 1
rows = [[a, str(sum(v.values())), str(v.get("Passed", 0)), str(v.get("Failed", 0))] for a, v in asm.items()]
rows.append(["**Total**", "**%d**" % sum(sum(v.values()) for v in asm.values()), "**%d**" % sum(v.get("Passed", 0) for v in asm.values()), "**%d**" % sum(v.get("Failed", 0) for v in asm.values())])
r.table(["Test assembly", "Tests", "Passed", "Failed"], rows, widths=[6.0, 2.5, 2.5, 2.5], size=9, subheader_rows=(len(rows) - 1,), caption="Unity EditMode results by assembly (EV-12)")
r.p("The table above shows the latest run (EV-12, 522 tests on 16/11/2026); the Round 1 run EV-01 on main @ 7f8ee32 had 515 tests and EV-06 on main @ 5963501 had 516, all passed. EV-12 adds the display settings tests and was run before three small later edits of the window frame; the tests added on 17/11/2026 (match detail, XP curve, ranked doubles roster, character animation) have not been run yet. The ten decision tables of Report5_Unit Test.xlsx contain %d unit test cases (normal, abnormal and boundary), all passed in EV-01, EV-06 and EV-12; the time-out case of UT-06 was removed with the rule on 15/11/2026. The other automated suites run in the GitHub Actions pipeline. In run 52 on main @ 96ff8e9 (EV-11) the jobs \"Backend integration checks\" (fifteen IntegrationChecks groups: client-security, api-contract, schema, status-enums, content, training-sync, progress-reset, moderation, analytics, expert-review, room-invitations, dedicated, ranked-doubles, social-content and hybrid, each on a disposable PostgreSQL 16 database with Redis 7.4), \"FastAPI tests\" (pytest of the AI service) and \"Admin web type check\" all succeeded. A check group stops with an error at its first failed assertion, so a successful step means that every assertion passed. The earlier team runs of 06/11/2026 (EV-05) stay uncounted because their logs are not in the repository. The Unity EditMode suite is not part of the pipeline yet, and the job logs are kept by GitHub, not in the repository; the job results are stored in evidence/ci_runs_20261117.json. Code coverage has not been measured yet; the Unity Code Coverage package will be enabled in Round 2." % sum(len(f["cases"]) for f in R.UNIT))
r.h3("5.3 System and Integration Test Results")
st, lv, auto = R.stats()
rows = []
for s, name, _ in R.SHEETS:
    cs = [c for c in R.CASES if c["sheet"] == s]
    p = sum(1 for c in cs if c["status"] == "Passed")
    f = sum(1 for c in cs if c["status"] == "Failed")
    pe = sum(1 for c in cs if c["status"] == "Pending")
    rows.append([name, str(len(cs)), str(p), str(f), str(pe)])
rows.append(["**Total**", "**%d**" % len(R.CASES), "**%d**" % st["Passed"], "**%d**" % st["Failed"], "**%d**" % st["Pending"]])
r.table(["Module", "Cases", "Passed", "Failed", "Pending"], rows, widths=[8.0, 2.0, 2.0, 2.0, 2.0], size=9, subheader_rows=(len(rows) - 1,), caption="Results by module after Round 1, the checks of 13–17/11/2026, the end-to-end runs of 17–18/11/2026 and the defect retest of 18/11/2026")
executed = st["Passed"] + st["Failed"]
open_high = [x[0] for x in R.DEFECTS if x[3] == "High" and R.defect_open(x[0])]
r.table(["Metric", "Value", "Target", "Status"], [
    ["Test coverage (executed / total)", "%.1f%% (%d / %d)" % (executed * 100.0 / len(R.CASES), executed, len(R.CASES)), "100% before release", "Not met"],
    ["Pass rate of executed cases", "%.1f%% (%d / %d)" % (st["Passed"] * 100.0 / executed, st["Passed"], executed), "≥ 90%", "Met for executed cases"],
    ["Test successful coverage (passed / total)", "%.1f%%" % (st["Passed"] * 100.0 / len(R.CASES)), "≥ 90%", "Not met"],
    ["Automated cases", "%.1f%% (%d / %d)" % (auto * 100.0 / len(R.CASES), auto, len(R.CASES)), "≥ 50%, run in CI", "Not met (%d of them run in CI)" % sum(1 for c in R.CASES if c["tester"] == R.CI_TESTER)],
    ["Functional requirements covered by a passed case", "%d / 35" % covered, "35 / 35", "Not met"],
    ["Open Critical / High defects", "0 / %d (%s)" % (len(open_high), ", ".join(open_high)), "0 / 0", "Not met"],
    ["Non-functional requirements measured and met", "%d measured, 0 met" % len(nfr_run), "12 / 12", "Not met"],
], widths=[6.0, 3.6, 3.2, 3.2], size=9, caption="Release criteria on 18/11/2026")
r.h3("5.4 Defects")
r.table(["ID", "Summary", "Type", "Severity", "Found by", "Owner", "Status"],
        [[a, b, c, d_, e, g, R.defect_status(a) + (" (" + R.DEFECT_FIX[a] + ")" if a in R.DEFECT_FIX else "")] for a, b, c, d_, e, f, g in R.DEFECTS],
        widths=[1.1, 6.4, 2.2, 1.5, 1.8, 1.4, 1.7], size=8, caption="Defects found in Round 1, in the checks of 11–17/11/2026 and in the end-to-end runs of 17–18/11/2026")
sev = Counter(dft[3] for dft in R.DEFECTS)
fixed = [x for x in R.DEFECTS if R.defect_status(x[0]) == "Fixed"]
closed = [x for x in R.DEFECTS if R.defect_status(x[0]) == "Closed"]
still_open = [x[0] for x in R.DEFECTS if R.defect_open(x[0])]
r.p("Round 1 (09–11/11/2026), the follow-up checks of 11–17/11/2026 and the end-to-end runs of 17–18/11/2026 (EV-15, EV-16) found %d defects: %d High, %d Medium and %d Low. %d of them (%s) were closed after a successful retest in EV-15. %d more (%s) are fixed in code and wait for their retest; the fix commit is shown in the Status column. The user-interface fixes were checked in Unity Play Mode (EV-07), the HTTPS switch of D-18 was verified on staging (EV-09) and D-21 is fixed by the same commit but not yet verified with a new Android build. %d defects still need work, including %d High defects (%s)." % (len(R.DEFECTS), sev["High"], sev["Medium"], sev["Low"], len(closed), ", ".join(x[0] for x in closed), len(fixed), ", ".join(x[0] for x in fixed), len(still_open), len(open_high), ", ".join(open_high)))
r.bullets(["**%s – %s (%s).** %s." % (k, v[0], v[1], v[2]) for k, v in R.DEFECT_STATE.items()] +
          ["**%s – closed (%s).** %s." % (k, v[0], v[1]) for k, v in R.DEFECT_CLOSED.items()])
r.p("Severity follows Report 3 NFR-08: Critical means loss of player data or inability to sign in or play; High means a main workflow or a security requirement fails; Medium means a requirement is partly wrong or a rule is inconsistent; Low means a cosmetic or wording problem. The defect life cycle (Open → Fixed → Verified → Closed) is tracked in PickleVerse_Project_Tracking.xlsx.")
r.h3("5.5 Analysis and Next Actions")
r.bullets([
    "**Engine-level quality is good; the EditMode suite compiles again but has not been re-run.** All 522 EditMode tests passed on 16/11/2026 (EV-12). The duplicate assembly reference of main @ 96ff8e9 was removed in 291542a (D-24) and the Windows builds of 18/11/2026 compiled the synchronised test assemblies, but the suite with the tests added by b08f5c7 has not been run. Every executable single-player system case now passes, including the offline analysis status (D-02, EV-17).",
    "**Online play, rooms and reports now work end to end.** In EV-15 two human accounts were matched, played a ranked 1v1 to the end on the dedicated server and received the result, the Elo change and a replay, and a private room started and was stopped by the administrator, so D-09 is closed and workflow W03 passed in both variants. Ranked doubles with bot fill and the replay changed in b08f5c7 also work in the client (EV-16). The three remaining blockers were fixed on 18/11/2026 and retested in EV-17: guests leave the summary of a 2v2 room normally after the host closes the party (D-29; a temporary input probe showed that the guests had been left on Fusion's empty scene without an EventSystem); a player reports an opponent from the match detail and the administrator decides the report, so workflow W04 passed with a suspension and an accepted appeal (D-27); and the forfeit after the reconnection window matches the corrected rule BR-28 v1.5 (D-28; TC-F09-03 passed with −17 Elo and no reward). After a normal end the lobby shows \"Trận đã kết thúc.\" instead of \"Mất kết nối Photon.\" (D-14). The stale restriction text after an accepted appeal (D-32, Low) remains for Round 2 (owner: Thuận).",
    "**The admin web passed its first full run.** Users, progress reset with preview and the refusal during a search, lesson draft and publication with the stale-version check, live match monitoring with stop, analytics with the 366-day limit, and the appeal review all worked on staging (EV-15). The follow-up fixes of 0cee928 are on staging since 18/11/2026: the header shows the real API origin (D-25), the expert review shows MSG64 (D-26) and the ranking reset returns Elo to 100 as BR-23 requires (D-08).",
    "**Security improved on 13/11/2026 but is not closed.** Commit be556a1 moved the game client, the API and the admin web to https://tad-min.io.vn, closed port 8080 and removed the credentials from the settings file and the fallback JWT key from the code; EV-09 confirms the transport part (D-18 fixed). The removed values are still readable in the Git history, so D-17 stays partly fixed and TC-N09 failed until the team rotates them on staging and records the rotation; the rest of the NFR-09 checklist and a secret scan in the pipeline are planned for Round 2 (owner: Đạt).",
    "**The social feature asked for at Review 1 now works with video on Windows.** Besides the CI check social-content (EV-11), three accounts used the screens in EV-15: friend requests, accept and decline, a friends-only post with like and comment, direct messages with the unread count and a whisper to a non-friend all behaved as specified (TC-F12-01, TC-F19-06). In EV-17 a training video chosen in the Windows file dialog was uploaded, played and deleted (TC-F19-05 passed). The Android build still cannot pick a video, so D-22 stays partly fixed until a native picker is added and tested on a device in Round 2 (owner: Đạt).",
    "**Coverage grew through the pipeline and the end-to-end run, and is now limited by test data.** The CI run EV-08 moved %d cases to Passed; run 52 (EV-11) added the ranked doubles and social checks; EV-15 passed 19 more system and state cases with three accounts and EV-17 passed the 10 cases that the fixed defects had blocked. %d cases are still pending, mainly because a test mailbox (registration, password reset, tutorial for a new account), seed data for state transitions and the NFR measurements are missing. The seed data and the mailbox must be added before Round 2 ends (owner: Thuận)." % (len(R.CI_PASSED), st["Pending"]),
    "**Automation is below the 50%% target.** %d of %d cases are automated: %d by the Unity EditMode suite, which is still run by hand, and %d by the IntegrationChecks of the CI validate job. Next: add the EditMode suite as a GameCI unity-test-runner step that reuses the licence secrets of the dedicated-server build, and add the check groups that the job does not run yet (admin authorisation, shared replays, social, password recovery) so that the remaining state-transition cases can be linked to them." % (auto, len(R.CASES), sum(1 for c in R.CASES if c["auto"].startswith(R.EM)), len(R.CI_PASSED)),
    "**%d defects are closed, %d are fixed but not yet closed and %d still need work.** EV-15 closed D-04, D-09, D-15, D-19 and D-20; EV-17 closed 18 more, including D-28 and D-30 by aligning Report 3 v1.5 with the implemented rules. Fixed but not closed: D-14 and D-31 (planned server stop and the two fault names checked by review only), D-18 (verified in EV-09), D-21 (needs an Android device) and D-23 (skipped-check case not yet seen). Still open: D-13 (the hard bot needs a balance session with real players), D-17 (rotate the old credentials on staging), D-22 (Android video upload) and D-32 (stale restriction text). D-12 is accepted as the release 1.0 scope of BR-36. The membership subscription decided after Review 1 (Report 1, CR-01) has no test case yet because it is not implemented." % (len(closed), len(fixed), len(still_open)),
    "**Android is built but untested.** APKs were built on 12/11 and 14/11/2026 (EV-10) and the inspection found D-21; no Android device has run the game yet. A new APK from main @ 96ff8e9, which uses the HTTPS address and protocol 20, is needed for TC-N03 and TC-N10 in Round 2.",
    "**The delivery pipeline works, but does not always deploy.** By 17/11/2026 17 of 53 runs have passed (EV-11, EV-14); before 13/11/2026 the failures came from configuration errors, a billing lock of the GitHub account (06/11–11/11), dedicated-server build failures and deployment steps. In nine passing runs of 13–16/11/2026 the deploy job was skipped because the FastAPI and admin web checks were skipped (D-23), so staging stayed on the release of 15/11/2026 until run 52 deployed main @ 96ff8e9 on 17/11/2026. Commit 291542a changed the condition of the deploy job (D-23); the two runs of 18/11/2026 passed every job and deployed the fixes.",
])

out = r.save("Report5_Test Documentation.docx", "Report 5 – Software Test Documentation")
print(out, "tables", r.tbl_no, "figures", r.fig_no, "covered", covered)
