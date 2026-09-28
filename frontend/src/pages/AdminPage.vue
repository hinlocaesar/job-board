<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { adminApi } from '../api/admin'
import type { AuditDto, JobQueueDto, UserSummaryDto } from '../api/types'
import AlertBox from '../components/common/AlertBox.vue'
import EmptyState from '../components/common/EmptyState.vue'
import Spinner from '../components/common/Spinner.vue'
import StatusBadge from '../components/common/StatusBadge.vue'
import { errorMessage, formatDate, humanize } from '../composables/useFormat'
import { useSeo } from '../composables/useSeo'

useSeo({ title: 'Admin · moderation', noindex: true })

type Tab = 'queue' | 'users' | 'audit'

const tab = ref<Tab>('queue')
const queue = ref<JobQueueDto[]>([])
const users = ref<UserSummaryDto[]>([])
const audit = ref<AuditDto[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const notice = ref<string | null>(null)
const busyId = ref<string | null>(null)

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const [queueResult, userResult, auditResult] = await Promise.all([
      adminApi.queue('Pending'),
      adminApi.users(),
      adminApi.audit(100),
    ])
    queue.value = queueResult
    users.value = userResult
    audit.value = auditResult
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    loading.value = false
  }
}

onMounted(() => void load())

async function moderate(job: JobQueueDto, action: 'approve' | 'reject' | 'flag'): Promise<void> {
  let reason: string | null = null
  if (action !== 'approve') {
    reason = window.prompt(`${humanize(action)} — reason (visible to the employer):`, action === 'reject' ? 'Does not meet our posting guidelines' : 'Under review')
    if (reason === null) return
  }

  busyId.value = job.id
  notice.value = null
  error.value = null
  try {
    if (action === 'approve') await adminApi.approve(job.id)
    else if (action === 'reject') await adminApi.reject(job.id, reason ?? '')
    else await adminApi.flag(job.id, reason ?? '')
    notice.value = `“${job.title}” ${action}${action === 'approve' ? 'd' : action === 'reject' ? 'ed' : 'ged'}.`
    await load()
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    busyId.value = null
  }
}

async function toggleBan(user: UserSummaryDto): Promise<void> {
  const banning = user.accountStatus !== 'Banned'
  if (banning) {
    const reason = window.prompt(`Ban ${user.email}? Reason:`, 'Abuse')
    if (reason === null) return
    busyId.value = user.id
    try {
      await adminApi.ban(user.id, reason)
      notice.value = `${user.email} banned.`
    } catch (caught) {
      error.value = errorMessage(caught)
    } finally {
      busyId.value = null
    }
  } else {
    busyId.value = user.id
    try {
      await adminApi.unban(user.id)
      notice.value = `${user.email} unbanned.`
    } catch (caught) {
      error.value = errorMessage(caught)
    } finally {
      busyId.value = null
    }
  }
  await load()
}

const tabs: { id: Tab; label: string }[] = [
  { id: 'queue', label: 'Job moderation' },
  { id: 'users', label: 'Users' },
  { id: 'audit', label: 'Audit log' },
]
</script>

