import { beforeAll, describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { routes } from '../router'
import HomePage from '../pages/HomePage.vue'
import JobsPage from '../pages/JobsPage.vue'
import JobDetailPage from '../pages/JobDetailPage.vue'
import BlogListPage from '../pages/BlogListPage.vue'
import BlogPostPage from '../pages/BlogPostPage.vue'
import FaqPage from '../pages/FaqPage.vue'
import LandingPage from '../pages/LandingPage.vue'
import NotFoundPage from '../pages/NotFoundPage.vue'

/**
 * Renders the real pages against the real servers (Vite proxies /api → Marketplace
 * API and /umbraco → Umbraco). Skipped automatically when they are not running so
 * `pnpm test` stays green on a bare checkout.
 *
 *   dotnet run --project src\JobBoard.Api   (http://localhost:5201)
 *   dotnet run --project src\JobBoard.Cms   (https://localhost:7123)
 *   pnpm dev                                (http://localhost:5173)
 */
async function reachable(url: string): Promise<boolean> {
  try {
    const response = await fetch(url)
    return response.ok
  } catch {
    return false
  }
}

const apiUp = await reachable('http://localhost:5201/health')
const cmsUp = await reachable('http://localhost:5173/umbraco/delivery/api/v2/content/item')
const live = apiUp && cmsUp

if (!live) {
  console.warn(`[live-pages] skipped — api:${apiUp} cms:${cmsUp}. Start the API, the CMS and Vite to run these.`)
}

async function renderPage(component: unknown, path: string) {
  const router = createRouter({ history: createMemoryHistory(), routes })
  await router.push(path)
  await router.isReady()

  // Pages read the auth store for the header/apply state, so Pinia is required.
  const wrapper = mount(component as never, { global: { plugins: [createPinia(), router] } })
  // The pages fetch in onServerPrefetch (SSR) or onMounted (client); give the
  // real HTTP round-trips time to land.
  for (let i = 0; i < 20; i += 1) {
    await flushPromises()
    await new Promise((resolve) => setTimeout(resolve, 50))
  }
  return wrapper
}

describe.skipIf(!live)('pages rendered against the live API + CMS', () => {
  beforeAll(() => {
    expect(live, 'API and CMS must be running for the live suite').toBe(true)
  })

  it('home shows the Umbraco hero and real jobs', async () => {
    const wrapper = await renderPage(HomePage, '/')
    const text = wrapper.text()
    expect(text).toContain('Hire the best Filipino virtual assistants') // heroHeadline (CMS)
    expect(text).toContain('Latest jobs')
    expect(text).toMatch(/Acme|Senior Vue|Virtual Assistant/) // seeded jobs from the API
    expect(wrapper.html()).not.toContain('Could not reach the CMS')
  })

  it('job board lists jobs from the API with pay ranges', async () => {
    const wrapper = await renderPage(JobsPage, '/jobs')
    const text = wrapper.text()
    expect(text).toContain('Find remote work')
    expect(text).toMatch(/\/hour|\/month|project/)
    // Each result row is a single link to the job detail page.
    const rows = wrapper.findAll('a[href^="/jobs/"]')
    expect(rows.length).toBeGreaterThan(0)
  })

  it('job detail renders the description and apply box', async () => {
    const result = await fetch('http://localhost:5201/api/jobs?pageSize=1').then((r) => r.json())
    const job = result.items[0]

    const wrapper = await renderPage(JobDetailPage, `/jobs/${job.slug}`)
    const text = wrapper.text()
    expect(text).toContain('About the role')
    expect(text).toContain('Apply for this job')
    // useSeo() writes the head tags on the client (the prerender covers SSR).
    expect(document.title).toBe(`${job.title} · JobBoard`)
    expect(document.head.querySelector('meta[name="description"]')?.getAttribute('content')).toBeTruthy()
    expect(document.head.querySelector('link[rel="canonical"]')?.getAttribute('href')).toBe(
      `http://localhost:5173/jobs/${job.slug}`,
    )
  })

  it('blog list shows the CMS posts', async () => {
    const wrapper = await renderPage(BlogListPage, '/blog')
    const text = wrapper.text()
    expect(text).toContain('How to Hire Your First Filipino Virtual Assistant')
    expect(text).toContain('Maria Santos')
  })

  it('blog post renders rich text from the CMS', async () => {
    const wrapper = await renderPage(BlogPostPage, '/blog/5-tasks-you-can-delegate-to-a-va-this-week')
    const html = wrapper.html()
    expect(html).toContain('Inbox triage')
    expect(html).toContain('<strong>Inbox triage</strong>') // CMS rich text survived
  })

  it('FAQ renders the block list from the CMS', async () => {
    const wrapper = await renderPage(FaqPage, '/faq')
    const text = wrapper.text()
    expect(text).toContain('Frequently asked questions')
    expect(text).toContain('How much does it cost to hire a Filipino VA?')
  })

  it('landing page renders the CMS landing content', async () => {
    const wrapper = await renderPage(LandingPage, '/hire-filipino-virtual-assistants')
    expect(wrapper.text()).toContain('Hire Filipino virtual assistants, hand-picked for your workflow')
  })

  it('404 page renders for unknown routes', async () => {
    const wrapper = await renderPage(NotFoundPage, '/definitely-not-a-page')
    expect(wrapper.text()).toContain('This page has moved on')
  })
})
