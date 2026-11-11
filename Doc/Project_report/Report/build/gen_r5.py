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
    "GitHub Actions has no Unity licence; Unity tests are run with the Unity CLI on a team PC and the result file is stored with the commit hash.",
    "Tests that need several players (rooms, friends, reports) require two or more accounts and devices at the same time.",
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
    ["API and database integration checks", "IntegrationChecks console harness", "In-house", "main @ 7f8ee32"],
    ["Manual API calls", "Swagger UI", "Swashbuckle (open source)", "10.3.1"],
    ["API load test (NFR-04)", "k6", "Grafana Labs (open source)", "latest stable, planned"],
    ["Frame-rate measurement", "Unity Profiler", "Unity Technologies", "6000.6.2f1"],
    ["Network statistics", "Photon Fusion statistics and dashboard", "Exit Games", "Fusion 2.1.3"],
    ["Test case and result management", "Microsoft Excel (Report5 workbooks)", "Microsoft", "Microsoft 365"],
    ["Continuous integration", "GitHub Actions (validate job)", "GitHub", "Hosted runner"],
], widths=[5.2, 4.4, 3.4, 3.0], size=9, caption="Supporting tools")

# ------------------------------------------------------------------ 3
r.h2("3. Test Plan")
r.h3("3.1 Human Resources")
r.table(["Worker/Doer", "Role", "Specific Responsibilities/Comments"], [
    ["Trần Nguyễn Hiếu Thuận", "Test lead", "Owns the test plan and workbooks; runs system rounds; tracks defects; tests FE-01 and FE-06 screens; workflow W02"],
    ["Nguyễn Gia Huy", "Tester, gameplay owner", "Unit tests of rules, physics, training and bot; playtests; runs the Unity EditMode suite; workflow W01"],
    ["Nguyễn Văn Thành Đạt", "Tester, backend and network owner", "IntegrationChecks, API security and load tests, online and dedicated cases; workflow W03"],
    ["Huỳnh Trung Kiên", "Tester, leader", "Admin web cases, acceptance session with the supervisor, release decision; workflow W04"],
    ["Mr. Lại Đức Hùng", "Supervisor", "Witnesses the acceptance scenarios and approves the release criteria"],
], widths=[4.0, 3.6, 8.4], size=9, caption="Human resources")
r.h3("3.2 Test Environment")
r.table(["Purpose", "Tool", "Provider", "Version"], [
    ["Staging server", "Ubuntu with Docker Engine and Docker Compose (API, AI service, dedicated server, PostgreSQL, Redis, S3 storage, web)", "Team VPS tad-min.io.vn", "Ubuntu 24.04; images of main"],
    ["Game client under test", "Unity Editor and Windows build", "Unity Technologies", "6000.6.2f1"],
    ["Test PC", "Windows PC with keyboard and mouse", "Team member", "Windows 11 Home"],
    ["Android device", "Android phone (ARM64) for NFR-03 and NFR-10", "Team member", "Android 8.0 or later (to be recorded in Round 2)"],
    ["Real-time service", "Photon Cloud", "Exit Games", "Fusion 2.1.3"],
    ["Database and cache", "PostgreSQL, Redis", "Docker images (pinned digests)", "16 / 7.4"],
    ["Admin web browser", "Google Chrome, Microsoft Edge", "Google, Microsoft", "Latest stable"],
], widths=[3.4, 6.2, 3.4, 3.0], size=8.5, caption="Test environment")
r.h3("3.3 Test Milestones")
r.table(["Milestone Task", "Start Date", "End Date"], [
    ["Round 1 – automated suites and first system pass on staging", "09/11/2026", "13/11/2026"],
    ["Prepare seed accounts, test mailbox and analytics seed", "16/11/2026", "18/11/2026"],
    ["Round 2 – all system cases; fix verification of D-01 to D-18", "16/11/2026", "27/11/2026"],
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
r.p("The number of cases follows the Review 2 formula: 18 committed features × 3 cases (54) + 4 workflows + 42 state transitions (three per state machine SM-01 to SM-14) + 12 NFRs = 112 cases. Five cases are added: two for the expert review tool (FE-10), which is not in the Review 2 feature list, and three for FR-03 to FR-05, which the Review 2 feature F01 did not detail, giving **%d cases**." % len(R.CASES))
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
r.p("%d of the 33 functional requirements are covered by at least one passed case after Round 1. NFR-01 to NFR-12 are traced one-to-one to TC-N01 to TC-N12." % covered)

# ------------------------------------------------------------------ 5
r.h2("5. Test Reports")
r.h3("5.1 Evidence")
r.table(["ID", "Run", "Date", "Build / commit", "Result", "Location"], [
    ["EV-01", "Unity EditMode suite through the Unity CLI (Pipeline test run)", "11/11/2026", "main @ 7f8ee32", "515 tests, 515 passed, 0 failed, 6.19 s", "Doc/Project_report/Report/evidence/editmode_results_20261111.json"],
    ["EV-02", "Playtest on the staging server (offline training, signed-in menu, bot match, AI coaching, ranked 1v1 with bot fallback)", "10/11/2026", "main @ 82f6a7a client; staging images of 06/11/2026", "See Report5_Test Report.xlsx", "screenshots/01–28, evidence/playtest_log_20261110.md"],
    ["EV-03", "UI check of the register, verification, password-reset screens and the tutorial", "11/11/2026", "main @ 7f8ee32", "See TC-F01-03, TC-F02-01", "screenshots/29–34"],
    ["EV-04", "Match history of the team test accounts (two ranked 1v1 attempts)", "09/11/2026", "staging", "Both interrupted", "screenshots/12_history.png"],
    ["EV-05", "Team-recorded IntegrationChecks on disposable PostgreSQL databases (12 areas)", "06/11/2026", "df5f671", "Reported as passed; logs not in the repository, so not counted", "Review3_Evidence.md (file names and SHA-256)"],
], widths=[1.2, 5.0, 1.8, 2.6, 2.6, 2.8], size=8, caption="Test evidence")
r.h3("5.2 Unit Test Results")
asm = OrderedDict()
if EV_FILE and os.path.exists(EV_FILE):
    res = json.load(open(EV_FILE, encoding="utf-8"))
    lst = json.load(open(EV_FILE.replace("editmode_results_20261111.json", "editmode_list.json"), encoding="utf-8"))["data"]["result"]["Tests"]
    amap = {t["FullName"]: t["Assembly"] for t in lst}
    for x in res["results"]:
        a = amap.get(x["FullName"], "?").replace("PickleVerse.", "")
        asm.setdefault(a, Counter())[x["Status"]] += 1
rows = [[a, str(sum(v.values())), str(v.get("Passed", 0)), str(v.get("Failed", 0))] for a, v in asm.items()]
rows.append(["**Total**", "**%d**" % sum(sum(v.values()) for v in asm.values()), "**%d**" % sum(v.get("Passed", 0) for v in asm.values()), "**%d**" % sum(v.get("Failed", 0) for v in asm.values())])
r.table(["Test assembly", "Tests", "Passed", "Failed"], rows, widths=[6.0, 2.5, 2.5, 2.5], size=9, subheader_rows=(len(rows) - 1,), caption="Unity EditMode results by assembly (EV-01)")
r.p("The ten decision tables of Report5_Unit Test.xlsx contain %d unit test cases (normal, abnormal and boundary), all passed in EV-01. The other automated suites were not executed for this report: the 22 pytest tests of the AI service need FastAPI and scikit-learn, which are not installed on the test PC, and the IntegrationChecks need restored NuGet packages and a PostgreSQL/Redis instance. The team recorded passing IntegrationChecks runs on 06/11/2026 (EV-05), but their logs are not in the repository, so they are not counted here. Both suites run in the GitHub Actions validate job, whose latest result must be attached in Round 2. Code coverage has not been measured yet; the Unity Code Coverage package will be enabled in Round 2." % sum(len(f["cases"]) for f in R.UNIT))
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
r.table(["Module", "Cases", "Passed", "Failed", "Pending"], rows, widths=[8.0, 2.0, 2.0, 2.0, 2.0], size=9, subheader_rows=(len(rows) - 1,), caption="Round 1 results by module")
executed = st["Passed"] + st["Failed"]
r.table(["Metric", "Value", "Target", "Status"], [
    ["Test coverage (executed / total)", "%.1f%% (%d / %d)" % (executed * 100.0 / len(R.CASES), executed, len(R.CASES)), "100% before release", "Not met"],
    ["Pass rate of executed cases", "%.1f%% (%d / %d)" % (st["Passed"] * 100.0 / executed, st["Passed"], executed), "≥ 90%", "Met for executed cases"],
    ["Test successful coverage (passed / total)", "%.1f%%" % (st["Passed"] * 100.0 / len(R.CASES)), "≥ 90%", "Not met"],
    ["Automated cases", "%.1f%% (%d / %d)" % (auto * 100.0 / len(R.CASES), auto, len(R.CASES)), "≥ 50%, run in CI", "Not met"],
    ["Functional requirements covered by a passed case", "%d / 33" % covered, "33 / 33", "Not met"],
    ["Open Critical / High defects", "0 / 3 (D-09, D-17, D-18)", "0 / 0", "Not met"],
], widths=[6.0, 3.6, 3.2, 3.2], size=9, caption="Release criteria after Round 1")
r.h3("5.4 Defects")
r.table(["ID", "Summary", "Type", "Severity", "Found by", "Owner"], [[a, b, c, d_, e, g] for a, b, c, d_, e, f, g in R.DEFECTS],
        widths=[1.2, 8.0, 2.0, 1.6, 2.0, 1.2], size=8, caption="Defects found in Round 1 (all Open)")
sev = Counter(dft[3] for dft in R.DEFECTS)
r.p("Round 1 found %d defects: %d High, %d Medium and %d Low. Severity follows Report 3 NFR-08: Critical means loss of player data or inability to sign in or play; High means a main workflow or a security requirement fails; Medium means a requirement is partly wrong or a rule is inconsistent; Low means a cosmetic or wording problem. The defect life cycle (Open → Fixed → Verified → Closed) is tracked in PickleVerse_Project_Tracking.xlsx." % (len(R.DEFECTS), sev["High"], sev["Medium"], sev["Low"]))
r.h3("5.5 Analysis and Next Actions")
r.bullets([
    "**Engine-level quality is good.** All 515 EditMode tests pass, and every system case that could be executed for the single-player features (training, bot match, coaching, progress screens) passed, apart from the offline analysis status (D-02).",
    "**Online play between humans is the main risk.** The only human-versus-human ranked attempts (EV-04) were interrupted (D-09), which also blocks the reconnection, report and appeal cases. Reproducing D-09 with server logs is the first task of Round 2 (owner: Đạt, by 18/11/2026).",
    "**Security requirements are not yet met** because of committed credentials and HTTP on staging (D-17, D-18). Both must be fixed before the acceptance test.",
    "**Coverage is limited by test data, not by tooling.** 62 cases are pending mainly because seed accounts, a test mailbox and a second player were not available. These will be added to Database.sql before Round 2 (owner: Thuận, by 18/11/2026).",
    "**Automation is below the 50% target and not in CI.** 33 of 114 cases are automated. The EditMode suite will be run in CI with a GameCI runner or, if no licence is available, by a scheduled Unity CLI run whose result file is committed; the pytest and IntegrationChecks results of the validate job will be linked to their test case IDs.",
])

out = r.save("Report5_Test Documentation.docx", "Report 5 – Software Test Documentation")
print(out, "tables", r.tbl_no, "figures", r.fig_no, "covered", covered)
