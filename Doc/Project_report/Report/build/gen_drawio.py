"""Converts the PlantUML use case diagrams and Chen conceptual ERDs (exported as SVG) into one editable draw.io file.

Usage: python gen_drawio.py <svg folder>
The SVG files are produced with: java -jar plantuml.jar -tsvg diagrams/src/r3_uc_*.puml diagrams/src/r3_cerd_*.puml
"""
import os
import re
import sys
import xml.etree.ElementTree as ET
from xml.sax.saxutils import escape

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "PickleVerse_UseCase_ConceptualERD.drawio"))
NS = {"s": "http://www.w3.org/2000/svg"}
K = 1 / 1.5625  # PlantUML output is rendered at 150 dpi

PAGES = [
    ("UC Overview", "r3_uc_overview"), ("UC P1 Account", "r3_uc_p1_account"), ("UC P2 Learning", "r3_uc_p2_learning"),
    ("UC P3 Online", "r3_uc_p3_online"), ("UC P4a Social", "r3_uc_p4a_social"), ("UC P4b Economy", "r3_uc_p4b_economy"),
    ("UC P5 Restriction", "r3_uc_p5_restricted"), ("UC P6 Admin Users", "r3_uc_p6_admin_users"), ("UC P7 Admin Content", "r3_uc_p7_admin_content"),
    ("UC P8 Admin Monitoring", "r3_uc_p8_admin_monitor"),
    ("CERD Account", "r3_cerd_account"), ("CERD Training", "r3_cerd_training"), ("CERD Match", "r3_cerd_match"),
    ("CERD Economy", "r3_cerd_economy"), ("CERD Moderation", "r3_cerd_moderation"), ("CERD Coaching", "r3_cerd_coaching"),
]


def nums(text):
    return [float(x) for x in re.findall(r"-?\d+(?:\.\d+)?", text)]


def bbox_of(el):
    xs, ys = [], []
    for sh in el.iter():
        tag = sh.tag.split("}")[-1]
        a = sh.attrib
        if tag == "rect":
            x, y, w, h = float(a["x"]), float(a["y"]), float(a["width"]), float(a["height"])
            xs += [x, x + w]; ys += [y, y + h]
        elif tag == "ellipse":
            cx, cy, rx, ry = float(a["cx"]), float(a["cy"]), float(a["rx"]), float(a["ry"])
            xs += [cx - rx, cx + rx]; ys += [cy - ry, cy + ry]
        elif tag == "polygon":
            p = nums(a["points"]); xs += p[0::2]; ys += p[1::2]
        elif tag == "path":
            p = nums(a.get("d", "")); xs += p[0::2]; ys += p[1::2]
        elif tag == "line":
            xs += [float(a["x1"]), float(a["x2"])]; ys += [float(a["y1"]), float(a["y2"])]
    return (min(xs), min(ys), max(xs), max(ys)) if xs else None


def texts(el):
    return [(t.text or "").strip() for t in el.iter("{%s}text" % NS["s"]) if (t.text or "").strip()]


class Page:
    def __init__(self, name):
        self.name, self.cells, self.n = name, [], 2

    def vertex(self, label, style, box):
        self.n += 1
        x0, y0, x1, y1 = [v * K for v in box]
        self.cells.append('<mxCell id="v%d" value="%s" style="%s" vertex="1" parent="1"><mxGeometry x="%.1f" y="%.1f" width="%.1f" height="%.1f" as="geometry"/></mxCell>'
                          % (self.n, escape(label, {'"': "&quot;"}), style, x0, y0, max(x1 - x0, 10), max(y1 - y0, 10)))
        return "v%d" % self.n

    def edge(self, src, dst, style, label=""):
        self.n += 1
        self.cells.append('<mxCell id="e%d" value="%s" style="%s" edge="1" parent="1" source="%s" target="%s"><mxGeometry relative="1" as="geometry"/></mxCell>'
                          % (self.n, escape(label, {'"': "&quot;"}), style, src, dst))

    def xml(self):
        return ('<diagram name="%s"><mxGraphModel grid="1" gridSize="10" page="1" pageWidth="1169" pageHeight="827"><root><mxCell id="0"/><mxCell id="1" parent="0"/>%s</root></mxGraphModel></diagram>'
                % (escape(self.name), "".join(self.cells)))


UC_STYLE = "ellipse;whiteSpace=wrap;html=1;fillColor=#F5F9FC;strokeColor=#333333;fontSize=12;"
ACTOR_STYLE = "shape=umlActor;verticalLabelPosition=bottom;verticalAlign=top;html=1;outlineConnect=0;fontSize=12;"
BOX_STYLE = "rounded=1;arcSize=2;whiteSpace=wrap;html=1;fillColor=none;verticalAlign=top;fontStyle=1;fontSize=13;strokeColor=#777777;"
ASSOC = "endArrow=none;html=1;rounded=0;"
DEP = "endArrow=open;endSize=10;dashed=1;html=1;rounded=0;"
GEN = "endArrow=block;endFill=0;endSize=12;html=1;rounded=0;"


