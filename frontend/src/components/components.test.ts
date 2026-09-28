import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import JobCard from '../components/jobs/JobCard.vue'
import PaginationBar from '../components/common/PaginationBar.vue'
import type { JobSearchItem } from '../api/types'

const job: JobSearchItem = {
  id: '9f1b2c3d-0000-0000-0000-000000000001',
  title: 'Senior Vue Developer',
  slug: 'senior-vue-developer',
  categoryId: '9f1b2c3d-0000-0000-0000-000000000002',
  categoryName: 'Development',
  categorySlug: 'development',
  companyName: 'Acme Remote',
  companySlug: 'acme-remote',
  jobType: 'FullTime',
  region: 'Worldwide',
  payType: 'Hourly',
  payMin: 35,
  payMax: 55,
  currency: 'USD',
  experienceLevel: 'Senior',
  hoursPerWeek: 40,
  publishedAt: new Date().toISOString(),
  skills: ['Vue.js', 'TypeScript', 'Tailwind CSS', 'Pinia', 'Vitest', 'Node.js', 'GraphQL'],
}

describe('JobCard', () => {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/jobs/:slug', name: 'job-detail', component: { template: '<div />' } }],
  })

  it('renders the job essentials', () => {
    const wrapper = mount(JobCard, { props: { job }, global: { plugins: [router] } })

    expect(wrapper.text()).toContain('Senior Vue Developer')
    expect(wrapper.text()).toContain('Acme Remote')
    expect(wrapper.text()).toContain('Development')
    expect(wrapper.text()).toContain('$35 – $55 /hour')
    expect(wrapper.text()).toContain('Full Time')
    expect(wrapper.text()).toContain('40 hrs/week')
    expect(wrapper.text()).toContain('Vue.js')
    // only the first six skills are shown, the rest collapse
    expect(wrapper.text()).toContain('+1 more')
    expect(wrapper.text()).not.toContain('GraphQL')
  })

  it('links to the job detail route', async () => {
    const wrapper = mount(JobCard, { props: { job }, global: { plugins: [router] } })
    const link = wrapper.get('a')
    expect(link.attributes('href')).toBe('/jobs/senior-vue-developer')
  })
})

describe('PaginationBar', () => {
  it('renders nothing for a single page', () => {
    const wrapper = mount(PaginationBar, { props: { page: 1, totalPages: 1 } })
    expect(wrapper.find('nav').exists()).toBe(false)
  })

  it('emits page-change for prev/next and numbered buttons', async () => {
    const wrapper = mount(PaginationBar, { props: { page: 3, totalPages: 5 } })

    const buttons = wrapper.findAll('button')
    // 5 pages fits the window without ellipses: Prev, 1..5, Next
    expect(buttons.map((b) => b.text())).toEqual([
      '← Prev',
      '1',
      '2',
      '3',
      '4',
      '5',
      'Next →',
    ])

    await buttons[buttons.length - 1].trigger('click')
    expect(wrapper.emitted('page-change')).toEqual([[4]])

    await wrapper.get('button:nth-of-type(1)').trigger('click')
    expect(wrapper.emitted('page-change')?.[1]).toEqual([2])
  })

  it('disables prev on the first page and next on the last', () => {
    const first = mount(PaginationBar, { props: { page: 1, totalPages: 3 } })
    expect(first.findAll('button')[0].attributes('disabled')).toBeDefined()
    expect(first.findAll('button').at(-1)?.attributes('disabled')).toBeUndefined()

    const last = mount(PaginationBar, { props: { page: 3, totalPages: 3 } })
    expect(last.findAll('button').at(-1)?.attributes('disabled')).toBeDefined()
  })

  it('ignores clicks while disabled', async () => {
    const wrapper = mount(PaginationBar, { props: { page: 2, totalPages: 4, disabled: true } })
    await wrapper.findAll('button')[0].trigger('click')
    expect(wrapper.emitted('page-change')).toBeUndefined()
  })
})
