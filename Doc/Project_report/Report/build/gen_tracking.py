"""Builds PickleVerse_Project_Tracking.xlsx (on the course template) and PickleVerse_Project_Schedule.xml (MS Project XML)
from the same WBS as Report 2, so effort, owners, iterations and dates stay consistent."""
import datetime as dt
import os
import re
import sys
from xml.sax.saxutils import escape

import openpyxl
from openpyxl.styles import Alignment, Font, PatternFill
from openpyxl.worksheet.datavalidation import DataValidation

HERE = os.path.dirname(os.path.abspath(__file__))
REPORT_DIR = os.path.abspath(os.path.join(HERE, ".."))
TPL = os.path.abspath(os.path.join(REPORT_DIR, "..", "ReportTemplete", "Report3_Project Tracking.xlsx"))

src = open(os.path.join(HERE, "gen_r2.py"), encoding="utf-8").read()
WBS = eval(re.search(r"^WBS = (\[.*?^\])", src, re.S | re.M).group(1))  # same data as Report 2
import r5_cases as R  # noqa: E402

ITER = {0: (dt.date(2026, 9, 7), dt.date(2026, 9, 18)), 1: (dt.date(2026, 9, 21), dt.date(2026, 10, 2)), 2: (dt.date(2026, 10, 5), dt.date(2026, 10, 16)),
        3: (dt.date(2026, 10, 19), dt.date(2026, 10, 30)), 4: (dt.date(2026, 11, 2), dt.date(2026, 11, 13)), 5: (dt.date(2026, 11, 16), dt.date(2026, 11, 27)),
        6: (dt.date(2026, 11, 30), dt.date(2026, 12, 11))}
