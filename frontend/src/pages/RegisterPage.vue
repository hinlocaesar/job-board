<script setup lang="ts">
import { computed, ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import AlertBox from '../components/common/AlertBox.vue'
import { ApiError } from '../api/http'
import { errorMessage } from '../composables/useFormat'
import { useSeo } from '../composables/useSeo'
import { useAuthStore } from '../stores/auth'

useSeo({ title: 'Create an account', noindex: true })

const PRIVACY_VERSION = '2026-09-01'

const auth = useAuthStore()
const router = useRouter()

const role = ref<'worker' | 'employer'>('worker')
const fullName = ref('')
const email = ref('')
const password = ref('')
const confirm = ref('')
const acceptPrivacy = ref(false)
const marketing = ref(false)

const loading = ref(false)
const error = ref<string | null>(null)
const fields = ref<Record<string, string[]>>({})

const passwordError = computed(() =>
  password.value && password.value !== confirm.value ? 'Passwords do not match.' : null,
)

async function submit(): Promise<void> {
  fields.value = {}
  error.value = null

  if (passwordError.value) {
    error.value = passwordError.value
    return
  }
  if (!acceptPrivacy.value) {
    error.value = 'You must accept the privacy policy to continue.'
    return
  }

  loading.value = true
  try {
    await auth.register({
      email: email.value.trim(),
      password: password.value,
      fullName: fullName.value.trim(),
      role: role.value,
      acceptPrivacy: true,
      privacyPolicyVersion: PRIVACY_VERSION,
      marketingConsent: marketing.value,
    })
    await router.push({ name: 'verify-email', query: { email: email.value.trim() } })
  } catch (caught) {
    if (caught instanceof ApiError) {
      error.value = caught.status === 409 ? 'An account with that e-mail already exists.' : caught.message
      fields.value = caught.fields
    } else {
      error.value = errorMessage(caught)
    }
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="mx-auto max-w-lg px-4 py-12 sm:px-6">
    <h1 class="text-2xl font-bold text-slate-900">Create an account</h1>
    <p class="mt-1 text-sm text-slate-500">
      Already registered?
      <RouterLink :to="{ name: 'login' }" class="font-medium text-brand-600 hover:underline">Log in</RouterLink>
    </p>

    <div class="mt-6 grid grid-cols-2 gap-3">
      <button
        type="button"
        class="card text-left"
        :class="role === 'worker' ? 'border-brand-500 ring-2 ring-brand-100' : 'hover:border-slate-300'"
        @click="role = 'worker'"
      >
        <p class="font-semibold text-slate-900">I'm looking for work</p>
        <p class="mt-1 text-sm text-slate-500">Build a profile, upload a resume and apply to jobs.</p>
      </button>
      <button
        type="button"
        class="card text-left"
        :class="role === 'employer' ? 'border-brand-500 ring-2 ring-brand-100' : 'hover:border-slate-300'"
        @click="role = 'employer'"
      >
        <p class="font-semibold text-slate-900">I'm hiring</p>
        <p class="mt-1 text-sm text-slate-500">Post jobs and review applicants in one place.</p>
      </button>
    </div>

    <form class="mt-6 space-y-4" @submit.prevent="submit">
      <div>
        <label class="label" for="fullName">Full name</label>
        <input id="fullName" v-model="fullName" class="input" autocomplete="name" required />
        <p v-if="fields.fullName" class="mt-1 text-xs text-rose-600">{{ fields.fullName[0] }}</p>
      </div>

      <div>
        <label class="label" for="regEmail">E-mail</label>
        <input id="regEmail" v-model="email" type="email" class="input" autocomplete="email" required />
        <p v-if="fields.email" class="mt-1 text-xs text-rose-600">{{ fields.email[0] }}</p>
      </div>

      <div class="grid gap-4 sm:grid-cols-2">
        <div>
          <label class="label" for="regPassword">Password</label>
          <input id="regPassword" v-model="password" type="password" class="input" autocomplete="new-password" required />
          <p class="mt-1 text-xs text-slate-400">8+ chars, upper &amp; lower case and a digit.</p>
          <p v-if="fields.password" class="mt-1 text-xs text-rose-600">{{ fields.password[0] }}</p>
        </div>
        <div>
          <label class="label" for="confirmPassword">Confirm password</label>
          <input id="confirmPassword" v-model="confirm" type="password" class="input" autocomplete="new-password" required />
          <p v-if="passwordError" class="mt-1 text-xs text-rose-600">{{ passwordError }}</p>
        </div>
      </div>

      <label class="flex items-start gap-2 text-sm text-slate-600">
        <input v-model="acceptPrivacy" type="checkbox" class="mt-0.5" required />
        <span>
          I have read and accept the privacy policy (version {{ PRIVACY_VERSION }}) and agree to the processing of my
          data for job-matching purposes.
        </span>
      </label>

      <label class="flex items-start gap-2 text-sm text-slate-600">
        <input v-model="marketing" type="checkbox" class="mt-0.5" />
        <span>Send me job alerts and product updates (optional).</span>
      </label>

      <AlertBox v-if="error" :message="error" />

      <button class="btn-primary w-full" type="submit" :disabled="loading">
        {{ loading ? 'Creating account…' : 'Create account' }}
      </button>
    </form>
  </div>
</template>
