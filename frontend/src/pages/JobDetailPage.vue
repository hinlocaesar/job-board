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
import Icon from '../components/common/Icon.vue'
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
  <div class="border-b border-slate-200">
    <div class="shell py-4">
      <nav class="breadcrumb" aria-label="Breadcrumb">
        <RouterLink :to="{ name: 'home' }">Home</RouterLink>
        <span class="breadcrumb-sep" aria-hidden="true">›</span>
        <RouterLink :to="{ name: 'jobs' }">Jobs</RouterLink>
        <span class="breadcrumb-sep" aria-hidden="true">›</span>
        <span class="truncate text-slate-900">{{ job?.title ?? 'Job' }}</span>
      </nav>
    </div>
  </div>

  <div class="shell py-14 sm:py-20">
    <Spinner v-if="loading && !job" />

    <AlertBox v-else-if="error" :message="error" type="error">
      <p class="mt-2">
        <RouterLink :to="{ name: 'jobs' }" class="link">Browse all jobs</RouterLink>
      </p>
    </AlertBox>

    <template v-else-if="job">
      <!-- Title block -->
      <div class="grid gap-8 lg:grid-cols-[minmax(0,1fr)_320px] lg:gap-16">
        <div class="max-w-2xl">
          <div class="flex flex-wrap items-center gap-3">
            <span class="kicker">{{ job.categoryName }}</span>
            <StatusBadge v-if="job.status !== 'Published'" :status="job.status" />
          </div>

          <h1 class="display mt-4 text-[36px] sm:text-[52px]">{{ job.title }}</h1>

          <p class="mt-4 flex flex-wrap items-center gap-x-2.5 text-[19px] text-slate-600">
            <span class="text-slate-900">{{ job.companyName }}</span>
            <span class="text-slate-300" aria-hidden="true">·</span>
            <span>{{ humanize(job.region) }}</span>
            <template v-if="job.publishedAt">
              <span class="text-slate-300" aria-hidden="true">·</span>
              <span class="text-slate-500">Posted {{ formatDate(job.publishedAt) }}</span>
            </template>
          </p>

          <p class="nums mt-8 text-[34px] font-semibold tracking-tight text-slate-900">
            {{ formatPay(job) }}
          </p>

          <div class="mt-6 flex flex-wrap gap-2">
            <span class="chip">{{ humanize(job.jobType) }}</span>
            <span class="chip">{{ humanize(job.experienceLevel) }}</span>
            <span v-if="job.hoursPerWeek" class="chip nums">{{ job.hoursPerWeek }} hrs/week</span>
            <span v-if="job.closesAt" class="chip">Closes {{ formatDate(job.closesAt) }}</span>
          </div>
        </div>

        <!-- Apply rail -->
        <aside class="lg:sticky lg:top-20 lg:self-start">
          <div class="rounded-2xl bg-slate-100 p-6">
            <h2 class="text-[19px] font-semibold text-slate-900">Apply for this job</h2>

            <template v-if="!auth.isAuthenticated">
              <p class="muted mt-2 text-[15px]">Create a free worker account to apply.</p>
              <div class="mt-4 flex flex-col gap-2">
                <RouterLink class="btn-primary w-full" :to="{ name: 'register', query: { redirect: route.fullPath } }">
                  Register &amp; apply
                </RouterLink>
                <RouterLink class="btn-secondary w-full" :to="{ name: 'login', query: { redirect: route.fullPath } }">
                  I already have an account
                </RouterLink>
              </div>
            </template>

            <template v-else-if="!auth.isWorker">
              <p class="muted mt-2 text-[15px]">
                You are signed in as an {{ auth.roles[0] }}. Only worker accounts can apply to jobs.
              </p>
            </template>

            <template v-else>
              <form class="mt-4 space-y-3" @submit.prevent="apply">
                <div>
                  <label class="label" for="cover">Cover letter</label>
                  <textarea
                    id="cover"
                    v-model="coverLetter"
                    class="input min-h-32 text-[15px]"
                    placeholder="Introduce yourself and why you are a great fit…"
                    :disabled="applying || !!applyMessage"
                  />
                </div>
                <button class="btn-primary w-full" type="submit" :disabled="applying || !!applyMessage">
                  {{ applying ? 'Sending…' : applyMessage?.type === 'success' ? 'Applied' : 'Apply now' }}
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

          <!-- Job type and experience already appear as chips above. -->
          <dl class="mt-8">
            <div v-if="job.hoursPerWeek" class="meta-row">
              <dt class="meta-key">Hours per week</dt>
              <dd class="meta-value nums">{{ job.hoursPerWeek }} hrs</dd>
            </div>
            <div class="meta-row">
              <dt class="meta-key">Pay basis</dt>
              <dd class="meta-value">{{ humanize(job.payType) }}</dd>
            </div>
            <div v-if="job.closesAt" class="meta-row">
              <dt class="meta-key">Closes</dt>
              <dd class="meta-value">{{ formatDate(job.closesAt) }}</dd>
            </div>
            <div v-if="job.source" class="meta-row">
              <dt class="meta-key">Source</dt>
              <dd class="meta-value">{{ sourceLabel(job.source) }}</dd>
            </div>
          </dl>

          <div class="mt-8">
            <h2 class="text-[15px] font-semibold text-slate-900">About {{ job.companyName }}</h2>
            <p v-if="job.companyWebsite" class="mt-2 text-[15px]">
              <a
                :href="job.companyWebsite"
                target="_blank"
                rel="noopener nofollow"
                class="link inline-flex items-center gap-1 break-all"
              >
                {{ job.companyWebsite }}
                <Icon name="external-link" :size="13" />
              </a>
            </p>
            <p v-else class="muted mt-2 text-[15px]">No website provided.</p>
          </div>

          <div v-if="job.canEdit" class="mt-8 rounded-2xl bg-slate-100 p-5">
            <div class="grid grid-cols-2 gap-4">
              <div>
                <p class="text-[12px] text-slate-500">Views</p>
                <p class="nums mt-0.5 text-[24px] font-semibold text-slate-900">{{ job.viewCount ?? 0 }}</p>
              </div>
              <div>
                <p class="text-[12px] text-slate-500">Applicants</p>
                <p class="nums mt-0.5 text-[24px] font-semibold text-slate-900">{{ job.applicantCount ?? 0 }}</p>
              </div>
            </div>
            <RouterLink
              class="arrow-link mt-3 text-[15px]"
              :to="{ name: 'applicants', params: { jobId: job.id } }"
            >
              View applicants
              <Icon name="arrow-right" :size="15" />
            </RouterLink>
          </div>
        </aside>
      </div>

      <div
        v-if="job.status !== 'Published'"
        class="mt-10 flex flex-wrap items-center gap-2 rounded-2xl bg-amber-50 px-5 py-4 text-[15px] text-amber-900"
      >
        <Icon name="shield" :size="17" class="text-amber-700" />
        <span>
          <strong>Preview.</strong> This job is {{ job.status.toLowerCase() }} and is not visible on the public board.
          <span v-if="job.statusReason">Reason: {{ job.statusReason }}</span>
        </span>
        <RouterLink
          v-if="job.canEdit"
          :to="{ name: 'job-edit', params: { id: job.id } }"
          class="link ml-auto"
        >
          Edit job
        </RouterLink>
      </div>

      <!-- Description -->
      <article class="mt-12 max-w-2xl border-t border-slate-200 pt-8">
        <h2 class="section-title">About the role</h2>
        <p class="mt-5 whitespace-pre-line text-pretty text-[19px] leading-[1.7] text-slate-700">
          {{ job.description }}
        </p>

        <div v-if="job.skills?.length" class="mt-10">
          <h2 class="text-[19px] font-semibold text-slate-900">Skills</h2>
          <div class="mt-4 flex flex-wrap gap-2">
            <span v-for="skill in job.skills" :key="skill" class="chip">{{ skill }}</span>
          </div>
        </div>
      </article>
    </template>
  </div>
</template>
