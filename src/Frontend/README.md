# PickleVerse Admin Web

React 18, Vite, TypeScript, Tailwind CSS, TanStack React Query, and Recharts admin SPA.

## Feature-Based Architecture

```text
src/
├── api/                    # Axios client and shared admin API contracts
├── components/layout/      # App shell, sidebar, route guard
├── context/                # Admin authentication session
├── features/
│   ├── auth/                # Admin login
│   ├── dashboard/           # Operational overview
│   ├── users/               # Account lookup, role, ban, Elo reset
│   ├── content/             # Lesson and drill creation
│   ├── matches/             # Recent and active match monitoring
│   ├── moderation/          # Abuse report review
│   └── analytics/           # Technique error heatmap
├── types/                   # Shared API DTOs
└── App.tsx                  # Routes and query provider
```

Feature pages are grouped with their feature and exported through a feature `index.ts`. Shared API client, session, layout and DTO code stays outside feature folders to avoid duplicated infrastructure.

## Development

```sh
npm install
npm run dev
```

### UI demo without a backend

Copy `.env.example` to `.env.local`, keep `VITE_USE_MOCK_DATA=true`, then restart Vite. Sign in with `admin@pickleverse.local` / `Admin123!`. The seeded users, matches, reports, and analytics are frontend fixtures; actions update in-memory demo data for the current page session only. No request is sent to the backend.

The API base URL defaults to `http://localhost:8080`; override it with `VITE_API_BASE_URL` when needed. Admin routes require an authenticated account whose API role is `Admin`.

The current backend exposes creation endpoints for lessons and drills, but does not yet expose edit/publish controls, disconnect actions, appeals, retention, popular-lesson analytics, or matchmaking-health analytics. Those workflows require corresponding API endpoints before this web client can persist them.
