import { deliveryBase } from './http'

/**
 * Client for the Umbraco 17 Content Delivery API v2.
 *
 * Contract verified against the running instance (17.7.0) and the official docs
 * (https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/headless-and-apis/content-delivery-api):
 *
 * - `GET /content/item/{path}`   → one item, or `GET /content/item` for the root
 * - `GET /content?fetch=children:{idOrPath}` → `{ total, items: [...] }`
 * - `GET /content?filter=contentType:{alias}` (also `name`, `createDate`, `updateDate`)
 * - `sort` (`createDate:desc`, …), `skip`, `take`, `fields`, `expand`
 * - Draft content is NOT a query parameter — it is the `Preview: true` header
 *   (and requires the `Api-Key` header), which this app never sends.
 * - `route` is an object: `{ path, queryString, startItem: { id, path } }`.
 * - Rich text is `{ markup, blocks }` (HTML, because RichTextOutputAsJson is off).
 */

export interface DeliveryRoute {
  /** e.g. `/blog/my-post/` — note the trailing slash. */
  path: string | null
  queryString?: string | null
  startItem?: { id?: string; path?: string | null } | null
}

export interface DeliveryContent {
  /** Content GUID. */
  id: string
  name: string
  contentType: string
  createDate: string
  updateDate: string
  route?: DeliveryRoute
  /** Property aliases → values. Rich text values are `{ markup, blocks }`. */
  properties: Record<string, unknown>
  cultures?: Record<string, unknown>
}

/** Paged multi-item envelope returned by `GET /content`. */
export interface DeliveryEnvelope<T> {
  total: number
  items: T[]
}

type QueryValue = string | number | boolean | undefined | null

function deliveryGet<T>(path: string, query: Record<string, QueryValue>): Promise<T> {
  const base = deliveryBase()

  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== '') params.append(key, String(value))
  }
  const search = params.toString()

  // `base` is relative in the browser ("/umbraco/delivery/api/v2", proxied by
  // Vite) and absolute during prerender — `new URL()` needs a base, so only use
  // it for the absolute case.
  const suffix = `${path}${search ? `?${search}` : ''}`
  const absolute = /^https?:\/\//i.test(base)
  const target = absolute ? new URL(`${base}${suffix}`).toString() : `${base}${suffix}`

  return fetch(target, { headers: { accept: 'application/json' } }).then(async (response) => {
    if (!response.ok) {
      throw new Error(`Content Delivery API responded ${response.status} for ${target}`)
    }
    return (await response.json()) as T
  })
}

/** `content/item` takes the route *without* a leading slash; the root has none. */
function itemPath(route: string): string {
  const trimmed = route.replace(/^\/+|\/+$/g, '')
  return trimmed ? `/content/item/${trimmed}` : '/content/item'
}

export const umbracoApi = {
  /** The published item at a route (`/`, `/faq`, `/blog/my-post`, …). */
  byRoute: (route: string) => deliveryGet<DeliveryContent>(itemPath(route), {}),

  /** One item by GUID. */
  byId: (id: string) => deliveryGet<DeliveryContent>(`/content/item/${encodeURIComponent(id)}`, {}),

  /** Immediate children of a route, e.g. `/blog`. */
  children: (route: string, take = 20, skip = 0) =>
    deliveryGet<DeliveryEnvelope<DeliveryContent>>('/content', {
      fetch: `children:${route.replace(/^\/+|\/+$/g, '')}`,
      take,
      skip,
    }),

  /** All items of a content type, newest first. */
  byContentType: (alias: string, take = 20, skip = 0) =>
    deliveryGet<DeliveryEnvelope<DeliveryContent>>('/content', {
      filter: `contentType:${alias}`,
      sort: 'updateDate:desc',
      take,
      skip,
    }),

  /** Best-effort read: returns null instead of throwing when the route is missing. */
  async firstByRoute(route: string): Promise<DeliveryContent | null> {
    try {
      return await umbracoApi.byRoute(route)
    } catch {
      return null
    }
  },
}

/** `route.path` with the trailing slash removed — matches the Vue route params. */
export function routeSlug(content: DeliveryContent | null): string {
  const path = content?.route?.path ?? ''
  return path.replace(/^\/+|\/+$/g, '').split('/').filter(Boolean).pop() ?? ''
}

/** Reads a property value with a fallback (`undefined`/empty → fallback). */
export function prop<T>(content: DeliveryContent | null, alias: string, fallback: T): T {
  if (!content) return fallback
  const value = content.properties?.[alias]
  return (value === undefined || value === null || value === '' ? fallback : value) as T
}

/**
 * Plain-text property reader. Rich text arrives as `{ markup, blocks }`, so that
 * shape is unwrapped to its HTML here.
 */
export function propString(content: DeliveryContent | null, alias: string, fallback = ''): string {
  const value = prop<unknown>(content, alias, null)
  if (typeof value === 'string') return value
  if (value && typeof value === 'object') {
    const rich = value as { markup?: unknown }
    if (typeof rich.markup === 'string') return rich.markup
  }
  return fallback
}
