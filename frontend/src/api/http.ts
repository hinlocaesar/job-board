import { session } from './session'

/**
 * Problem-details error surfaced by the Marketplace API
 * (`application/problem+json` with a `code` extension).
 */
export class ApiError extends Error {
  readonly status: number
  readonly code: string
  readonly fields: Record<string, string[]>

  constructor(status: number, code: string, message: string, fields: Record<string, string[]> = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.fields = fields
  }

  /** First validation message for a field, if any. */
  field(name: string): string | undefined {
    return this.fields[name]?.[0]
  }

  static from(status: number, body: unknown): ApiError {
    if (body && typeof body === 'object') {
      const problem = body as Record<string, unknown>
      const fields = (problem.errors ?? {}) as Record<string, string[]>
      const message = String(problem.detail ?? problem.title ?? `Request failed (${status})`)
      const code = String(problem.code ?? problem.title ?? 'error')
      return new ApiError(status, code, message, fields)
    }
    return new ApiError(status, 'error', `Request failed (${status})`)
  }
}

/** Absolute-or-relative base for the Marketplace API. */
function apiBase(): string {
  if (import.meta.env.SSR) {
    // Prerender runs in Node against the real dev server — the `/api` prefix is
    // normally supplied by the Vite dev proxy, so it must be explicit here.
    return (globalThis as { PRERENDER_API_BASE?: string }).PRERENDER_API_BASE || 'http://localhost:5201/api'
  }
  return import.meta.env.VITE_API_BASE || '/api'
}

/** Absolute base for the Umbraco Content Delivery API. */
export function deliveryBase(): string {
  if (import.meta.env.SSR) {
    return (
      (globalThis as { PRERENDER_UMBRACO_BASE?: string }).PRERENDER_UMBRACO_BASE ||
      'https://localhost:7123/umbraco/delivery/api/v2'
    )
  }
  return import.meta.env.VITE_UMBRACO_BASE || '/umbraco/delivery/api/v2'
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  /** Plain object => JSON body. FormData is passed through untouched. */
  body?: unknown
  query?: Record<string, string | number | boolean | undefined | null>
  headers?: Record<string, string>
  /** Attach the bearer token (default true). */
  auth?: boolean
  signal?: AbortSignal
}

function buildUrl(base: string, path: string, query?: RequestOptions['query']): string {
  const url = new URL(path.startsWith('http') ? path : `${base}${path}`, 'http://placeholder.local')
  if (query) {
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') url.searchParams.set(key, String(value))
    }
  }
  const relative = `${url.pathname}${url.search}`
  return url.origin === 'http://placeholder.local' ? relative : `${url.origin}${relative}`
}

let refreshInFlight: Promise<string | null> | null = null

/** Single-flight token refresh so parallel 401s only refresh once. */
async function refreshSession(): Promise<string | null> {
  const refreshToken = session.current?.refreshToken
  if (!refreshToken) return null

  refreshInFlight ??= (async () => {
    try {
      const response = await fetch(buildUrl(apiBase(), '/auth/refresh'), {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      })
      if (!response.ok) return null
      const data = (await response.json()) as { accessToken: string; refreshToken: string }
      session.updateTokens(data.accessToken, data.refreshToken)
      return data.accessToken
    } catch {
      return null
    } finally {
      refreshInFlight = null
    }
  })()

  return refreshInFlight
}

export async function request<T>(path: string, options: RequestOptions = {}, retry = true): Promise<T> {
  const { method = 'GET', body, query, headers = {}, auth = true, signal } = options

  const finalHeaders: Record<string, string> = { accept: 'application/json', ...headers }
  const isForm = typeof FormData !== 'undefined' && body instanceof FormData

  if (body !== undefined && !isForm) finalHeaders['content-type'] = 'application/json'

  const token = auth ? session.accessToken : null
  if (token) finalHeaders.authorization = `Bearer ${token}`

  let response: Response
  try {
    response = await fetch(buildUrl(apiBase(), path, query), {
      method,
      headers: finalHeaders,
      body: body === undefined ? undefined : isForm ? (body as FormData) : JSON.stringify(body),
      signal,
    })
  } catch (error) {
    if ((error as Error).name === 'AbortError') throw error
    throw new ApiError(0, 'network_error', 'Could not reach the server. Check your connection and try again.')
  }

  if (response.status === 401 && auth && session.current) {
    const refreshed = await refreshSession()
    if (refreshed && retry) return request<T>(path, options, false)
    session.clear()
  }

  if (!response.ok) {
    let payload: unknown = null
    try {
      payload = await response.json()
    } catch {
      // Non-JSON error body (e.g. 429 HTML) — fall back to status text.
    }
    throw ApiError.from(response.status, payload ?? { detail: response.statusText })
  }

  if (response.status === 204) return undefined as T

  const contentType = response.headers.get('content-type') ?? ''
  if (!contentType.includes('application/json')) return (await response.text()) as T
  return (await response.json()) as T
}

/** Multipart upload (resume/logo) with progress-free simplicity. */
export async function upload<T>(path: string, file: File, fieldName = 'file'): Promise<T> {
  const form = new FormData()
  form.append(fieldName, file)
  return request<T>(path, { method: 'POST', body: form })
}

/** Authenticated file download → Blob (the endpoint requires a bearer header). */
export async function downloadBlob(path: string): Promise<Blob> {
  const headers: Record<string, string> = {}
  const token = session.accessToken
  if (token) headers.authorization = `Bearer ${token}`

  const response = await fetch(buildUrl(apiBase(), path), { headers })
  if (!response.ok) {
    throw ApiError.from(response.status, await response.json().catch(() => null))
  }
  return response.blob()
}
