# PickleVerse Review 3 evidence

Source snapshot for the reports is `df5f671` on 6 November 2026. The checks below were already recorded in the local development evidence. The logs do not each record a full commit and image digest. The team must record the source, build, data and result for each review run.

The Acceptance sheet keeps Review 2 test IDs as a plan. A local check below can support one rule. Full staging, real devices, a tagged CI run and expert model acceptance have their own checks.

The team reported that the Review 2 meeting was postponed. BM01 and BM04 give proposed targets. Reviewer decisions and the Review 3 checklist are still pending.

## EV-01 Account, email and session

Local PostgreSQL with a captured email adapter.

Local file `artifacts/functional-completion-20261106/check-auth.log`. SHA-256 `544f4044090c55d791ca49874a33dce21f1ca4c7f596b4535d1b7361dbab6ba9`.

```text
PASS: registration accepts the four-field contract without a player name
PASS: registration validation rejects mismatched confirmation
SUCCESS: 132 checks; real PostgreSQL; captured email adapter; temporary database removed.
```

## EV-02 Training catalog, stars and rewards

Local service and PostgreSQL. Game screen acceptance is open.

Local file `artifacts/functional-completion-20261106/check-content.log`. SHA-256 `80a30384b999720dd174580533d80c96f1b42cb899e4609eded5e374ac196e74`.

```text
PASS: catalog contains 15 seeded playable lessons
PASS: published CMS lesson enters player catalog
PASS: stale CMS edit is rejected
PASS: ten completed lessons award THOT
PASS: level 25 grants GEMSTONE
SUCCESS: 21 CMS/version/reward checks; disposable database removed.
```

## EV-03 Offline sync and onboarding

Local backend and database. Device restart and reconnect acceptance are open.

Local file `artifacts/functional-completion-20261106/check-training-sync.log`. SHA-256 `0f907b9a84567ca348c240ac4f2375f584403ba1c4e1f8cfc0fd1e15f975edb4`.

```text
PASS: offline sync recalculates stars and ignores client reward values
PASS: lost-response retry returns duplicate without repeating rewards
PASS: old accepted receipt cannot revive progress after reset
PASS: experienced onboarding does not change competitive Elo
SUCCESS: 18 offline-sync/onboarding checks; disposable database removed.
```

## EV-04 Queue, room and ready check

Local backend checks. Full device flow is open.

Local file `artifacts/functional-completion-20261106/check-online-multiplayer.log`. SHA-256 `330ee67442dfe0fed5d9ec32d8ce4fcb0d34bdf873169837fc6586cc1fe5ab9a`.

```text
PASS: court cannot start before every account accepts
PASS: consent cannot be reused in another Photon room
PASS: concurrent start requests create one playing match
SUCCESS: 34 online game checks; disposable PostgreSQL database removed.
```

## EV-05 Dedicated lease, result and stop

Local lease and backend checks. Full 1v1, 2v2, reconnect and stop acceptance are open.

Local file `artifacts/functional-completion-20261106/check-dedicated.log`. SHA-256 `d2618e692183f967aa9c38a966511278ed241361de2f1590691919279647c6ab`.

```text
PASS: first match reserves the single server slot
PASS: custom match reports server busy
PASS: ranked match remains waiting while the slot is occupied
PASS: server completion grants the singles result exactly once
PASS: lost server ends interrupted without inferring a winner
PASS: admin interruption creates no Elo ledger
PASS: late normal result cannot resurrect the interrupted match
SUCCESS: 26 dedicated backend/lease checks; disposable database removed.
```

## EV-06 Coaching job and data gates

Local backend checks. Model quality and Dink calibration need real labeled data.

Local file `artifacts/functional-completion-20261106/check-hybrid.log`. SHA-256 `f84be7688094b6b4bf840e989d13b60442eb26ba0101f7c7616623e02485c3a8`.

```text
PASS: partial telemetry does not create a job
PASS: duplicate telemetry part is idempotent
PASS: retry stops at ten attempts and keeps failed job
SUCCESS: 36 hybrid checks; disposable database removed.
```

## EV-07 Moderation and restricted sessions

Local PostgreSQL with role and session checks. Browser route test is separate.

Local file `artifacts/functional-completion-20261106/check-moderation.log`. SHA-256 `8811e7797e3f62d9adad2edf15f4a3973e28445e256b0e1b0fb457e0acf593fd`.

```text
PASS: player cannot issue sanctions
PASS: restricted session is invalid for the game scope
PASS: scope middleware blocks all unmarked endpoints even with an admin role claim
PASS: report decision, sanction and durable stop command commit together
SUCCESS: 19 moderation/auth/appeal checks; disposable database removed.
```

## EV-09 Player game and social rules

Local PostgreSQL. Individual checks support only the named outcome.

Local file `artifacts/functional-completion-20261106/check-online-game.log`. SHA-256 `bc933b8070c47feb52735c4863b956f6bf5488e8c1885f4a16fe4cfed7ee1f1d`.

