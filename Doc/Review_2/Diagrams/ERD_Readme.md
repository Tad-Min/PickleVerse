# ERD documentation sources

- `ERD-DB-A00`: full current27-table SQL/EF overview, keys only for zoomable poster.
- `ERD-DB-A01..A08`: all current attributes split by domain; external parent references are keys-only.
- `ERD-DB-T00`: alternative71-table candidate overview (NOT current EF / deployment proof).
- `ERD-DB-T01..T18`: candidate full attributes, split into domain views.
- `Data_Dictionary_current.csv` / `Data_Dictionary_candidate.csv` in the Review_2 root contain all fields, definitions, nullability, keys and source lines. UTF-8 BOM for Excel.
- `ERD_Coverage_Manifest.json` verifies table coverage and lists diagram source/spec paths.

Mermaid `.mmd` files in `Source` use `erDiagram`. Geometry graphs in `_work/diagram_specs` are renderer inputs for SVG/draw.io. Diagrams show SQL declarations, not a deployed database. Each child has exactly1 or0..1 parent according to nullable FK; each parent may have0..many children unless the child FK tuple is itself unique. The label reads **parents per child → children per parent**.

All fields are covered in domain diagrams/CSV. A standalone `UK` marker denotes a single-column unique constraint; composite uniqueness remains in the dictionary table constraints, not incorrectly attached individually to each column. `?` means NULL allowed. Candidate expressions, checks, defaults and triggers are detailed in source, not certified by running SQL.

Current SQL uses unquoted CamelCase names that PostgreSQL folds to lowercase; current EF `ToTable` uses lowercase names. Cardinalities are structural; do not infer a mandatory profile, full2v2 team or verified reward ownership from FK existence alone.
