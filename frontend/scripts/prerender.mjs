/**
 * Build-time prerender: renders every public route to static HTML so job posts,
 * profiles and CMS pages are indexable (Phase 1 SEO requirement).
 *
 * Run via: pnpm build:prerender
 *   1. vite build                 → dist/ (client bundle + index.html template)
 *   2. vite build --ssr           → dist-ssr/entry-server.js
 *   3. node scripts/prerender.mjs → dist/<route>/index.html for each route
 *
 * Requires the Marketplace API (localhost:5201); the Umbraco instance
 * (localhost:7123) is optional — routes are skipped with a warning if it is off.
 */
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

// Local dev certs (Umbraco) are self-signed; this script never runs in prod CI
// with real endpoints.
process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0'

const API_BASE = process.env.PRERENDER_API_BASE || 'http://localhost:5201/api'
const UMBRACO_BASE =
  process.env.PRERENDER_UMBRACO_BASE || 'https://localhost:7123/umbraco/delivery/api/v2'

// Make the bases visible to the SSR bundle (it reads globals at request time).
globalThis.PRERENDER_API_BASE = API_BASE
globalThis.PRERENDER_UMBRACO_BASE = UMBRACO_BASE

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const dist = join(root, 'dist')

const template = readFileSync(join(dist, 'index.html'), 'utf-8')
  // The prerendered head supplies its own <title>/description/canonical/OG
  // tags, so the dev-only fallback block (and any stray <title>) is dropped to
  // avoid duplicate/conflicting metadata.
  .replace(/[ \t]*<!--default-head-->[\s\S]*?<!--\/default-head-->/, '')
  .replace(/\s*<title>[\s\S]*?<\/title>/, '')

const { render } = await import(pathToFileURL(join(root, 'dist-ssr', 'entry-server.js')).href)

async function getJson(url) {
  const response = await fetch(url, { headers: { accept: 'application/json' } })
  if (!response.ok) throw new Error(`${response.status} for ${url}`)
  return response.json()
}

/** Static marketing + auth routes that always make sense to prerender. */
const routes = new Set([
  '/',
  '/jobs',
  '/blog',
  '/faq',
  '/register',
  '/login',
  '/hire-filipino-virtual-assistants',
])

// Dynamic: the most recent published jobs (the SEO-critical pages). The board is
// live for everything else; prerendering is capped so builds stay bounded.
const JOB_PAGE_LIMIT = 50
try {
  const jobs = await getJson(`${API_BASE}/jobs?pageSize=${JOB_PAGE_LIMIT}&sort=newest`)
  for (const job of jobs.items ?? []) routes.add(`/jobs/${job.slug}`)
  console.log(`+ ${jobs.items?.length ?? 0} job pages`)
} catch (error) {
  console.warn(`! Marketplace API unavailable (${error.message}) — skipping job pages`)
}

// Dynamic: Umbraco blog posts (optional).
try {
  const blog = await getJson(`${UMBRACO_BASE}/content?fetch=children:/blog&take=50`)
  let count = 0
  for (const item of blog.items ?? []) {
    // `route` is an object: `{ path: "/blog/some-post/", ... }`
    const segment = String(item.route?.path ?? '')
      .split('/')
      .filter(Boolean)
      .pop()
    if (segment) {
      routes.add(`/blog/${segment}`)
      count += 1
    }
  }
  console.log(`+ ${count} blog pages`)
} catch (error) {
  console.warn(`! Umbraco unavailable (${error.message}) — skipping blog posts`)
}

let written = 0
for (const route of routes) {
  try {
    const { html, head } = await render(route)
    const output = template.replace('<!--app-head-->', head).replace('<!--app-html-->', html)
    const file = route === '/' ? join(dist, 'index.html') : join(dist, route.replace(/^\//, ''), 'index.html')
    mkdirSync(dirname(file), { recursive: true })
    writeFileSync(file, output)
    written += 1
  } catch (error) {
    console.warn(`! failed ${route}: ${error.message}`)
  }
}

console.log(`Prerendered ${written}/${routes.size} routes into dist/`)
