# Four-form validation 27 October 2026

Baseline: `087769927cdd38a5e4907ceb3cbabc60fc2e0baa`. Scope: the four root BM01–BM04 Word forms and supporting documentation in Review_2 only.

The final validation recorded **70 checks passed, zero failed**. This includes document consistency, package preservation, page geometry, proposed test allocations, source integrity and the recorded visual inspection gate. These are document checks, not product acceptance tests.

| Form | Final pages | Layout result |
|---|---:|---|
| BM01 | 8 | Main sections 0–8 occupy exactly four A4 pages; appendices start on page 5. |
| BM02 | 5 | Original standard prompt retained verbatim; all 14 findings retain their six raw output fields. |
| BM03 | 2 | Six proposed changes; reviewer decisions and notes remain blank. |
| BM04 | 21 | Six workflows, four proposed member slots, summaries, fixes and unsigned confirmations; no trailing blank page. |

All 36 final pages were visually checked, directly or through pixel identity with already inspected pages. Final Word files retain the source template section geometry, relationships, footers and other opaque package parts; changes are restricted to document content, styles, settings and core properties. Native Microsoft Word exported read-only PDF previews after the bundled renderer could not find LibreOffice. The previews remain internal QA files.

Cross-checks confirmed 18 feature groups, six NFR groups, five design decisions, six workflows, four member slots, 192 unique proposed cases and a 96-case CI target. Workflow and member allocations agree. All ten High Planned findings map to BM01 and all twelve Planned findings map to BM04. No finding is marked Fixed. All test execution values remain NOT RUN for this refresh. Proposed classifier macro F1 ≥0.80 is labelled as a target for reviewer agreement, not a measured score.

SHA-256 comparison found **all 900 inventoried game/backend/frontend source and configuration files unchanged**. Proposal and existing supporting Word sources were unchanged. Git HEAD was unchanged; all Git-visible edits stayed within `Doc/Review_2`. No game build, backend/database mutation, cloud write, commit or tag creation was performed.

The public HTTP readiness endpoint returned 200 ready during a read-only check. HTTPS readiness was not verified on the attempted path. This does not establish release connectivity, full administrative integration, load capacity, uptime, encryption, device performance or formal acceptance. Existing SRS/SDD, diagrams and older archives remain the 23 October baseline and need alignment before formal submission. Names, student IDs, actual ownership, review date, approved registration version and real signatures still require the team/reviewers.

Detailed hashes, page records and validation results are retained locally under `_work/refresh20261027/final_validation.json`.
