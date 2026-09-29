<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { onServerPrefetch } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import type { CategoryDto, JobSearchQuery, JobSearchResult } from '../api/types'
import { categoriesApi, jobsApi } from '../api/jobs'
import JobCard from '../components/jobs/JobCard.vue'
import JobFilters from '../components/jobs/JobFilters.vue'
import AlertBox from '../components/common/AlertBox.vue'
import Spinner from '../components/common/Spinner.vue'
import EmptyState from '../components/common/EmptyState.vue'
import PaginationBar from '../components/common/PaginationBar.vue'
import { useSeo } from '../composables/useSeo'
import { errorMessage } from '../composables/useFormat'

useSeo({
  title: 'Remote jobs',
  description:
    'Browse remote jobs for Filipino talent — development, design, virtual assistance, writing and more. Search by skill, pay and job type.',
})

const route = useRoute()
const router = useRouter()

function queryValue(value: unknown): string | undefined {
  return typeof value === 'string' && value ? value : undefined
}

const filters = reactive<JobSearchQuery>({
  q: queryValue(route.query.q),
  category: queryValue(route.query.category),
  jobType: queryValue(route.query.jobType),
  experience: queryValue(route.query.experience),
  payType: queryValue(route.query.payType),
  minPay: route.query.minPay ? Number(route.query.minPay) : undefined,
  sort: queryValue(route.query.sort) ?? 'newest',
  page: route.query.page ? Number(route.query.page) : 1,
  pageSize: 10,
})

const categories = ref<CategoryDto[]>([])
const result = ref<JobSearchResult | null>(null)
const error = ref<string | null>(null)
const loading = ref(false)

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const [search, categoryList] = await Promise.all([
      jobsApi.search(filters),
      categories.value.length > 0 ? Promise.resolve(categories.value) : categoriesApi.list(),
    ])
    categories.value = categoryList
    result.value = search
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    loading.value = false
  }
}

onServerPrefetch(() => load())
onMounted(() => {
  if (!result.value) void load()
})

// Keep the URL shareable/bookmarkable; the request follows automatically.
watch(
  () => ({ ...filters }),
  async () => {
    const query: Record<string, string> = {}
    for (const [key, value] of Object.entries(filters)) {
      if (value !== undefined && value !== null && value !== '' && key !== 'pageSize') {
        query[key] = String(value)
      }
    }
    await router.replace({ query })
    await load()
  },
)

function goToPage(page: number): void {
  filters.page = page
  window.scrollTo({ top: 0, behavior: 'smooth' })
}

const activeCategory = computed(() =>
  categories.value.find((c) => c.slug === filters.category)?.name ?? null,
)

const heading = computed(() => activeCategory.value ?? 'Find remote work')

const countLabel = computed(() => {
  if (!result.value) return ''
  const { totalCount, page, totalPages } = result.value
  const noun = totalCount === 1 ? 'job' : 'jobs'
  if (totalPages <= 1) return `${totalCount.toLocaleString('en-US')} ${noun}`
  return `${totalCount.toLocaleString('en-US')} ${noun} · page ${page} of ${totalPages}`
})
</script>

<template>
  <div>
    <div class="border-b border-slate-200">
      <div class="shell py-4">
        <nav class="breadcrumb" aria-label="Breadcrumb">
          <RouterLink :to="{ name: 'home' }">Home</RouterLink>
          <span class="breadcrumb-sep" aria-hidden="true">›</span>
          <span class="font-medium text-slate-900">Jobs</span>
          <template v-if="activeCategory">
            <span class="breadcrumb-sep" aria-hidden="true">›</span>
            <span class="font-medium text-slate-900">{{ activeCategory }}</span>
          </template>
        </nav>
      </div>
    </div>

    <div class="shell py-14 sm:py-20">
      <div class="max-w-2xl">
        <h1 class="display text-[40px] sm:text-[52px]">{{ heading }}</h1>
        <p class="muted mt-4 text-[19px]">
          {{ countLabel }} — search and refine by skill, pay and job type.
        </p>
      </div>

      <div class="mt-10">
        <JobFilters v-model="filters" :categories="categories" :total="result?.totalCount" />
      </div>

      <div class="mt-12">
        <AlertBox v-if="error" :message="error" type="error" />

        <Spinner v-if="loading && !result" label="Loading jobs…" />

        <template v-if="result">
          <div v-if="result.items.length">
            <JobCard v-for="job in result.items" :key="job.id" :job="job" />
          </div>

          <EmptyState
            v-else
            title="No jobs match your filters"
            message="Try removing a filter or searching for a broader term."
          />

          <div class="mt-10">
            <PaginationBar
              :page="result.page"
              :total-pages="result.totalPages"
              :disabled="loading"
              @page-change="goToPage"
            />
          </div>
        </template>
      </div>
    </div>
  </div>
</template>
