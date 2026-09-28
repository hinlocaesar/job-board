<script setup lang="ts">
import { ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import AlertBox from '../components/common/AlertBox.vue'
import { authApi } from '../api/auth'
import { errorMessage } from '../composables/useFormat'
import { useSeo } from '../composables/useSeo'

useSeo({ title: 'Choose a new password', noindex: true })

const route = useRoute()
const router = useRouter()

const email = ref(typeof route.query.email === 'string' ? route.query.email : '')
const code = ref(typeof route.query.code === 'string' ? route.query.code : '')
const password = ref('')
const confirm = ref('')

const loading = ref(false)
const error = ref<string | null>(null)

async function submit(): Promise<void> {
  error.value = null
  if (password.value !== confirm.value) {
    error.value = 'Passwords do not match.'
    return
  }

  loading.value = true
  try {
    await authApi.resetPassword({
      email: email.value.trim(),
      code: code.value.trim(),
      newPassword: password.value,
    })
    await router.push({ name: 'login' })
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="mx-auto max-w-md px-4 py-12 sm:px-6">
    <h1 class="text-2xl font-bold text-slate-900">Choose a new password</h1>
    <p class="mt-1 text-sm text-slate-500">Paste the code from your e-mail and pick a new password.</p>

    <form class="mt-6 space-y-4" @submit.prevent="submit">
      <div>
        <label class="label" for="resetEmail">E-mail</label>
        <input id="resetEmail" v-model="email" type="email" class="input" autocomplete="email" required />
      </div>
      <div>
        <label class="label" for="resetCode">Reset code</label>
        <input id="resetCode" v-model="code" class="input font-mono" placeholder="48-character code" required />
      </div>
      <div class="grid gap-4 sm:grid-cols-2">
        <div>
          <label class="label" for="newPassword">New password</label>
          <input id="newPassword" v-model="password" type="password" class="input" autocomplete="new-password" required />
        </div>
        <div>
          <label class="label" for="confirmNew">Confirm new password</label>
          <input id="confirmNew" v-model="confirm" type="password" class="input" autocomplete="new-password" required />
        </div>
      </div>

      <AlertBox v-if="error" :message="error" />

      <button class="btn-primary w-full" type="submit" :disabled="loading">
        {{ loading ? 'Saving…' : 'Save new password' }}
      </button>
    </form>

    <p class="mt-6 text-sm text-slate-500">
      <RouterLink :to="{ name: 'login' }" class="text-brand-600 hover:underline">Back to log in</RouterLink>
    </p>
  </div>
</template>