# id -> (first iteration, last iteration, status, % complete, note)
PLAN = {
    "1.1": (0, 6, "Doing", 60, "Weekly tracking continues to the defense"),
    "1.2": (4, 4, "Done", 100, "v1.0 on 11/11/2026; first versions late (06/11)"),
    "1.3": (4, 4, "Done", 100, "v1.0 on 11/11/2026"),
    "1.4": (4, 4, "Done", 100, "v1.0 on 11/11/2026"),
    "1.5": (4, 6, "Doing", 80, "Reports 5–7 v1.0 done on 11/11/2026; defense slides pending"),
    "2.1": (0, 1, "Done", 100, "Unity project initialised 17/09/2026"),
    "2.2": (3, 3, "Done", 100, "Normalised schema 31/10/2026 (51 tables); 59 tables since the social feature of 15/11/2026, 60 since the membership of 22/11/2026"),
    "2.3": (4, 4, "Done", 100, "Docker deployment 04/11/2026"),
    "2.4": (4, 5, "Doing", 90, "33 of the 51 runs since 18/11/2026 passed (run 103 on adcb4e2 with 16 check groups, EV-19); D-23 closed (291542a, PB-24); open: Unity tests in CI (PB-09), membership and replay-launch groups (PB-27), schema check in the deployment (PB-28)"),
    "3.1": (2, 2, "Done", 100, "Covered by EditMode tests"),
    "3.2": (2, 2, "Done", 100, "PaddleContactTests, SinglesMatchTests"),
    "3.3": (2, 2, "Done", 100, "SinglesMatchTests, MultiplayerRulesTests"),
    "3.4": (2, 2, "Done", 100, "D-07 fixed 11/11/2026 (6d1ed37); retest in Round 2"),
    "3.5": (2, 3, "Done", 100, "D-10 fixed (9557581, 329810f) and closed 18/11/2026 (EV-17)"),
    "4.1": (3, 3, "Done", 100, "TC-F01-01 pending (test mailbox)"),
    "4.2": (3, 4, "Done", 100, "TC-F01-02 passed"),
    "4.3": (4, 4, "Done", 100, "Not yet system-tested"),
    "4.4": (3, 4, "Done", 100, "D-07 fixed 11/11/2026 (6d1ed37); retest in Round 2"),
    "4.5": (3, 3, "Done", 100, ""),
    "4.6": (5, 5, "Done", 100, "16/11/2026 (bc53722, 9dec659, 1b64424); EV-12, EV-13"),
    "4.7": (5, 5, "Done", 100, "21/11/2026 (2398d98, 6e25c2c); CI check single-session passes (EV-19); two-device test TC-F01-10 pending"),
    "5.1": (2, 2, "Done", 100, "TC-F02-01 passed"),
    "5.2": (2, 4, "Done", 100, "TC-F03-01..03 passed"),
    "5.3": (4, 4, "Done", 100, "15 lessons seeded"),
    "5.4": (4, 5, "Doing", 80, "TC-F14-02 pending; D-02 fixed and closed 18/11/2026 (EV-17)"),
    "6.1": (2, 2, "Done", 100, "D-13 balance session pending"),
    "6.2": (3, 3, "Done", 100, "BhTreeTests passed"),
    "6.3": (2, 2, "Done", 100, "D-05 fixed (d2fc5be)"),
    "7.1": (3, 3, "Done", 100, "Moved to Photon Fusion 20/10/2026"),
    "7.2": (4, 5, "Done", 100, "D-15 closed after retest on 18/11/2026 (EV-15): bot offered after 3 min 01 s"),
    "7.3": (4, 5, "Done", 100, "D-09 closed after retest on 17/11/2026 (EV-15): human ranked 1v1 played to the end"),
    "7.4": (4, 5, "Done", 100, "Reconnection within 20 s works (EV-15); the forfeit after 65 s matches BR-28 v1.5 (TC-F09-03 passed in EV-17; D-28 resolved by requirement change)"),
    "7.5": (4, 4, "Done", 100, "Start Elo 100; the ranking reset uses the same value since 0cee928 (D-08 closed)"),
    "7.6": (4, 5, "Done", 100, "Server pool 15/11/2026 (e160b4e); ranked doubles with bot fill 17/11/2026 (b08f5c7); CI check ranked-doubles passes (EV-11); played in the client with bot fill on 18/11/2026 (EV-16); four-human test pending; D-29 fixed and closed (EV-17)"),
    "7.7": (5, 5, "Done", 100, "19–25/11/2026 (2a2c37c, 610633a, 824a196); TC-F07-05, TC-F07-06 passed (EV-18, EV-20)"),
    "7.8": (5, 5, "Doing", 90, "22–24/11/2026 (08e2cb4, 308f5fa, 40685b4, 5424415); TC-F08-05, TC-F08-06, TC-F09-07, TC-F11-06 passed; surrender with confirmation instead of Rời trận (D-34) fixed on 25/11, to be released; replay court D-35, AFK match D-47 open"),
    "8.1": (4, 4, "Done", 100, ""),
    "8.2": (3, 4, "Done", 100, "Tier model gated (NFR-06)"),
    "8.3": (4, 4, "Done", 100, "D-02 and D-11 fixed and closed 18/11/2026 (EV-17)"),
    "8.4": (5, 5, "Done", 100, "25/11/2026 (b1013f8, c616ec5); TC-F06-04, TC-F09-04, TC-F09-05 passed (EV-20); D-37 open"),
    "9.1": (2, 2, "Done", 100, ""),
    "9.2": (2, 2, "Done", 100, ""),
    "9.3": (2, 2, "Done", 100, "D-12 Elite Token source"),
    "10.1": (2, 3, "Done", 100, "TC-F12-01 passed (EV-15)"),
    "10.2": (2, 3, "Done", 100, ""),
    "10.3": (2, 2, "Done", 100, ""),
    "10.4": (4, 5, "Doing", 80, "Implemented 15/11/2026 (e160b4e); CI check social-content passes (EV-11); video upload from the Windows build since 9557581 (EV-17), not yet on Android (D-22); no moderation of posts and messages (PB-25)"),
    "10.5": (4, 4, "Done", 100, "15/11/2026 (e160b4e); TC-F11-04 not yet run"),
    "11.1": (4, 4, "Done", 100, "TC-F17-01 and W04 passed on 18/11/2026 after the fix of D-27 (EV-17)"),
    "11.2": (4, 4, "Done", 100, "TC-F17-03 passed (EV-15)"),
    "12.1": (3, 3, "Done", 100, ""),
    "12.2": (4, 4, "Done", 100, "Admin web cases run on 17–18/11/2026 (EV-15); D-25 and D-26 fixed and closed (EV-17)"),
    "12.3": (4, 4, "Done", 100, ""),
    "12.4": (4, 4, "Done", 100, ""),
    "12.5": (4, 4, "Done", 100, ""),
    "12.6": (4, 4, "Done", 100, ""),
    "12.7": (5, 5, "Doing", 90, "20–22/11/2026 (41ab7d1, 171368e, 1e80a97, 0e56440, d196829); TC-F16-04 passed; D-36, D-45 and TC-F17-05 open"),
    "13.1": (4, 4, "Done", 100, "No labelled real data yet"),
    "14.1": (1, 2, "Done", 100, ""),
    "14.2": (2, 2, "Done", 100, ""),
    "14.3": (3, 4, "Done", 100, "D-01 open; D-04 and D-19 closed after retest (EV-15)"),
    "14.4": (4, 4, "Done", 100, "15/11/2026; frame rate measured only in the Editor (Doc/Sunset_District_20261115.md); no phone measurement"),
    "14.5": (5, 5, "Done", 100, "24–25/11/2026 (fc14a22 to 50aa330, b845c65); Doc/ArtBible; no measurement on the reference devices"),
    "14.6": (5, 5, "Done", 100, "24–25/11/2026 (b27c1ba, 8eb4fc1, 3c55c5e, 9046ab2, f1b4863, adcb4e2); Dragon Ruby not on sale on staging (D-48)"),
    "15.1": (4, 5, "Doing", 70, "730 EditMode tests passed on 25/11/2026 (EV-18); IntegrationChecks (16 groups) pass in CI on main @ adcb4e2 (EV-19); coverage not measured"),
    "15.2": (5, 6, "Doing", 50, "Round 1 done; %d of %d cases passed after the checks of 13–17/11/2026 the end-to-end runs of 17–18/11/2026 (EV-15, EV-16), the defect retest EV-17 and the runs of 25/11/2026 (EV-18 to EV-20); rest of Round 2 and Round 3 planned" % (R.stats()[0]["Passed"], len(R.CASES))),
    "15.3": (5, 6, "Pending", 0, "Acceptance 07–11/12/2026"),
    "15.4": (4, 6, "Doing", 60, "Staging deployed by the pipeline (run 103 deployed adcb4e2 on 25/11/2026); Windows build of adcb4e2 made on 25/11/2026 (EV-20); Windows build of main @ 96ff8e9 made on 17/11/2026 (EV-15) and Windows builds with the fixes of 18/11/2026 (EV-17); APKs of 12/11 and 14/11/2026 use protocol 18 and must be rebuilt (protocol 20); release package pending"),
    "16.1": (5, 5, "Doing", 85, "22/11/2026 (d196829); TC-F20-01, TC-F20-04 passed (EV-20); real payment, non-member locks and CI group open (TC-F20-02, TC-F20-03, TC-F20-05)"),
}
MEMBERS = [("Huỳnh Trung Kiên", "Kiên", "KienHT"), ("Nguyễn Văn Thành Đạt", "Đạt", "DatNVT"), ("Nguyễn Gia Huy", "Huy", "HuyNG"), ("Trần Nguyễn Hiếu Thuận", "Thuận", "ThuanTNH")]
SHORT = {m[1]: m[0] for m in MEMBERS}
WRAP = Alignment(wrap_text=True, vertical="top")
HEAD = PatternFill("solid", fgColor="D9E2F3")


