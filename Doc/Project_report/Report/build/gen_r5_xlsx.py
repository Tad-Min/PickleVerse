"""Builds Report5_Test Report.xlsx and Report5_Unit Test.xlsx on the official templates."""
import copy
import datetime
import os

import openpyxl
from openpyxl.styles import Alignment, Font, PatternFill
from openpyxl.worksheet.datavalidation import DataValidation

import r5_cases as R

HERE = os.path.dirname(os.path.abspath(__file__))
REPORT_DIR = os.path.abspath(os.path.join(HERE, ".."))
TPL = os.path.abspath(os.path.join(REPORT_DIR, "..", "ReportTemplete"))
UNIT_TPL = os.environ.get("PKV_UNIT_TPL")  # xlsx converted from Report5_Unit Test.xls
PROJECT = "PickleVerse – Multiplayer Pickleball Skill-Training and Match Game with AI Coaching"
WRAP = Alignment(wrap_text=True, vertical="top")
GROUP_FILL = PatternFill("solid", fgColor="DDEBF7")
STATUS_FILL = {"Passed": "C6EFCE", "Failed": "FFC7CE", "Pending": "FFEB9C"}
ISSUE = datetime.date(2026, 11, 12)      # Unit Test workbook v1.1 (unchanged)
ISSUE_TR = datetime.date(2026, 11, 13)   # Test Report workbook v1.2


def d(text):
    return datetime.datetime.strptime(text, "%d/%m/%Y").date() if text else None


def cover(ws, title_code, changes, version="1.1", issue=None):
    ws["B4"] = PROJECT
    ws["B5"] = "PKV"
    ws["F4"] = R.HUY
    ws["F5"] = issue or ISSUE
    ws["F5"].number_format = "dd/mm/yyyy"
    ws["B6"] = "PKV_%s_v%s" % (title_code, version)
    ws["F6"] = version
    for i, row in enumerate(changes):
        for j, v in enumerate(row):
            c = ws.cell(row=11 + i, column=1 + j, value=v)
            c.alignment = WRAP
            if j == 0:
                c.number_format = "dd/mm/yyyy"


