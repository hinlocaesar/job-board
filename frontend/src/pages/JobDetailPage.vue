<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { onServerPrefetch } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { jobsApi } from '../api/jobs'
import { applicationsApi } from '../api/applications'
import type { JobDetailDto } from '../api/types'
import AlertBox from '../components/common/AlertBox.vue'
import Spinner from '../components/common/Spinner.vue'
import StatusBadge from '../components/common/StatusBadge.vue'
import { siteOrigin, useSeo } from '../composables/useSeo'
import { errorMessage, formatDate, formatPay, humanize } from '../composables/useFormat'
import { useAuthStore } from '../stores/auth'

const route = useRoute()
const auth = useAuthStore()

const slug = computed(() => String(route.params.slug))
const job = ref<JobDetailDto | null>(null)
const error = ref<string | null>(null)
const loading = ref(true)

const coverLetter = ref('')
const applying = ref(false)
const applyMessage = ref<{ type: 'success' | 'error'; text: string } | null>(null)

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    job.value = await jobsApi.detail(slug.value)
    const description = (job.value.description ?? '').replace(/\s+/g, ' ').trim()
    useSeo({
      title: job.value.title,
      description: description.slice(0, 155) || `${job.value.title} at ${job.value.companyName}`,
      canonical: `${siteOrigin()}/jobs/${job.value.slug}`,
      ogType: 'article',
    })
  } catch (caught) {
    job.value = null
    error.value = errorMessage(caught)
  } finally {
    loading.value = false
  }
}

onServerPrefetch(() => load())
onMounted(() => {
  if (!job.value) void load()
})
watch(slug, () => void load())

async function apply(): Promise<void> {
  if (!job.value) return
  applying.value = true
  applyMessage.value = null
  try {
    await applicationsApi.apply(job.value.id, coverLetter.value.trim())
    applyMessage.value = { type: 'success', text: 'Application sent! Track it under “My applications”.' }
    coverLetter.value = ''
  } catch (caught) {
    const message = errorMessage(caught)
    applyMessage.value = {
      type: 'error',
      text:
        caught && typeof caught === 'object' && 'status' in caught && (caught as { status: number }).status === 409
          ? 'You already applied to this job.'
          : message,
    }
  } finally {
    applying.value = false
  }
}

function applyErrorText(): string | undefined {
  return applyMessage.value?.text ?? undefined
}

/** Feed keys are stored in `jobs.source`; show something a reader recognises. */
const SOURCE_LABELS: Record<string, string> = {
  remotive: 'Remotive',
  jobicy: 'Jobicy',
  remoteok: 'RemoteOK',
  arbeitnow: 'Arbeitnow',
}

function sourceLabel(source: string): string {
  return SOURCE_LABELS[source.toLowerCase()] ?? source
}
</script>