```text
PASS: purchase rejects insufficient server balance
PASS: server rejects a locked lesson even when the client requests it directly
PASS: server calculates original rewards and stars, ignoring client values: serve
PASS: unblocking does not recreate friendships
SUCCESS: 84 online game checks; disposable PostgreSQL database removed.
```

## EV-10 Admin analytics

Local analytics service and PostgreSQL.

Local file `artifacts/functional-completion-20261106/check-analytics.log`. SHA-256 `3bb435511a93fd62be312a175c23d996858a7521a2ace8f2698e456df876fd8a`.

```text
PASS: DAU counts distinct human players and excludes admins and bots
PASS: analytics counts all human Player accounts beyond 1000
PASS: popular lessons count starts, terminal sessions and passed attempts separately
SUCCESS: 17 analytics/queue/daily-activity checks; disposable database removed.
```

## EV-11 Expert review export

Local data contract. The seed is simulated.

Local file `artifacts/functional-completion-20261106/check-expert-review.log`. SHA-256 `ab7dd44fd8c105e4a7018ffae356c870a3807047901902a638b474e2d3e69225`.

```text
PASS: development seed includes ten separate simulated sessions per tier
PASS: dataset checksum identifies the exact downloaded bytes
SUCCESS: 11 expert review and dataset contract checks; disposable database removed.
```

## EV-12 Admin progress reset

Local PostgreSQL and service checks.

Local file `artifacts/functional-completion-20261106/check-progress-reset.log`. SHA-256 `ba88e41dd8f53eaabb426e305fe01664f062e195623f69b950ea7523ed1bedb1`.

```text
PASS: ordinary player cannot execute admin reset
PASS: lost-response retry returns one reset receipt
PASS: late persistence failure rolls back the whole reset
SUCCESS: 21 admin-reset checks; disposable database removed.
```

## EV-13 Queue reservation

Local PostgreSQL and Redis.

Local file `artifacts/functional-completion-20261106/check-redis.log`. SHA-256 `551f52321f251ca8a58d536e74626e22bc9ef1e2942c94826e742a4e6fa811a5`.

```text
PASS: a single player waits in the Redis queue with a server deadline
PASS: concurrent polling produces one shared match/session and consumes each queue seat once
SUCCESS: 28 Redis checks; disposable SQL database and Redis keys removed.
```

## EV-08 Admin web access note

The 6 November progress note says a local HTTP check denied a Player and let an Admin reach the label-review area. This is a reported HTTP observation. Test the Admin and Player browser sessions and the admin-users route as separate cases. Check the named publish action with a Player token.

[Functional_Completion_20261106.md](https://github.com/Tad-Min/PickleVerse/blob/df5f671d4880de2a99af81a6e58277655d9c4305/Doc/Functional_Completion_20261106.md)

## EV-14 Test totals and CI

The progress note reports 513 passing Unity EditMode checks, 22 passing Python checks and an admin build. It reports zero registered Unity PlayMode checks. These totals count runner checks. The 116 Review 2 cases and 60 CI candidates are a different count and need a per-ID result.

The API contract folder contains more than one run. The file named check-api-contract-current.log ends with an error. The progress note reports a separate successful contract run. Use the run ID and a clean log when reporting the next review.

[Functional_Completion_20261106.md](https://github.com/Tad-Min/PickleVerse/blob/df5f671d4880de2a99af81a6e58277655d9c4305/Doc/Functional_Completion_20261106.md) | [README.md](https://github.com/Tad-Min/PickleVerse/blob/df5f671d4880de2a99af81a6e58277655d9c4305/src/Testing/README.md) | [dockerhub.yml](https://github.com/Tad-Min/PickleVerse/blob/df5f671d4880de2a99af81a6e58277655d9c4305/.github/workflows/dockerhub.yml)

## EV-15 Release checks to run

- Record the staging host, API and Dedicated image digests, game build, seed and test account roles.
- Run full 1v1 and 2v2, reconnect, stop and result checks on the same release.
- Verify iOS 14 support, RTT, frame time, HTTPS, storage encryption, uptime and load.
- Use real sessions with expert labels for model acceptance. The synthetic seed is for the data flow only.

[Proposal_Progress_Review_20261105.md](https://github.com/Tad-Min/PickleVerse/blob/df5f671d4880de2a99af81a6e58277655d9c4305/Doc/Proposal/Proposal_Progress_Review_20261105.md) | [AI_Expert_Review.md](https://github.com/Tad-Min/PickleVerse/blob/df5f671d4880de2a99af81a6e58277655d9c4305/Doc/AI_Expert_Review.md) | [deploy.sh](https://github.com/Tad-Min/PickleVerse/blob/df5f671d4880de2a99af81a6e58277655d9c4305/deploy/linux/deploy.sh)
