# Step 1 — Discovery & Proposal (approval gate)

Remote-work job marketplace (OnlineJobs.ph style): **Umbraco 17 (headless, Content Delivery API) + ASP.NET Core Marketplace API + Vue 3 frontend.**

---

## 1. Tool inventory (as required by Prompt.txt §TOOLING PRIORITY)

I enumerated every tool available to me in this session (`search()` over the whole catalog, by namespace and by keyword).

| MCP server / tool | Status | Notes |
| --- | --- | --- |
| **Umbraco Developer MCP** (`@umbraco-cms/mcp-dev`) | ❌ **not configured** | No `mcp` block exists anywhere (no `opencode.json` in the workspace, no global config). Exact snippet in §2. |
| **Playwright MCP** | ❌ not configured | My built-in **desktop browser tools** are present (45 `browser.*` tools) as the fallback for page verification. |
| **GitHub MCP** | ❌ not configured | Fallback: local `git` (v2.50.1 is installed; repo already initialised). |
| **Vue / Vite / Vue DevTools MCP** | ❌ not configured | Fallback: official docs via web fetch (`vuejs.org`). |
| Other tools I *do* have | ✅ | `shell`, `read`/`write`/`edit`, `glob`/`grep`, `webfetch`, `websearch`, `subagent`, `browser.*` (45), `opencode.*` (3). |

**Nothing official was silently skipped** — §2 is exactly what must be added.

### Docs read (fallback path 3 in Agent.md)
- Content Delivery API (enablement, config, all v2 endpoints, query params, limitations) — docs.umbraco.com
- API versioning & OpenAPI — docs.umbraco.com
- CMS Developer MCP (setup, env vars, version matrix) — docs.umbraco.com *(link you provided)*
- OpenCode MCP server config schema — opencode.ai/docs/mcp-servers

> ⚠️ **Correction to Agent.md:** `/umbraco/swagger/delivery/swagger.json` is the legacy (≤ v13) path. In Umbraco 16/17 the OpenAPI documents and Swagger UI are served from **`/umbraco/openapi`** (documents like `/umbraco/openapi/delivery.json`, default route template `openapi/{documentName}.json`; disabled in Production environment by default). I will confirm the exact document URL against the running instance's spec at build time and generate TS types from it — not hand-write them.

---

## 2. Add this to `opencode.json` (workspace root)

```jsonc
{
  "$schema": "https://opencode.ai/config.json",
  "mcp": {
    "umbraco-mcp": {
      "type": "local",
      "command": ["npx", "-y", "@umbraco-cms/mcp-dev@lts-17"],
      "enabled": true,
      "environment": {
        "NODE_TLS_REJECT_UNAUTHORIZED": "0",
        "UMBRACO_CLIENT_ID": "<api-user-client-id>",
        "UMBRACO_CLIENT_SECRET": "<api-user-client-secret>",
        "UMBRACO_BASE_URL": "https://localhost:7123",
        "UMBRACO_INCLUDE_TOOL_COLLECTIONS": "document,media,document-type,data-type,template"
      },
      "timeout": 15000
    }
  }
}
```

- `@lts-17` matches **Umbraco 17 (LTS)**; `@latest` = 18.x and would fail the first tool call (official version matrix).
- The client id/secret come from an **API user** created in the backoffice (Users → API users) — admin permissions if I am to create document types. **Dev instance only, never production.**
- Requires Node ≥ 22 → we have **v22.17.1** ✅.
- Optional later: `"playwright": { "type": "local", "command": ["npx","-y","@playwright/mcp@latest"] }` and a GitHub MCP. My built-in browser covers verification for now, so I did not add them to avoid bloating context.

**The MCP can only connect once Umbraco runs** (needs the API user + a live URL), so step 2 uses the documented fallback: Delivery API config/queries from official docs → live OpenAPI spec → manual code. MCP takes over for backoffice/document-type work as soon as you paste the snippet and create the API user.

---

## 3. Environment findings (verified, not assumed)

| Check | Result |
| --- | --- |
| .NET SDK | **8.0.410 only** ❌ — Umbraco templates 17.7.0 scaffold `net10.0` → **restore fails (NETSDK1045)**. A .NET 10 SDK install is required. |
| Umbraco templates | `Umbraco.Templates 17.7.0` installed (18.2.0 available on NuGet) |
| Node / pnpm / npm | v22.17.1 / pnpm 12.3.4 / npm 11.5.1 ✅ |
| PostgreSQL | **PostgreSQL 18 service running on localhost:5432** ✅ (no docker, no psql CLI) |
| git | 2.50.1 ✅, repo initialised, only `Prompt.txt` + `Agent.md.txt` present |
| Browser tools | ❌ **no desktop browser connected** — needs the experimental browser setting in the OpenCode desktop app |
| MCP config | none present (see §2) |

---

