import { renderToString } from 'vue/server-renderer'
import { createSSRApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import { createAppRouter } from './router'
import { useAuthStore } from './stores/auth'
import { renderHeadHtml, resetSeo } from './composables/useSeo'

export interface RenderResult {
  html: string
  head: string
}

/**
 * Renders one URL to HTML for the prerender step.
 *
 * `onServerPrefetch` hooks in the pages fetch real API/CMS data first, so the
 * emitted markup contains actual content (the SEO requirement). Only public
 * routes are rendered — there is no session during prerender, so the auth
 * store stays anonymous and the route guards redirect private pages to /login.
 */
export async function render(url: string): Promise<RenderResult> {
  resetSeo()

  const app = createSSRApp(App)
  const pinia = createPinia()
  const router = createAppRouter()

  app.use(pinia)
  app.use(router)

  // Mirrors main.ts; the session is empty in Node, so guards see "anonymous".
  useAuthStore(pinia).restore()

  await router.push(url)
  await router.isReady()

  const html = await renderToString(app)
  return { html, head: renderHeadHtml() }
}
