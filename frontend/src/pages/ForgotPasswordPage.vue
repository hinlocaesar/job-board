<script setup lang="ts">
import { ref } from 'vue'
import { RouterLink } from 'vue-router'
import AlertBox from '../components/common/AlertBox.vue'
import { authApi } from '../api/auth'
import { errorMessage } from '../composables/useFormat'
import { useSeo } from '../composables/useSeo'

useSeo({ title: 'Reset your password', noindex: true })

/** Dev-only hint about the e-mail outbox. */
const isDev = import.meta.env.DEV

const email = ref('')
const loading = ref(false)
const sent = ref(false)
const error = ref<string | null>(null)

async function submit(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    await authApi.forgotPassword({ email: email.value.trim() })
    sent.value = true
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="mx-auto max-w-md px-4 py-12 sm:px-6">
    <h1 class="text-2xl font-bold text-slate-900">Forgot your password?</h1>
    <p class="mt-1 text-sm text-slate-500">We'll e-mail you a link to choose a new password.</p>

    <template v-if="sent">
      <AlertBox
        class="mt-6"
        type="success"
        title="Check your inbox"
        message="If an account exists for that address, a reset link is on its way. The link expires shortly."
      />
      <p v-if="isDev" class="mt-3 text-xs text-slate-500">
        Dev mode: open
        <RouterLink class="text-brand-600 underline" :to="{ name: 'reset-password', query: { email } }">
          reset password
        </RouterLink>
        and use the code from the outbox, or follow the link printed in the API's <code>logs/outbox.log</code>.
      </p>
    </template>

    <form v-else class="mt-6 space-y-4" @submit.prevent="submit">
      <div>
        <label class="label" for="forgotEmail">E-mail</label>
        <input id="forgotEmail" v-model="email" type="email" class="input" autocomplete="email" required />
      </div>

      <AlertBox v-if="error" :message="error" />

      <button class="btn-primary w-full" type="submit" :disabled="loading">
        {{ loading ? 'Sending…' : 'Send reset link' }}
      </button>
    </form>

    <p class="mt-6 text-sm text-slate-500">
      Remembered it?
      <RouterLink :to="{ name: 'login' }" class="text-brand-600 hover:underline">Back to log in</RouterLink>
    </p>
  </div>
</template>