def owners(text):
    if text == "All":
        return [m[0] for m in MEMBERS]
    return [SHORT[x.strip()] for x in text.split(",")]


def write_rows(ws, start, rows, wrap_cols=None):
    for i, row in enumerate(rows):
        for j, v in enumerate(row):
            c = ws.cell(row=start + i, column=1 + j, value=v)
            c.alignment = WRAP
            if isinstance(v, (dt.date, dt.datetime)):
                c.number_format = "dd/mm/yyyy"


def clear(ws, first_row, cols):
    for r in range(first_row, ws.max_row + 1):
        for c in range(1, cols + 1):
            ws.cell(row=r, column=c).value = None


def tracking():
    wb = openpyxl.load_workbook(TPL)
    # ---------------- WBS
    ws = wb["WBS"]
    ws["D5"] = "PROJECT WORK BREAKDOWN STRUCTURE – PickleVerse (PKV), updated 25/11/2026"
    clear(ws, 8, 14)
    extra = ["WBS ID", "Owner", "Est. effort (man-days)", "Start", "End", "% complete", "Notes"]
    for j, h in enumerate(extra):
        c = ws.cell(row=7, column=8 + j, value=h)
        c.font = Font(bold=True)
        c.fill = HEAD
        c.alignment = WRAP
    rows = []
    for wid, name, items in WBS:
        for iid, iname, cx, md, own in items:
            a, b, status, pct, note = PLAN[iid]
            it = "Iteration %d" % a if a == b else "Iterations %d–%d" % (a, b)
            rows.append([len(rows) + 1, iname, name, cx, iname, it, status, iid, own, md, ITER[a][0], ITER[b][1], pct / 100.0, note])
    write_rows(ws, 8, rows)
    for i in range(len(rows)):
        ws.cell(row=8 + i, column=13).number_format = "0%"
    end = 8 + len(rows) - 1
    ws.cell(row=end + 2, column=9, value="Total").font = Font(bold=True)
    ws.cell(row=end + 2, column=10, value="=SUM(J8:J%d)" % end).font = Font(bold=True)
    ws.cell(row=end + 3, column=9, value="Weighted % complete")
    ws.cell(row=end + 3, column=10, value="=SUMPRODUCT(J8:J%d,M8:M%d)/SUM(J8:J%d)" % (end, end, end)).number_format = "0.0%"
    ws.data_validations.dataValidation = []
    dv1 = DataValidation(type="list", formula1='"Pending, Doing, Deferred, Cancelled, Done"', allow_blank=True)
    dv1.add("G8:G%d" % end)
    dv2 = DataValidation(type="list", formula1='"Simple,Medium,Complex"', allow_blank=True)
    dv2.add("D8:D%d" % end)
    ws.add_data_validation(dv1)
    ws.add_data_validation(dv2)
    for col, w in zip("ABCDEFGHIJKLMN", [5, 34, 26, 10, 34, 14, 10, 8, 12, 10, 11, 11, 10, 38]):
        ws.column_dimensions[col].width = w
    # ---------------- Backlog (product and iteration backlog for the remaining iterations)
    bl = wb.create_sheet("Backlog", 1)
    hdr = ["ID", "Backlog item (user story or task)", "Type", "Priority", "Iteration", "Owner", "Estimate (man-days)", "Status", "Acceptance / link"]
    for j, h in enumerate(hdr):
        c = bl.cell(row=1, column=1 + j, value=h)
        c.font = Font(bold=True)
        c.fill = HEAD
    items = [
        ("PB-01", "As a player I want ranked matches between two humans to finish normally so that my Elo changes", "Defect fix", "High", "Iteration 5", "Đạt", 3, "Done", "D-09; TC-F09-01, TC-F09-03"),
        ("PB-02", "Remove committed credentials, rotate them and load secrets from the environment", "Security", "High", "Iteration 5", "Đạt", 1, "Doing", "D-17 partly fixed 13/11/2026 (be556a1): values removed from appsettings.json and Program.cs; left: rotate the exposed values on staging and record it; TC-N09"),
        ("PB-03", "Serve the API and admin web over HTTPS on staging", "Security", "High", "Iteration 4", "Đạt", 1, "Done", "D-18 fixed 13/11/2026 (be556a1) and verified by EV-09: API and admin web on https://tad-min.io.vn, port 8080 closed; retest in Round 2"),
        ("PB-04", "Add seed accounts, a test mailbox and an analytics seed to Database.sql", "Test data", "High", "Iteration 5", "Thuận", 2, "Pending", "Pending cases of Report 5"),
        ("PB-05", "As an offline player I want a clear analysis status so that I know when tips will arrive", "Defect fix", "Medium", "Iteration 5", "Đạt", 0.5, "Done", "D-02 fixed in 9557581; TC-F06-03 passed (EV-17)"),
        ("PB-06", "Apply the selected character in bot match, training and tutorial", "Defect fix", "Medium", "Iteration 4", "Huy", 1, "Done", "D-07 fixed 11/11/2026 (6d1ed37); retest in Round 2"),
        ("PB-07", "Fix overlapping UI elements (offline banner, appeals button, result button, key hints)", "Defect fix", "Medium", "Iteration 5", "Thuận", 1.5, "Done", "D-03, D-04, D-19 fixed 11/11/2026; D-01 and D-10 closed 18/11/2026 (EV-17)"),
        ("PB-08", "Decide the starting Elo and the Elite Token source with the supervisor and implement the decision", "Decision", "Medium", "Iteration 5", "Kiên", 1, "Doing", "D-08: reset uses the start value 100 (0cee928); D-12: release 1.0 scope of BR-36, to be confirmed by the supervisor (Q&A 3–4)"),
        ("PB-09", "Run the EditMode suite automatically and link results to test case IDs", "Test automation", "Medium", "Iteration 5", "Thuận", 2, "Doing", "Issue I-07; IntegrationChecks of the CI job linked to 14 cases on 13/11/2026; EditMode step still missing"),
        ("PB-10", "Measure NFR-02 to NFR-05 and run the usability test (NFR-01)", "Test", "Medium", "Iteration 5", "Thuận", 3, "Pending", "TC-N01–TC-N05"),
        ("PB-11", "Bot balance session with players of known level", "Balance", "Medium", "Iteration 5", "Huy", 1, "Pending", "D-13"),
        ("PB-12", "Write Report 6 – user guides (installation, player, administrator)", "Document", "High", "Iteration 4", "Huy", 3, "Done", "M09; v1.0 on 11/11/2026"),
        ("PB-13", "Write Report 7 – final report consolidating Reports 1–6", "Document", "High", "Iteration 4", "Huy", 3, "Done", "M10; v1.0 on 11/11/2026"),
        ("PB-15", "Prepare the defense slides and rehearse the demo of W01–W04", "Document", "High", "Iteration 6", "Kiên", 2, "Pending", "M10"),
        ("PB-14", "Acceptance test W01–W04 with the supervisor", "Test", "High", "Iteration 6", "Kiên", 1, "Pending", "TC-W01–TC-W04"),
        ("PB-16", "Restore the CI/CD pipeline: resolve the GitHub billing lock, then fix the build and deployment failures", "DevOps", "High", "Iteration 4", "Kiên", 1.5, "Done", "Lock lifted 11/11/2026; dedicated build and deployment fixed by Đạt 12–13/11/2026; runs 27, 28, 30, 33 and 34 pass (EV-08); issue I-11 closed"),
        ("PB-17", "Install the Android build on a reference phone; smoke test and frame-rate capture", "Test", "High", "Iteration 5", "Thuận", 1.5, "Pending", "TC-N03, TC-N10; Report 1 LI-11; the APK of 12/11/2026 (EV-10) predates the HTTPS switch – rebuild first (PB-20)"),
        ("PB-18", "Decide the XP curve or the GEMSTONE level and record the decision", "Decision", "Medium", "Iteration 4", "Kiên", 1, "Done", "D-20 first accepted as designed (13/11/2026), then fixed by the XP curve level × 10 (b08f5c7, 17/11/2026); D-12 stays in PB-08"),
        ("PB-19", "Remove the 24 endpoints that no client calls, or document them as public API", "Maintenance", "Low", "Iteration 5", "Kiên", 1, "Pending", "Issue I-12; Report 4 §3.9"),
        ("PB-20", "Set the Android package name, label and version, then rebuild the APK and the Windows build from the current main (HTTPS address, protocol 22)", "Defect fix", "High", "Iteration 5", "Huy", 0.5, "Doing", "Windows build of adcb4e2 made on 25/11/2026 (EV-20); APK still pending; D-21 fixed in be556a1; TC-N10"),
        ("PB-21", "Add a daily database dump with an off-host copy (since be556a1 the pipeline takes a verified dump before each deployment, kept on the same host)", "DevOps", "Medium", "Iteration 5", "Đạt", 1, "Pending", "NFR-08; Report 2 risk 11"),
        ("PB-22", "CR-01 (Review 1, finance): membership – built on 22/11/2026 (49,000 VND once, PayOS, 3,000 Elite Token); left: confirm price and benefits with the supervisor, refund procedure, terms of sale, order list for administrators, one real payment by an account owner", "Change request", "High", "Iteration 6", "Kiên", 2, "Doing", "Report 1 FE-11, LI-14; FR-36; TC-F20-01 to TC-F20-05"),
        ("PB-23", "Let the Windows and Android builds pick and upload an MP4 video for a post", "Defect fix", "High", "Iteration 5", "Đạt", 1, "Doing", "Windows done in 9557581 (TC-F19-05 passed, EV-17); Android picker pending (D-22 partly fixed); Report 1 LI-12"),
        ("PB-24", "Run the deploy job after every passing pipeline run, also when the FastAPI and admin web checks are skipped", "DevOps", "Medium", "Iteration 5", "Đạt", 0.5, "Done", "D-23 closed 25/11/2026 (291542a, EV-19)"),
        ("PB-25", "Let players report posts, comments and messages and let administrators remove them", "Moderation", "Medium", "Iteration 6", "Kiên", 2, "Pending", "Report 2 risk 15; Review 1 comment on admin complaints"),
        ("PB-26", "Test the social feed, messages, whispers and ranked doubles with two to four accounts", "Test", "High", "Iteration 5", "Thuận", 1.5, "Doing", "TC-F19-05, TC-F19-06; four-client part of TC-F07-04"),
        ("PB-27", "Add the IntegrationChecks groups membership and replay-launch to the validate job", "DevOps", "High", "Iteration 5", "Đạt", 0.5, "Pending", "D-49; TC-F20-05, TC-F17-05; Report 2 risk 19"),
        ("PB-28", "Stop a deployment whose schema is not on the host (extend the schema check of ssh-deploy.sh) and record the manual schema step", "DevOps", "High", "Iteration 5", "Đạt", 0.5, "Pending", "D-48; Report 2 risk 17"),
        ("PB-29", "Đầu hàng with a confirmation box instead of Rời trận; in doubles both partners confirm", "Defect fix", "High", "Iteration 5", "Huy", 1, "Doing", "D-34 fixed in the working tree of 25/11/2026 (EV-18, EV-21); left: release, TC-F09-06 and TC-F09-09 on staging, TC-F09-10 in CI"),
        ("PB-30", "Release the fix of D-33, distribute the new build, then add the Dragon Ruby rows to the staging database", "Defect fix", "High", "Iteration 5", "Huy", 0.5, "Doing", "D-33 fixed in the working tree of 25/11/2026; D-48"),
    ]
    write_rows(bl, 2, [list(x) for x in items])
    for col, w in zip("ABCDEFGHI", [8, 70, 14, 10, 12, 10, 12, 10, 28]):
        bl.column_dimensions[col].width = w
    # ---------------- Issues
    ws = wb["Issues"]
    clear(ws, 4, 9)
    issues = [
        ("Reports 1–4 were first submitted on 06/11/2026, 4–8 weeks after their due dates", "Lower process marks; review comments arrive late", "High", "KienHT", dt.date(2026, 11, 6), dt.date(2026, 11, 11), "Closed", "Reports revised to v1.0; deadlines now tracked weekly (Report 2 §1.2)"),
        ("Two ranked 1v1 matches between humans ended interrupted on 09/11/2026", "Main online workflow W03 cannot be accepted", "High", "DatNVT", dt.date(2026, 11, 9), dt.date(2026, 11, 17), "Closed", "D-09 closed: a human ranked 1v1 was played to the end on 17/11/2026 (EV-15)"),
        ("Development credentials and a fallback JWT key are committed to the repository", "Account takeover or e-mail abuse if the repository leaks", "High", "DatNVT", dt.date(2026, 11, 10), None, "Open", "D-17 partly fixed 13/11/2026 (be556a1): removed from the settings file and the code; the values are still in the Git history, so rotate them on staging and record the rotation (PB-02)"),
        ("Staging API and admin web use HTTP", "Credentials and tokens travel unencrypted", "High", "DatNVT", dt.date(2026, 11, 10), dt.date(2026, 11, 13), "Closed", "D-18 fixed by be556a1 and verified on 13/11/2026 16:21 (EV-09): HTTPS only, port 80 redirects, port 8080 closed"),
        ("Uneven contribution: two members hold most of the commits", "Key-person risk; members cannot explain modules they did not build", "Medium", "KienHT", dt.date(2026, 11, 10), None, "Open", "Pairing sessions and workflow ownership (Report 2 risk 1)"),
        ("No pull-request review: pull requests were used on 21/11/2026 only (#2–#6, merged by their author)", "Defects and secrets reach main without review", "Medium", "KienHT", dt.date(2026, 11, 10), None, "Open", "Pull requests with one reviewer were planned from Iteration 5; on 25/11/2026 commits are pushed directly to main again"),
        ("Unity tests are not run in CI (the pipeline builds the server with a Unity licence but has no test step)", "Automation target of Review 2 not demonstrated", "Medium", "ThuanTNH", dt.date(2026, 11, 11), None, "Open", "PB-09"),
        ("No test mailbox, seed accounts or analytics seed", "%d test cases pending" % R.stats()[0]["Pending"], "Medium", "ThuanTNH", dt.date(2026, 11, 11), None, "Open", "PB-04"),
        ("Elite Token has no earning source", "GEMSTONE can only be granted at level 25", "Medium", "KienHT", dt.date(2026, 11, 10), dt.date(2026, 11, 22), "Closed", "Since 22/11/2026 the membership credits 3,000 Elite Token (BR-58); D-12; Q&A 3"),
        ("Capacity of online matches is limited by the game-server containers started on staging (one by default)", "One online match at a time unless PKV_GAME_REPLICAS is raised; capacity target not load-tested", "Medium", "DatNVT", dt.date(2026, 11, 3), None, "Open", "Server pool since 15/11/2026 (e160b4e); Report 1 LI-07; Q&A 2"),
        ("GitHub Actions did not pass before 13/11/2026: configuration failures on 05/11, account locked for a billing issue from 06/11 to 11/11, dedicated-server build failures on 11–12/11", "No automated validation or deployment; staging was released by hand on 08–10/11/2026", "High", "KienHT", dt.date(2026, 11, 6), dt.date(2026, 11, 13), "Closed", "Runs 27, 28, 30, 33 and 34 passed every job on 13/11/2026 (EV-08); PB-16 done"),
        ("24 API endpoints are not called by the game, the server or the admin web", "Untested surface that still accepts requests", "Low", "DatNVT", dt.date(2026, 11, 12), None, "Open", "PB-19; Report 4 §3.9"),
        ("Stored data is not encrypted at rest (proposal: AES-256)", "Deviation from the proposal; database and backups readable on the host", "Medium", "DatNVT", dt.date(2026, 11, 12), None, "Open", "Report 1 LI-10; disk encryption before release"),
        ("The Android build has not been tested on a device", "NFR-03 and NFR-10 unverified; touch controls only checked in the Editor", "Medium", "ThuanTNH", dt.date(2026, 11, 12), None, "Open", "PB-17, PB-20; Report 1 LI-11; the APK of 12/11/2026 (EV-10) predates the HTTPS switch"),
        ("The deploy job is skipped when the FastAPI and admin web checks are skipped (nine passing runs of 13–16/11/2026 did not deploy)", "Staging can run an older release than main without anyone noticing", "Medium", "DatNVT", dt.date(2026, 11, 17), dt.date(2026, 11, 25), "Closed", "D-23 closed (291542a, EV-19), PB-24; run 52 deployed 96ff8e9 on 17/11/2026"),
        ("The repository is public and the old credentials are still in its Git history", "Anyone can read the removed SMTP password and JWT key until they are rotated", "High", "DatNVT", dt.date(2026, 11, 17), None, "Open", "D-17, PB-02; Report 2 risk 2"),
        ("The admin web was not reachable at the staging address after the deployment of 13/11/2026 01:18", "The admin workflow W04 and the admin test cases could not be run on staging", "High", "DatNVT", dt.date(2026, 11, 13), dt.date(2026, 11, 13), "Closed", "Fixed by be556a1 (the Caddyfile routes every path except /api and /health/ready to the admin web); https://tad-min.io.vn opens the admin web since the deployment of 14:34 (EV-09)"),
        ("Schema changes are not part of the deployment: the new columns and tables of 22/11/2026 were added to staging by hand and the Dragon Ruby rows are missing", "A release can pass the pipeline and fail at run time", "High", "DatNVT", dt.date(2026, 11, 22), None, "Open", "Report 2 risk 17; PB-28; D-48"),
        ("The membership uses the production address of PayOS on staging, without refund procedure, terms of sale and order list", "Real money can be lost or disputed; a paywall stands before the demonstration of W03", "High", "KienHT", dt.date(2026, 11, 22), None, "Open", "Report 2 risks 14 and 18; Report 1 LI-14; PB-22; keep member accounts for the defense"),
        ("One item added in the CMS could stop the game for every player (D-33)", "All workflows of all players", "High", "HuyNG", dt.date(2026, 11, 25), None, "Open", "Fixed in the client and the API on 25/11/2026; to be released (PB-30)"),
    ]
    write_rows(ws, 4, [[i + 1] + list(x) for i, x in enumerate(issues)])
    ws.data_validations.dataValidation = []
    dv = DataValidation(type="list", formula1='"Low, Medium, High"', allow_blank=True)
    dv.add("D4:D%d" % (3 + len(issues)))
    dv2 = DataValidation(type="list", formula1='"Open, Closed, Cancelled"', allow_blank=True)
    dv2.add("H4:H%d" % (3 + len(issues)))
    ws.add_data_validation(dv)
    ws.add_data_validation(dv2)
    # ---------------- Defects
    ws = wb["Defects"]
    clear(ws, 4, 12)
    for j, h in enumerate(["Defect ID", "Severity", "Found on", "Test case"]):
        c = ws.cell(row=3, column=8 + j, value=h)
        c.font = Font(bold=True)
        c.fill = HEAD
    feat = {"TC-F14-01": "FE-02", "TC-F06-03": "FE-05", "TC-F03-01": "FE-02", "TC-F01-02": "FE-01", "TC-F05-01": "FE-03", "TC-F10-01": "FE-04",
            "TC-F09-01": "FE-04", "TC-F06-01": "FE-05", "TC-F04-01": "FE-06", "TC-F04-03": "FE-06", "TC-W03": "FE-04", "TC-F07-01": "FE-04", "Unit test run": "Testing", "TC-N09": "Security", "TC-N10": "Android client",
            "TC-F19-05": "FE-07", "CI history (EV-14)": "CI/CD", "Windows build (EV-15)": "Testing", "TC-F15-01": "FE-08/09", "TC-X01-02": "FE-10",
            "TC-F17-01": "FE-07", "TC-F09-03": "FE-04", "TC-F08-04": "FE-04",
            "TC-F04-04": "FE-06", "TC-F09-06": "FE-04", "TC-F11-06": "FE-07", "TC-F18-01": "FE-09", "TC-F18-03": "FE-09", "TC-F06-04": "FE-05", "TC-F11-02": "FE-07",
            "TC-F08-05": "FE-04", "TC-F08-01": "FE-04", "TC-F09-05": "FE-04", "TC-F07-03": "FE-04", "CI history (EV-19)": "CI/CD"}
    tester = {"TC-F09-01": "Nguyễn Gia Huy, Nguyễn Văn Thành Đạt"}
    rows = []
    for did, summary, typ, sev, tc, date, own in R.DEFECTS:
        c = next((x for x in R.CASES if x["id"] == tc), None)
        screen = c["desc"] if c else ("GitHub Actions workflow dockerhub.yml" if tc.startswith("CI") else ("EditMode test assembly definitions (Windows build)" if tc.startswith("Windows") else "Sync-UnityTests.ps1"))
        state = R.DEFECT_STATE.get(did)
        closed = R.DEFECT_CLOSED.get(did)
        suffix = (" [fixed in %s]" % R.DEFECT_FIX[did] if did in R.DEFECT_FIX else "") + (" [closed on %s: %s]" % closed if closed else "")
        if not suffix and state:
            suffix = " [%s on %s: %s]" % (state[0].lower(), state[1], state[2])
        life = "Closed" if closed else ("Fixed" if did in R.DEFECT_FIX else ("Closed" if state and state[0] == "Accepted" else "Assigned"))
        rows.append([feat.get(tc, ""), screen, tester.get(tc, "Nguyễn Gia Huy"), summary + suffix, SHORT[own],
                     life, typ, did, sev, dt.datetime.strptime(date, "%d/%m/%Y").date(), tc])
    write_rows(ws, 4, rows)
    ws.data_validations.dataValidation = []
    dv = DataValidation(type="list", formula1='"Pending, Assigned, Fixed, Closed, Cancelled"', allow_blank=True)
    dv.add("F4:F%d" % (3 + len(rows)))
    ws.add_data_validation(dv)
    ws.cell(row=2, column=1, value="Life cycle: Pending → Assigned → Fixed → Closed (after retest, or when the team accepts the behaviour as designed). Severity: Critical, High, Medium, Low (Report 5 §5.4).")
    # ---------------- Q&A
    ws = wb["Q&A"]
    clear(ws, 4, 6)
    qa = [
        "Do you approve the deviations from the proposal listed in Report 1 LI-03 to LI-08 (Elo only for ranked singles and ranked bot, ASP.NET Core, Photon Fusion, uGUI, Windows and Android only)?",
        "Is the capacity requirement accepted as 2,000 concurrent API users, with as many simultaneous online matches as game-server containers on staging (NFR-04, LI-07)?",
        "Should Elite Token receive an earning source in release 1.0, or should items priced only in Elite Token be removed (D-12)?",
        "New players start at 100 Elo (BR-23) and, since 18/11/2026, the admin ranking reset returns to the same value (D-08). Please confirm 100 as the start value.",
        "Is the expert review tool (FE-10) accepted as part of the committed scope or only as supporting tooling?",
        "What are the date and the required package of Review 3?",
        "Which Android device should be the reference device for NFR-03 and NFR-10?",
        "Please confirm the RACI matrix in Report 2 §4 for the documents and modules (workflow owners are fixed by BM04).",
        "Level 25 needs 60,000 XP with the current curve (n × 200 per level), so GEMSTONE is effectively unreachable (D-20). Should the curve be flattened, or should GEMSTONE be granted at a lower level?",
        "Review 1 (finance): the membership is built (CR-01: 49,000 VND once, PayOS, online play, private rooms and the adaptive bot). Do you accept the price, the benefits and the paywall before ranked play (F07–F10), and which refund procedure, terms of sale and handling of minors are required?",
        "Review 1 (club owner): the team does not plan club owner or club functions. Is it acceptable to record this as out of scope (Report 1 LI-02)?",
        "Review 1 (schedule management): is a match schedule required, or are the ranked queue, room codes and friend invitations enough (Report 1 LI-13)?",
        "Must administrators be able to remove posts, comments and messages before release 1.0 (Report 2 risk 15, PB-25)?",
    ]
    by = ["Huỳnh Trung Kiên", "Nguyễn Văn Thành Đạt", "Huỳnh Trung Kiên", "Nguyễn Văn Thành Đạt", "Nguyễn Gia Huy", "Huỳnh Trung Kiên", "Trần Nguyễn Hiếu Thuận", "Huỳnh Trung Kiên", "Huỳnh Trung Kiên",
          "Huỳnh Trung Kiên", "Huỳnh Trung Kiên", "Nguyễn Văn Thành Đạt", "Huỳnh Trung Kiên"]
    pr = ["High", "High", "Medium", "Medium", "Medium", "High", "Medium", "High", "Medium", "High", "Medium", "Medium", "Medium"]
    answers = {8: ("Closed", "13/11/2026: first accepted as designed; on 17/11/2026 the curve was changed to n × 10 per level (b08f5c7), so level 25 needs 3,000 XP and D-20 is fixed (PB-18).")}
    asked = lambda i: dt.date(2026, 11, 11) if i < 8 else (dt.date(2026, 11, 12) if i == 8 else dt.date(2026, 11, 17))
    write_rows(ws, 4, [[asked(i), q, by[i], pr[i]] + list(answers.get(i, ("Open", "To be asked at the next supervisor meeting; answer and date to be recorded here"))) for i, q in enumerate(qa)])
    for name in wb.sheetnames:
        wb[name].sheet_view.zoomScale = 90
    out = os.path.join(REPORT_DIR, "PickleVerse_Project_Tracking.xlsx")
    wb.save(out)
    return out, rows


