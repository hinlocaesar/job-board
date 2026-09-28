<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { profilesApi } from '../api/profiles'
import type { EmployerProfileInput } from '../api/types'
import AlertBox from '../components/common/AlertBox.vue'
import Spinner from '../components/common/Spinner.vue'
import { errorMessage } from '../composables/useFormat'
import { useSeo } from '../composables/useSeo'

useSeo({ title: 'Company profile', noindex: true })

const existing = ref(false)
const loading = ref(true)
const saving = ref(false)
const error = ref<string | null>(null)
const notice = ref<string | null>(null)
const fields = ref<Record<string, string[]>>({})

const form = reactive<EmployerProfileInput>({
  companyName: '',
  website: null,
  description: null,
  country: null,
  logoFileId: null,
})

async function load(): Promise<void> {
  loading.value = true
  try {
    const profile = await profilesApi.currentEmployer()
    existing.value = true
    form.companyName = profile.companyName
    form.website = profile.website ?? null
    form.description = profile.description ?? null
    form.country = profile.country ?? null
    form.logoFileId = profile.logoFileId ?? null
  } catch {
    existing.value = false
  } finally {
    loading.value = false
  }
}

onMounted(() => void load())

async function save(): Promise<void> {
  saving.value = true
  error.value = null
  notice.value = null
  fields.value = {}
  try {
    const payload: EmployerProfileInput = {
      companyName: form.companyName.trim(),
      website: form.website?.trim() || null,
      description: form.description?.trim() || null,
      country: form.country?.trim() || null,
      logoFileId: form.logoFileId,
    }
    const message = existing.value
      ? await profilesApi.updateEmployer(payload)
      : await profilesApi.createEmployer(payload)
    existing.value = true
    notice.value = `Saved — your public slug is “${message.slug}”.`
  } catch (caught) {
    if (caught && typeof caught === 'object' && 'fields' in caught) {
      fields.value = (caught as { fields: Record<string, string[]> }).fields
    }
    error.value = errorMessage(caught)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="mx-auto max-w-2xl px-4 py-8 sm:px-6">
    <RouterLink :to="{ name: 'dashboard' }" class="text-sm text-brand-600 hover:underline">← Back to dashboard</RouterLink>
    <h1 class="mt-3 text-2xl font-bold text-slate-900">Company profile</h1>
    <p class="mt-1 text-sm text-slate-500">Shown to candidates on every job you post.</p>

    <Spinner v-if="loading" label="Loading…" />

    <form v-else class="mt-6 space-y-4" @submit.prevent="save">
      <div class="card space-y-4">
        <div>
          <label class="label" for="companyName">Company name *</label>
          <input id="companyName" v-model="form.companyName" class="input" required />
          <p v-if="fields.companyName" class="mt-1 text-xs text-rose-600">{{ fields.companyName[0] }}</p>
        </div>

        <div class="grid gap-4 sm:grid-cols-2">
          <div>
            <label class="label" for="website">Website</label>
            <input id="website" v-model="form.website" type="url" class="input" placeholder="https://…" />
            <p v-if="fields.website" class="mt-1 text-xs text-rose-600">{{ fields.website[0] }}</p>
          </div>
          <div>
            <label class="label" for="country">Country</label>
            <input id="country" v-model="form.country" class="input" />
          </div>
        </div>

        <div>
          <label class="label" for="about">About the company</label>
          <textarea id="about" v-model="form.description" class="input min-h-32" />
          <p v-if="fields.description" class="mt-1 text-xs text-rose-600">{{ fields.description[0] }}</p>
        </div>
      </div>

      <AlertBox v-if="error" :message="error" />
      <AlertBox v-if="notice" type="success" :message="notice" />

      <div class="flex gap-3">
        <button class="btn-primary px-6" type="submit" :disabled="saving">
          {{ saving ? 'Saving…' : existing ? 'Save profile' : 'Create profile' }}
        </button>
        <RouterLink class="btn-secondary" :to="{ name: 'dashboard' }">Cancel</RouterLink>
      </div>
    </form>
  </div>
</template>