<template>
  <div class="mx-auto max-w-5xl px-4 py-8 sm:px-6">
    <h1 class="text-2xl font-bold text-slate-900">Admin</h1>
    <p class="mt-1 text-sm text-slate-500">Moderate listings, manage accounts and review the audit trail.</p>

    <div class="mt-5 flex gap-1 border-b border-slate-200">
      <button
        v-for="entry in tabs"
        :key="entry.id"
        class="px-4 py-2 text-sm font-medium"
        :class="tab === entry.id ? 'border-b-2 border-brand-600 text-brand-700' : 'text-slate-500 hover:text-slate-800'"
        type="button"
        @click="tab = entry.id"
      >
        {{ entry.label }}
        <span v-if="entry.id === 'queue'" class="ml-1 rounded-full bg-amber-100 px-1.5 text-xs text-amber-800">
          {{ queue.length }}
        </span>
      </button>
    </div>

    <div class="mt-6">
      <Spinner v-if="loading" label="Loading…" />
      <template v-else>
        <AlertBox v-if="error" :message="error" class="mb-4" />
        <AlertBox v-if="notice" type="success" :message="notice" class="mb-4" />

        <!-- Moderation queue -->
        <div v-if="tab === 'queue'" class="space-y-4">
          <EmptyState v-if="queue.length === 0" title="Queue is clear" message="No jobs are waiting for review." />
          <article v-for="job in queue" :key="job.id" class="card">
            <div class="flex flex-wrap items-start justify-between gap-3">
              <div>
                <div class="flex items-center gap-2">
                  <RouterLink
                    :to="{ name: 'job-detail', params: { slug: job.slug } }"
                    class="font-semibold text-slate-900 hover:text-brand-600"
                  >
                    {{ job.title }}
                  </RouterLink>
                  <StatusBadge :status="job.status" />
                </div>
                <p class="mt-1 text-sm text-slate-500">
                  {{ job.companyName }} · {{ job.categoryName }} · submitted {{ formatDate(job.createdAt) }}
                </p>
                <p v-if="job.statusReason" class="mt-1 text-xs text-rose-600">Previous reason: {{ job.statusReason }}</p>
              </div>

              <div class="flex gap-2">
                <button class="btn-primary text-xs" type="button" :disabled="busyId === job.id" @click="moderate(job, 'approve')">
                  Approve
                </button>
                <button class="btn-secondary text-xs" type="button" :disabled="busyId === job.id" @click="moderate(job, 'flag')">
                  Flag
                </button>
                <button class="btn-danger text-xs" type="button" :disabled="busyId === job.id" @click="moderate(job, 'reject')">
                  Reject
                </button>
              </div>
            </div>
          </article>
        </div>

        <!-- Users -->
        <div v-else-if="tab === 'users'" class="overflow-x-auto">
          <table class="min-w-full text-sm">
            <thead>
              <tr class="border-b border-slate-200 text-left text-xs uppercase text-slate-400">
                <th class="py-2 pr-4">User</th>
                <th class="py-2 pr-4">Roles</th>
                <th class="py-2 pr-4">Status</th>
                <th class="py-2 pr-4">Joined</th>
                <th class="py-2 pr-4">Jobs</th>
                <th class="py-2 pr-4">Applications</th>
                <th class="py-2">Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="user in users" :key="user.id" class="border-b border-slate-100">
                <td class="py-2 pr-4">
                  <p class="font-medium text-slate-800">{{ user.fullName ?? '—' }}</p>
                  <p class="text-xs text-slate-400">{{ user.email }}</p>
                </td>
                <td class="py-2 pr-4">{{ user.roles.join(', ') }}</td>
                <td class="py-2 pr-4"><StatusBadge :status="user.accountStatus" /></td>
                <td class="py-2 pr-4 text-slate-500">{{ formatDate(user.createdAt) }}</td>
                <td class="py-2 pr-4">{{ user.jobCount }}</td>
                <td class="py-2 pr-4">{{ user.applicationCount }}</td>
                <td class="py-2">
                  <button
                    v-if="!user.roles.includes('admin')"
                    class="text-xs font-medium"
                    :class="user.accountStatus === 'Banned' ? 'text-emerald-600 hover:underline' : 'text-rose-600 hover:underline'"
                    type="button"
                    :disabled="busyId === user.id"
                    @click="toggleBan(user)"
                  >
                    {{ user.accountStatus === 'Banned' ? 'Unban' : 'Ban' }}
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Audit log -->
        <div v-else class="overflow-x-auto">
          <table class="min-w-full text-sm">
            <thead>
              <tr class="border-b border-slate-200 text-left text-xs uppercase text-slate-400">
                <th class="py-2 pr-4">When</th>
                <th class="py-2 pr-4">Action</th>
                <th class="py-2 pr-4">Entity</th>
                <th class="py-2 pr-4">Details</th>
                <th class="py-2">IP</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="entry in audit" :key="entry.id" class="border-b border-slate-100 align-top">
                <td class="py-2 pr-4 text-slate-500">{{ formatDate(entry.createdAt, { dateStyle: 'medium', timeStyle: 'short' }) }}</td>
                <td class="py-2 pr-4 font-medium text-slate-700">{{ entry.action }}</td>
                <td class="py-2 pr-4 text-slate-500">{{ entry.entityType }}/{{ entry.entityId.slice(0, 8) }}</td>
                <td class="py-2 pr-4 text-slate-600">{{ entry.details ?? '—' }}</td>
                <td class="py-2 text-slate-400">{{ entry.ip ?? '—' }}</td>
              </tr>
            </tbody>
          </table>
          <EmptyState v-if="audit.length === 0" title="No audit entries yet" />
        </div>
      </template>
    </div>
  </div>
</template>