# ====================================================================== MS Project XML
def schedule():
    def d(x, h=8):
        return dt.datetime(x.year, x.month, x.day, h, 0, 0).strftime("%Y-%m-%dT%H:%M:%S")

    def workdays(a, b):
        n, cur = 0, a
        while cur <= b:
            if cur.weekday() < 5:
                n += 1
            cur += dt.timedelta(days=1)
        return n

    tasks, assigns = [], []
    uid = 0

    def add(name, level, outline, start, finish, summary=False, milestone=False, pct=0, work_h=0, notes="", preds=()):
        nonlocal uid
        uid += 1
        days = 0 if milestone else workdays(start, finish)
        tasks.append(dict(uid=uid, name=name, level=level, outline=outline, start=start, finish=finish, summary=summary, milestone=milestone,
                          pct=pct, dur=days * 8, work=work_h, notes=notes, preds=list(preds)))
        return uid

    milestones = [("M01 Report 1 – Project Introduction", dt.date(2026, 9, 13), 100), ("M02 Report 2 – Project Management Plan", dt.date(2026, 9, 20), 100),
                  ("M03 Report 3 – SRS", dt.date(2026, 9, 27), 100), ("M04 Report 4 – SDD and test plan", dt.date(2026, 10, 11), 100),
                  ("M05 Software Package 1 (Review 2)", dt.date(2026, 10, 25), 100), ("M06 Software Package 2", dt.date(2026, 11, 8), 100),
                  ("M07 Software Package 3 (Review 3)", dt.date(2026, 11, 22), 0), ("M08 Full software package", dt.date(2026, 12, 6), 0),
                  ("M09 Report 6 – User Guides", dt.date(2026, 12, 13), 100), ("M10 Report 7 and final defense", dt.date(2026, 12, 20), 0)]
    top = 0
    group_uid = {}
    item_uid = {}
    for wid, name, items in WBS:
        top += 1
        starts = [ITER[PLAN[i[0]][0]][0] for i in items]
        ends = [ITER[PLAN[i[0]][1]][1] for i in items]
        tot = sum(i[3] for i in items)
        pct = round(sum(i[3] * PLAN[i[0]][3] for i in items) / tot)
        g = add("%s %s" % (wid, name), 1, str(top), min(starts), max(ends), summary=True, pct=pct, work_h=tot * 8)
        group_uid[wid] = g
        for k, (iid, iname, cx, md, own) in enumerate(items, 1):
            a, b, status, p, note = PLAN[iid]
            u = add("%s %s" % (iid, iname), 2, "%s.%d" % (top, k), ITER[a][0], ITER[b][1], pct=p, work_h=md * 8,
                    notes="Complexity %s; %s; %s" % (cx, status, note))
            item_uid[iid] = u
            people = owners(own)
            for person in people:
                assigns.append((u, person, md * 8.0 / len(people)))
    top += 1
    msg = add("Milestones", 1, str(top), dt.date(2026, 9, 13), dt.date(2026, 12, 20), summary=True)
    pred = {"M01": ["1.2"], "M02": ["1.2"], "M03": ["1.3"], "M04": ["1.4"], "M05": ["3.3", "5.2", "14.1"], "M06": ["2.4", "12.6", "8.3"],
            "M07": ["7.3", "15.2"], "M08": ["15.2"], "M09": ["1.5"], "M10": ["15.3", "15.4"]}
    for k, (n, date, p) in enumerate(milestones, 1):
        add(n, 2, "%d.%d" % (top, k), date, date, milestone=True, pct=p, preds=[item_uid[x] for x in pred[n[:3]]],
            notes="Actual: " + {"M01": "06/11/2026", "M02": "06/11/2026", "M03": "06/11/2026", "M04": "06/11/2026", "M05": "27/10/2026", "M06": "06/11/2026", "M09": "11/11/2026 (v1.0)", "M10": "Report 7 v1.0 on 11/11/2026; defense pending"}.get(n[:3], "planned"))
    res_uid = {m[0]: i + 1 for i, m in enumerate(MEMBERS)}
    out = ['<?xml version="1.0" encoding="UTF-8" standalone="yes"?>', '<Project xmlns="http://schemas.microsoft.com/project">',
           "  <SaveVersion>14</SaveVersion>", "  <Name>PickleVerse_Project_Schedule</Name>", "  <Title>PickleVerse (PKV) – GFA26SE111 master schedule</Title>",
           "  <Company>FPT University</Company>", "  <Author>GFA26SE111</Author>", "  <ScheduleFromStart>1</ScheduleFromStart>",
           "  <StartDate>%s</StartDate>" % d(dt.date(2026, 9, 7)), "  <FinishDate>%s</FinishDate>" % d(dt.date(2026, 12, 20), 17),
           "  <CurrentDate>%s</CurrentDate>" % d(dt.date(2026, 11, 25)), "  <StatusDate>%s</StatusDate>" % d(dt.date(2026, 11, 25), 17),
           "  <CalendarUID>1</CalendarUID>", "  <DefaultStartTime>08:00:00</DefaultStartTime>", "  <DefaultFinishTime>17:00:00</DefaultFinishTime>",
           "  <MinutesPerDay>480</MinutesPerDay>", "  <MinutesPerWeek>2400</MinutesPerWeek>", "  <DaysPerMonth>20</DaysPerMonth>",
           "  <Calendars>", "    <Calendar>", "      <UID>1</UID>", "      <Name>Standard</Name>", "      <IsBaseCalendar>1</IsBaseCalendar>", "      <WeekDays>"]
    for day in range(1, 8):
        working = day not in (1, 7)
        out += ["        <WeekDay>", "          <DayType>%d</DayType>" % day, "          <DayWorking>%d</DayWorking>" % (1 if working else 0)]
        if working:
            out += ["          <WorkingTimes>", "            <WorkingTime><FromTime>08:00:00</FromTime><ToTime>12:00:00</ToTime></WorkingTime>",
                    "            <WorkingTime><FromTime>13:00:00</FromTime><ToTime>17:00:00</ToTime></WorkingTime>", "          </WorkingTimes>"]
        out += ["        </WeekDay>"]
    out += ["      </WeekDays>", "    </Calendar>", "  </Calendars>", "  <Tasks>"]
    for i, t in enumerate(tasks, 1):
        out += ["    <Task>", "      <UID>%d</UID>" % t["uid"], "      <ID>%d</ID>" % i, "      <Name>%s</Name>" % escape(t["name"]), "      <Type>1</Type>",
                "      <WBS>%s</WBS>" % t["outline"], "      <OutlineNumber>%s</OutlineNumber>" % t["outline"], "      <OutlineLevel>%d</OutlineLevel>" % t["level"],
                "      <Start>%s</Start>" % d(t["start"]), "      <Finish>%s</Finish>" % (d(t["finish"], 17) if not t["milestone"] else d(t["finish"], 17)),
                "      <Duration>PT%dH0M0S</Duration>" % t["dur"], "      <DurationFormat>7</DurationFormat>", "      <Work>PT%dH0M0S</Work>" % t["work"],
                "      <Milestone>%d</Milestone>" % (1 if t["milestone"] else 0), "      <Summary>%d</Summary>" % (1 if t["summary"] else 0),
                "      <PercentComplete>%d</PercentComplete>" % t["pct"], "      <PercentWorkComplete>%d</PercentWorkComplete>" % t["pct"],
                "      <ConstraintType>4</ConstraintType>" if not t["summary"] else "      <ConstraintType>0</ConstraintType>",
                "      <ConstraintDate>%s</ConstraintDate>" % d(t["start"]) if not t["summary"] else ""]
        for p in t["preds"]:
            out += ["      <PredecessorLink><PredecessorUID>%d</PredecessorUID><Type>1</Type></PredecessorLink>" % p]
        if t["notes"]:
            out += ["      <Notes>%s</Notes>" % escape(t["notes"])]
        out += ["    </Task>"]
    out += ["  </Tasks>", "  <Resources>"]
    for m in MEMBERS:
        out += ["    <Resource>", "      <UID>%d</UID>" % res_uid[m[0]], "      <ID>%d</ID>" % res_uid[m[0]], "      <Name>%s</Name>" % escape(m[0]),
                "      <Type>1</Type>", "      <Initials>%s</Initials>" % m[2], "      <MaxUnits>1.00</MaxUnits>", "      <CalendarUID>1</CalendarUID>", "    </Resource>"]
    out += ["  </Resources>", "  <Assignments>"]
    for k, (tu, person, hours) in enumerate(assigns, 1):
        out += ["    <Assignment>", "      <UID>%d</UID>" % k, "      <TaskUID>%d</TaskUID>" % tu, "      <ResourceUID>%d</ResourceUID>" % res_uid[person],
                "      <Units>1</Units>", "      <Work>PT%dH0M0S</Work>" % round(hours), "    </Assignment>"]
    out += ["  </Assignments>", "</Project>", ""]
    text = "\n".join(x for x in out if x != "")
    path = os.path.join(REPORT_DIR, "PickleVerse_Project_Schedule.xml")
    with open(path, "w", encoding="utf-8") as f:
        f.write(text)
    return path, len(tasks), len(assigns)


if __name__ == "__main__":
    print(tracking()[0])
    print(schedule())
