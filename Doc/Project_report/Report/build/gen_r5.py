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
]
EV_FILE = os.environ.get("PKV_EDITMODE_RESULTS")

r = Report("Report5_Test Documentation.docx", "Report 5 – Software Test Documentation", CHANGES)
r.h1("II. Testing Documentation")

# ------------------------------------------------------------------ 1
r.h2("1. Scope of Testing")
r.p("The target of test is PickleVerse release 1.0 as specified in Report 3: the ten features FE-01 to FE-10 with their functional requirements FR-01 to FR-33, the business rules BR-01 to BR-51, and the non-functional requirements NFR-01 to NFR-12. Testing covers the Unity game client (Windows build and Unity Editor), the dedicated game server, the Web API with its workers, the AI coaching service and the administration web application deployed on the staging server.")
r.p("**In scope.** All screens and non-screen functions of Report 3, the gameplay rules and physics parameters of Report 4 §3.2, online matchmaking and dedicated play, data integrity of rewards and results, role-based access, and the measurable quality attributes NFR-01 to NFR-12.")
r.p("**Out of scope.** iOS builds and real-money payment (Report 1, LI-02 and LI-06); load beyond the API capacity target, because the staging deployment has one dedicated-server slot (LI-07); third-party services themselves (Photon Cloud, Gmail SMTP), which are only tested through their integration points; and the tier model's accuracy until enough expert-labelled data exists (NFR-06).")
r.table(["Level", "In charge", "Inputs and time", "Focus", "Acceptance (exit) criteria"], [
    ["Unit", "Each module owner; review by Trần Nguyễn Hiếu Thuận", "Source code, BR-xx; before every merge", "Rules, scoring, Elo, input decoding, economy, offline store, matchmaking policies (Unity EditMode, pytest)", "All unit tests pass; every public rule method has at least one normal, abnormal and boundary case"],
    ["Integration", "Nguyễn Văn Thành Đạt", "API build, Database.sql seed, PostgreSQL and Redis; each package milestone", "API contracts, database writes, dedicated tickets and leases, training sync receipts, role and scope checks", "All IntegrationChecks pass on a fresh database; no contract difference between Unity, API and AI service"],
    ["System", "Trần Nguyễn Hiếu Thuận with the feature owners", "Staging deployment, test accounts and seed data; Rounds 1–3 (§3.3)", "Every FR through the user interface, state transitions, NFR measurements", "≥ 90% of system cases passed, all FRs covered by a passed case, no open Critical/High defect"],
    ["Acceptance", "Huỳnh Trung Kiên with the supervisor", "Release candidate, demo scenarios W01–W04", "End-to-end workflows of the four members", "All four workflows pass in front of the supervisor"],
], widths=[2.0, 3.0, 3.4, 4.2, 3.4], size=8.5, caption="Test levels: responsibility, inputs, focus and exit criteria")
r.p("**Constraints and assumptions.**")
r.bullets([
    "The staging server runs one dedicated-server container, so online cases that need two simultaneous matches cannot be executed.",
    "The GitHub Actions pipeline passes since 13/11/2026 (EV-08) and runs the IntegrationChecks, the FastAPI tests and the admin web type check for the parts that a push changes, but it has no Unity test step. The Unity EditMode suite is therefore still run with the Unity CLI on a team PC and the result file is stored with the commit hash; its last run is on main @ 5963501, before the client address changed in be556a1.",
    "An Android APK was built on 12/11/2026 (EV-10), but no Android phone has been used for testing yet. That APK predates the HTTPS switch, so the Android cases (TC-N03, TC-N10) need a new APK from main @ d9200cc and a reference device in Round 2.",
    "Tests that need several players (rooms, friends, reports) require two or more accounts and devices at the same time.",
    "The admin web moved to https://tad-min.io.vn on 13/11/2026 and was unreachable between the deployments of 01:18 and 14:34 that day; the admin web cases (TC-F15 to TC-F18, TC-W04) are run against the new address in Round 2.",
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
    ["API and database integration checks", "IntegrationChecks console harness", "In-house", "main @ d9200cc (run by the CI pipeline)"],
    ["Manual API calls", "Swagger UI", "Swashbuckle (open source)", "10.3.1"],
    ["API load test (NFR-04)", "k6", "Grafana Labs (open source)", "latest stable, planned"],
    ["Frame-rate measurement", "Unity Profiler", "Unity Technologies", "6000.6.2f1"],
    ["Network statistics", "Photon Fusion statistics and dashboard", "Exit Games", "Fusion 2.1.3"],
    ["Test case and result management", "Microsoft Excel (Report5 workbooks)", "Microsoft", "Microsoft 365"],
    ["Continuous integration", "GitHub Actions (plan, checks, build, publish and deploy jobs)", "GitHub", "Hosted runner ubuntu-24.04; runs 27, 28, 30, 33 and 34 of 13/11/2026 passed every job"],
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
    ["Round 2 – all system cases; retest of the fixed defects and fix verification of D-01 to D-21", "16/11/2026", "27/11/2026"],
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
r.p("The number of cases follows the Review 2 formula: 18 committed features × 3 cases (54) + 4 workflows + 42 state transitions (three per state machine SM-01 to SM-14) + 12 NFRs = 112 cases. Six cases are added: two for the expert review tool (FE-10), which is not in the Review 2 feature list, three for FR-03 to FR-05, which the Review 2 feature F01 did not detail, and one for quick doubles (TC-F07-04), which F07 describes only in general, giving **%d cases**." % len(R.CASES))
r.table(["Sheet", "Scope", "Cases", "Unit", "Integration", "System", "Acceptance", "Automated"],
        [[s, name, str(cnt[s])] + [str(sum(1 for c in R.CASES if c["sheet"] == s and c["level"] == lv)) for lv in ("Unit", "Integration", "System", "Acceptance")] +
         [str(sum(1 for c in R.CASES if c["sheet"] == s and c["auto"] != "Manual"))] for s, name, _ in R.SHEETS] +
        [["**Total**", "", "**%d**" % len(R.CASES)] + ["**%d**" % sum(1 for c in R.CASES if c["level"] == lv) for lv in ("Unit", "Integration", "System", "Acceptance")] +
         ["**%d**" % sum(1 for c in R.CASES if c["auto"] != "Manual")]],
        widths=[2.6, 5.0, 1.2, 1.2, 1.6, 1.4, 1.6, 1.6], size=8.5, subheader_rows=(len(R.SHEETS),), caption="Test cases by sheet and level")
r.h3("4.1 Requirement Traceability")
trace = OrderedDict(("FR-%02d" % i, []) for i in range(1, 34))
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
r.p("%d of the 33 functional requirements are covered by at least one passed case after Round 1 and the CI run of 13/11/2026 (EV-08); %s have no passed case yet. NFR-01 to NFR-12 are traced one-to-one to TC-N01 to TC-N12: %d of the 12 have been executed (%s), none has passed." % (covered, ", ".join(missing), len(nfr_run), ", ".join("%s %s" % (c["id"], c["status"].lower()) for c in nfr_run) or "none"))

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
], widths=[1.2, 5.0, 1.8, 2.6, 2.6, 2.8], size=8, caption="Test evidence")
r.h3("5.2 Unit Test Results")
asm = OrderedDict()
if EV_FILE and os.path.exists(EV_FILE):
    res = json.load(open(EV_FILE, encoding="utf-8"))
    lst = json.load(open(os.path.join(os.path.dirname(EV_FILE), "editmode_list.json"), encoding="utf-8"))["data"]["result"]["Tests"]
    amap = {t["FullName"]: t["Assembly"] for t in lst}
    cmap = {t["FullName"].rsplit(".", 1)[0]: t["Assembly"] for t in lst}  # tests added after the list was exported
    for x in res["results"]:
        a = amap.get(x["FullName"]) or cmap.get(x["FullName"].split("(")[0].rsplit(".", 1)[0], "?")
        a = a.replace("PickleVerse.", "")
        asm.setdefault(a, Counter())[x["Status"]] += 1
