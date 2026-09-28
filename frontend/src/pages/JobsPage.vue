<script setup lang="ts">
import { onMounted, reactive, ref, watch } from 'vue'
import { onServerPrefetch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
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
</script>

<template>
  <div class="mx-auto max-w-6xl px-4 py-8 sm:px-6">
    <div class="mb-6">
      <h1 class="text-2xl font-bold text-slate-900 sm:text-3xl">Find remote work</h1>
      <p class="mt-1 text-slate-500">Filter by skill, pay and job type. New jobs every day.</p>
    </div>

    <JobFilters v-model="filters" :categories="categories" :total="result?.totalCount" />

    <div class="mt-6 space-y-4">
      <AlertBox v-if="error" :message="error" type="error" />

      <Spinner v-if="loading && !result" label="Loading jobs…" />

      <template v-if="result">
        <EmptyState
          v-if="result.items.length === 0"
          title="No jobs match your filters"
          message="Try removing a filter or searching for a broader term."
        />

        <JobCard v-for="job in result.items" :key="job.id" :job="job" />

        <div class="pt-2">
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
</template>
