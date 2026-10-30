# Project instructions

## Development database policy

Confirmed by the user on 2026-10-31:

- This project is currently in development. When a schema change needs a database reset, the user authorizes dropping and recreating the project's development database without asking again.
- Use `src/Database.sql` as the single source for schema and initial seed data. Do not add migration scripts for these development schema changes.
- Identify the exact project development instance and database before resetting it. The current local instance is Docker container `pickleverse_postgres`, database `pickleverse`, normally exposed on localhost:15432. Recheck the active configuration before acting.
- This permission does not apply to production, shared databases, or databases belonging to other projects.
- Keep the running backend compatible with the recreated schema and clear only this project's stale cache when needed.

Additional project handover context: `Remember/PickleVerse_Codex_Handover.md`.
