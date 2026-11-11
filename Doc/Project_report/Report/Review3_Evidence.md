# PickleVerse – Test Evidence Register

Version 1.0, 11/11/2026. This register lists every piece of evidence used by Report 5. Results counted in the Report 5 statistics come only from EV-01 to EV-04, whose files are stored in this folder. EV-05 lists results that the team recorded on 06/11/2026; their logs are kept outside the repository, so they are not counted until the logs are attached.

| ID | Run | Date | Build / commit | Result | Location |
|---|---|---|---|---|---|
| EV-01 | Unity EditMode suite through the Unity CLI (Pipeline `test run`) | 11/11/2026 | main @ 7f8ee32 | 515 tests: 515 passed, 0 failed (6.19 s) | `evidence/editmode_results_20261111.json`, `evidence/editmode_list.json` |
| EV-02 | Playtest on the staging server: offline training, signed-in menu, bot match, AI coaching, ranked 1v1 with bot fallback | 10/11/2026 | client main @ 82f6a7a; staging images of 06/11/2026 | See Report5_Test Report.xlsx | `screenshots/01–28`, `evidence/playtest_log_20261110.md` |
| EV-03 | UI check of register, verification, password reset and tutorial screens | 11/11/2026 | main @ 7f8ee32 | TC-F01-03, TC-F02-01 passed | `screenshots/29–34` |
| EV-04 | Match history of the team test accounts | 09/11/2026 | staging | Two ranked 1v1 attempts interrupted (D-09) | `screenshots/12_history.png` |

## EV-05 Team-recorded IntegrationChecks of 06/11/2026 (not counted)

Each run used a disposable PostgreSQL database created from `src/Database.sql`. The log files are in the local `artifacts/` folder of the machine that ran them, which Git ignores. To be counted in Report 5 the logs must be attached together with the commit hash.

| Area | Log file | SHA-256 | Recorded summary |
|---|---|---|---|
| Account, email and session | `artifacts/functional-completion-20261106/check-auth.log` | `544f4044090c55d791ca49874a33dce21f1ca4c7f596b4535d1b7361dbab6ba9` | 132 checks; real PostgreSQL; captured email adapter; temporary database removed. |
| Training catalog, stars and rewards | `artifacts/functional-completion-20261106/check-content.log` | `80a30384b999720dd174580533d80c96f1b42cb899e4609eded5e374ac196e74` | 21 CMS/version/reward checks; disposable database removed. |
| Offline sync and onboarding | `artifacts/functional-completion-20261106/check-training-sync.log` | `0f907b9a84567ca348c240ac4f2375f584403ba1c4e1f8cfc0fd1e15f975edb4` | 18 offline-sync/onboarding checks; disposable database removed. |
| Queue, room and ready check | `artifacts/functional-completion-20261106/check-online-multiplayer.log` | `330ee67442dfe0fed5d9ec32d8ce4fcb0d34bdf873169837fc6586cc1fe5ab9a` | 34 online game checks; disposable PostgreSQL database removed. |
| Dedicated lease, result and stop | `artifacts/functional-completion-20261106/check-dedicated.log` | `d2618e692183f967aa9c38a966511278ed241361de2f1590691919279647c6ab` | 26 dedicated backend/lease checks; disposable database removed. |
| Coaching job and data gates | `artifacts/functional-completion-20261106/check-hybrid.log` | `f84be7688094b6b4bf840e989d13b60442eb26ba0101f7c7616623e02485c3a8` | 36 hybrid checks; disposable database removed. |
| Moderation and restricted sessions | `artifacts/functional-completion-20261106/check-moderation.log` | `8811e7797e3f62d9adad2edf15f4a3973e28445e256b0e1b0fb457e0acf593fd` | 19 moderation/auth/appeal checks; disposable database removed. |
| Player game and social rules | `artifacts/functional-completion-20261106/check-online-game.log` | `bc933b8070c47feb52735c4863b956f6bf5488e8c1885f4a16fe4cfed7ee1f1d` | 84 online game checks; disposable PostgreSQL database removed. |
| Admin analytics | `artifacts/functional-completion-20261106/check-analytics.log` | `3bb435511a93fd62be312a175c23d996858a7521a2ace8f2698e456df876fd8a` | 17 analytics/queue/daily-activity checks; disposable database removed. |
| Expert review export | `artifacts/functional-completion-20261106/check-expert-review.log` | `ab7dd44fd8c105e4a7018ffae356c870a3807047901902a638b474e2d3e69225` | 11 expert review and dataset contract checks; disposable database removed. |
| Admin progress reset | `artifacts/functional-completion-20261106/check-progress-reset.log` | `ba88e41dd8f53eaabb426e305fe01664f062e195623f69b950ea7523ed1bedb1` | 21 admin-reset checks; disposable database removed. |
| Queue reservation | `artifacts/functional-completion-20261106/check-redis.log` | `551f52321f251ca8a58d536e74626e22bc9ef1e2942c94826e742a4e6fa811a5` | 28 Redis checks; disposable SQL database and Redis keys removed. |

## Not yet available

- Python pytest log of the AI service (22 tests) with commit hash.
- GitHub Actions validate-job result linked to test case IDs.
- Measurements for NFR-01 to NFR-07 (Report 5 §3.3).