rows = [[a, str(sum(v.values())), str(v.get("Passed", 0)), str(v.get("Failed", 0))] for a, v in asm.items()]
rows.append(["**Total**", "**%d**" % sum(sum(v.values()) for v in asm.values()), "**%d**" % sum(v.get("Passed", 0) for v in asm.values()), "**%d**" % sum(v.get("Failed", 0) for v in asm.values())])
r.table(["Test assembly", "Tests", "Passed", "Failed"], rows, widths=[6.0, 2.5, 2.5, 2.5], size=9, subheader_rows=(len(rows) - 1,), caption="Unity EditMode results by assembly (EV-06)")
r.p("The table above shows the latest run (EV-06, 516 tests); the Round 1 run EV-01 on main @ 7f8ee32 had 515 tests, all passed, and EV-06 adds the camera framing regression test of D-03. The ten decision tables of Report5_Unit Test.xlsx contain %d unit test cases (normal, abnormal and boundary), all passed in EV-01 and EV-06. The other automated suites run in the GitHub Actions pipeline. In run 30 on main @ d9200cc (EV-08) the jobs \"Backend integration checks\" (twelve IntegrationChecks groups: client-security, api-contract, schema, status-enums, content, training-sync, progress-reset, moderation, analytics, expert-review, dedicated and hybrid, each on a disposable PostgreSQL 16 database with Redis 7.4), \"FastAPI tests\" (pytest of the AI service) and \"Admin web type check\" all succeeded. A check group stops with an error at its first failed assertion, so a successful step means that every assertion passed. The backend integration checks have passed in every run since run 20 of 11/11/2026 (twelve runs); the FastAPI tests failed once, in run 29, and passed again in run 30. The earlier team runs of 06/11/2026 (EV-05) stay uncounted because their logs are not in the repository. The Unity EditMode suite is not part of the pipeline yet, and the job logs are kept by GitHub, not in the repository; the step results are stored in evidence/ci_runs_20261113.json. Code coverage has not been measured yet; the Unity Code Coverage package will be enabled in Round 2." % sum(len(f["cases"]) for f in R.UNIT))
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
r.table(["Module", "Cases", "Passed", "Failed", "Pending"], rows, widths=[8.0, 2.0, 2.0, 2.0, 2.0], size=9, subheader_rows=(len(rows) - 1,), caption="Results by module after Round 1 and the CI run of 13/11/2026")
executed = st["Passed"] + st["Failed"]
open_high = [x[0] for x in R.DEFECTS if x[3] == "High" and R.defect_open(x[0])]
r.table(["Metric", "Value", "Target", "Status"], [
    ["Test coverage (executed / total)", "%.1f%% (%d / %d)" % (executed * 100.0 / len(R.CASES), executed, len(R.CASES)), "100% before release", "Not met"],
    ["Pass rate of executed cases", "%.1f%% (%d / %d)" % (st["Passed"] * 100.0 / executed, st["Passed"], executed), "≥ 90%", "Met for executed cases"],
    ["Test successful coverage (passed / total)", "%.1f%%" % (st["Passed"] * 100.0 / len(R.CASES)), "≥ 90%", "Not met"],
    ["Automated cases", "%.1f%% (%d / %d)" % (auto * 100.0 / len(R.CASES), auto, len(R.CASES)), "≥ 50%, run in CI", "Not met (%d of them run in CI)" % sum(1 for c in R.CASES if c["tester"] == R.CI_TESTER)],
    ["Functional requirements covered by a passed case", "%d / 33" % covered, "33 / 33", "Not met"],
    ["Open Critical / High defects", "0 / %d (%s)" % (len(open_high), ", ".join(open_high)), "0 / 0", "Not met"],
    ["Non-functional requirements measured and met", "%d measured, 0 met" % len(nfr_run), "12 / 12", "Not met"],
], widths=[6.0, 3.6, 3.2, 3.2], size=9, caption="Release criteria on 13/11/2026")
r.h3("5.4 Defects")
r.table(["ID", "Summary", "Type", "Severity", "Found by", "Owner", "Status"],
        [[a, b, c, d_, e, g, R.defect_status(a) + (" (" + R.DEFECT_FIX[a] + ")" if a in R.DEFECT_FIX else "")] for a, b, c, d_, e, f, g in R.DEFECTS],
        widths=[1.1, 6.4, 2.2, 1.5, 1.8, 1.4, 1.7], size=8, caption="Defects found in Round 1 and in the checks of 11–13/11/2026")