# ====================================================================== Test Report
def build_test_report():
    wb = openpyxl.load_workbook(os.path.join(TPL, "Report5_Test Report.xlsx"))
    cover(wb["Cover"], "TestReport", [
        (d("06/11/2026"), "0.1", "Test case list", "A", "First list of 116 planned cases (all Not run)", "Report 3 v0.2"),
        (d("11/11/2026"), "1.0", "All sheets", "M", "Rewritten: 114 cases with concrete steps and data, mapped to FR-01–FR-33, real results of 09–11/11/2026, automated test names", "Report 3 v1.0, Report 4 v1.0"),
        (d("12/11/2026"), "1.1", "All sheets", "M", "%d cases: quick doubles case TC-F07-04 added; workflow names as in BM04; Defects sheet with D-19, D-20 and the fix commits of D-03, D-04, D-05, D-07 and D-19" % len(R.CASES), "Report 5 v1.2"),
        (d("13/11/2026"), "1.2", "FE04 Online, FE06 Progress, FE08-09 Admin, FE10 Expert, States, NFR, Defects, Test Statistics", "M", "Results of the CI run EV-08: %d cases passed by the IntegrationChecks of the validate job, %d cases annotated as partly covered; TC-N09 executed and failed (EV-09); defect D-21 added; D-18 and D-21 fixed, D-17 partly fixed, D-20 accepted" % (len(R.CI_PASSED), len(R.CI_PARTLY)), "Report 5 v1.3"),
    ], version="1.2", issue=ISSUE_TR)
    base = wb["Feature 1"]
    # ---------------- Test Cases sheet
    tc = wb["Test Cases"]
    tc["D3"] = PROJECT
    tc["D4"] = "PKV"
    tc["D5"] = ("Staging tad-min.io.vn (Ubuntu 24.04, Docker, Caddy): Web API and admin web on https://tad-min.io.vn since 13/11/2026 (in Round 1 the API was on "
                "http://tad-min.io.vn:8080 and the admin web on http://tad-min.io.vn), dedicated server UDP 3669. Client: Windows 11 PC, Unity 6000.6.2f1 Editor and Windows build of "
                "main @ 7f8ee32 (Round 1) and main @ 5963501 (fix checks). Unit: Unity Test Framework EditMode through the Unity CLI. Integration: IntegrationChecks in the GitHub Actions "
                "pipeline on main @ d9200cc (PostgreSQL 16, Redis 7.4). Android APK built on 12/11/2026 from main @ 5963501, no Android device yet.")
    for r in range(9, tc.max_row + 1):
        for c in range(2, 7):
            tc.cell(row=r, column=c).value = None
    for i, (sheet, name, req) in enumerate(R.SHEETS):
        n = sum(1 for c in R.CASES if c["sheet"] == sheet)
        vals = [i + 1, name, sheet, "%s (%d cases)" % (req, n), "See the Pre-conditions column of each case"]
        for j, v in enumerate(vals):
            cell = tc.cell(row=9 + i, column=2 + j, value=v)
            cell.alignment = WRAP
    # ---------------- feature sheets
    for sheet, name, req in R.SHEETS:
        ws = wb.copy_worksheet(base)
        ws.title = sheet
        ws["B2"] = name
        ws["B3"] = req
        last = 12
        for r in range(11, ws.max_row + 1):
            for c in range(1, 16):
                ws.cell(row=r, column=c).value = None
        row = 11
        group = None
        for case in [c for c in R.CASES if c["sheet"] == sheet]:
            g = case["fr"].split(",")[0].strip() if sheet not in ("States",) else case["fr"]
            if sheet == "States":
                g = case["fr"]
            if g != group:
                group = g
                cell = ws.cell(row=row, column=1, value=g)
                cell.font = Font(bold=True)
                for c in range(1, 16):
                    ws.cell(row=row, column=c).fill = GROUP_FILL
                row += 1
            vals = [case["id"], "%s [%s, %s; %s]" % (case["desc"], case["level"], case["kind"], case["fr"]), case["steps"], case["expected"], case["pre"],
                    case["status"], d(case["date"]), case["tester"], None, None, None, None, None, None,
                    (case["note"] + ("\nAutomated: " + case["auto"] if case["auto"] != "Manual" else "\nManual test")).strip()]
            for j, v in enumerate(vals):
                cell = ws.cell(row=row, column=1 + j, value=v)
                cell.alignment = WRAP
                if j == 6:
                    cell.number_format = "dd/mm/yyyy"
            st = ws.cell(row=row, column=6)
            st.fill = PatternFill("solid", fgColor=STATUS_FILL.get(case["status"], "FFFFFF"))
            row += 1
        last = row - 1
        ws["B4"] = "=COUNTA(A12:A%d)-%d" % (max(last, 12), len({(c["fr"].split(",")[0].strip() if sheet != "States" else c["fr"]) for c in R.CASES if c["sheet"] == sheet}) - 1)
        for k, col in enumerate(["F", "I", "L"]):
            for j, colx in enumerate(["B", "C", "D", "E"]):
                ws["%s%d" % (colx, 6 + k)] = '=COUNTIF($%s$12:$%s$%d,%s$5)' % (col, col, max(last, 12), colx)
        ws.data_validations.dataValidation = []
        dv = DataValidation(type="list", formula1="$R$2:$R$5", allow_blank=True)
        dv.add("F12:F%d" % max(last, 12))
        dv.add("I12:I%d" % max(last, 12))
        dv.add("L12:L%d" % max(last, 12))
        ws.add_data_validation(dv)
        ws.freeze_panes = "B12"
    # Number of TCs = non-empty IDs minus group rows: simpler to count IDs directly
    for sheet, _, _ in R.SHEETS:
        ws = wb[sheet]
        ws["B4"] = sum(1 for c in R.CASES if c["sheet"] == sheet)
    del wb["Feature 1"]
    del wb["Feature 2"]
    # ---------------- statistics
    st = wb["Test Statistics"]
    st["C3"] = PROJECT
    st["C4"] = "PKV"
    st["H3"] = R.HUY
    st["H4"] = "Huỳnh Trung Kiên"
    st["C5"] = "PKV_TestReport_v1.2"
    st["H5"] = ISSUE_TR
    st["H5"].number_format = "dd/mm/yyyy"
    st["C6"] = "Round 1 (09–11/11/2026) on main @ 7f8ee32; fixed defects checked on main @ 5963501; IntegrationChecks of the CI run of 13/11/2026 on main @ d9200cc (EV-08). Modules: FE-01 to FE-10, workflows, state transitions and NFRs."
    for r in range(9, 30):
        for c in range(2, 9):
            st.cell(row=r, column=c).value = None
    st["B10"], st["C10"], st["D10"], st["E10"], st["F10"], st["G10"], st["H10"] = "No", "Module code", "Passed", "Failed", "Pending", "N/A", "Number of  test cases"
    r0 = 11
    for i, (sheet, name, _) in enumerate(R.SHEETS):
        r = r0 + i
        st.cell(row=r, column=2, value=i + 1)
        st.cell(row=r, column=3, value="='%s'!B2" % sheet)
        for j, col in enumerate(["B", "C", "D", "E"]):
            st.cell(row=r, column=4 + j, value="='%s'!%s6" % (sheet, col))
        st.cell(row=r, column=8, value="='%s'!B4" % sheet)
    end = r0 + len(R.SHEETS) - 1
    tot = end + 2
    st.cell(row=tot, column=3, value="Sub total").font = Font(bold=True)
    for j, col in enumerate(["D", "E", "F", "G", "H"]):
        st.cell(row=tot, column=4 + j, value="=SUM(%s%d:%s%d)" % (col, r0, col, end)).font = Font(bold=True)
    st.cell(row=tot + 2, column=3, value="Test coverage (executed / applicable)")
    st.cell(row=tot + 2, column=5, value="=ROUND((D%d+E%d)*100/(H%d-G%d),1)" % (tot, tot, tot, tot))
    st.cell(row=tot + 2, column=6, value="%")
    st.cell(row=tot + 3, column=3, value="Test successful coverage (passed / applicable)")
    st.cell(row=tot + 3, column=5, value="=ROUND(D%d*100/(H%d-G%d),1)" % (tot, tot, tot))
    st.cell(row=tot + 3, column=6, value="%")
    st.cell(row=tot + 4, column=3, value="Pass rate of executed cases")
    st.cell(row=tot + 4, column=5, value="=ROUND(D%d*100/(D%d+E%d),1)" % (tot, tot, tot))
    st.cell(row=tot + 4, column=6, value="%")
    auto = sum(1 for c in R.CASES if c["auto"] != "Manual")
    st.cell(row=tot + 5, column=3, value="Automated test cases")
    st.cell(row=tot + 5, column=5, value=auto)
    st.cell(row=tot + 5, column=6, value="of %d (%.1f%%); %d run locally with the Unity CLI, %d run in CI (EV-08)" % (
        len(R.CASES), auto * 100.0 / len(R.CASES), sum(1 for c in R.CASES if c["auto"].startswith(R.EM)), sum(1 for c in R.CASES if c["tester"] == R.CI_TESTER)))
    # ---------------- defects sheet (reference list; the tracking workbook owns the life cycle)
    ws = wb.create_sheet("Defects")
    hdr = ["Defect ID", "Summary", "Type", "Severity", "Found by test", "Found on", "Owner", "Status", "Fix commit", "Status note"]
    for j, h in enumerate(hdr):
        c = ws.cell(row=1, column=1 + j, value=h)
        c.font = Font(bold=True)
        c.fill = PatternFill("solid", fgColor="D9E2F3")
    for i, dft in enumerate(R.DEFECTS):
        state = R.DEFECT_STATE.get(dft[0])
        vals = list(dft) + [R.defect_status(dft[0]), R.DEFECT_FIX.get(dft[0], ""), "%s: %s" % (state[1], state[2]) if state else (R.DEFECT_CHECK.get(dft[0], "Fix checked in Play Mode (EV-07)") + "; retest in Round 2" if dft[0] in R.DEFECT_FIX else "")]
        vals[5] = d(vals[5])
        for j, v in enumerate(vals):
            c = ws.cell(row=2 + i, column=1 + j, value=v)
            c.alignment = WRAP
            if j == 5:
                c.number_format = "dd/mm/yyyy"
    for col, w in zip("ABCDEFGHIJ", [10, 70, 14, 10, 16, 12, 10, 12, 12, 60]):
        ws.column_dimensions[col].width = w
    for row in st.iter_rows(min_row=11, max_row=tot + 5, min_col=2, max_col=8):
        for c in row:
            c.number_format = "General"
    out = os.path.join(REPORT_DIR, "Report5_Test Report.xlsx")
    wb.save(out)
    return out


