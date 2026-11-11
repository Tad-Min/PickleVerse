"""Parses src/Database.sql into tables, columns, primary keys and foreign keys."""
import os
import re

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", ".."))
SQL_PATH = os.path.join(REPO, "src", "Database.sql")


def _split(body):
    parts, depth, cur, inq = [], 0, "", False
    for ch in body:
        if ch == "'":
            inq = not inq
        if not inq and ch == "(":
            depth += 1
        if not inq and ch == ")":
            depth -= 1
        if ch == "," and depth == 0 and not inq:
            parts.append(cur.strip())
            cur = ""
        else:
            cur += ch
    if cur.strip():
        parts.append(cur.strip())
    return parts


def parse(path=SQL_PATH):
    sql = open(path, encoding="utf-8").read()
    sql = re.sub(r"--[^\n]*", "", sql)
    tables = {}
    for m in re.finditer(r"CREATE TABLE (?:IF NOT EXISTS )?(\w+)\s*\((.*?)\);", sql, re.S | re.I):
        name, body = m.group(1), m.group(2)
        cols, pk, fks = [], [], []
        for p in _split(body):
            p1 = re.sub(r"\s+", " ", p).strip()
            if re.match(r"(CONSTRAINT \w+ )?PRIMARY KEY", p1, re.I):
                pk = [x.strip() for x in re.findall(r"PRIMARY KEY\s*\(([^)]*)\)", p1, re.I)[0].split(",")]
            elif re.match(r"(CONSTRAINT \w+ )?FOREIGN KEY", p1, re.I):
                mm = re.search(r"FOREIGN KEY\s*\(([^)]*)\)\s*REFERENCES\s+(\w+)", p1, re.I)
                fks.append(([x.strip() for x in mm.group(1).split(",")], mm.group(2)))
            elif re.match(r"(CONSTRAINT|UNIQUE|CHECK)", p1, re.I):
                continue
            else:
                toks = p1.split(" ")
                rest = " ".join(toks[1:])
                mt = re.match(r"(\w+(?:\s+PRECISION|\s+VARYING)?(?:\([^)]*\))?)", rest)
                typ = (mt.group(1) if mt else toks[1]).upper()
                notnull = bool(re.search(r"NOT NULL|PRIMARY KEY", p1, re.I))
                cols.append({"name": toks[0], "type": typ, "notnull": notnull})
                if re.search(r"PRIMARY KEY", p1, re.I):
                    pk = [toks[0]]
                mm = re.search(r"REFERENCES\s+(\w+)", p1, re.I)
                if mm:
                    fks.append(([toks[0]], mm.group(1)))
        tables[name] = {"columns": cols, "pk": pk, "fks": fks}
    for t, c, r in re.findall(r"ALTER TABLE (\w+)\s+ADD (?:CONSTRAINT \w+ )?FOREIGN KEY\s*\(([^)]*)\)\s*REFERENCES\s+(\w+)", sql, re.I):
        if t in tables:
            tables[t]["fks"].append(([x.strip() for x in c.split(",")], r))
    canon = {n.lower(): n for n in tables}
    for t in tables.values():
        t["fks"] = [(cols, canon.get(ref.lower(), ref)) for cols, ref in t["fks"]]
    return tables


def erd(tables, focus, refs, title=None):
    """Crow's-foot logical ERD. focus: tables with full columns; refs: tables shown with keys only."""
    lines = ["hide circle", "hide empty members", "skinparam linetype ortho", "left to right direction"]
    shown = list(focus) + [r for r in refs if r not in focus]
    for name in shown:
        t = tables[name]
        pkset = {c.lower() for c in t["pk"]}
        fkcols = {c.lower() for cols, _ in t["fks"] for c in cols}
        lines.append('entity "%s" as %s {' % (name, name))
        for c in t["columns"]:
            if c["name"].lower() in pkset:
                tag = " <<PK, FK>>" if c["name"].lower() in fkcols else " <<PK>>"
                lines.append("  * %s : %s%s" % (c["name"], c["type"], tag))
        lines.append("  --")
        if name in focus:
            for c in t["columns"]:
                if c["name"].lower() in pkset:
                    continue
                tag = " <<FK>>" if c["name"].lower() in fkcols else ""
                star = "* " if c["notnull"] else ""
                lines.append("  %s%s : %s%s" % (star, c["name"], c["type"], tag))
        lines.append("}")
    seen = set()
    for name in shown:
        t = tables[name]
        for cols, ref in t["fks"]:
            if ref not in shown or (name not in focus and ref not in focus):
                continue
            key = (name, tuple(cols), ref)
            if key in seen:
                continue
            seen.add(key)
            colmap = {c["name"].lower(): c for c in t["columns"]}
            optional = any(not colmap.get(c.lower(), {"notnull": True})["notnull"] for c in cols)
            one_to_one = {c.lower() for c in cols} == {c.lower() for c in t["pk"]}
            left = "|o" if optional else "||"
            right = "o|" if one_to_one else "o{"
            label = ", ".join(cols)
            lines.append('%s %s--%s %s : "%s"' % (ref, left, right, name, label))
    return "\n".join(lines)
