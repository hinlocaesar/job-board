# Umbraco Content Delivery API contract (verified)

Verified against the running instance (**Umbraco 17.7.0**, LTS) on `https://localhost:7123`
and the official documentation:
<https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/headless-and-apis/content-delivery-api>

The frontend client lives in `frontend/src/api/umbraco.ts`; this document is the
source of truth for it. Do not guess query shapes — re-check here (or the docs) first.

---

## 1. Setup that must be in place

| Piece | Where | Value |
| --- | --- | --- |
| Delivery API enabled | `appsettings.json` → `Umbraco:CMS:DeliveryApi:Enabled` | `true` |
| Builder call | `Program.cs` | `.AddDeliveryApi()` on `CreateUmbracoBuilder()` |
| Database | `appsettings.Development.json` → `ConnectionStrings:umbracoDbDSN` + `umbracoDbDSN_ProviderName` | SQLite, `Microsoft.Data.Sqlite` |
| Public access | default (`PublicAccess: true`, no API key) | anonymous GETs work |
| Index | `DeliveryApiContentIndex` | the seeder rebuilds it on boot |

`umbracoDbDSN_ProviderName` is the **exact** key (underscore). With the wrong key
Umbraco falls back to `Microsoft.Data.SqlClient` and the unattended install dies with
`Keyword not supported: 'cache'`.

Draft content is **not** a query parameter. It is the request header
`Preview: true` plus `Api-Key`. This app never sends either — it only ever reads
published content.

---

## 2. Endpoints

Base URL: `https://localhost:7123/umbraco/delivery/api/v2` (in the browser it is
proxied as `/umbraco/delivery/api/v2`).

| Purpose | Request |
| --- | --- |
| One item by route | `GET /content/item/{path}` — e.g. `/content/item/faq`, `/content/item/blog/5-tasks-you-can-delegate-to-a-va-this-week` |
| One item (site root) | `GET /content/item` — no path segment |
| One item by GUID | `GET /content/item/{guid}` |
| Several items by GUID | `GET /content/items?id={guid}&id={guid}` |
| Children of a node | `GET /content?fetch=children:{id\|path}` (e.g. `fetch=children:/blog`) |
| Ancestors / descendants | `GET /content?fetch=ancestors:/blog` / `?fetch=descendants:/blog` |
| Filter by type | `GET /content?filter=contentType:blogPost&take=10&skip=0` |
| Sort | `&sort=updateDate:desc` (`createDate`, `level`, `name`, `sortOrder`, `updateDate`; combinable) |
| Limit properties | `&fields=properties[title,body]` (`$all` for everything) |
| Expand picked content | `&expand=properties[alias]` |

