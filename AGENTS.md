# Project instructions

## Development database policy

Confirmed by the user on 2026-10-31:

- This project is currently in development. When a schema change needs a database reset, the user authorizes dropping and recreating the project's development database without asking again.
- Use `src/Database.sql` as the single source for schema and initial seed data. Do not add migration scripts for these development schema changes.
- Identify the exact project development instance and database before resetting it. The current local instance is Docker container `pickleverse_postgres`, database `pickleverse`, normally exposed on localhost:15432. Recheck the active configuration before acting.
- This permission does not apply to production, shared databases, or databases belonging to other projects.
- Keep the running backend compatible with the recreated schema and clear only this project's stale cache when needed.

Additional project handover context: `Remember/PickleVerse_Codex_Handover.md`.

## Backend data access

- The user requires Npgsql Entity Framework Core for the backend (confirmed 2026-10-31).
- Use `PickleVerseContext`, entity mappings/Fluent API and EF Core queries/updates for domain persistence. Keep the model consistent with `src/Database.sql`.
- PostgreSQL-specific operations such as advisory locks, sequence values and upserts may use parameterized SQL through the EF Core connection/transaction.
- In GameMatchPlayers, use joins/calculated aggregates for data already available in profiles, cosmetics, MatchScore or ratingledger. Do not add duplicate descriptive fields or totals. Keep facts specific to the participant in that match, including the fixed Elo input required before a rating ledger exists.
- In GameMatches, derive team game-win totals from completed MatchScore rows; do not store TeamAScore/TeamBScore copies. Keep Mode explicit because custom Doubles currently permits an underfilled roster, and preserve the match's creation-time rating policy even before a ledger exists.

## Backend architecture and API conventions

Confirmed by the user on 2026-10-31, with `D:/Repository/SmashHub/BESmashClub` as a reference:

- Keep the existing layered architecture: `Repositories -> Entities`; `Services -> Entities, Repositories`; `API -> Entities, Services`. Do not introduce Domain/Application/Infrastructure projects as a replacement.
- Put DTOs and common API response/exception types in `Entities`. Put service contracts in `Services/Interfaces` and business implementations in `Services/Implementations`.
- Services access domain repositories through `IUnitOfWork`; repository implementations own EF Core queries, database locks and persistence. Keep multi-step match/reward operations in one shared transaction.
- Controllers depend on service interfaces. JSON responses use `ApiResponse<T>` / `ApiResponse`; central API handling maps service exceptions to HTTP statuses. Binary/file downloads retain their contents and media type.
- Bind Redis configuration and register its connection in `API/Program.cs`. Keep Redis settings as a plain configuration model in `Entities`; cache/repository implementations receive the settings and connection through dependency injection rather than reading configuration themselves.
- Synchronize response changes with Unity `ApiClient` and the frontend Axios client. Preserve error messages/codes and avoid exposing internal exception details in HTTP 500 responses.
- Production backend projects compile their own models and rules; do not link source under Unity `Assets`. IntegrationChecks may compile the pure C# Unity client to verify the actual wire contract.
