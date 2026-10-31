# PickleVerse Review 2 working pack

Current four-form refresh: **087769927cdd38a5e4907ceb3cbabc60fc2e0baa**, 27 October 2026. The canonical BM01–BM04 drafts are the four `SWP490_R2_BM0*.docx` files directly in this directory. They record the implemented C# ASP.NET Core .NET 10 backend, the public readiness check, proposal traceability and unresolved acceptance work. They are prepared drafts pending named ownership and reviewer confirmation.

Read [the current completion checklist](Review2_Completion_20261027.md) before submission. `SelfReview_20261027_Raw.json`, `Feature_Traceability_20261027.csv` and `Test_Case_Register_20261027.csv` support these four forms. The 192 test cases and 96 CI executions are **proposed targets**, not achieved results.

The **Prepared/** reports, diagrams, combined PDFs and archives remain the **23 October baseline** at c32f2a4c0a546d0ed4f0df1b2c97e185ed831fb6. They have not been rewritten in this focused four-form refresh and need alignment before the formal review-2 freeze. Start with [the earlier backend pull analysis](Pull_Changes_c32f2a4.md) for that baseline. `PKV_DRAFT` is a temporary filename marker, not a confirmed official project code.

- Report3_SRS: proposal requirements, 36 detailed user goals, 18 feature groups and acceptance criteria.
- Report4_SDD: actual versus target architecture, 37-table schema, state/guard models, workflow activities, contracts and operations.
- Report5_TestPlan: earlier 184-case plan; the current register extends it to 192 with invitation/presence guard cases. Current execution evidence is still required.
- Prepared DesignBrief, SelfReview, RecordOfChanges, Commitment_Draft: older drafts superseded by the four root BM01–BM04 Word files for current findings and commitments. BM02 requires a frozen-tag rerun; BM04 remains unsigned.
- Registration_Source: byte-identical copy of the supplied proposal, not a new approved registration.
- Preparation_Guide_VI: internal Vietnamese checklist; submission drafts are English.

Editable diagrams for the 23 October baseline: `Diagrams/Current_c32f2a4/PickleVerse_Review2_Current.drawio`. Mermaid/PlantUML sources, SVGs and PNGs are adjacent. Extend the invitation/presence state and deployment records identified by the current BM02. Historical diagram sources and `_work` evidence at d44e8c0 are superseded for current implementation conclusions. The separate 71-table candidate is not labelled deployed.

Data and traceability: `Data_Dictionary_current.csv`, `Data_Dictionary_candidate.csv`, `API_Endpoint_Inventory.csv`, `Feature_Traceability.csv`, `Test_Case_Register.csv`.

Open formal gates: approved registration/code, named ownership, reviewer decisions, actual staging/CI/device results and local `review-2` tag. Report 1, Report 2 and official Project Tracking were not supplied and still require team completion. No signatures or approvals have been invented. No code, game, Git or cloud resource was modified.

Read [Validation_Report.md](Validation_Report.md) for the earlier pack's checks and limits, and [the current validation note](Validation_20261027.md) for the four-form refresh. The older combined Registration PDF includes the unchanged proposal plus the earlier proposed BM03; it is not the refreshed submission. Diagram_Atlas is A3 for readable printing. Seed_Manifest.csv is a proposed reproducible dataset specification, not data already created.
