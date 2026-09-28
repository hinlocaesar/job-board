<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { onServerPrefetch } from 'vue'
import { RouterLink } from 'vue-router'
import { jobsApi } from '../api/jobs'
import { profilesApi } from '../api/profiles'
import type { JobMineDto } from '../api/types'
import AlertBox from '../components/common/AlertBox.vue'
import EmptyState from '../components/common/EmptyState.vue'
import Spinner from '../components/common/Spinner.vue'
import StatusBadge from '../components/common/StatusBadge.vue'
import { errorMessage, formatDate, formatPay, humanize } from '../composables/useFormat'
import { useSeo } from '../composables/useSeo'

useSeo({ title: 'Employer dashboard', noindex: true })

const jobs = ref<JobMineDto[]>([])
const hasProfile = ref(true)
const loading = ref(true)
const error = ref<string | null>(null)
const busyId = ref<string | null>(null)

// First-time employer profile setup (only when none exists yet)
const setup = ref({ companyName: '', website: '', description: '', country: '' })
const savingProfile = ref(false)

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    await profilesApi.currentEmployer()
    hasProfile.value = true
    jobs.value = await jobsApi.mine()
  } catch (caught) {
    const status = (caught as { status?: number })?.status
    if (status === 404 || status === 400) {
      hasProfile.value = false
    } else {
      error.value = errorMessage(caught)
    }
  } finally {
    loading.value = false
  }
}

onServerPrefetch(() => load())
onMounted(() => void load())

async function createProfile(): Promise<void> {
  savingProfile.value = true
  error.value = null
  try {
    await profilesApi.createEmployer({
      companyName: setup.value.companyName.trim(),
      website: setup.value.website.trim() || null,
      description: setup.value.description.trim() || null,
      country: setup.value.country.trim() || null,
      logoFileId: null,
    })
    await load()
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    savingProfile.value = false
  }
}

async function closeJob(job: JobMineDto): Promise<void> {
  if (!window.confirm(`Close “${job.title}”? Applicants can no longer apply.`)) return
  busyId.value = job.id
  try {
    await jobsApi.close(job.id)
    await load()
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    busyId.value = null
  }
}
</script>

<template>
  <div class="mx-auto max-w-5xl px-4 py-8 sm:px-6">
    <div class="flex flex-wrap items-center justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold text-slate-900">Employer dashboard</h1>
        <p class="mt-1 text-sm text-slate-500">Manage your job posts and review applicants.</p>
      </div>
      <div class="flex gap-2">
        <RouterLink v-if="hasProfile" class="btn-secondary" :to="{ name: 'employer-profile' }">Company profile</RouterLink>
        <RouterLink v-if="hasProfile" class="btn-primary" :to="{ name: 'job-new' }">+ Post a job</RouterLink>
      </div>
    </div>

    <div class="mt-6">
      <Spinner v-if="loading" label="Loading your jobs…" />
      <AlertBox v-else-if="error" :message="error" />

      <!-- First run: create the company profile -->
      <template v-else-if="!hasProfile">
        <div class="card max-w-xl">
          <h2 class="font-semibold text-slate-900">Create your company profile</h2>
          <p class="mt-1 text-sm text-slate-500">Required before posting jobs — candidates see this on every listing.</p>

          <form class="mt-4 space-y-4" @submit.prevent="createProfile">
            <div>
              <label class="label" for="companyName">Company name</label>
              <input id="companyName" v-model="setup.companyName" class="input" required />
            </div>
            <div class="grid gap-4 sm:grid-cols-2">
              <div>
                <label class="label" for="website">Website</label>
                <input id="website" v-model="setup.website" type="url" class="input" placeholder="https://…" />
              </div>
              <div>
                <label class="label" for="country">Country</label>
                <input id="country" v-model="setup.country" class="input" />
              </div>
            </div>
            <div>
              <label class="label" for="description">About the company</label>
              <textarea id="description" v-model="setup.description" class="input min-h-24" />
            </div>
            <button class="btn-primary" type="submit" :disabled="savingProfile">
              {{ savingProfile ? 'Saving…' : 'Create profile' }}
            </button>
          </form>
        </div>
      </template>

      <EmptyState
        v-else-if="jobs.length === 0"
        title="No jobs yet"
        message="Post your first role — it goes live after a quick moderation review."
      >
        <RouterLink class="btn-primary" :to="{ name: 'job-new' }">Post a job</RouterLink>
      </EmptyState>

      <div v-else class="space-y-4">
        <article v-for="job in jobs" :key="job.id" class="card">
          <div class="flex flex-wrap items-start justify-between gap-3">
            <div>
              <div class="flex flex-wrap items-center gap-2">
                <RouterLink
                  :to="{ name: 'job-detail', params: { slug: job.slug } }"
                  class="font-semibold text-slate-900 hover:text-brand-600"
                >
                  {{ job.title }}
                </RouterLink>
                <StatusBadge :status="job.status" />
              </div>
              <p class="mt-1 text-sm text-slate-500">
                {{ job.categoryName }} · {{ humanize(job.jobType) }} · {{ formatPay(job) }}
              </p>
              <p class="mt-1 text-xs text-slate-400">
                Created {{ formatDate(job.createdAt) }}
                <span v-if="job.publishedAt"> · published {{ formatDate(job.publishedAt) }}</span>
              </p>
            </div>

            <div class="text-right text-sm">
              <p class="text-slate-600">
                <strong>{{ job.applicantCount }}</strong> applicant{{ job.applicantCount === 1 ? '' : 's' }}
              </p>
              <p class="text-slate-400">{{ job.viewCount }} view{{ job.viewCount === 1 ? '' : 's' }}</p>
            </div>
          </div>

          <p
            v-if="job.status === 'Pending'"
            class="mt-3 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-800"
          >
            Awaiting moderation — this job is not visible on the public board yet.
          </p>
          <p v-if="job.status === 'Rejected'" class="mt-3 rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-700">
            Rejected by moderation{{ job.statusReason ? `: ${job.statusReason}` : '' }} — edit and resubmit.
          </p>

          <div class="mt-4 flex flex-wrap gap-2">
            <RouterLink class="btn-secondary text-xs" :to="{ name: 'applicants', params: { jobId: job.id } }">
              Review applicants ({{ job.applicantCount }})
            </RouterLink>
            <RouterLink class="btn-secondary text-xs" :to="{ name: 'job-edit', params: { id: job.id } }">
              Edit
            </RouterLink>
            <RouterLink class="btn-secondary text-xs" :to="{ name: 'job-detail', params: { slug: job.slug } }">
              Preview
            </RouterLink>
            <button
              v-if="job.status === 'Published' || job.status === 'Pending'"
              class="text-xs text-rose-600 hover:underline"
              type="button"
              :disabled="busyId === job.id"
              @click="closeJob(job)"
            >
              {{ busyId === job.id ? 'Closing…' : 'Close job' }}
            </button>
          </div>
        </article>
      </div>
    </div>
  </div>
</template>
