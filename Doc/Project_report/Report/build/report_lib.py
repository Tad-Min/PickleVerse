"""Builds capstone report .docx files on top of the official SWP490 templates.

The template keeps its cover page, table of contents field and styles. Everything
after the Record of Changes table is replaced by the content written here.
"""
import copy
import os
import re

from docx import Document
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_BREAK
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Pt, RGBColor

HERE = os.path.dirname(os.path.abspath(__file__))
REPORT_DIR = os.path.abspath(os.path.join(HERE, ".."))
TEMPLATE_DIR = os.path.abspath(os.path.join(REPORT_DIR, "..", "ReportTemplete"))
DIAGRAMS = os.path.join(REPORT_DIR, "diagrams", "png")
SCREENS = os.path.join(REPORT_DIR, "screenshots")

PROJECT_NAME = "PickleVerse – Multiplayer Pickleball Skill-Training and Match Game with AI Coaching"
HEADER_FILL = "D9E2F3"
SUB_FILL = "F2F2F2"


def _set_cell_fill(cell, fill):
    tcPr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:val"), "clear")
    shd.set(qn("w:color"), "auto")
    shd.set(qn("w:fill"), fill)
    tcPr.append(shd)


def _set_cell_width(cell, cm):
    tcPr = cell._tc.get_or_add_tcPr()
    for old in tcPr.findall(qn("w:tcW")):
        tcPr.remove(old)
    w = OxmlElement("w:tcW")
    w.set(qn("w:w"), str(int(cm * 567)))
    w.set(qn("w:type"), "dxa")
    tcPr.append(w)


def _repeat_header(row):
    trPr = row._tr.get_or_add_trPr()
    el = OxmlElement("w:tblHeader")
    el.set(qn("w:val"), "true")
    trPr.append(el)


def _keep_row(row):
    trPr = row._tr.get_or_add_trPr()
    el = OxmlElement("w:cantSplit")
    el.set(qn("w:val"), "true")
    trPr.append(el)


INLINE = re.compile(r"(\*\*[^*]+\*\*|`[^`]+`)")


def add_runs(paragraph, text, size=None, bold=None):
    """Adds text with **bold** and `code` inline markup."""
    for part in INLINE.split(text):
        if not part:
            continue
        if part.startswith("**") and part.endswith("**"):
            r = paragraph.add_run(part[2:-2])
            r.bold = True
        elif part.startswith("`") and part.endswith("`"):
            r = paragraph.add_run(part[1:-1])
            r.font.name = "Consolas"
            r._element.rPr.rFonts.set(qn("w:eastAsia"), "Consolas")
        else:
            r = paragraph.add_run(part)
        if size:
            r.font.size = Pt(size)
        if bold is not None and not (part.startswith("**")):
            r.bold = bold
    return paragraph


