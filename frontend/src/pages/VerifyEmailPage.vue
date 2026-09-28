<script setup lang="ts">
import { ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import AlertBox from '../components/common/AlertBox.vue'
import { authApi } from '../api/auth'
import { ApiError } from '../api/http'
import { errorMessage } from '../composables/useFormat'
import { useSeo } from '../composables/useSeo'

useSeo({ title: 'Verify your e-mail', noindex: true })

const route = useRoute()
const router = useRouter()

/** The outbox helper only exists while the API runs in Development. */
const isDev = import.meta.env.DEV

const email = ref(typeof route.query.email === 'string' ? route.query.email : '')
const code = ref(typeof route.query.code === 'string' ? route.query.code : '')

const loading = ref(false)
const error = ref<string | null>(null)
const notice = ref<string | null>(null)

/** Dev helper: the API logs e-mails to an outbox instead of sending them. */
const devOutbox = ref<string[]>([])
const loadingOutbox = ref(false)

async function submit(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    await authApi.verifyEmail({ email: email.value.trim(), code: code.value.trim() })
    notice.value = 'E-mail verified — redirecting to log in…'
    setTimeout(() => void router.push({ name: 'login', query: { email: email.value.trim() } }), 700)
  } catch (caught) {
    error.value =
      caught instanceof ApiError && caught.status === 400
        ? caught.message || 'That verification code is not valid or has expired.'
        : errorMessage(caught, 'Verification failed. Please try again.')
  } finally {
    loading.value = false
  }
}

async function loadOutbox(): Promise<void> {
  loadingOutbox.value = true
  error.value = null
  try {
    devOutbox.value = await authApi.outbox()
    const last = devOutbox.value[devOutbox.value.length - 1] ?? ''
    const match = /email=([^&]+).*?code=([0-9a-f]{48})/i.exec(last)
    if (match) {
      email.value = decodeURIComponent(match[1])
      code.value = match[2]
      notice.value = 'Filled in from the latest outbox message.'
    } else {
      notice.value = 'Outbox loaded — copy the code manually.'
    }
  } catch (caught) {
    error.value = errorMessage(caught, 'Outbox is only available while the API runs in Development.')
  } finally {
    loadingOutbox.value = false
  }
}
</script>

<template>
  <div class="mx-auto max-w-md px-4 py-12 sm:px-6">
    <h1 class="text-2xl font-bold text-slate-900">Verify your e-mail</h1>
    <p class="mt-1 text-sm text-slate-500">
      We sent a 48-character code to your inbox. Paste it below to activate your account.
    </p>

    <form class="mt-6 space-y-4" @submit.prevent="submit">
      <div>
        <label class="label" for="verifyEmail">E-mail</label>
        <input id="verifyEmail" v-model="email" type="email" class="input" autocomplete="email" required />
      </div>
      <div>
        <label class="label" for="verifyCode">Verification code</label>
        <input id="verifyCode" v-model="code" class="input font-mono" placeholder="48-character code" required />
      </div>

      <AlertBox v-if="error" :message="error" />
      <AlertBox v-if="notice" type="success" :message="notice" />

      <button class="btn-primary w-full" type="submit" :disabled="loading || !email || !code">
        {{ loading ? 'Verifying…' : 'Verify e-mail' }}
      </button>
    </form>

    <div v-if="isDev" class="mt-6 rounded-lg border border-dashed border-slate-300 bg-slate-50 p-4">
      <p class="text-xs font-semibold text-slate-600">Dev mode</p>
      <p class="mt-1 text-xs text-slate-500">No local e-mail server: the API writes messages to an outbox endpoint.</p>
      <button class="btn-secondary mt-2 text-xs" type="button" :disabled="loadingOutbox" @click="loadOutbox">
        {{ loadingOutbox ? 'Reading…' : 'Load latest code from outbox' }}
      </button>
      <details v-if="devOutbox.length" class="mt-2">
        <summary class="cursor-pointer text-xs text-slate-500">Show last 3 outbox messages</summary>
        <pre class="mt-2 max-h-48 overflow-auto whitespace-pre-wrap break-all text-[10px] text-slate-500">{{ devOutbox.slice(-3).join('\n\n') }}</pre>
      </details>
    </div>

    <p class="mt-6 text-sm text-slate-500">
      <RouterLink :to="{ name: 'login' }" class="text-brand-600 hover:underline">Back to log in</RouterLink>
    </p>
  </div>
</template>
