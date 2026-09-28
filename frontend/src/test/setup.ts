import { vi } from 'vitest'

// jsdom runs with `http://localhost:5173` (see vite.config.ts) so page code can
// use the same relative `/api` + `/umbraco` URLs the browser would — but Node's
// fetch rejects relative URLs, so resolve them against the jsdom origin first.
const nodeFetch = globalThis.fetch.bind(globalThis)
globalThis.fetch = ((input: RequestInfo | URL, init?: RequestInit) => {
  if (typeof input === 'string' && input.startsWith('/')) {
    return nodeFetch(`${window.location.origin}${input}`, init)
  }
  return nodeFetch(input as RequestInfo, init)
}) as typeof fetch

// jsdom is missing a few browser APIs used by layout code.
window.matchMedia =
  window.matchMedia ||
  ((query: string) =>
    ({
      matches: false,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    }) as unknown as MediaQueryList)

class ResizeObserverStub {
  observe() {}
  unobserve() {}
  disconnect() {}
}
window.ResizeObserver = window.ResizeObserver || (ResizeObserverStub as unknown as typeof ResizeObserver)

window.scrollTo = window.scrollTo || vi.fn()
