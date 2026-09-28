<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { meApi } from '../api/account'
import type { MeDto } from '../api/types'
import AlertBox from '../components/common/AlertBox.vue'
import Spinner from '../components/common/Spinner.vue'
import { errorMessage, formatDate } from '../composables/useFormat'
import { useSeo } from '../composables/useSeo'
import { useAuthStore } from '../stores/auth'

useSeo({ title: 'My account', noindex: true })

const auth = useAuthStore()
const router = useRouter()

const me = ref<MeDto | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)
const notice = ref<string | null>(null)

const confirmText = ref('')
const password = ref('')
const deleting = ref(false)
const exporting = ref(false)

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    me.value = await meApi.get()
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    loading.value = false
  }
}

onMounted(() => void load())

async function exportData(): Promise<void> {
  exporting.value = true
  error.value = null
  try {
    const data = await meApi.export()
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `jobboard-export-${new Date().toISOString().slice(0, 10)}.json`
    link.click()
    URL.revokeObjectURL(url)
    notice.value = 'Your data export has been downloaded.'
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    exporting.value = false
  }
}

async function deleteAccount(): Promise<void> {
  if (confirmText.value !== 'DELETE') {
    error.value = 'Type DELETE to confirm.'
    return
  }
  deleting.value = true
  error.value = null
  try {
    await meApi.remove({ password: password.value })
    await auth.logout()
    await router.push({ name: 'home' })
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    deleting.value = false
  }
}
</script>

<template>
  <div class="mx-auto max-w-2xl px-4 py-8 sm:px-6">
    <h1 class="text-2xl font-bold text-slate-900">My account</h1>

    <Spinner v-if="loading" label="Loading…" />

    <template v-else-if="me">
      <section class="card mt-6">
        <h2 class="font-semibold text-slate-900">Profile</h2>
        <dl class="mt-3 space-y-2 text-sm">
          <div class="flex justify-between gap-4">
            <dt class="text-slate-500">Name</dt>
            <dd class="font-medium text-slate-800">{{ me.user.fullName }}</dd>
          </div>
          <div class="flex justify-between gap-4">
            <dt class="text-slate-500">E-mail</dt>
            <dd class="font-medium text-slate-800">{{ me.user.email }}</dd>
          </div>
          <div class="flex justify-between gap-4">
            <dt class="text-slate-500">E-mail verified</dt>
            <dd>{{ me.user.emailVerified ? '✅ Yes' : '⚠️ No' }}</dd>
          </div>
          <div class="flex justify-between gap-4">
            <dt class="text-slate-500">Roles</dt>
            <dd>{{ me.user.roles.join(', ') }}</dd>
          </div>
          <div class="flex justify-between gap-4">
            <dt class="text-slate-500">Account status</dt>
            <dd>{{ me.user.accountStatus }}</dd>
          </div>
          <div class="flex justify-between gap-4">
            <dt class="text-slate-500">Privacy consent</dt>
            <dd class="text-right">
              {{ me.privacyConsentAt ? formatDate(me.privacyConsentAt) : '—' }}
              <span v-if="me.privacyPolicyVersion" class="block text-xs text-slate-400">
                policy {{ me.privacyPolicyVersion }}
              </span>
            </dd>
          </div>
          <div class="flex justify-between gap-4">
            <dt class="text-slate-500">Marketing consent</dt>
            <dd>{{ me.marketingConsent ? 'Yes' : 'No' }}</dd>
          </div>
        </dl>

        <div class="mt-4 flex flex-wrap gap-2">
          <RouterLink v-if="auth.isWorker" class="btn-secondary text-xs" :to="{ name: 'profile' }">Edit worker profile</RouterLink>
          <RouterLink v-if="auth.isEmployer" class="btn-secondary text-xs" :to="{ name: 'employer-profile' }">
            Edit company profile
          </RouterLink>
          <RouterLink v-if="!me.user.emailVerified" class="btn-secondary text-xs" :to="{ name: 'verify-email', query: { email: me.user.email } }">
            Verify e-mail
          </RouterLink>
        </div>
      </section>

      <section class="card mt-6">
        <h2 class="font-semibold text-slate-900">Export my data</h2>
        <p class="mt-1 text-sm text-slate-500">
          Download a JSON copy of everything your account stores (profile, applications, audit references).
        </p>
        <button class="btn-secondary mt-3" type="button" :disabled="exporting" @click="exportData">
          {{ exporting ? 'Preparing…' : 'Download export' }}
        </button>
      </section>

      <section class="mt-6 rounded-xl border border-rose-200 bg-rose-50 p-5">
        <h2 class="font-semibold text-rose-800">Delete account</h2>
        <p class="mt-1 text-sm text-rose-700">
          This permanently removes your account, profile and applications. It cannot be undone.
        </p>

        <form class="mt-3 space-y-3" @submit.prevent="deleteAccount">
          <div>
            <label class="label" for="deletePassword">Password</label>
            <input id="deletePassword" v-model="password" type="password" class="input" autocomplete="current-password" required />
          </div>
          <div>
            <label class="label" for="deleteConfirm">Type DELETE to confirm</label>
            <input id="deleteConfirm" v-model="confirmText" class="input" placeholder="DELETE" required />
          </div>
          <button class="btn-danger" type="submit" :disabled="deleting">
            {{ deleting ? 'Deleting…' : 'Delete my account' }}
          </button>
        </form>
      </section>
    </template>

    <AlertBox v-if="error" class="mt-4" :message="error" />
    <AlertBox v-if="notice" class="mt-4" type="success" :message="notice" />
  </div>
</template>
