"""Report 7 - Final Project Report.

Fills the cover, acknowledgement and acronym table of the official template, then assembles Parts I–VI by running
the generators of Reports 1–6 against this document. Figures and tables are numbered within each part, so the
cross-references written in each report stay correct.
"""
import os
import re

from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml.ns import qn
from docx.shared import Pt

from report_lib import Report, PROJECT_NAME, add_runs, _set_cell_width

HERE = os.path.dirname(os.path.abspath(__file__))
os.environ.setdefault("PKV_EDITMODE_RESULTS", os.path.abspath(os.path.join(HERE, "..", "evidence", "editmode_results_20261111.json")))

PARTS = [
    ("gen_r1.py", "I. Project Introduction"),
    ("gen_r2.py", "II. Project Management Plan"),
    ("gen_r3.py", "III. Software Requirement Specification"),
    ("gen_r4.py", "IV. Software Design Description"),
    ("gen_r5.py", "V. Software Testing Documentation"),
    ("gen_r6.py", "VI. Release Package & User Guides"),
]

ACRONYMS = [
    ("AI", "Artificial Intelligence"), ("API", "Application Programming Interface"), ("BR", "Business Rule"),
    ("CI/CD", "Continuous Integration / Continuous Delivery"), ("CMS", "Content Management System (lesson, item and mission management in the admin web)"),
    ("DAU", "Daily Active Users"), ("Dedicated server", "Headless Unity server that runs an online match with authority over the result"),
    ("Drill", "Practice exercise with a fixed number of shots and a target"), ("Elo", "Rating system used for ranked matches (K-factor 32, floor 100)"),
    ("ERD", "Entity Relationship Diagram"), ("EV", "Evidence item of a test run (Report 5, §5.1)"), ("FE", "Major feature (FE-01 to FE-10)"),
    ("FR", "Functional Requirement (FR-01 to FR-33)"), ("Fusion", "Photon Fusion 2, the real-time networking library and cloud"),
    ("GUI", "Graphical User Interface"), ("HMAC", "Hash-based Message Authentication Code"), ("HUD", "Head-Up Display (in-match information)"),
    ("JWT", "JSON Web Token"), ("Kitchen / NVZ", "Non-volley zone, the area within 2.13 m of the net where volleys are not allowed"),
    ("LI", "Limitation or exclusion (Report 1, §6.2)"), ("NFR", "Non-Functional Requirement (NFR-01 to NFR-12)"), ("OTP", "One-Time Password"),
    ("PB", "Product backlog item"), ("RACI", "Responsibility matrix: Do, Review, Support, Inform"), ("RTT", "Round-Trip Time"),
    ("S3", "Amazon Simple Storage Service API, used for replay storage"), ("SDD", "Software Design Description"),
    ("SPMP", "Software Project Management Plan"), ("SRS", "Software Requirement Specification"), ("TC", "Test Case"),
    ("Tokenpad", "In-game currency earned by playing"), ("UAT", "User Acceptance Test"), ("UC", "Use Case (UC-01 to UC-68)"),
    ("UI", "User Interface"), ("W01–W04", "End-to-end review workflows committed in the Review 2 form BM04"),
    ("WBS", "Work Breakdown Structure"), ("XP", "Experience points"),
]

ACK = [
    "We would like to express our sincere gratitude to our supervisor, Mr. Lại Đức Hùng, for his guidance throughout the capstone project. His questions about scope, requirements and evidence shaped the way we planned the work, designed the system and reported our results.",
    "We thank FPT University and the lecturers of the Software Engineering programme for the knowledge and the learning environment that made this project possible, and the members of the review committees for their comments at each review, which helped us improve both the product and the documents.",
    "We also acknowledge the open-source communities and tool vendors whose work we built on, in particular Unity, Photon, the .NET, PostgreSQL, Redis, FastAPI and React communities, and the authors of PlantUML and draw.io used for our diagrams.",
    "Finally, we thank our families and friends for their patience and support during the many evenings of development and testing.",
]


class Part:
    """Lets a report generator write into Report 7: the first heading becomes the part title and saving is skipped."""

    def __init__(self, base, title):
        self.__dict__["_base"] = base
        self.__dict__["_title"] = title

    def __getattr__(self, name):
        return getattr(self._base, name)

    def h1(self, text):
        self._base.page_break()
        return self._base.h1(self._title)

    def save(self, *args, **kwargs):
        return "(written into Report 7)"


def set_cell(cell, text, bold=False, size=12, center=True):
    for p in cell.paragraphs[1:]:
        p._element.getparent().remove(p._element)
    p = cell.paragraphs[0]
    for run in p.runs:
        run.text = ""
    p.text = ""
    run = p.add_run(text)
    run.bold = bold
    run.font.size = Pt(size)
    if center:
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER


r = Report("Report7_Final Project Report.docx", "Report 7 – Final Project Report", [], prepare=False)
doc = r.doc
body = doc.element.body

# ---------------------------------------------------------------- cover
tables = doc.tables
for t in tables[:3]:
    for row in t.rows:
        for cell in row.cells:
            txt = cell.text.strip()
            if txt == "[Project name]":
                set_cell(cell, PROJECT_NAME, bold=True, size=16)
            elif txt == "<Group Name>":
                set_cell(cell, "GFA26SE111", bold=True, size=14)
info = tables[2]
for row in info.rows:
    label = row.cells[0].text.strip()
    if label == "Group Members":
        set_cell(row.cells[-1], "", center=False)
        p = row.cells[-1].paragraphs[0]
        members = ["Huỳnh Trung Kiên – SE184454 (Leader)", "Nguyễn Văn Thành Đạt – SE184450", "Nguyễn Gia Huy – SE184439", "Trần Nguyễn Hiếu Thuận – SE184471"]
        for k, m in enumerate(members):
            para = p if k == 0 else row.cells[-1].add_paragraph()
            para.add_run(m).font.size = Pt(12)
    elif label == "Supervisor":
        set_cell(row.cells[-1], "Mr. Lại Đức Hùng", center=False)
    elif label == "Ext Supervisor":
        set_cell(row.cells[-1], "None", center=False)
    elif label == "Capstone Project code":
        set_cell(row.cells[-1], "PKV", center=False)
for p in doc.paragraphs[:20]:
    if "Hanoi" in p.text:
        for run in p.runs[1:]:
            run.text = ""
        p.runs[0].text = "– Ho Chi Minh City, November 2026 –"

# ---------------------------------------------------------------- acknowledgement and acronyms
paras = doc.paragraphs
ack_i = next(i for i, p in enumerate(paras) if p.text.strip() == "Acknowledgement")
placeholder = paras[ack_i + 1]
placeholder.text = ""
add_runs(placeholder, ACK[0])
placeholder.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
anchor = placeholder
for text in ACK[1:]:
    new = doc.add_paragraph()
    add_runs(new, text)
    new.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
    anchor._element.addnext(new._element)
    anchor = new

paras = doc.paragraphs
def_i = next(i for i, p in enumerate(paras) if p.text.strip().startswith("Definition and Acronyms"))
intro = paras[def_i + 1]
intro.text = "The definitions and acronyms used in this document are listed below. Pickleball terms and product-specific terms are also explained in Part III, §5.4."
acr = next(t for t in doc.tables if t.rows[0].cells[0].text.strip() == "Acronym")
while len(acr.rows) > 1:
    acr._tbl.remove(acr.rows[-1]._tr)
for a, d in ACRONYMS:
    row = acr.add_row()
    set_cell(row.cells[0], a, bold=True, size=11, center=False)
    set_cell(row.cells[1], d, size=11, center=False)
for row in acr.rows:
    _set_cell_width(row.cells[0], 3.6)
    _set_cell_width(row.cells[1], 12.4)

# ---------------------------------------------------------------- remove the template parts I–VI
children = list(body.iterchildren())
start = None
for k, el in enumerate(children):
    if el.tag == qn("w:p") and "".join(t.text or "" for t in el.iter(qn("w:t"))).strip() == "I. Project Introduction":
        start = k
        break
sect = body.find(qn("w:sectPr"))
for el in children[start:]:
    if el is not sect:
        body.remove(el)

note = r.p("**About this document.** This final report consolidates the six capstone reports of the project in their latest versions: Part I – Project Introduction (Report 1), Part II – Project Management Plan (Report 2), Part III – Software Requirement Specification (Report 3), Part IV – Software Design Description (Report 4), Part V – Software Testing Documentation (Report 5) and Part VI – Release Package & User Guides (Report 6). A reference of the form \"Report n\" points to Part n of this document. Figures and tables are numbered separately within each part. The test workbooks, the project tracking workbook and the schedule are delivered as separate files (Part VI, §1).")
acr._tbl.addnext(note._element)  # keep the note on the acronym page, before the template's page break

# ---------------------------------------------------------------- parts
for script, title in PARTS:
    r.fig_no = 0
    r.tbl_no = 0
    src = open(os.path.join(HERE, script), encoding="utf-8").read()
    src, n = re.subn(r"^r = Report\(.*\)\s*$", "r = PART", src, count=1, flags=re.M)
    assert n == 1, script
    namespace = {"__name__": "report7_part", "__file__": os.path.join(HERE, script), "PART": Part(r, title)}
    exec(compile(src, script, "exec"), namespace)

out = r.save("Report7_Final Project Report.docx", "Report 7 – Final Project Report")
print(out, "sections", len(r.doc.sections))
