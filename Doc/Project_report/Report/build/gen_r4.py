"""Report 4 - Software Design Document."""
import os
import re
import sys

from report_lib import Report, REPORT_DIR

sys.path.insert(0, os.path.join(REPORT_DIR, "diagrams"))
import schema_lib  # noqa: E402
import r4_text as T  # noqa: E402

CHANGES = [
    ("27/10/2026", "A", "Nguyễn Gia Huy", "v0.1 – Architecture, workflows W01–W04 and state machines summarised for the Review 2 design brief (BM01)."),
    ("06/11/2026", "A", "Nguyễn Văn Thành Đạt", "v0.2 – First SDD on the official template: architecture, packages, database groups, 7 class/sequence pairs, activities and state machines."),
    ("11/11/2026", "M", "Nguyễn Gia Huy", "v1.0 – Rewritten after the internal review: deployment diagram and architecture decisions; full package diagrams; logical ERDs covering all 51 tables with keys and a data dictionary; UML class diagrams with class specifications; 15 sequence diagrams with alternative paths; new sections for gameplay physics, netcode, bot AI, API and security design; activity diagrams with swimlanes; 14 corrected state machines."),
    ("11/11/2026", "M", "Nguyễn Gia Huy", "v1.1 – Activity diagrams: workflow owners aligned with BM04."),
    ("12/11/2026", "M", "Nguyễn Gia Huy", "v1.2 – Revised after the second internal review: API table regenerated from the controllers (129 endpoints, access and Used by); dedicated-server controllers corrected; time-out removed from the gameplay class diagram and SM-05; FlowState Delay, camera framing, lobby classes and quick doubles added; netcode design table; table descriptions of Users, PlayerProfiles and GameMatchPlayers corrected; delivery pipeline status and data-at-rest deviation recorded; workflow names aligned with BM04."),
    ("13/11/2026", "M", "Nguyễn Gia Huy", "v1.3 – Deployment updated after the fixes of 12–13/11/2026 (main @ d9200cc): delivery pipeline described as it runs (plan, checks, build, publish by digest, deploy with a verified dump) with the history of the 32 runs; Caddy HTTPS reverse proxy and the ports of staging; transport, secrets and data-protection rows of the security table."),
]

DESIGN_SRC = os.path.join(REPORT_DIR, "diagrams", "gen_r4_design.py")
# Figures whose PlantUML source (diagrams/src) is newer than the rendered PNG. Empty this set after rendering them again.
STALE_FIGURES = {"r4_architecture.png", "r4_deployment.png"}


def class_blocks(diagram):
    """Returns [(name, stereotype, members)] parsed from a PlantUML class diagram in gen_r4_design.py."""
    src = open(DESIGN_SRC, encoding="utf-8").read()
    body = re.search(r'D\["%s"\] = r"""(.*?)"""' % diagram, src, re.S).group(1)
    out = []
    for m in re.finditer(r'^\s*(abstract class|class|interface|enum)\s+("[^"]+"(?:\s+as\s+\w+)?|\w+)[ \t]*(<<[^>]+>>)?[ \t]*(\{(.*?)^\s*\})?', body, re.S | re.M):
        kind, name, stereo, _, members = m.groups()
        name = name.split(" as ")[0].strip('"').replace("\\n", " ")
        mem = [x.strip() for x in (members or "").splitlines() if x.strip() and x.strip() not in ("--", "..")]
        out.append((name, kind if kind != "class" else (stereo or "").strip("<>"), mem))
    return out


def class_spec(r, diagram, caption):
    rows = []
    for name, kind, mem in class_blocks(diagram):
        resp = T.RESP.get(name, "")
        attrs = [x for x in mem if "(" not in x]
        ops = [x for x in mem if "(" in x]
        label = name + ("\n«%s»" % kind if kind else "")
        rows.append([label, resp, "\n".join(attrs) if attrs else "–", "\n".join(ops) if ops else "–"])
    r.table(["Class", "Responsibility", "Main attributes", "Main operations"], rows,
            widths=[3.2, 4.6, 3.6, 4.6], size=7.5, caption=caption)


r = Report("Report4_Software Design Document.docx", "Report 4 – Software Design Document", CHANGES)
r.h1("II. Software Design Document")

# ================================================================== 1. System design
r.h2("1. System Design")
r.h3("1.1 System Architecture")
r.p(T.ARCH_INTRO)
r.figure("r4_architecture.png", "System architecture (component view)", width_cm=16.5)
if "r4_architecture.png" in STALE_FIGURES:
    r.p("Note: the figure was drawn on 12/11/2026. It names the host proxy nginx and shows the game client calling the API directly; since 13/11/2026 the client and the admin web reach the API through the Caddy HTTPS reverse proxy, as described in Table 2.")
