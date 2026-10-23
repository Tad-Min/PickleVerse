# Document validation and limits

Snapshot: `c32f2a4c0a546d0ed4f0df1b2c97e185ed831fb6`.

- 70/70 documentation integrity/layout checks passed; 0 failed.
- Word main section of Design Brief: 4 A4 pages; Appendix begins on page 5.
- All 184 product test cases remain NOT RUN; CI target is at least 92 actual executions after agreement.
- Current ERD coverage: 37 tables, 208 columns, 54 declared SQL foreign keys. Editable atlas: 43 diagrams.
- Main worktree status outside Doc/Review_2 matches the saved pre-authoring snapshot: True. Submodule internals are excluded because the existing submodule metadata prevents default status.
- Original input files rehashed: 7; unchanged: 7.
- The registration source copy is byte-identical; the standard BM02 prompt table is unchanged.
- Native Word read-only PDF export was used after the packaged renderer reported unavailable LibreOffice. Every final document was rendered to page images and checked; no game/backend tests were executed.
- Source files, runtime config, scenes, cloud services and Git tags were not edited. Production deployment, physical devices, staging/CI runs and reviewer decisions remain unverified.

These checks establish document consistency only. They do not establish that the game/backend meets the acceptance criteria.