## 4. Proposed ERD (Marketplace DB — PostgreSQL via EF Core)

```
┌────────────────────────── ASP.NET Identity ──────────────────────────┐
│ users                                                                 │
│   id uuid PK   email  normalized_email  email_confirmed bool          │
│   password_hash  security_stamp  concurrency_stamp                    │
│   phone_number (never returned to anonymous callers)                  │
│   user_name  lockout_end  access_failed_count                         │
│   account_status ENUM(active, suspended, banned, deleted)             │
│   privacy_consent_at timestamptz  privacy_policy_version text         │
│   marketing_consent bool   created_at  updated_at                     │
│        │ 1                                                           │
│        ├──* user_roles ──── roles (name: admin|employer|worker)       │
│        ├──* user_tokens (purpose: email_confirmation|password_reset,  │
│        │                  value_hash, expires_at, used_at)            │
│        ├──* refresh_tokens (token_hash UQ, expires_at, revoked_at,    │
│        │                    user_agent, ip)                           │
│        ├──1 employer_profiles                                         │
│        ├──1 worker_profiles                                           │
│        └──* files                                                     │
└───────────────────────────────────────────────────────────────────────┘

employer_profiles
  id uuid PK   user_id FK→users UNIQUE   company_name  slug UQ
  website  description  country  logo_file_id FK→files NULL
  is_verified bool  created_at  updated_at

worker_profiles
  id uuid PK   user_id FK→users UNIQUE   slug UQ
  headline  summary  country('PH')  city  timezone
  years_of_experience smallint
  rate_min numeric(12,2)  rate_max numeric(12,2)
  currency char(3) ('PHP'|'USD')  rate_period ENUM(hour, day, month)
  availability ENUM(full_time, part_time, freelance, project_based)
  is_public bool          ◄── public/private toggle
  resume_file_id FK→files NULL
  created_at  updated_at
        │1
        ├──* worker_skills ── skills (id, name UQ, normalized lower)
        │      (level 1..5, years_of_experience)
        └──* work_experiences (title, company, location, start_date,
                               end_date NULL, is_current, description, sort_order)

skills  id  name UQ  category_hint

categories
  id uuid PK  name  slug UQ  parent_id FK→categories NULL (self)
  short_description  icon  sort_order  is_active bool
  umbraco_content_key uuid NULL  ◄── joins to Umbraco SEO landing/FAQ page

jobs
  id uuid PK
  employer_profile_id FK→employer_profiles
  category_id FK→categories
  title  slug UQ  description (markdown)
  job_type ENUM(full_time, part_time, contract, freelance, internship)
  region ENUM(worldwide, philippines_only, custom)
  pay_type ENUM(hourly, monthly, fixed_price)
  pay_min numeric(12,2)  pay_max numeric(12,2)  currency char(3)
  experience_level ENUM(junior, mid, senior, lead)
  hours_per_week smallint NULL
  status ENUM(draft, pending, published, flagged, rejected, closed, expired)
  status_reason text NULL          ◄── admin moderation (approve/flag)
  published_at  closes_at  view_count int
  created_at  updated_at
  search_vector tsvector GENERATED ALWAYS AS
      (to_tsvector('simple', title || ' ' || description)) STORED
      + GIN index                       ◄── full-text search, behind ISearchService
        │1
        ├──* job_skills (skill_name denormalized, for filter chips)
        └──1* applications

job_skills   id  job_id FK  skill_name

applications
  id uuid PK   job_id FK→jobs   worker_profile_id FK→worker_profiles
  UQ (job_id, worker_profile_id)              ◄── one application per worker/job
  cover_letter text   resume_file_id FK→files (snapshot at apply time)
  status ENUM(submitted, shortlisted, rejected, withdrawn)
  rejection_reason text NULL
  applied_at  updated_at

files                        ◄── blob storage metadata (resume uploads)
  id uuid PK   owner_user_id FK→users
  purpose ENUM(resume, logo, attachment)
  original_name  storage_key  content_type
  size_bytes  sha256  created_at  deleted_at NULL
  ── validation: ≤ 5 MB, type ∈ {pdf, doc, docx, rtf}

audit_events                 ◄── admin moderation trail
  id bigserial PK  actor_user_id FK→users NULL
  action text  entity_type  entity_id  details jsonb  ip  created_at
```

**Rules baked into the model**
- Roles `admin | employer | worker` → server-side authorization on every mutating endpoint.
- `account_status = banned` blocks login/token refresh; `status = flagged/rejected` handles job moderation.
- PH Data Privacy Act (in spirit): consent columns + policy version on `users`; `worker_profiles` contact fields are stripped for unauthenticated responses; account export/deletion endpoints (soft-delete → anonymize) derive from `users`/`files`.
- Rate limiting (ASP.NET Core `RateLimiter`) + FluentValidation on all public endpoints.
- **Deliberate future extension points, not built now:** `jobs` carries no payment columns; `applications.status` is an extensible enum; planned-but-unbuilt tables: `subscription_plans`, `employer_subscriptions`, `conversations`/`messages`, `notifications` (push/email), `saved_jobs`.