<template>
  <div class="mx-auto max-w-4xl px-4 py-8 sm:px-6">
    <RouterLink :to="{ name: 'jobs' }" class="text-sm text-brand-600 hover:underline">← Back to jobs</RouterLink>

    <Spinner v-if="loading && !job" />

    <AlertBox v-else-if="error" :message="error" type="error">
      <template #default>
        <p class="mt-2">
          <RouterLink :to="{ name: 'jobs' }" class="font-medium underline">Browse all jobs</RouterLink>
        </p>
      </template>
    </AlertBox>

    <template v-else-if="job">
      <header class="mt-4 border-b border-slate-200 pb-6">
        <div class="flex flex-wrap items-start justify-between gap-4">
          <div>
            <div class="flex flex-wrap items-center gap-2">
              <h1 class="text-2xl font-bold text-slate-900 sm:text-3xl">{{ job.title }}</h1>
              <StatusBadge v-if="job.status !== 'Published'" :status="job.status" />
            </div>
            <p class="mt-1 text-slate-500">
              {{ job.companyName }} · {{ job.categoryName }}
              <span v-if="job.source" class="ml-1 text-xs text-slate-400">
                (imported from {{ sourceLabel(job.source) }})
              </span>
            </p>
          </div>
          <div class="text-right">
            <p class="text-lg font-semibold text-emerald-700">{{ formatPay(job) }}</p>
            <p v-if="job.publishedAt" class="text-xs text-slate-400">Posted {{ formatDate(job.publishedAt) }}</p>
          </div>
        </div>

        <div class="mt-4 flex flex-wrap gap-2 text-xs text-slate-500">
          <span class="chip">{{ humanize(job.jobType) }}</span>
          <span class="chip">{{ humanize(job.experienceLevel) }}</span>
          <span class="chip">{{ humanize(job.region) }}</span>
          <span v-if="job.hoursPerWeek" class="chip">{{ job.hoursPerWeek }} hrs/week</span>
          <span v-if="job.closesAt" class="chip">Closes {{ formatDate(job.closesAt) }}</span>
          <span v-if="job.source" class="chip bg-sky-50 text-sky-700">Imported · {{ sourceLabel(job.source) }}</span>
        </div>
      </header>

      <div
        v-if="job.status !== 'Published'"
        class="mt-4 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800"
      >
        <strong>Preview.</strong> This job is {{ job.status.toLowerCase() }} and is not visible on the public board.
        <span v-if="job.statusReason">Reason: {{ job.statusReason }}</span>
        <RouterLink v-if="job.canEdit" :to="{ name: 'job-edit', params: { id: job.id } }" class="ml-2 font-medium underline">
          Edit job
        </RouterLink>
      </div>

      <div class="mt-6 grid gap-8 lg:grid-cols-[1fr_320px]">
        <article>
          <h2 class="text-lg font-semibold text-slate-900">Job description</h2>
          <p class="mt-3 whitespace-pre-line leading-relaxed text-slate-700">{{ job.description }}</p>

          <div v-if="job.skills?.length" class="mt-6">
            <h2 class="text-lg font-semibold text-slate-900">Skills</h2>
            <div class="mt-2 flex flex-wrap gap-2">
              <span v-for="skill in job.skills" :key="skill" class="chip bg-brand-50 text-brand-700">{{ skill }}</span>
            </div>
          </div>
        </article>

        <aside class="space-y-4">
          <div class="card">
            <h2 class="font-semibold text-slate-900">Apply for this job</h2>

            <template v-if="!auth.isAuthenticated">
              <p class="mt-2 text-sm text-slate-600">Create a worker account to apply.</p>
              <div class="mt-3 flex gap-2">
                <RouterLink class="btn-primary flex-1" :to="{ name: 'register', query: { redirect: route.fullPath } }">
                  Sign up
                </RouterLink>
                <RouterLink class="btn-secondary" :to="{ name: 'login', query: { redirect: route.fullPath } }">
                  Log in
                </RouterLink>
              </div>
            </template>

            <template v-else-if="!auth.isWorker">
              <p class="mt-2 text-sm text-slate-600">
                You are signed in as an {{ auth.roles[0] }}. Only worker accounts can apply to jobs.
              </p>
            </template>

            <template v-else>
              <form class="mt-3 space-y-3" @submit.prevent="apply">
                <div>
                  <label class="label" for="cover">Cover letter</label>
                  <textarea
                    id="cover"
                    v-model="coverLetter"
                    class="input min-h-32"
                    placeholder="Introduce yourself and why you are a great fit…"
                    :disabled="applying || !!applyMessage"
                  />
                </div>
                <button class="btn-primary w-full" type="submit" :disabled="applying || !!applyMessage">
                  {{ applying ? 'Sending…' : applyMessage?.type === 'success' ? 'Applied ✓' : 'Apply now' }}
                </button>
              </form>

              <AlertBox
                v-if="applyErrorText()"
                class="mt-3"
                :type="applyMessage?.type === 'success' ? 'success' : 'error'"
                :message="applyErrorText()"
              />
            </template>
          </div>

          <div v-if="job.canEdit" class="card text-sm text-slate-600">
            <p><strong>Views:</strong> {{ job.viewCount ?? 0 }}</p>
            <p><strong>Applicants:</strong> {{ job.applicantCount ?? 0 }}</p>
            <RouterLink class="mt-2 inline-block font-medium text-brand-600 hover:underline" :to="{ name: 'applicants', params: { jobId: job.id } }">
              View applicants →
            </RouterLink>
          </div>

          <div class="card text-sm text-slate-600">
            <h3 class="font-semibold text-slate-900">About {{ job.companyName }}</h3>
            <p v-if="job.companyWebsite" class="mt-2 break-all">
              <a :href="job.companyWebsite" target="_blank" rel="noopener nofollow" class="text-brand-600 hover:underline">
                {{ job.companyWebsite }}
              </a>
            </p>
            <p class="mt-2 text-xs text-slate-400">{{ job.categoryName }} · {{ humanize(job.region) }}</p>
          </div>
        </aside>
      </div>
    </template>
  </div>
</template>
