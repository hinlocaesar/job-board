<script setup lang="ts">
import { RouterLink } from 'vue-router'

const year = new Date().getFullYear()

const columns = [
  {
    heading: 'For job seekers',
    links: [
      { label: 'Browse jobs', to: { name: 'jobs' } },
      { label: 'Create a profile', to: { name: 'register' } },
      { label: 'My applications', to: { name: 'applications' } },
    ],
  },
  {
    heading: 'For employers',
    links: [
      { label: 'Post a job', to: { name: 'job-new' } },
      { label: 'Dashboard', to: { name: 'dashboard' } },
      { label: 'Company profile', to: { name: 'employer-profile' } },
    ],
  },
  {
    heading: 'Explore',
    links: [
      { label: 'Virtual assistants', to: { name: 'jobs', query: { category: 'virtual-assistance' } } },
      { label: 'Web developers', to: { name: 'jobs', query: { category: 'web-development' } } },
      { label: 'Graphic designers', to: { name: 'jobs', query: { category: 'graphic-design' } } },
      { label: 'Customer support', to: { name: 'jobs', query: { category: 'customer-support' } } },
    ],
  },
  {
    heading: 'Company',
    links: [
      { label: 'Blog', to: { name: 'blog' } },
      { label: 'Help & FAQ', to: { name: 'faq' } },
      {
        label: 'Hire Filipino VAs',
        to: { name: 'landing', params: { slug: 'hire-filipino-virtual-assistants' } },
      },
    ],
  },
]
</script>

<template>
  <footer class="border-t border-slate-200 bg-slate-100">
    <div class="shell py-12">
      <p class="max-w-md text-[13px] leading-relaxed text-slate-600">
        Remote jobs for Filipino talent. Every listing is reviewed before it is published.
      </p>

      <div class="mt-8 grid grid-cols-2 gap-8 sm:grid-cols-4">
        <nav v-for="column in columns" :key="column.heading" :aria-label="column.heading">
          <h2 class="text-[12px] font-semibold text-slate-900">{{ column.heading }}</h2>
          <ul class="mt-3 space-y-2.5">
            <li v-for="link in column.links" :key="link.label">
              <RouterLink :to="link.to" class="text-[12px] text-slate-600 hover:text-brand-600 hover:underline">
                {{ link.label }}
              </RouterLink>
            </li>
          </ul>
        </nav>
      </div>

      <div class="mt-10 flex flex-wrap items-center justify-between gap-2 border-t border-slate-200 pt-6">
        <p class="text-[12px] text-slate-500">Copyright &copy; {{ year }} Filipino VA. All rights reserved.</p>
        <p class="text-[12px] text-slate-500">Built with Umbraco &amp; Vue 3</p>
      </div>
    </div>
  </footer>
</template>
