<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { onServerPrefetch } from 'vue'
import { RouterLink } from 'vue-router'
import { jobsApi } from '../api/jobs'
import { propString, umbracoApi, type DeliveryContent } from '../api/umbraco'
import type { JobSearchItem } from '../api/types'
import JobCard from '../components/jobs/JobCard.vue'
import Spinner from '../components/common/Spinner.vue'
import { siteOrigin, useSeo } from '../composables/useSeo'
import { errorMessage } from '../composables/useFormat'

/** Hero copy comes from the Umbraco homepage when available, else a fallback. */
const FALLBACK = {
  headline: 'Remote jobs for Filipino talent',
  subtext: 'Hire vetted virtual assistants, developers, designers and writers — or find your next remote role.',
}

const homepage = ref<DeliveryContent | null>(null)
const jobs = ref<JobSearchItem[]>([])
const jobsError = ref<string | null>(null)
const cmsError = ref<string | null>(null)
const ready = ref(false)

const headline = () => propString(homepage.value, 'heroHeadline') || FALLBACK.headline
const subtext = () => propString(homepage.value, 'heroSubtext') || FALLBACK.subtext

async function load(): Promise<void> {
  // The CMS may not be reachable in every environment — never block the page.
  try {
    homepage.value = await umbracoApi.firstByRoute('/')
  } catch (caught) {
    cmsError.value = errorMessage(caught)
  }

  try {
    const result = await jobsApi.search({ pageSize: 6, sort: 'newest' })
    jobs.value = result.items
  } catch (caught) {
    jobsError.value = errorMessage(caught)
  } finally {
    ready.value = true
  }

  // The `seoFields` composition overrides the generic fallback copy.
  useSeo({
    title: propString(homepage.value, 'metaTitle') || headline(),
    description: propString(homepage.value, 'metaDescription') || subtext().slice(0, 155),
    canonical: `${siteOrigin()}/`,
  })
}

onServerPrefetch(() => load())
onMounted(() => {
  if (!ready.value) void load()
})
</script>

<template>
  <div>
    <!-- Hero -->
    <section class="border-b border-slate-200 bg-gradient-to-b from-brand-50 to-white">
      <div class="mx-auto max-w-6xl px-4 py-16 sm:px-6 lg:py-20">
        <div class="max-w-3xl">
          <span class="chip bg-white text-brand-700 shadow-sm">Phase 1 MVP · remote work marketplace</span>
          <h1 class="mt-4 text-3xl font-bold tracking-tight text-slate-900 sm:text-5xl">
            {{ headline() }}
          </h1>
          <p class="mt-4 max-w-2xl text-lg text-slate-600">{{ subtext() }}</p>

          <div class="mt-8 flex flex-wrap gap-3">
            <RouterLink :to="{ name: 'jobs' }" class="btn-primary px-6 py-3 text-base">Browse jobs</RouterLink>
            <RouterLink :to="{ name: 'register' }" class="btn-secondary px-6 py-3 text-base">Post a job</RouterLink>
          </div>
        </div>
      </div>
    </section>

    <!-- Latest jobs -->
    <section class="mx-auto max-w-6xl px-4 py-12 sm:px-6">
      <div class="mb-6 flex items-end justify-between">
        <div>
          <h2 class="text-xl font-semibold text-slate-900">Latest jobs</h2>
          <p class="text-sm text-slate-500">Fresh roles from verified employers.</p>
        </div>
        <RouterLink :to="{ name: 'jobs' }" class="text-sm font-medium text-brand-600 hover:underline">
          View all →
        </RouterLink>
      </div>

      <Spinner v-if="!ready && jobs.length === 0" />

      <div v-else class="space-y-4">
        <p v-if="jobsError" class="rounded-lg bg-rose-50 px-4 py-3 text-sm text-rose-700">{{ jobsError }}</p>

        <template v-if="jobs.length">
          <JobCard v-for="job in jobs" :key="job.id" :job="job" />
        </template>
        <p v-else class="rounded-lg bg-slate-50 px-4 py-6 text-center text-sm text-slate-500">
          No jobs published yet — check back soon.
        </p>
      </div>
    </section>

    <!-- Value props -->
    <section class="border-t border-slate-200 bg-slate-50">
      <div class="mx-auto grid max-w-6xl gap-6 px-4 py-12 sm:px-6 md:grid-cols-3">
        <div class="card">
          <h3 class="font-semibold text-slate-900">For job seekers</h3>
          <p class="mt-2 text-sm text-slate-600">
            Build a public profile with your skills, rates and resume, then apply in one click.
          </p>
          <RouterLink class="mt-3 inline-block text-sm font-medium text-brand-600 hover:underline" :to="{ name: 'register' }">
            Create your profile →
          </RouterLink>
        </div>
        <div class="card">
          <h3 class="font-semibold text-slate-900">For employers</h3>
          <p class="mt-2 text-sm text-slate-600">
            Post a role, review applicants side by side and shortlist the best fit — all in one dashboard.
          </p>
          <RouterLink class="mt-3 inline-block text-sm font-medium text-brand-600 hover:underline" :to="{ name: 'job-new' }">
            Post a job →
          </RouterLink>
        </div>
        <div class="card">
          <h3 class="font-semibold text-slate-900">Moderated &amp; fair</h3>
          <p class="mt-2 text-sm text-slate-600">
            Every listing is reviewed by our team. Contact details stay private until you choose to share them.
          </p>
          <RouterLink class="mt-3 inline-block text-sm font-medium text-brand-600 hover:underline" :to="{ name: 'faq' }">
            Read the FAQ →
          </RouterLink>
        </div>
      </div>
    </section>
  </div>
</template>