r.table(["Component", "Technology", "Responsibility"], T.COMPONENTS, widths=[3.4, 3.6, 9.0], size=8.5, first_col_bold=True,
        caption="Architecture components")
r.p(T.AUTHORITY)
r.h4("Deployment")
r.p(T.DEPLOY_INTRO)
r.figure("r4_deployment.png", "Deployment diagram of the staging environment", width_cm=16.5)
if "r4_deployment.png" in STALE_FIGURES:
    r.p("Note: the figure was drawn on 12/11/2026. It still shows the pipeline as not passing, the manual release, the former proxy on port 80 and the clients calling port 8080; the paragraph above and Table 2 describe the deployment of 13/11/2026 (pipeline passing, Caddy reverse proxy with HTTPS on port 443 in front of the API and the admin web).")
r.table(["Node / container", "Image or artifact", "Port", "Notes"], T.NODES, widths=[3.6, 4.2, 2.2, 6.0], size=8.5,
        caption="Deployment nodes")

r.h3("1.2 Package Diagram")
r.p("The three code bases are organised in layers. Dependencies point only downwards: a package may use the packages below it but never the reverse. Figures %d to %d show the packages of the Web API, the Unity client and the admin web with the AI service." % (r.fig_no + 1, r.fig_no + 3))
r.figure("r4_pkg_backend.png", "Package diagram of the Web API solution", width_cm=16)
r.table(["No", "Package", "Description"], T.PKG_BACKEND, widths=[1.0, 4.4, 10.6], size=8.5, caption="Web API package descriptions")
r.figure("r4_pkg_unity.png", "Package diagram of the Unity client (assembly definitions)", width_cm=16)
r.table(["No", "Package", "Description"], T.PKG_UNITY, widths=[1.0, 4.4, 10.6], size=8.5, caption="Unity client package descriptions")
r.figure("r4_pkg_web_ai.png", "Package diagram of the admin web application and the AI coaching service", width_cm=16)
r.table(["No", "Package", "Description"], T.PKG_WEB_AI, widths=[1.0, 4.4, 10.6], size=8.5, caption="Admin web and AI service package descriptions")

r.h3("1.3 Technology Stack")
r.table(["Layer", "Technology and version", "Purpose"], T.STACK, widths=[3.0, 5.4, 7.6], size=8.5, first_col_bold=True, caption="Technology stack")
r.h3("1.4 Architecture Decisions")
r.p("Table %d records the main decisions that differ from the registered proposal, the alternatives considered and their consequences. Report 1 (LI-08) refers to this table." % (r.tbl_no + 1))
r.table(["#", "Decision", "Alternatives", "Reason", "Consequence"], T.DECISIONS, widths=[0.7, 3.4, 3.0, 4.6, 4.3], size=8, caption="Architecture decisions")

# ================================================================== 2. Database design
r.h2("2. Database Design")
r.p(T.DB_INTRO % (r.fig_no + 1, r.fig_no + len(T.ERD_FIGS)))
tables = schema_lib.parse()
for f, cap in T.ERD_FIGS:
    r.figure(f, cap, width_cm=16.5, max_height_cm=21)
r.p("**Table Descriptions**")
rows = []
for i, (name, t) in enumerate(tables.items(), 1):
    fk = ["%s → %s" % (", ".join(c), ref) for c, ref in t["fks"]]
    desc = T.TABLE_DESC.get(name, "")
    rows.append([("%02d" % i), name, desc + "\n- Primary keys: " + ", ".join(t["pk"]) + "\n- Foreign keys: " + ("; ".join(fk) if fk else "none")])
r.table(["No", "Table", "Description"], rows, widths=[1.0, 3.8, 11.2], size=8, caption="Table descriptions (51 tables)")
r.h3("2.1 Data Dictionary of the Core Tables")
r.p("The core tables of the account, match, training and moderation domains are listed column by column. Types are PostgreSQL types; * marks NOT NULL.")
for name in T.DICT_TABLES:
    t = tables[name]
    pk = {c.lower() for c in t["pk"]}
    fkc = {c.lower(): ref for cols, ref in t["fks"] for c in cols}
    drows = []
    for c in t["columns"]:
        key = []
        if c["name"].lower() in pk:
            key.append("PK")
        if c["name"].lower() in fkc:
            key.append("FK → " + fkc[c["name"].lower()])
        drows.append([c["name"], c["type"], "*" if c["notnull"] else "", ", ".join(key), T.COL_NOTES.get((name, c["name"]), "")])
    r.table(["Column", "Type", "Null", "Key", "Notes"], drows, widths=[3.6, 3.0, 1.0, 3.4, 5.0], size=7.5, caption="Data dictionary – %s" % name)
