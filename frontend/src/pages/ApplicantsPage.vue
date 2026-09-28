<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { applicationsApi } from '../api/applications'
import { jobsApi } from '../api/jobs'
import type { ApplicantDto, JobDetailDto } from '../api/types'
import AlertBox from '../components/common/AlertBox.vue'
import EmptyState from '../components/common/EmptyState.vue'
import Spinner from '../components/common/Spinner.vue'
import StatusBadge from '../components/common/StatusBadge.vue'
import { meApi } from '../api/account'
import { errorMessage, formatDate, formatPay, humanize } from '../composables/useFormat'
import { useSeo } from '../composables/useSeo'

useSeo({ title: 'Review applicants', noindex: true })

const route = useRoute()
const jobId = computed(() => String(route.params.jobId))

const job = ref<JobDetailDto | null>(null)
const applicants = ref<ApplicantDto[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const busyId = ref<string | null>(null)
const notice = ref<string | null>(null)

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const [jobDetail, list] = await Promise.all([jobsApi.detail(jobId.value), applicationsApi.applicants(jobId.value)])
    job.value = jobDetail
    applicants.value = list
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    loading.value = false
  }
}

onMounted(() => void load())

async function decide(applicant: ApplicantDto, status: 'Shortlisted' | 'Rejected'): Promise<void> {
  let reason: string | null = null
  if (status === 'Rejected') {
    reason = window.prompt('Reason for rejection (shared with the candidate):', 'Role filled')
    if (reason === null) return
  }

  busyId.value = applicant.id
  notice.value = null
  error.value = null
  try {
    await applicationsApi.decide(applicant.id, status, reason ?? undefined)
    notice.value = `${applicant.fullName} marked as ${status.toLowerCase()}.`
    await load()
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    busyId.value = null
  }
}

async function downloadResume(applicant: ApplicantDto): Promise<void> {
  if (!applicant.resumeFileId) return
  try {
    const blob = await meApi.resumeBlob(applicant.resumeFileId)
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `${applicant.fullName.replace(/\s+/g, '-').toLowerCase()}-resume`
    link.click()
    URL.revokeObjectURL(url)
  } catch (caught) {
    error.value = errorMessage(caught)
  }
}
</script>

<template>
  <div class="mx-auto max-w-4xl px-4 py-8 sm:px-6">
    <RouterLink :to="{ name: 'dashboard' }" class="text-sm text-brand-600 hover:underline">← Back to dashboard</RouterLink>

    <div class="mt-3">
      <h1 class="text-2xl font-bold text-slate-900">Applicants</h1>
      <p v-if="job" class="mt-1 text-sm text-slate-500">
        {{ job.title }} · {{ job.categoryName }} · {{ formatPay(job) }} ·
        <span class="align-middle"><StatusBadge :status="job.status" /></span>
      </p>
    </div>

    <div class="mt-6">
      <Spinner v-if="loading" label="Loading applicants…" />

      <template v-else>
        <AlertBox v-if="error" :message="error" class="mb-4" />
        <AlertBox v-if="notice" type="success" :message="notice" class="mb-4" />

        <EmptyState
          v-if="applicants.length === 0"
          title="No applications yet"
          message="When workers apply, they'll appear here with their skills, rates and resume."
        />

        <div v-else class="space-y-4">
          <article v-for="applicant in applicants" :key="applicant.id" class="card">
            <div class="flex flex-wrap items-start justify-between gap-3">
              <div>
                <RouterLink
                  :to="{ name: 'worker-profile', params: { slug: applicant.workerSlug } }"
                  class="font-semibold text-slate-900 hover:text-brand-600"
                >
                  {{ applicant.fullName }}
                </RouterLink>
                <p class="text-sm text-slate-500">{{ applicant.headline }}</p>
                <p class="mt-0.5 text-xs text-slate-400">Applied {{ formatDate(applicant.appliedAt) }}</p>
              </div>
              <StatusBadge :status="applicant.status" />
            </div>

            <div class="mt-3 flex flex-wrap gap-2 text-xs text-slate-500">
              <span class="chip">{{ humanize(applicant.availability) }}</span>
              <span class="chip bg-emerald-50 text-emerald-700">
                {{ applicant.currency }} {{ applicant.rateMin }}–{{ applicant.rateMax }}
              </span>
              <span v-for="skill in applicant.skills.slice(0, 6)" :key="skill" class="chip bg-brand-50 text-brand-700">
                {{ skill }}
              </span>
            </div>

            <p v-if="applicant.coverLetter" class="mt-3 whitespace-pre-line rounded-lg bg-slate-50 p-3 text-sm text-slate-600">
              {{ applicant.coverLetter }}
            </p>

            <div class="mt-3 grid gap-1 text-sm text-slate-600 sm:grid-cols-2">
              <p>
                📧
                <a v-if="applicant.email" :href="`mailto:${applicant.email}`" class="text-brand-600 hover:underline">
                  {{ applicant.email }}
                </a>
                <span v-else class="text-slate-400">hidden until shortlisted</span>
              </p>
              <p v-if="applicant.phoneNumber">📞 {{ applicant.phoneNumber }}</p>
            </div>

            <div class="mt-4 flex flex-wrap items-center gap-2">
              <button
                v-if="applicant.status === 'Submitted'"
                class="btn-primary text-xs"
                type="button"
                :disabled="busyId === applicant.id"
                @click="decide(applicant, 'Shortlisted')"
              >
                Shortlist
              </button>
              <button
                v-if="applicant.status === 'Submitted' || applicant.status === 'Shortlisted'"
                class="btn-secondary text-xs"
                type="button"
                :disabled="busyId === applicant.id"
                @click="decide(applicant, 'Rejected')"
              >
                Reject
              </button>
              <button
                v-if="applicant.resumeFileId"
                class="btn-secondary text-xs"
                type="button"
                @click="downloadResume(applicant)"
              >
                Download resume
              </button>
              <RouterLink class="btn-secondary text-xs" :to="{ name: 'worker-profile', params: { slug: applicant.workerSlug } }">
                View profile
              </RouterLink>
            </div>

            <p v-if="applicant.status === 'Rejected' && applicant.rejectionReason" class="mt-2 text-xs text-rose-600">
              Rejection reason sent: {{ applicant.rejectionReason }}
            </p>
          </article>
        </div>
      </template>
    </div>
  </div>
</template>
