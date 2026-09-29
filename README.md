# Filipino VA — remote-work job marketplace (Phase 1 MVP)

Umbraco 17 (headless CMS) + ASP.NET Core Marketplace API + Vue 3 frontend, built
from `Prompt.txt` and `AGENTS.md`. Phase 1 covers accounts, worker profiles, job
posting, the public job board, applications/shortlisting, the CMS pages and admin
moderation. **No payments, messaging or notifications yet.**

```
job-board/
├─ backend/
│  ├─ src/JobBoard.Domain/          entities + enums
│  ├─ src/JobBoard.Infrastructure/ EF Core (PostgreSQL), search, files, e-mail
│  ├─ src/JobBoard.Api/            REST API (JWT + rotating refresh tokens)
│  ├─ src/JobBoard.Cms/            Umbraco 17, Content Delivery API v2
│  └─ tests/                       125 unit + 35 integration tests
├─ frontend/                       Vue 3 + TS + Vite (prerendered for SEO)
└─ docs/                           step1-proposal.md · umbraco-content-api.md
```

## Prerequisites

| Tool | Version used |
| --- | --- |
| .NET SDK | 10.0.401 |
| Node / pnpm | v22.17.1 / 12.3.4 |
| PostgreSQL | 18 on `127.0.0.1:5432` (db `jobboard`, role `jobboard`) |

## Configuration

```bash
copy .env.example .env      # then set Jwt__AccessTokenSecret
```

`.env` is gitignored and read by the Marketplace API. The Umbraco CMS keeps its
own dev credentials in `backend/src/JobBoard.Cms/appsettings.Local.json`
(also gitignored) — the unattended install needs `UnattendedUserPassword` there on
first boot.

## Run the three servers

```bash
# 1) Marketplace API — http://localhost:5201  (launch profile = Development → seeds demo data)
cd backend
dotnet run --project src/JobBoard.Api

# 2) Umbraco CMS — https://localhost:7123  (unattended install + content seed on first boot)
dotnet run --project src/JobBoard.Cms

# 3) Frontend — http://localhost:5173
cd ../frontend
pnpm install
pnpm dev
```

Vite proxies `/api` → `localhost:5201` and `/umbraco` → `localhost:7123`, so the
browser bundle contains no API keys and needs no CORS.

### Demo logins (seeded in Development only)

| Role | E-mail | Password |
| --- | --- | --- |
| Admin | `admin@jobboard.local` | `Admin123!` |
| Employer | `employer@jobboard.local` | `Employer123!` |
| Worker | `worker@jobboard.local` | `Worker123!` |

There is no local SMTP: verification/reset e-mails are written to
`backend/src/JobBoard.Api/logs/outbox.log` and exposed on the dev-only
`GET /api/auth/outbox` endpoint (the *Verify e-mail* page can pull the code from it).

Umbraco backoffice: `https://localhost:7123/umbraco` (`admin@jobboard.local`).

### Filling the board with real jobs

The Marketplace API can pull live remote jobs from two free, key-less feeds
(Remotive, Jobicy) and publish them as normal listings:

```powershell
# admin-only; ?limit= is capped at 200 per feed
Invoke-RestMethod "http://localhost:5201/api/admin/import/remote-jobs?limit=100" -Method Post `
  -Headers @{ Authorization = "Bearer <admin access token>" }
```

- **Idempotent** — each posting is stored with `jobs.source` + `jobs.source_id` behind
  a unique index, so re-running only adds what is new.
- Imported jobs publish immediately (they are curated public feeds) and always end
  with an attribution line linking to the original posting.
- Category, job type, seniority, skills and pay are inferred from the feed data
  (`ExternalJobMapper`); pay stays "Negotiable" when the feed publishes none.
- Each external company gets its own employer profile on a locked system account
  (`company-<slug>@imports.jobboard.local`, never e-mail verified → cannot log in).

To run it automatically on boot in Development, set `Imports__RemoteJobsOnStartup=true`
(limit via `Imports__LimitPerSource`).

## Commands

```bash
# backend
dotnet build JobBoard.slnx
dotnet test  JobBoard.slnx     # stop a running API first — it locks bin/

# frontend
pnpm type-check
pnpm lint
pnpm test
pnpm build            # SPA bundle
pnpm build:prerender  # + static HTML per public route (SEO)
```

`pnpm build:prerender` renders every public route in Node (13 routes: home, jobs,
each job, blog + posts, FAQ, landing, login/register) and needs the API and CMS to
be running; missing services are skipped with a warning.

`pnpm test` also runs `src/test/live-pages.test.ts`, which mounts the real pages
against the live API + CMS and asserts they render real content. It skips itself
when the servers are not up.

## Regenerating the API types

```bash
# from backend/ with the API running
cd ..\frontend
pnpm dlx openapi-typescript http://localhost:5201/openapi/v1.json -o src/api/generated/marketplace.ts
```

`src/api/types.ts` derives every DTO from that file — do not hand-write shapes.

## Documentation

- `docs/step1-proposal.md` — approved plan, ERD and tooling decisions
- `docs/umbraco-content-api.md` — verified Umbraco 17 Delivery API contract and the
  seeded content model (read this before changing `src/api/umbraco.ts`)
