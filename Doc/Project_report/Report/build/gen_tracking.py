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
    "2.2": (3, 3, "Done", 100, "Normalised schema 31/10/2026 (51 tables)"),
    "2.3": (4, 4, "Done", 100, "Docker deployment 04/11/2026"),
    "2.4": (4, 4, "Done", 100, "Validate, publish and deploy jobs; Unity tests not in CI"),
    "3.1": (2, 2, "Done", 100, "Covered by EditMode tests"),
    "3.2": (2, 2, "Done", 100, "PaddleContactTests, SinglesMatchTests"),
    "3.3": (2, 2, "Done", 100, "SinglesMatchTests, MultiplayerRulesTests"),
    "3.4": (2, 2, "Done", 100, "D-07 open"),
    "3.5": (2, 3, "Done", 100, "D-10 open"),
    "4.1": (3, 3, "Done", 100, "TC-F01-01 pending (test mailbox)"),
    "4.2": (3, 4, "Done", 100, "TC-F01-02 passed"),
    "4.3": (4, 4, "Done", 100, "Not yet system-tested"),
    "4.4": (3, 3, "Doing", 80, "D-07 character choice not applied everywhere"),
    "4.5": (3, 3, "Done", 100, ""),
    "5.1": (2, 2, "Done", 100, "TC-F02-01 passed"),
    "5.2": (2, 4, "Done", 100, "TC-F03-01..03 passed"),
    "5.3": (4, 4, "Done", 100, "15 lessons seeded"),
    "5.4": (4, 5, "Doing", 70, "TC-F14-02 pending; D-02 open"),
    "6.1": (2, 2, "Done", 100, "D-13 balance session pending"),
    "6.2": (3, 3, "Done", 100, "BhTreeTests passed"),
    "6.3": (2, 2, "Done", 100, "D-05 label"),
    "7.1": (3, 3, "Done", 100, "Moved to Photon Fusion 20/10/2026"),
    "7.2": (4, 5, "Doing", 85, "D-15 fallback timing"),
    "7.3": (4, 5, "Doing", 80, "D-09 human ranked matches interrupted"),
    "7.4": (4, 5, "Doing", 70, "Reconnect fix 10/11/2026; TC-F09-03 pending"),
    "7.5": (4, 4, "Done", 100, "D-08 start Elo decision"),
    "8.1": (4, 4, "Done", 100, ""),
    "8.2": (3, 4, "Done", 100, "Tier model gated (NFR-06)"),
    "8.3": (4, 4, "Done", 100, "D-02, D-11 open"),
    "9.1": (2, 2, "Done", 100, ""),
    "9.2": (2, 2, "Done", 100, ""),
    "9.3": (2, 2, "Done", 100, "D-12 Elite Token source"),
    "10.1": (2, 3, "Done", 100, "TC-F12-01 pending"),
    "10.2": (2, 3, "Done", 100, ""),
    "10.3": (2, 2, "Done", 100, ""),
    "11.1": (4, 4, "Done", 100, "TC-F17-01 pending"),
    "11.2": (4, 4, "Done", 100, "TC-F17-03 pending"),
    "12.1": (3, 3, "Done", 100, ""),
    "12.2": (4, 4, "Done", 100, "Admin cases pending in Round 2"),
    "12.3": (4, 4, "Done", 100, ""),
    "12.4": (4, 4, "Done", 100, ""),
    "12.5": (4, 4, "Done", 100, ""),
    "12.6": (4, 4, "Done", 100, ""),
    "13.1": (4, 4, "Done", 100, "No labelled real data yet"),
    "14.1": (1, 2, "Done", 100, ""),
    "14.2": (2, 2, "Done", 100, ""),
    "14.3": (3, 4, "Done", 100, "D-01, D-04 open"),
    "15.1": (4, 5, "Doing", 50, "515 EditMode tests passed; coverage not measured"),
    "15.2": (5, 6, "Doing", 30, "Round 1 done; Rounds 2–3 planned"),
    "15.3": (5, 6, "Pending", 0, "Acceptance 07–11/12/2026"),
    "15.4": (4, 6, "Doing", 50, "Staging running; release package pending"),
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
    ws["D5"] = "PROJECT WORK BREAKDOWN STRUCTURE – PickleVerse (PKV), updated 11/11/2026"
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
        ("PB-01", "As a player I want ranked matches between two humans to finish normally so that my Elo changes", "Defect fix", "High", "Iteration 5", "Đạt", 3, "Doing", "D-09; TC-F09-01, TC-F09-03"),
        ("PB-02", "Remove committed credentials, rotate them and load secrets from the environment", "Security", "High", "Iteration 5", "Đạt", 1, "Pending", "D-17; TC-N09"),
        ("PB-03", "Serve the API and admin web over HTTPS on staging", "Security", "High", "Iteration 5", "Đạt", 1, "Pending", "D-18; TC-N09"),
        ("PB-04", "Add seed accounts, a test mailbox and an analytics seed to Database.sql", "Test data", "High", "Iteration 5", "Thuận", 2, "Pending", "Pending cases of Report 5"),
        ("PB-05", "As an offline player I want a clear analysis status so that I know when tips will arrive", "Defect fix", "Medium", "Iteration 5", "Đạt", 0.5, "Pending", "D-02; TC-F06-03"),
        ("PB-06", "Apply the selected character in bot match, training and tutorial", "Defect fix", "Medium", "Iteration 5", "Huy", 1, "Pending", "D-07"),
        ("PB-07", "Fix overlapping UI elements (offline banner, appeals button, result button, key hints)", "Defect fix", "Medium", "Iteration 5", "Thuận", 1.5, "Pending", "D-01, D-03, D-04, D-10"),
        ("PB-08", "Decide the starting Elo and the Elite Token source with the supervisor and implement the decision", "Decision", "Medium", "Iteration 5", "Kiên", 1, "Pending", "D-08, D-12; Q&A 3–4"),
        ("PB-09", "Run the EditMode suite automatically and link results to test case IDs", "Test automation", "Medium", "Iteration 5", "Thuận", 2, "Pending", "Issue I-07"),
        ("PB-10", "Measure NFR-02 to NFR-05 and run the usability test (NFR-01)", "Test", "Medium", "Iteration 5", "Thuận", 3, "Pending", "TC-N01–TC-N05"),
        ("PB-11", "Bot balance session with players of known level", "Balance", "Medium", "Iteration 5", "Huy", 1, "Pending", "D-13"),
        ("PB-12", "Write Report 6 – user guides (installation, player, administrator)", "Document", "High", "Iteration 4", "Huy", 3, "Done", "M09; v1.0 on 11/11/2026"),
        ("PB-13", "Write Report 7 – final report consolidating Reports 1–6", "Document", "High", "Iteration 4", "Huy", 3, "Done", "M10; v1.0 on 11/11/2026"),
        ("PB-15", "Prepare the defense slides and rehearse the demo of W01–W04", "Document", "High", "Iteration 6", "Kiên", 2, "Pending", "M10"),
        ("PB-14", "Acceptance test W01–W04 with the supervisor", "Test", "High", "Iteration 6", "Kiên", 1, "Pending", "TC-W01–TC-W04"),
    ]
    write_rows(bl, 2, [list(x) for x in items])
    for col, w in zip("ABCDEFGHI", [8, 70, 14, 10, 12, 10, 12, 10, 28]):
        bl.column_dimensions[col].width = w
    # ---------------- Issues
    ws = wb["Issues"]
    clear(ws, 4, 9)
    issues = [
        ("Reports 1–4 were first submitted on 06/11/2026, 4–8 weeks after their due dates", "Lower process marks; review comments arrive late", "High", "KienHT", dt.date(2026, 11, 6), dt.date(2026, 11, 11), "Closed", "Reports revised to v1.0; deadlines now tracked weekly (Report 2 §1.2)"),
        ("Two ranked 1v1 matches between humans ended interrupted on 09/11/2026", "Main online workflow W03 cannot be accepted", "High", "DatNVT", dt.date(2026, 11, 9), None, "Open", "D-09; reproduce with server logs by 18/11/2026"),
        ("Development credentials and a fallback JWT key are committed to the repository", "Account takeover or e-mail abuse if the repository leaks", "High", "DatNVT", dt.date(2026, 11, 10), None, "Open", "D-17; rotate values and move them to the server .env file"),
        ("Staging API and admin web use HTTP", "Credentials and tokens travel unencrypted", "High", "DatNVT", dt.date(2026, 11, 10), None, "Open", "D-18"),
        ("Uneven contribution: two members hold most of the commits", "Key-person risk; members cannot explain modules they did not build", "Medium", "KienHT", dt.date(2026, 11, 10), None, "Open", "Pairing sessions and workflow ownership (Report 2 risk 1)"),
        ("No pull-request review before 10/11/2026", "Defects and secrets reach main without review", "Medium", "KienHT", dt.date(2026, 11, 10), None, "Open", "Pull requests with one reviewer from Iteration 5"),
        ("Unity tests are not run in CI (the pipeline builds the server with a Unity licence but has no test step)", "Automation target of Review 2 not demonstrated", "Medium", "ThuanTNH", dt.date(2026, 11, 11), None, "Open", "PB-09"),
        ("No test mailbox, seed accounts or analytics seed", "62 test cases pending", "Medium", "ThuanTNH", dt.date(2026, 11, 11), None, "Open", "PB-04"),
        ("Elite Token has no earning source", "GEMSTONE can only be granted at level 25", "Medium", "KienHT", dt.date(2026, 11, 10), None, "Open", "D-12; Q&A 3"),
        ("One dedicated-server slot on staging", "One online match at a time; capacity target not reachable", "Medium", "DatNVT", dt.date(2026, 11, 3), None, "Open", "Report 1 LI-07; Q&A 2"),
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
            "TC-F09-01": "FE-04", "TC-F06-01": "FE-05", "TC-F04-01": "FE-06", "TC-W03": "FE-04", "TC-F07-01": "FE-04", "Unit test run": "Testing", "TC-N09": "Security"}
    tester = {"TC-F09-01": "Nguyễn Gia Huy, Nguyễn Văn Thành Đạt"}
    rows = []
    for did, summary, typ, sev, tc, date, own in R.DEFECTS:
        c = next((x for x in R.CASES if x["id"] == tc), None)
        screen = c["desc"] if c else "Sync-UnityTests.ps1"
        rows.append([feat.get(tc, ""), screen, tester.get(tc, "Nguyễn Gia Huy"), summary, SHORT[own], "Assigned", typ, did, sev, dt.datetime.strptime(date, "%d/%m/%Y").date(), tc])
    write_rows(ws, 4, rows)
    ws.data_validations.dataValidation = []
    dv = DataValidation(type="list", formula1='"Pending, Assigned, Fixed, Closed, Cancelled"', allow_blank=True)
    dv.add("F4:F%d" % (3 + len(rows)))
    ws.add_data_validation(dv)
    ws.cell(row=2, column=1, value="Life cycle: Pending → Assigned → Fixed → Closed (after retest). Severity: Critical, High, Medium, Low (Report 5 §5.4).")
    # ---------------- Q&A
    ws = wb["Q&A"]
    clear(ws, 4, 6)
    qa = [
        "Do you approve the deviations from the proposal listed in Report 1 LI-03 to LI-08 (Elo only for ranked singles and ranked bot, ASP.NET Core, Photon Fusion, uGUI, Windows and Android only)?",
        "Is the capacity requirement accepted as 2,000 concurrent API users with one dedicated match slot on staging (NFR-04, LI-07)?",
        "Should Elite Token receive an earning source in release 1.0, or should items priced only in Elite Token be removed (D-12)?",
        "Which starting Elo should new players have: 100 (current, equal to the floor) or another value, and should admin reset use the same value (D-08)?",
        "Is the expert review tool (FE-10) accepted as part of the committed scope or only as supporting tooling?",
        "What are the date and the required package of Review 3?",
        "Which Android device should be the reference device for NFR-03 and NFR-10?",
        "Please confirm the RACI matrix in Report 2 §4 for the documents and modules (workflow owners are fixed by BM04).",
    ]
    by = ["Huỳnh Trung Kiên", "Nguyễn Văn Thành Đạt", "Huỳnh Trung Kiên", "Nguyễn Văn Thành Đạt", "Nguyễn Gia Huy", "Huỳnh Trung Kiên", "Trần Nguyễn Hiếu Thuận", "Huỳnh Trung Kiên"]
    pr = ["High", "High", "Medium", "Medium", "Medium", "High", "Medium", "High"]
    write_rows(ws, 4, [[dt.date(2026, 11, 11), q, by[i], pr[i], "Open", "To be asked at the next supervisor meeting; answer and date to be recorded here"] for i, q in enumerate(qa)])
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
           "  <CurrentDate>%s</CurrentDate>" % d(dt.date(2026, 11, 11)), "  <StatusDate>%s</StatusDate>" % d(dt.date(2026, 11, 11), 17),
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