sev = Counter(dft[3] for dft in R.DEFECTS)
fixed = [x for x in R.DEFECTS if x[0] in R.DEFECT_FIX]
still_open = [x[0] for x in R.DEFECTS if R.defect_open(x[0])]
r.p("Round 1 (09–11/11/2026) and the follow-up checks of 11–13/11/2026 found %d defects: %d High, %d Medium and %d Low. %d of them (%s) are fixed in code; the fix commit is shown in the Status column. The five user-interface fixes were checked in Unity Play Mode (EV-07), the HTTPS switch of D-18 was verified on staging (EV-09), and D-21 is fixed by the same commit but not yet verified with a new Android build. Fixed defects stay in the Fixed state until the Round 2 retest. %d defects still need work, including %d High defects (%s)." % (len(R.DEFECTS), sev["High"], sev["Medium"], sev["Low"], len(fixed), ", ".join(x[0] for x in fixed), len(still_open), len(open_high), ", ".join(open_high)))
r.bullets(["**%s – %s (%s).** %s." % (k, v[0], v[1], v[2]) for k, v in R.DEFECT_STATE.items()])
r.p("Severity follows Report 3 NFR-08: Critical means loss of player data or inability to sign in or play; High means a main workflow or a security requirement fails; Medium means a requirement is partly wrong or a rule is inconsistent; Low means a cosmetic or wording problem. The defect life cycle (Open → Fixed → Verified → Closed) is tracked in PickleVerse_Project_Tracking.xlsx.")
r.h3("5.5 Analysis and Next Actions")
r.bullets([
    "**Engine-level quality is good.** All 516 EditMode tests pass (EV-06), and every system case that could be executed for the single-player features (training, bot match, coaching, progress screens) passed, apart from the offline analysis status (D-02).",
    "**Online play between humans is the main risk.** The only human-versus-human ranked attempts (EV-04) were interrupted (D-09), which also blocks the reconnection, report and appeal cases. Reproducing D-09 with server logs is the first task of Round 2 (owner: Đạt, by 18/11/2026).",
    "**Security improved on 13/11/2026 but is not closed.** Commit be556a1 moved the game client, the API and the admin web to https://tad-min.io.vn, closed port 8080 and removed the credentials from the settings file and the fallback JWT key from the code; EV-09 confirms the transport part (D-18 fixed). The removed values are still readable in the Git history, so D-17 stays partly fixed and TC-N09 failed until the team rotates them on staging and records the rotation; the rest of the NFR-09 checklist and a secret scan in the pipeline are planned for Round 2 (owner: Đạt).",
    "**Coverage grew through the pipeline, and is now limited by test data.** The CI run EV-08 moved %d cases to Passed and three more requirements (FR-20, FR-32, FR-33) to Covered. %d cases are still pending, mainly because seed accounts, a test mailbox and a second player were not available; %d of them are partly covered by a CI check and still need the missing step named in their note. The seed data will be added to Database.sql before Round 2 (owner: Thuận, by 18/11/2026)." % (len(R.CI_PASSED), st["Pending"], len(R.CI_PARTLY)),
    "**Automation is below the 50%% target.** %d of %d cases are automated: %d by the Unity EditMode suite, which is still run by hand, and %d by the IntegrationChecks of the CI validate job. Next: add the EditMode suite as a GameCI unity-test-runner step that reuses the licence secrets of the dedicated-server build, and add the check groups that the job does not run yet (admin authorisation, shared replays, social, password recovery) so that the remaining state-transition cases can be linked to them." % (auto, len(R.CASES), sum(1 for c in R.CASES if c["auto"].startswith(R.EM)), len(R.CI_PASSED)),
    "**%d defects are fixed but not yet closed.** D-03, D-04, D-05, D-07 and D-19 were fixed on 11/11/2026 and checked in Play Mode; D-18 and D-21 were fixed by be556a1 on 13/11/2026. All are closed only after the Round 2 retest on the staging build. D-20 (60,000 XP for the GEMSTONE level) was accepted as designed on 13/11/2026 and is closed without a fix; D-12 (no Elite Token source) is still open." % len(fixed),
    "**Android is built but untested.** An APK was built from main @ 5963501 on 12/11/2026 (EV-10) and its inspection found D-21; no Android device has run the game yet. A new APK from main @ d9200cc, which uses the HTTPS address, is needed for TC-N03 and TC-N10 in Round 2.",
    "**The delivery pipeline works.** Of the 35 runs, 30 did not pass: two configuration failures on 05/11, 18 runs that did not start while the GitHub account was locked for a billing issue from 06/11 to 11/11, six dedicated-server build failures, one failed FastAPI test, two failed deployment steps (one of them a manual re-run on 13/11 at 16:07, fixed by commit ef72552) and one cancelled manual run. Runs 27, 28, 30, 33 and 34 of 13/11/2026 completed checks, build, publication and deployment in about 15 minutes each, and run 34 deployed the release that is on staging now.",
])

out = r.save("Report5_Test Documentation.docx", "Report 5 – Software Test Documentation")
print(out, "tables", r.tbl_no, "figures", r.fig_no, "covered", covered)