class Report:
    def __init__(self, template_name, report_title, changes, prepare=True):
        self.doc = Document(os.path.join(TEMPLATE_DIR, template_name))
        self.fig_no = 0
        self.tbl_no = 0
        self.figures = []
        self.tables = []
        if prepare:
            self._prepare(report_title, changes)

    # ------------------------------------------------------------ template handling
    def _prepare(self, report_title, changes):
        body = self.doc.element.body
        paras = self.doc.paragraphs
        # Cover page
        for p in paras[:25]:
            if p.text.startswith("Report "):
                for r in p.runs[1:]:
                    r.text = ""
                p.runs[0].text = report_title
            if "Hanoi" in p.text:
                for r in p.runs[1:]:
                    r.text = ""
                p.runs[0].text = "– Ho Chi Minh City, November 2026 –"
                p.add_run().add_break(WD_BREAK.PAGE)  # table of contents starts on page 2
        anchor = None
        for i, p in enumerate(paras[:25]):
            if p.text.startswith("Report "):
                anchor = i
        cover_lines = [
            ("", 12, False),
            (PROJECT_NAME, 15, True),
            ("", 10, False),
            ("Project code: PKV        Group: GFA26SE111", 12, False),
            ("Supervisor: Mr. Lại Đức Hùng", 12, False),
            ("Team: Huỳnh Trung Kiên (Leader), Nguyễn Văn Thành Đạt,", 12, False),
            ("Nguyễn Gia Huy, Trần Nguyễn Hiếu Thuận", 12, False),
        ]
        for k, (text, size, bold) in enumerate(cover_lines):
            p = paras[anchor + 1 + k]
            for r in p.runs:
                r.text = ""
            p.alignment = WD_ALIGN_PARAGRAPH.CENTER
            if text:
                r = p.add_run(text)
                r.font.size = Pt(size)
                r.bold = bold
        # Record of Changes table
        table = self.doc.tables[0]
        for row in table.rows:
            for i, w in enumerate((2.4, 1.3, 3.4, 9.4)):
                if i < len(row.cells):
                    _set_cell_width(row.cells[i], w)
        rows = table.rows[1:]
        for k, row in enumerate(rows):
            vals = changes[k] if k < len(changes) else ("", "", "", "")
            for c, v in enumerate(vals):
                cell = row.cells[c]
                for p in cell.paragraphs[1:]:
                    p._element.getparent().remove(p._element)
                cell.paragraphs[0].text = ""
                if v:
                    add_runs(cell.paragraphs[0], v, size=10)
        # Remove all body content after the Record of Changes table and its legend.
        children = list(body.iterchildren())
        tbl_el = table._tbl
        idx = children.index(tbl_el)
        cut = idx + 2  # keep legend paragraph "*A - Added ..."
        sect = body.find(qn("w:sectPr"))
        for el in children[cut:]:
            if el is sect:
                continue
            body.remove(el)

    # ------------------------------------------------------------ content API
    def page_break(self):
        p = self.doc.add_paragraph()
        p.add_run().add_break(WD_BREAK.PAGE)

    def h1(self, text):
        return self.doc.add_heading(text, level=1)

    def h2(self, text):
        return self.doc.add_heading(text, level=2)

    def h3(self, text):
        return self.doc.add_heading(text, level=3)

    def h4(self, text):
        return self.doc.add_heading(text, level=4)

    def p(self, text, bold=False, italic=False, align=None, size=None, after=6):
        para = self.doc.add_paragraph()
        add_runs(para, text, size=size, bold=bold if bold else None)
        if italic:
            for r in para.runs:
                r.italic = True
        if align == "center":
            para.alignment = WD_ALIGN_PARAGRAPH.CENTER
        elif align is None:
            para.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
        para.paragraph_format.space_after = Pt(after)
        return para

    def label(self, name, text):
        """A 'Name: text' paragraph with a bold name."""
        para = self.doc.add_paragraph()
        r = para.add_run(name + ": ")
        r.bold = True
        add_runs(para, text)
        para.paragraph_format.space_after = Pt(3)
        para.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
        return para

    def _list_para(self, marker, text):
        # The templates have no bullet style; a hanging indent with a marker keeps the layout stable.
        para = self.doc.add_paragraph(style="List Paragraph")
        para.add_run(marker + "\t")
        add_runs(para, text)
        pf = para.paragraph_format
        pf.left_indent = Cm(0.9)
        pf.first_line_indent = Cm(-0.5)
        pf.tab_stops.add_tab_stop(Cm(0.9))
        pf.space_before = Pt(0)
        pf.space_after = Pt(2)
        para.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
        return para

    def bullets(self, items):
        for it in items:
            self._list_para("•", it)

    def numbered(self, items):
        for k, it in enumerate(items, 1):
            self._list_para("%d." % k, it)

    def caption_table(self, text):
        self.tbl_no += 1
        para = self.doc.add_paragraph()
        r = para.add_run("Table %d. %s" % (self.tbl_no, text))
        r.bold = True
        r.italic = True
        r.font.size = Pt(10)
        para.alignment = WD_ALIGN_PARAGRAPH.CENTER
        para.paragraph_format.keep_with_next = True
        para.paragraph_format.space_before = Pt(6)
        para.paragraph_format.space_after = Pt(3)
        self.tables.append(text)
        return self.tbl_no

    def table(self, headers, rows, widths=None, caption=None, size=9.5, first_col_bold=False, subheader_rows=()):
        if caption:
            self.caption_table(caption)
        t = self.doc.add_table(rows=1, cols=len(headers))
        try:
            t.style = self.doc.styles["Table Grid"]
        except KeyError:
            pass
        t.alignment = WD_TABLE_ALIGNMENT.CENTER
        hdr = t.rows[0]
        _repeat_header(hdr)
        for i, h in enumerate(headers):
            cell = hdr.cells[i]
            cell.paragraphs[0].text = ""
            add_runs(cell.paragraphs[0], h, size=size, bold=True)
            _set_cell_fill(cell, HEADER_FILL)
        for ri, row in enumerate(rows):
            r = t.add_row()
            _keep_row(r)
            for i, v in enumerate(row):
                cell = r.cells[i]
                cell.paragraphs[0].text = ""
                lines = str(v).split("\n")
                for li, line in enumerate(lines):
                    para = cell.paragraphs[0] if li == 0 else cell.add_paragraph()
                    add_runs(para, line, size=size, bold=True if (first_col_bold and i == 0) else None)
                    para.paragraph_format.space_after = Pt(0)
                if ri in subheader_rows:
                    _set_cell_fill(cell, SUB_FILL)
        if widths:
            # Fixed layout so that Word honours the declared column widths instead of auto-fitting to content.
            t.autofit = False
            tblPr = t._tbl.tblPr
            for old in tblPr.findall(qn("w:tblLayout")):
                tblPr.remove(old)
            layout = OxmlElement("w:tblLayout")
            layout.set(qn("w:type"), "fixed")
            tblPr.append(layout)
            for i, gc in enumerate(t._tbl.tblGrid.findall(qn("w:gridCol"))):
                if i < len(widths):
                    gc.set(qn("w:w"), str(int(widths[i] * 567)))
            for row in t.rows:
                for i, w in enumerate(widths):
                    _set_cell_width(row.cells[i], w)
        sp = self.doc.add_paragraph()
        sp.paragraph_format.space_after = Pt(4)
        return t

    def _new_section(self, landscape):
        from docx.enum.section import WD_SECTION, WD_ORIENT
        body = self.doc.element.body
        carried = []
        if landscape:
            # Keep the headings that introduce the figure on the same (landscape) page.
            while True:
                last = body[-2] if len(body) > 1 else None
                if last is None or last.tag != qn("w:p"):
                    break
                style = last.find(qn("w:pPr") + "/" + qn("w:pStyle"))
                if style is None or not style.get(qn("w:val"), "").lower().startswith("heading"):
                    break
                body.remove(last)
                carried.insert(0, last)
        sec = self.doc.add_section(WD_SECTION.NEW_PAGE)
        for el in carried:
            body.find(qn("w:sectPr")).addprevious(el)
        is_land = sec.orientation == WD_ORIENT.LANDSCAPE
        if landscape != is_land:
            w, h = sec.page_width, sec.page_height
            sec.orientation = WD_ORIENT.LANDSCAPE if landscape else WD_ORIENT.PORTRAIT
            sec.page_width, sec.page_height = h, w
        return sec

    def figure(self, path, caption, width_cm=16.0, max_height_cm=21.0, landscape=None):
        if not os.path.isabs(path):
            for base in (DIAGRAMS, SCREENS):
                cand = os.path.join(base, path)
                if os.path.exists(cand):
                    path = cand
                    break
        from PIL import Image
        with Image.open(path) as im:
            w, h = im.size
        width = width_cm
        if h / w * width > max_height_cm:
            width = max_height_cm * w / h
        # Wide diagrams (rendered at 150 dpi) that would print below half size go on a landscape page.
        scale = width / (w / 150.0 * 2.54)
        if landscape is None:
            landscape = path.startswith(DIAGRAMS) and w / h >= 1.4 and scale < 0.5
        if landscape:
            self._new_section(True)
            width = 24.5
            if h / w * width > 14.5:
                width = 14.5 * w / h
        para = self.doc.add_paragraph()
        para.alignment = WD_ALIGN_PARAGRAPH.CENTER
        para.paragraph_format.keep_with_next = True
        para.add_run().add_picture(path, width=Cm(width))
        self.fig_no += 1
        cap = self.doc.add_paragraph()
        r = cap.add_run("Figure %d. %s" % (self.fig_no, caption))
        r.italic = True
        r.bold = True
        r.font.size = Pt(10)
        cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
        cap.paragraph_format.space_after = Pt(8)
        self.figures.append(caption)
        if landscape:
            self._new_section(False)
        return self.fig_no

    # ------------------------------------------------------------ header / footer
    @staticmethod
    def _field_runs(paragraph, instr, size=9):
        def fld(kind):
            r = paragraph.add_run()
            r.font.size = Pt(size)
            el = OxmlElement("w:fldChar")
            el.set(qn("w:fldCharType"), kind)
            r._element.append(el)
            return r
        fld("begin")
        r = paragraph.add_run()
        r.font.size = Pt(size)
        it = OxmlElement("w:instrText")
        it.set(qn("xml:space"), "preserve")
        it.text = " %s " % instr
        r._element.append(it)
        fld("separate")
        r = paragraph.add_run("1")
        r.font.size = Pt(size)
        fld("end")

    def header_footer(self, short_title):
        """Running header with the report name and a 'Page x of y' footer; the cover page has none."""
        def reset(part):
            # Remove the template's own header/footer content (fields, text boxes, tables) before adding ours.
            el = part._element
            for child in list(el):
                el.remove(child)
            el.append(OxmlElement("w:p"))
            return part.paragraphs[0]

        for idx, section in enumerate(self.doc.sections):
            section.different_first_page_header_footer = (idx == 0)
            section.header.is_linked_to_previous = False
            section.footer.is_linked_to_previous = False
            hp = reset(section.header)
            run = hp.add_run("PickleVerse (PKV) – GFA26SE111        %s" % short_title)
            run.font.size = Pt(9)
            run.italic = True
            hp.alignment = WD_ALIGN_PARAGRAPH.RIGHT
            fp = reset(section.footer)
            fp.alignment = WD_ALIGN_PARAGRAPH.CENTER
            r = fp.add_run("Page ")
            r.font.size = Pt(9)
            self._field_runs(fp, "PAGE")
            r = fp.add_run(" of ")
            r.font.size = Pt(9)
            self._field_runs(fp, "NUMPAGES")

    def references(self, items):
        for k, it in enumerate(items, 1):
            para = self.doc.add_paragraph()
            add_runs(para, "[%d] %s" % (k, it))
            para.paragraph_format.left_indent = Cm(0.8)
            para.paragraph_format.first_line_indent = Cm(-0.8)
            para.paragraph_format.space_after = Pt(3)

    def _drop_double_page_breaks(self):
        """Removes an empty page-break paragraph that directly follows another page break (it printed a blank page)."""
        def only_break(el):
            if el.tag != qn("w:p"):
                return False
            text = "".join(t.text or "" for t in el.iter(qn("w:t"))).strip()
            return not text and any(b.get(qn("w:type")) == "page" for b in el.iter(qn("w:br")))

        def ends_with_break(el):
            if el.tag != qn("w:p"):
                return False
            runs = list(el.iter(qn("w:r")))
            return bool(runs) and any(b.get(qn("w:type")) == "page" for b in runs[-1].iter(qn("w:br")))

        children = list(self.doc.element.body.iterchildren())
        for prev, el in zip(children, children[1:]):
            if only_break(el) and (only_break(prev) or ends_with_break(prev)):
                el.getparent().remove(el)

    def save(self, filename, short_title=None):
        self._drop_double_page_breaks()
        if short_title:
            self.header_footer(short_title)
        core = self.doc.core_properties
        core.author = "GFA26SE111"
        core.last_modified_by = "Nguyễn Gia Huy"
        import datetime
        now = datetime.datetime(2026, 11, 10, 22, 0, 0)
        core.created = now
        core.modified = now
        core.title = filename.replace(".docx", "")
        core.subject = PROJECT_NAME
        out = os.path.join(REPORT_DIR, filename)
        self.doc.save(out)
        return out
