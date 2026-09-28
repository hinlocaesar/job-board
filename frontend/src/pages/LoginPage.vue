<script setup lang="ts">
import { ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import AlertBox from '../components/common/AlertBox.vue'
import { ApiError } from '../api/http'
import { errorMessage } from '../composables/useFormat'
import { useSeo } from '../composables/useSeo'
import { useAuthStore } from '../stores/auth'

useSeo({ title: 'Log in', noindex: true })

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()

/** Dev-only helpers (demo logins, e-mail outbox) — stripped from production builds. */
const isDev = import.meta.env.DEV

const email = ref(typeof route.query.email === 'string' ? route.query.email : '')
const password = ref('')
const showOutbox = ref(false)
const loading = ref(false)
const error = ref<string | null>(null)

function defaultLanding(): { name: string } {
  const roles = auth.roles
  if (roles.includes('admin')) return { name: 'admin' }
  if (roles.includes('employer')) return { name: 'dashboard' }
  return { name: 'applications' }
}

async function submit(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    await auth.login(email.value.trim(), password.value)
    const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : null
    await router.push(redirect ?? defaultLanding())
  } catch (caught) {
    if (caught instanceof ApiError && caught.code === 'email_not_verified') {
      error.value = 'Please verify your e-mail first. Check your inbox (or the dev outbox below).'
      showOutbox.value = true
    } else {
      error.value = errorMessage(caught, 'Invalid e-mail or password.')
    }
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="mx-auto max-w-md px-4 py-12 sm:px-6">
    <h1 class="text-2xl font-bold text-slate-900">Log in</h1>
    <p class="mt-1 text-sm text-slate-500">
      No account yet?
      <RouterLink :to="{ name: 'register' }" class="font-medium text-brand-600 hover:underline">Sign up</RouterLink>
    </p>

    <form class="mt-6 space-y-4" @submit.prevent="submit">
      <div>
        <label class="label" for="email">E-mail</label>
        <input id="email" v-model="email" type="email" class="input" autocomplete="email" required />
      </div>
      <div>
        <label class="label" for="password">Password</label>
        <input id="password" v-model="password" type="password" class="input" autocomplete="current-password" required />
      </div>

      <AlertBox v-if="error" :message="error" />

      <button class="btn-primary w-full" type="submit" :disabled="loading">
        {{ loading ? 'Logging in…' : 'Log in' }}
      </button>

      <p class="text-center text-sm">
        <RouterLink :to="{ name: 'forgot-password' }" class="text-brand-600 hover:underline">
          Forgot your password?
        </RouterLink>
      </p>
    </form>

    <div v-if="isDev" class="mt-6 rounded-lg border border-dashed border-slate-300 bg-slate-50 p-4 text-xs text-slate-500">
      <p class="font-semibold text-slate-600">Dev logins (seeded locally)</p>
      <button class="mt-1 text-brand-600 underline" type="button" @click="showOutbox = !showOutbox">
        {{ showOutbox ? 'Hide' : 'Show' }} recent e-mail outbox
      </button>
      <ul class="mt-2 space-y-0.5">
        <li>admin@jobboard.local · Admin123!</li>
        <li>employer@jobboard.local · Employer123!</li>
        <li>worker@jobboard.local · Worker123!</li>
      </ul>
      <p v-if="showOutbox" class="mt-2 whitespace-pre-wrap break-all">
        Open <RouterLink :to="{ name: 'verify-email' }" class="text-brand-600 underline">verify e-mail</RouterLink>
        — it can read the outbox directly.
      </p>
    </div>
  </div>
</template>
