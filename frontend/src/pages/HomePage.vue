<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { onServerPrefetch } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { jobsApi, categoriesApi } from '../api/jobs'
import { propString, umbracoApi, type DeliveryContent } from '../api/umbraco'
import type { CategoryDto, JobSearchItem } from '../api/types'
import JobCard from '../components/jobs/JobCard.vue'
import Spinner from '../components/common/Spinner.vue'
import EmptyState from '../components/common/EmptyState.vue'
import Icon from '../components/common/Icon.vue'
import { siteOrigin, useSeo } from '../composables/useSeo'
import { errorMessage } from '../composables/useFormat'

/** Hero copy comes from the Umbraco homepage when available, else a fallback. */
const FALLBACK = {
  headline: 'Remote jobs for Filipino talent',
  subtext: 'Hire vetted virtual assistants, developers, designers and writers — or find your next remote role.',
}

const router = useRouter()
const homepage = ref<DeliveryContent | null>(null)
const jobs = ref<JobSearchItem[]>([])
const categories = ref<CategoryDto[]>([])
const totalCount = ref<number | null>(null)
const jobsError = ref<string | null>(null)
const ready = ref(false)

const keyword = ref('')
const chosenCategory = ref('')

const headline = () => propString(homepage.value, 'heroHeadline') || FALLBACK.headline
const subtext = () => propString(homepage.value, 'heroSubtext') || FALLBACK.subtext

const listingCount = computed(() =>
  totalCount.value === null ? null : totalCount.value.toLocaleString('en-US'),
)

function search(): void {
  const query: Record<string, string> = {}
  if (keyword.value.trim()) query.q = keyword.value.trim()
  if (chosenCategory.value) query.category = chosenCategory.value
  void router.push({ name: 'jobs', query })
}

async function load(): Promise<void> {
  // The CMS may not be reachable in every environment — the fallback copy
  // covers the hero, so a failure here must never break the page.
  try {
    homepage.value = await umbracoApi.firstByRoute('/')
  } catch {
    /* keep the fallback headline and subtext */
  }

  try {
    const [result, categoryList] = await Promise.all([
      jobsApi.search({ pageSize: 6, sort: 'newest' }),
      categoriesApi.list(),
    ])
    jobs.value = result.items
    totalCount.value = result.totalCount
    categories.value = categoryList
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
    <!-- Centred hero. The search lives inside it — splitting it into a second
         band left a dead gap between the buttons and the field. -->
    <section class="shell-wide pt-16 pb-20 sm:pt-24 sm:pb-28">
      <h1 class="display text-[44px] sm:text-[68px] lg:text-[80px]">{{ headline() }}</h1>
      <p class="muted mx-auto mt-6 max-w-2xl text-[19px] leading-relaxed sm:text-[21px]">{{ subtext() }}</p>

      <div class="mt-9 flex flex-wrap justify-center gap-3">
        <RouterLink :to="{ name: 'jobs' }" class="btn-primary btn-lg">Browse jobs</RouterLink>
        <RouterLink :to="{ name: 'job-new' }" class="btn-secondary btn-lg">Post a job</RouterLink>
      </div>

      <form class="mx-auto mt-12 max-w-2xl" role="search" @submit.prevent="search">
        <div class="flex flex-col gap-2 sm:flex-row">
          <div class="relative flex-1">
            <label class="sr-only" for="home-q">Job title, skill or keyword</label>
            <span class="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-slate-400">
              <Icon name="search" :size="17" />
            </span>
            <input
              id="home-q"
              v-model="keyword"
              type="search"
              class="input rounded-full py-3 pl-11"
              placeholder="Job title, skill or keyword"
            />
          </div>
          <div class="sm:w-52">
            <label class="sr-only" for="home-category">Category</label>
            <select id="home-category" v-model="chosenCategory" class="input rounded-full py-3">
              <option value="">All categories</option>
              <option v-for="c in categories" :key="c.id" :value="c.slug">{{ c.name }}</option>
            </select>
          </div>
          <button class="btn-dark px-7" type="submit">Search</button>
        </div>

        <p v-if="listingCount" class="mt-4 text-[13px] text-slate-500">
          {{ listingCount }} roles open right now
        </p>
      </form>
    </section>

    <!-- Categories -->
    <section v-if="categories.length" class="section-alt section">
      <div class="shell">
        <div class="max-w-2xl">
          <h2 class="section-title">Browse by category</h2>
          <p class="muted mt-3 text-[17px]">Ten specialist categories, from virtual assistance to data.</p>
        </div>

        <div class="mt-10 grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
          <RouterLink
            v-for="category in categories"
            :key="category.id"
            :to="{ name: 'jobs', query: { category: category.slug } }"
            class="group flex items-center justify-between gap-3 rounded-2xl bg-white px-5 py-4
                   transition-transform duration-150 hover:-translate-y-0.5"
          >
            <span class="text-[14px] font-medium text-slate-900">{{ category.name }}</span>
            <span class="text-slate-400 transition-transform group-hover:translate-x-0.5" aria-hidden="true">
              <Icon name="chevron-right" :size="15" />
            </span>
          </RouterLink>
        </div>
      </div>
    </section>

    <!-- Latest jobs -->
    <section class="section">
      <div class="shell">
        <div class="flex flex-wrap items-end justify-between gap-4">
          <div class="max-w-xl">
            <h2 class="section-title">Latest jobs</h2>
            <p class="muted mt-3 text-[17px]">Newest roles from employers hiring remote today.</p>
          </div>
          <RouterLink :to="{ name: 'jobs' }" class="arrow-link text-[15px]">
            View all jobs
            <Icon name="arrow-right" :size="15" />
          </RouterLink>
        </div>

        <Spinner v-if="!ready && jobs.length === 0" />

        <div v-else class="mt-8">
          <p v-if="jobsError" class="rounded-2xl bg-rose-50 px-5 py-4 text-[15px] text-rose-700">{{ jobsError }}</p>

          <div v-if="jobs.length">
            <JobCard v-for="job in jobs" :key="job.id" :job="job" />
          </div>

          <EmptyState
            v-else-if="!jobsError"
            title="No jobs published yet"
            message="Nothing has been published so far. Check back shortly, or post the first role."
          >
            <RouterLink :to="{ name: 'job-new' }" class="btn-primary">Post a job</RouterLink>
          </EmptyState>
        </div>
      </div>
    </section>

    <!-- Dark band — the one place the design allows itself contrast.
         Headings carry an explicit colour: the base rule sets headings to
         slate-900, which beats the inherited white and renders them invisible. -->
    <section class="bg-slate-950">
      <div class="shell py-24 text-center sm:py-32">
        <h2 class="display text-[36px] text-white sm:text-[52px]">Ready when you are.</h2>
        <p class="mx-auto mt-5 max-w-xl text-[19px] leading-relaxed text-slate-400">
          Whether you are hiring or looking, the process takes minutes.
        </p>
        <div class="mt-9 flex flex-wrap justify-center gap-3">
          <RouterLink :to="{ name: 'register' }" class="btn-on-dark btn-lg">Create a profile</RouterLink>
          <RouterLink
            :to="{ name: 'job-new' }"
            class="btn btn-lg border border-slate-700 text-white hover:bg-slate-900"
          >
            Post a job
          </RouterLink>
        </div>
      </div>
    </section>
  </div>
</template>
