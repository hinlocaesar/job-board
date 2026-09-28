<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { onServerPrefetch } from 'vue'
import { RouterLink } from 'vue-router'
import { applicationsApi } from '../api/applications'
import type { MyApplicationDto } from '../api/types'
import AlertBox from '../components/common/AlertBox.vue'
import EmptyState from '../components/common/EmptyState.vue'
import Spinner from '../components/common/Spinner.vue'
import StatusBadge from '../components/common/StatusBadge.vue'
import { errorMessage, formatDate } from '../composables/useFormat'
import { useSeo } from '../composables/useSeo'

useSeo({ title: 'My applications', noindex: true })

const applications = ref<MyApplicationDto[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const busyId = ref<string | null>(null)

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    applications.value = await applicationsApi.mine()
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    loading.value = false
  }
}

onServerPrefetch(() => load())
onMounted(() => void load())

async function withdraw(id: string): Promise<void> {
  if (!window.confirm('Withdraw this application?')) return
  busyId.value = id
  try {
    await applicationsApi.withdraw(id)
    await load()
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    busyId.value = null
  }
}
</script>

<template>
  <div class="mx-auto max-w-4xl px-4 py-8 sm:px-6">
    <h1 class="text-2xl font-bold text-slate-900">My applications</h1>
    <p class="mt-1 text-sm text-slate-500">Track where each application stands.</p>

    <div class="mt-6">
      <Spinner v-if="loading" />
      <AlertBox v-else-if="error" :message="error" />

      <EmptyState
        v-else-if="applications.length === 0"
        title="No applications yet"
        message="Find a role you like and apply in one click."
      >
        <RouterLink class="btn-primary" :to="{ name: 'jobs' }">Browse jobs</RouterLink>
      </EmptyState>

      <div v-else class="space-y-4">
        <article v-for="application in applications" :key="application.id" class="card">
          <div class="flex flex-wrap items-start justify-between gap-3">
            <div>
              <RouterLink
                :to="{ name: 'job-detail', params: { slug: application.jobSlug } }"
                class="font-semibold text-slate-900 hover:text-brand-600"
              >
                {{ application.jobTitle }}
              </RouterLink>
              <p class="text-sm text-slate-500">{{ application.companyName }}</p>
              <p class="mt-1 text-xs text-slate-400">Applied {{ formatDate(application.appliedAt) }}</p>
            </div>

            <div class="flex items-center gap-3">
              <StatusBadge :status="application.status" />
              <button
                v-if="application.status === 'Submitted'"
                class="text-sm text-rose-600 hover:underline"
                type="button"
                :disabled="busyId === application.id"
                @click="withdraw(application.id)"
              >
                {{ busyId === application.id ? 'Withdrawing…' : 'Withdraw' }}
              </button>
            </div>
          </div>

          <p
            v-if="application.status === 'Rejected' && application.rejectionReason"
            class="mt-3 rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-700"
          >
            Reason: {{ application.rejectionReason }}
          </p>
          <p
            v-else-if="application.status === 'Shortlisted'"
            class="mt-3 rounded-lg bg-violet-50 px-3 py-2 text-sm text-violet-700"
          >
            🎉 You've been shortlisted — the employer has your contact details.
          </p>
        </article>
      </div>
    </div>
  </div>
</template>