Supported `filter` values: `contentType`, `name`, `createDate`, `updateDate`
(`>`/`>=`/`<`/<=` comparisons on the dates; `!` negates `contentType`/`name`).
Only **one** `fetch` selector per request.

There is **no** `filter=route`, no `path` parameter and no `draft` parameter — the
legacy `/umbraco/swagger/delivery/swagger.json` shape is gone.

---

## 3. Response shapes (copied from live responses)

### Single item

```jsonc
{
  "contentType": "homepage",
  "name": "Home",
  "createDate": "2026-09-28T08:35:14.283285Z",
  "updateDate": "2026-09-28T08:35:15.2498663Z",
  "route": {
    "path": "/",                                  // NOTE: object, and trailing slash
    "queryString": null,
    "startItem": { "id": "aa568b0b…", "path": "home" }
  },
  "id": "aa568b0b-e5bf-4991-b73d-b7962bed533b",
  "properties": { "heroHeadline": "…", "metaTitle": "…", "metaDescription": "…" },
  "cultures": {}
}
```

### Multi item

```jsonc
{ "total": 3, "items": [ /* same item shape */ ] }
```

### Rich text

`RichTextOutputAsJson` is off, so rich text properties are **HTML inside an object**:

```jsonc
"body": { "markup": "<h2>Start with the job post</h2><p>…</p>", "blocks": [] }
```

`richText.ts` unwraps `.markup`; `propString()` does the same for convenience.

### Block list (FAQ)

`faqItems` comes back as `{ items: [{ content: { contentType, id, properties: {…} }, settings: null }] }`
— the element type alias is on `content.contentType` and the values on
`content.properties`.

---

## 4. Content model created by the seeder

`Composing/ContentSeedNotificationHandler.cs` runs on `UmbracoApplicationStartedNotification`
(runtime level `Run`) and is idempotent: it only creates what is missing and only
publishes when needed. Content is seeded as `Constants.Security.SuperUserKey`
(`Guid.Empty` is rejected by the async service APIs).

| Doc type (alias) | Route | Properties used by the frontend |
| --- | --- | --- |
| `homepage` | `/` | `heroHeadline`, `heroSubtext`, `ctaLabel`, `ctaLink`, `introBody` |
| `blogList` | `/blog` | `pageTitle`, `intro` |
| `blogPost` | `/blog/{slug}` | `title`, `heroSubtext`, `body` (rich text), `author`, `publishDate`, `excerpt` |
| `landingPage` | `/{slug}` | `headline`, `body`, `ctaLabel`, `ctaLink` |
| `faq` | `/faq` | `heading`, `intro`, `faqItems` (Block List) |
| `faqItem` (element) | — | `question`, `answer` |
| `seoFields` (composition) | — | `metaTitle`, `metaDescription` |

Allowed children: `homepage` → `blogList`, `landingPage`, `faq`; `blogList` → `blogPost`.

Seeded content: Home, Blog + 3 posts, “Hire Filipino Virtual Assistants” landing page,
FAQ with 6 Q&A blocks.

`publishDate` is set as a real `DateTime` — the old v13 `{"date":"…","timeZone":null}`
string is no longer a valid value and throws.

---

## 5. What the frontend does with it

| Page | Call |
| --- | --- |
| `/` HomePage | `byRoute('/')` → hero copy; jobs come from the Marketplace API |
| `/blog` BlogListPage | `children('/blog', 50)` |
| `/blog/:slug` BlogPostPage | `firstByRoute('/blog/{slug}')` |
| `/faq` FaqPage | `byRoute('/faq')` + `faqItems` block list |
| `/:slug(hire-.*)` LandingPage | `firstByRoute('/{slug}')` |

SEO comes from the `seoFields` composition (`metaTitle` → `<title>`, `metaDescription`
→ `<meta name="description">`); `useSeo()` does not append the site name again when the
CMS title already contains it.

During `pnpm build:prerender` the same calls run in Node, so the CMS content ends up in
the static HTML (`NODE_TLS_REJECT_UNAUTHORIZED=0` is set because the dev cert is
self-signed). If Umbraco is down the job pages still build; only blog/FAQ/landing
content falls back to the hard-coded copy.

---

## 6. Manual verification (PowerShell, self-signed dev cert)

```powershell
# TLS 1.2 + skip the dev certificate check
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
[Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
$base = "https://localhost:7123/umbraco/delivery/api/v2"

Invoke-RestMethod "$base/content/item"                     # homepage
Invoke-RestMethod "$base/content/item/faq"                # FAQ + block list
Invoke-RestMethod "$base/content?fetch=children:/blog&take=10"
```

`curl.exe -sk` works too (`Invoke-RestMethod` needs the two lines above on
Windows PowerShell 5.1).

---

## 7. Backoffice notes

- Umbraco backoffice: `https://localhost:7123/umbraco` → `admin@jobboard.local` /
  `JobBoard123!` (unattended install credentials live in the **gitignored**
  `appsettings.Local.json`; change them for anything but local dev).
- After editing content types, rebuild **Settings → Examine Management →
  DeliveryApiContentIndex** (the seeder does this automatically on boot when it
  created something).
- The Umbraco Developer MCP (`@umbraco-cms/mcp-dev`) can drive this instance once an
  **API user** exists (Users → API users) and the `umbraco-mcp` block in
  `opencode.json` is enabled with `UMBRACO_CLIENT_ID` / `UMBRACO_CLIENT_SECRET`.
  Dev instance only — never production.