r.h3("2.2 Design Notes")
r.bullets(T.DB_NOTES)

# ================================================================== 3. Detailed design
r.h2("3. Detailed Design")
r.p("Each subsection gives the class diagram of one feature group, the specification of its classes and the sequence diagrams of its main functions. Client classes (Unity) and server classes (Web API) are drawn in the same diagram only when the diagram shows their interaction; the boundary is the HTTP API, marked as a dependency to the controller.")
for sec in T.SECTIONS:
    r.h3(sec["title"])
    r.p(sec["intro"])
    if sec.get("params"):
        r.table(["Parameter", "Value", "Source / meaning"], sec["params"], widths=[4.2, 3.0, 8.8], size=8.5, caption=sec["params_caption"])
    k = 1
    if sec.get("cls"):
        r.h4("%s.%d Class Diagram" % (sec["num"], k))
        k += 1
        r.figure(sec["cls"] + ".png", sec["cls_caption"], width_cm=16.5, max_height_cm=20)
        r.h4("%s.%d Class Specifications" % (sec["num"], k))
        k += 1
        class_spec(r, sec["cls"], "Class specifications – " + sec["short"])
    for dg, cap, short in sec.get("extra_cls", []):
        r.h4("%s.%d %s" % (sec["num"], k, cap.replace("Class diagram – ", "Class Diagram – ")))
        k += 1
        r.figure(dg + ".png", cap, width_cm=16.5, max_height_cm=20)
        class_spec(r, dg, "Class specifications – " + short)
    for seq, cap, text in sec.get("seqs", []):
        r.h4("%s.%d %s" % (sec["num"], k, cap))
        k += 1
        r.p(text)
        r.figure(seq + ".png", "Sequence diagram – " + cap, width_cm=16.5, max_height_cm=20)
    for extra_title, extra_rows, extra_caption, headers, widths in sec.get("tables", []):
        r.h4("%s.%d %s" % (sec["num"], k, extra_title))
        k += 1
        r.table(headers, extra_rows, widths=widths, size=8, caption=extra_caption)

# ---------------------------------------------------------------- API appendix
r.h3("3.9 API Design")
r.p(T.API_INTRO)
r.table(["Item", "Design"], T.API_RULES, widths=[3.6, 12.4], size=8.5, first_col_bold=True, caption="API conventions")
import json  # noqa: E402
eps = json.load(open(T.API_FILE, encoding="utf-8"))
rows = sorted([[e["controller"], e["method"], e["route"], e["action"], e["access"], e["usedBy"]] for e in eps],
              key=lambda x: (x[0], x[2], x[1]))
r.table(["Controller", "Method", "Route", "Action", "Access", "Used by"], rows, widths=[2.4, 1.3, 5.8, 2.7, 1.9, 1.9], size=7,
        caption="Web API endpoints (%d)" % len(rows))
r.p(T.API_LEGACY)

r.h3("3.10 Security Design")
r.table(["Concern", "Design"], T.SECURITY, widths=[3.4, 12.6], size=8.5, first_col_bold=True, caption="Security design")

# ================================================================== 4. Workflows and states
r.h2("4. Workflows and State Changes")
r.h3("4.1 Activity Diagrams")
r.p("Each review workflow is owned by one team member, as committed in the Review 2 form BM04 (W01 Learn and sync a drill – Trần Nguyễn Hiếu Thuận; W02 Play a bot and get a tip – Nguyễn Gia Huy; W03 Play online and save the result – Nguyễn Văn Thành Đạt; W04 Review a report and publish a lesson – Huỳnh Trung Kiên), and is drawn as a UML activity diagram with one swimlane per participant, decisions and exception branches. W04 has two independent parts and therefore two diagrams.")
for f, cap, text in T.ACTIVITIES:
    r.h4(cap)
    r.p(text)
    r.figure(f, cap, width_cm=16, max_height_cm=21)
r.h3("4.2 State Machine Diagrams")
r.p("The following state machines specify every entity or runtime object with a life cycle. Each transition is labelled event [guard] / action.")
for f, cap, text in T.STATES:
    r.h4(cap)
    r.p(text)
    r.figure(f, cap, width_cm=15, max_height_cm=18)

out = r.save("Report4_Software Design Document.docx", "Report 4 – Software Design Document")
print(out, "tables", r.tbl_no, "figures", r.fig_no)
