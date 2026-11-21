"""Draws the conceptual ERDs of Report 3 in crow's-foot notation, the notation of the logical and physical ERDs of
Report 4, so that the three ERD levels use one notation (capstone guideline, section 8).

The subject areas are still written as Chen-style definitions in gen_r3.py (entities, relationships with their
attributes, and the 1/N/M cardinality of each side); chen_to_crowsfoot() turns one definition into a PlantUML
entity diagram: entities with their business attributes (key attributes above the line), and one crow's-foot line
per relationship, labelled with the relationship name and its attributes.
"""
import re
from collections import defaultdict


def chen_to_crowsfoot(body):
    ents, rels, edges, extra = {}, {}, [], []
    cur, kind = None, None
    for ln in body.strip("\n").splitlines():
        t = ln.strip()
        if not t or t.startswith("@startchen") or t.startswith("@endchen"):
            continue
        if t.startswith("skinparam"):
            extra.append(t)
            continue
        m = re.match(r"(entity|relationship)\s+(\w+)\s*\{\s*(\})?$", t)
        if m:
            kind, cur = m.group(1), m.group(2)
            (ents if kind == "entity" else rels)[cur] = []
            if m.group(3):
                cur = None
            continue
        if t == "}":
            cur = None
            continue
        if cur:
            (ents if kind == "entity" else rels)[cur].append(t)
            continue
        m = re.match(r"(\w+)\s+-(\w+)-\s+(\w+)$", t)
        if m:
            edges.append(m.groups())
    ends = defaultdict(list)
    for a, card, b in edges:
        if a in rels:
            ends[a].append((b, card))
        else:
            ends[b].append((a, card))
    out = ["hide circle", "hide empty members", "skinparam linetype ortho"] + extra
    for name, attrs in ents.items():
        keys = [a.replace("<<key>>", "").strip() for a in attrs if "<<key>>" in a]
        rest = [a for a in attrs if "<<key>>" not in a]
        out.append('entity "%s" as %s {' % (name, name))
        out += ["  * %s" % k for k in keys]
        if rest:
            out.append("  --")
            out += ["  %s" % a for a in rest]
        out.append("}")
    for rel, sides in ends.items():
        if len(sides) < 2:
            continue
        (e1, c1), others = sides[0], sides[1:]
        attrs = ", ".join(a.strip() for a in rels.get(rel, []))
        label = rel.replace("_", " ").lower() + (" (%s)" % attrs if attrs else "")
        for e2, c2 in others:
            left = "||" if c1 == "1" else "}o"
            right = "||" if c2 == "1" else "o{"
            out.append('%s %s--%s %s : "%s"' % (e1, left, right, e2, label))
    return "\n".join(out)