**Umbraco owns only editorial content** (no duplication of marketplace data): `categories` is the filtering source of truth in Postgres; Umbraco holds the SEO/description page joined by `slug`/`umbraco_content_key`.

---

## 5. Umbraco content model (Delivery API only on the frontend)

| Doc type | Route | Properties |
| --- | --- | --- |
| `home` | `/` | heroTitle, heroSubtitle, heroCta, stats (repeating), featuredCategories (multi content picker), SEO |
| `blogList` | `/blog` | title, intro, pageSize |
| `blogPost` | `/blog/{slug}` | publishDate, authorName, excerpt, coverImage (media), body (RTE), tags, SEO |
| `landingPage` | `/{slug}` | heroTitle, heroSubtitle, bodyBlocks (flexible: richText / stats / testimonial / faqEmbed / cta), targetCategorySlug, SEO |
| `faqPage` | `/help` | title, intro, groups → block list (groupTitle + Q/A items), SEO |
| `textPage` | `/pages/{slug}` | title, body — Privacy Policy, Terms, About *(needed for signup consent)* |
| `siteSettings` | non-routable | siteName, logo, social links, footer |
| `seoFields` | composition | metaTitle, metaDescription, ogImage, noIndex |

Frontend reads: `GET /umbraco/delivery/api/v2/content/item/{route}` and
`GET /umbraco/delivery/api/v2/content?filter=contentType:blogPost&sort=createDate:desc&take=10&skip=0`
(+ `?fetch=children:{id}` for blog/FAQ lists, `fields=`/`expand=` to avoid over-fetching).

---

## 6. Solution / folder structure

```
C:\job-board
├─ Prompt.txt  Agent.md.txt  AGENTS.md (copy of Agent.md.txt, so tools read it)
├─ opencode.json                 # MCP block (§2), gitignored? no — secrets via env instead
├─ .gitignore  .env.example
├─ docs/            step1-proposal.md (this) · erd.md · tooling-report.md
├─ backend/
│  ├─ JobBoard.sln                # one solution, two apps (per ARCHITECTURE)
│  ├─ src/
│  │  ├─ JobBoard.Cms/            # Umbraco 17, net10.0, Delivery API enabled, SQLite/PG
│  │  ├─ JobBoard.Api/            # marketplace API: auth, profiles, jobs, applications, admin
│  │  ├─ JobBoard.Domain/         # entities, enums, ISearchService, IFileStorage, IEmailSender
│  │  └─ JobBoard.Infrastructure/ # EF Core + Npgsql, local blob store, tsquery search, SMTP email
│  └─ tests/ JobBoard.UnitTests · JobBoard.IntegrationTests
├─ frontend/                      # Vue 3 + TS + Vite, <script setup>
│  ├─ src/ api/ · components/ · composables/ · pages/ · router/ · stores/ · types/
│  └─ scripts/gen-umbraco-types.mjs   # openapi-typescript ← /umbraco/openapi/delivery.json
└─ scripts/                       # start-dev.ps1 (Umbraco + API + Vite)
```

**Ports (proposal):** Umbraco `https://localhost:7123` · Marketplace API `http://localhost:5210` · Vite `http://localhost:5173` with dev proxy `/umbraco/* → 7123` and `/api/* → 5210` (no CORS, no keys in the browser bundle).

**Frontend stack:** Vue 3 + TypeScript + Vite · Vue Router · Pinia · TanStack Query · Tailwind · Vitest + Vue Test Utils · ESLint + Prettier · `pnpm`.

---

## 7. Open decisions (answer at the gate)

1. **Approve** this ERD + structure? (prompt: stop here and wait)
2. **.NET 10 SDK** — install via `winget install Microsoft.DotNet.SDK.10`? (hard requirement for Umbraco 17; nothing else can proceed without it)
3. **Umbraco 17 LTS** (prompt allows 16|17; templates 17.7.0 already installed, MCP `@lts-17`) — confirm, or say "18 / latest".
4. **SEO strategy** for public job/profile pages with Vue 3 + Vite: (a) prerender those routes at build time, (b) SSR via Nuxt 3 (still Vue 3 + Vite), (c) SPA-only + `prerender.io`-style later. Agent.md's structure says Vue 3 + Vite, so (a) is my default.
5. **PostgreSQL credentials** for `localhost:5432` (PG 18): role/password + desired DB name (I propose DB `jobboard`, role `jobboard`).
6. **Styling:** Tailwind or plain CSS? **Package manager:** pnpm (installed) — confirm.
7. **Browser:** no desktop browser is attached to this session — please enable the experimental browser setting in the OpenCode desktop app, then I can open a tab for you to watch incremental changes.