# ====================================================================== Unit Test
def build_unit():
    wb = openpyxl.load_workbook(UNIT_TPL)
    cover(wb["Cover"], "UnitTest", [
        (d("06/11/2026"), "0.1", "Unit test list", "A", "36 selected unit checks (24 passed, 12 not run)", "Report 5 v0.2"),
        (d("11/11/2026"), "1.0", "All sheets", "M", "Rebuilt on the template: 10 core functions as decision tables mapped to NUnit tests; EditMode run of 11/11/2026 (515/515 passed)", "EV-01"),
        (d("12/11/2026"), "1.1", "Functions, Statistics", "M", "Latest EditMode run on main @ 5963501: 516/516 passed (camera framing test added)", "EV-06"),
    ])
    fl = wb["Functions"]
    fl["E6"] = 100
    fl["E7"] = "Unity 6000.6.2f1 Editor (Windows 11), Unity Test Framework EditMode, run through the Unity CLI Pipeline on main @ 7f8ee32 (11/11/2026, 515 tests, 6.19 s) and again on main @ 5963501 (11/11/2026, 516 tests, 5.96 s)."
    for r in range(11, 24):
        for c in range(1, 9):
            fl.cell(row=r, column=c).value = None
    base = wb["Function 1"]
    names = []
    for i, f in enumerate(R.UNIT):
        sheet = "%s %s" % (f["code"], f["cls"])[:31]
        names.append(sheet)
        for j, v in enumerate([i + 1, f["req"], f["cls"], f["fn"], f["code"], sheet, "Unity EditMode tests in %s" % f["file"], "Test assembly synchronised into the Unity project"]):
            c = fl.cell(row=11 + i, column=1 + j, value=v)
            c.alignment = WRAP
        ws = wb.copy_worksheet(base)
        ws.title = sheet
        ws["C2"] = f["code"]
        ws["L2"] = f["cls"] + "." + f["fn"]
        ws["C3"] = "GFA26SE111"
        ws["L3"] = R.HUY
        ws["C4"] = f["loc"]
        ws["C5"] = "%s – %s" % (f["req"], f["file"])
        # clear the sample matrix
        for r in range(9, 43):
            for c in range(2, 30):
                if r in (39, 40, 41, 42) and c in (2, 3, 4):
                    continue
                if r == 10 and c == 2:
                    continue
                if r == 31 and c == 2:
                    continue
                ws.cell(row=r, column=c).value = None
        ws["A10"] = "Condition"
        ws["A31"] = "Confirm"
        n = len(f["cases"])
        for k in range(n):
            ws.cell(row=9, column=6 + k, value="UTCID%02d" % (k + 1))
        # conditions rows 11..30, grouped
        row = 11
        cond_rows = []
        prev = None
        for grp, val in f["cond"]:
            if grp != prev:
                ws.cell(row=row, column=2, value=grp).font = Font(bold=True)
                prev = grp
            ws.cell(row=row, column=4, value=val).alignment = Alignment(wrap_text=False)
            cond_rows.append(row)
            row += 1
        assert row <= 31, (f["code"], row)
        row = 32
        conf_rows = []
        prev = None
        for grp, val in f["conf"]:
            if grp != prev:
                ws.cell(row=row, column=2, value=grp).font = Font(bold=True)
                prev = grp
            ws.cell(row=row, column=4, value=val)
            conf_rows.append(row)
            row += 1
        assert row <= 39, (f["code"], row)
        for k, (typ, conds, confs, test) in enumerate(f["cases"]):
            col = 6 + k
            for ci in conds:
                ws.cell(row=cond_rows[ci], column=col, value="O")
            for ci in confs:
                ws.cell(row=conf_rows[ci], column=col, value="O")
            ws.cell(row=39, column=col, value=typ)
            ws.cell(row=40, column=col, value="P")
            ws.cell(row=41, column=col, value=ISSUE).number_format = "dd/mm"
            ws.cell(row=43, column=col, value=test).alignment = Alignment(text_rotation=90, vertical="bottom")
        ws["B43"] = "NUnit test"
        ws.row_dimensions[43].height = 230
        ws["O7"] = "=COUNTA(F9:AD9)"
        ws["A7"] = '=COUNTIF(F40:AD40,"P")'
        ws["C7"] = '=COUNTIF(F40:AD40,"F")'
        ws["L7"] = '=COUNTIF(F39:AD39,"N")'
        ws["M7"] = '=COUNTIF(F39:AD39,"A")'
        ws["N7"] = '=COUNTIF(F39:AD39,"B")'
        ws["L4"] = '=IF(Functions!E6<>"N/A",MAX(0,ROUNDUP(C4*Functions!E6/1000,0)-O7),"N/A")'
    for s in ["Function 1", "Function 2", "Function3", "Example"]:
        if s in wb.sheetnames:
            del wb[s]
    st = wb["Statistics"]
    st["F4"] = R.HUY
    st["F5"] = "Trần Nguyễn Hiếu Thuận"
    st["F6"] = ISSUE
    st["F6"].number_format = "dd/mm/yyyy"
    st["B6"] = "PKV_UnitTest_v1.0"
    st["B7"] = "Core rule, rating, input, matchmaking, economy and offline-sync functions of the Unity client (10 functions, %d test cases)" % sum(len(f["cases"]) for f in R.UNIT) + ". The full EditMode suite passed on 11/11/2026 (515 tests on main @ 7f8ee32; 516 tests on main @ 5963501)."
    for r in range(12, 30):
        for c in range(1, 10):
            st.cell(row=r, column=c).value = None
    for i, sname in enumerate(names):
        r = 12 + i
        st.cell(row=r, column=1, value=i + 1)
        st.cell(row=r, column=2, value=R.UNIT[i]["code"] + " " + R.UNIT[i]["cls"])
        for j, cell in enumerate(["A7", "C7", "F7", "L7", "M7", "N7", "O7"]):
            st.cell(row=r, column=3 + j, value="='%s'!%s" % (sname, cell))
    end = 12 + len(names) - 1
    tot = end + 2
    st.cell(row=tot, column=2, value="Sub total").font = Font(bold=True)
    for j, col in enumerate("CDEFGHI"):
        st.cell(row=tot, column=3 + j, value="=SUM(%s12:%s%d)" % (col, col, end)).font = Font(bold=True)
    labels = [("Test coverage", "=(C%d+D%d)*100/I%d"), ("Test successful coverage", "=C%d*100/I%d"), ("Normal case", "=F%d*100/I%d"), ("Abnormal case", "=G%d*100/I%d"), ("Boundary case", "=H%d*100/I%d")]
    for k, (lab, fml) in enumerate(labels):
        rr = tot + 2 + k
        st.cell(row=rr, column=2, value=lab)
        st.cell(row=rr, column=4, value=fml % ((tot, tot, tot) if fml.count("%d") == 3 else (tot, tot)))
        st.cell(row=rr, column=5, value="%")
    for row in st.iter_rows(min_row=12, max_row=tot + 7, min_col=1, max_col=9):
        for c in row:
            c.number_format = "General" if c.column != 4 or c.row <= tot else "0.0"
    out = os.path.join(REPORT_DIR, "Report5_Unit Test.xlsx")
    wb.save(out)
    return out


if __name__ == "__main__":
    print(build_test_report())
    print(build_unit())