def uc_page(name, root):
    page = Page(name)
    ids = {}
    clusters = [g for g in root.iter("{%s}g" % NS["s"]) if g.get("class") == "cluster"]
    for g in clusters:
        rect = g.find("s:rect", NS)
        box = (float(rect.get("x")), float(rect.get("y")), float(rect.get("x")) + float(rect.get("width")), float(rect.get("y")) + float(rect.get("height")))
        page.vertex(" ".join(texts(g)), BOX_STYLE, box)
    for g in root.iter("{%s}g" % NS["s"]):
        if g.get("class") != "entity":
            continue
        label = " ".join(texts(g))
        has_path = g.find("s:path", NS) is not None
        ell = g.findall("s:ellipse", NS)
        if has_path:  # actor: head ellipse + body paths; text below
            shapes = [e for e in g if e.tag.split("}")[-1] in ("ellipse", "path")]
            xs, ys = [], []
            for sh in shapes:
                b = bbox_of(sh)
                if b:
                    xs += [b[0], b[2]]; ys += [b[1], b[3]]
            ids[g.get("id")] = page.vertex(label, ACTOR_STYLE, (min(xs), min(ys), max(xs), max(ys)))
        elif ell:
            ids[g.get("id")] = page.vertex(label, UC_STYLE, bbox_of(g))
        else:
            ids[g.get("id")] = page.vertex(label, "rounded=0;whiteSpace=wrap;html=1;", bbox_of(g))
    for g in root.iter("{%s}g" % NS["s"]):
        if g.get("class") != "link":
            continue
        a, b = ids.get(g.get("data-entity-1")), ids.get(g.get("data-entity-2"))
        if not a or not b:
            continue
        lab = " ".join(texts(g)).replace("<<", "«").replace(">>", "»")
        kind = g.get("data-link-type", "")
        if "extension" in kind or "generalization" in kind:
            page.edge(a, b, GEN)
        elif "dependency" in kind or lab:
            page.edge(a, b, DEP, lab)
        else:
            page.edge(a, b, ASSOC)
    return page


def chen_page(name, root):
    page = Page(name)
    shapes = []
    for g in root.iter("{%s}g" % NS["s"]):
        if g.get("class") or g.find("s:text", NS) is None:
            continue
        kids = [k.tag.split("}")[-1] for k in g]
        if "rect" in kids or "polygon" in kids or "ellipse" in kids:
            t = g.find("s:text", NS)
            label = " ".join(texts(g))
            box = bbox_of(g)
            if "polygon" in kids:
                style = "rhombus;whiteSpace=wrap;html=1;fillColor=#F1F1F1;strokeColor=#181818;fontSize=11;"
            elif "ellipse" in kids:
                style = "ellipse;whiteSpace=wrap;html=1;fillColor=#F1F1F1;strokeColor=#181818;fontSize=11;" + ("fontStyle=4;" if t.get("text-decoration") == "underline" else "")
            else:
                style = "rounded=0;whiteSpace=wrap;html=1;fillColor=#F1F1F1;strokeColor=#181818;fontSize=12;fontStyle=1;"
            shapes.append((page.vertex(label, style, box), box))

    def nearest(x, y):
        best, bd = None, 1e18
        for vid, (x0, y0, x1, y1) in shapes:
            dx = max(x0 - x, 0, x - x1)
            dy = max(y0 - y, 0, y - y1)
            dist = dx * dx + dy * dy
            if dist < bd:
                best, bd = vid, dist
        return best

    for g in root.iter("{%s}g" % NS["s"]):
        if g.get("class") != "link":
            continue
        p = g.find("s:path", NS)
        pts = nums(p.get("d", ""))
        a, b = nearest(pts[0], pts[1]), nearest(pts[-2], pts[-1])
        if a and b and a != b:
            page.edge(a, b, ASSOC + "fontSize=11;", " ".join(texts(g)))
    return page


def main(svg_dir):
    pages = []
    for name, base in PAGES:
        root = ET.parse(os.path.join(svg_dir, base + ".svg")).getroot()
        pages.append((uc_page if base.startswith("r3_uc") else chen_page)(name, root))
    xml = '<mxfile host="app.diagrams.net" agent="PickleVerse report build" version="24.0.0">%s</mxfile>' % "".join(p.xml() for p in pages)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(xml)
    return OUT, [(p.name, p.n) for p in pages]


if __name__ == "__main__":
    print(main(sys.argv[1]))
