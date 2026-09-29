/**
 * Tiny head manager: no plugin dependency.
 * - During prerender it accumulates tags that `entry-server.ts` serialises into
 *   the HTML <head>.
 * - In the browser it writes the tags directly (SPA navigation updates title +
 *   description + canonical + Open Graph).
 */

export interface SeoOptions {
  title?: string
  description?: string
  canonical?: string
  ogType?: string
  image?: string
  noindex?: boolean
}

const SITE_NAME = 'Filipino VA'

let current: SeoOptions = {}

/** Safe origin during prerender (no `window` in Node). */
export function siteOrigin(): string {
  if (import.meta.env.SSR) return import.meta.env.VITE_SITE_URL || 'http://localhost:5173'
  return window.location.origin
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}

function fullTitle(title?: string): string {
  if (!title) return `${SITE_NAME} — Remote jobs for Filipino talent`
  // CMS authors often include the brand themselves ("… — Filipino VA Blog").
  if (title === SITE_NAME || title.toLowerCase().includes(SITE_NAME.toLowerCase())) return title
  return `${title} · ${SITE_NAME}`
}

/** Call in component setup (works during SSR/prerender too). */
export function useSeo(options: SeoOptions): void {
  current = { ...current, ...options }
  if (import.meta.env.SSR) return
  applyHead(current)
}

export function resetSeo(): void {
  current = {}
}

export function getSeo(): SeoOptions {
  return current
}

function setMeta(selector: string, attr: 'name' | 'property', key: string, content: string): void {
  let tag = document.head.querySelector<HTMLMetaElement>(selector)
  if (!tag) {
    tag = document.createElement('meta')
    tag.setAttribute(attr, key)
    document.head.appendChild(tag)
  }
  tag.setAttribute('content', content)
}

function applyHead(seo: SeoOptions): void {
  const title = fullTitle(seo.title)
  document.title = title

  if (seo.description) setMeta('meta[name="description"]', 'name', 'description', seo.description)

  if (seo.canonical) {
    let link = document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]')
    if (!link) {
      link = document.createElement('link')
      link.rel = 'canonical'
      document.head.appendChild(link)
    }
    link.href = seo.canonical
  }

  if (seo.title) setMeta('meta[property="og:title"]', 'property', 'og:title', title)
  if (seo.description) setMeta('meta[property="og:description"]', 'property', 'og:description', seo.description)
  setMeta('meta[property="og:site_name"]', 'property', 'og:site_name', SITE_NAME)
  setMeta('meta[property="og:type"]', 'property', 'og:type', seo.ogType ?? 'website')
  if (seo.image) setMeta('meta[property="og:image"]', 'property', 'og:image', seo.image)
  if (seo.canonical) setMeta('meta[property="og:url"]', 'property', 'og:url', seo.canonical)
  setMeta('meta[name="robots"]', 'name', 'robots', seo.noindex ? 'noindex, nofollow' : 'index, follow')
}

/** Serialise the collected tags — used by entry-server.ts during prerender. */
export function renderHeadHtml(seo: SeoOptions = current): string {
  const title = escapeHtml(fullTitle(seo.title))
  const tags: string[] = [`<title>${title}</title>`]

  if (seo.description) tags.push(`<meta name="description" content="${escapeHtml(seo.description)}">`)
  if (seo.canonical) tags.push(`<link rel="canonical" href="${escapeHtml(seo.canonical)}">`)
  if (seo.title) tags.push(`<meta property="og:title" content="${escapeHtml(title)}">`)
  if (seo.description) tags.push(`<meta property="og:description" content="${escapeHtml(seo.description)}">`)
  tags.push(`<meta property="og:site_name" content="${SITE_NAME}">`)
  tags.push(`<meta property="og:type" content="${escapeHtml(seo.ogType ?? 'website')}">`)
  if (seo.image) tags.push(`<meta property="og:image" content="${escapeHtml(seo.image)}">`)
  if (seo.canonical) tags.push(`<meta property="og:url" content="${escapeHtml(seo.canonical)}">`)
  tags.push(`<meta name="robots" content="${seo.noindex ? 'noindex, nofollow' : 'index, follow'}">`)

  return tags.join('\n    ')
}
