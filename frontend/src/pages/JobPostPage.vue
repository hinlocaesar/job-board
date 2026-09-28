<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { categoriesApi, jobsApi } from '../api/jobs'
import type { CategoryDto, JobInput } from '../api/types'
import AlertBox from '../components/common/AlertBox.vue'
import Spinner from '../components/common/Spinner.vue'
import { EXPERIENCE_LEVELS, JOB_TYPES, PAY_TYPES, REGIONS, errorMessage } from '../composables/useFormat'
import { useSeo } from '../composables/useSeo'

const route = useRoute()
const router = useRouter()

const isEdit = computed(() => route.name === 'job-edit')
const jobId = computed(() => (typeof route.params.id === 'string' ? route.params.id : ''))

useSeo({ title: isEdit.value ? 'Edit job' : 'Post a job', noindex: true })

const categories = ref<CategoryDto[]>([])
const loading = ref(isEdit.value)
const saving = ref(false)
const error = ref<string | null>(null)
const fields = ref<Record<string, string[]>>({})

const form = reactive<JobInput>({
  title: '',
  description: '',
  categoryId: '',
  jobType: 'FullTime',
  region: 'Worldwide',
  payType: 'Hourly',
  payMin: 0,
  payMax: 0,
  currency: 'USD',
  experienceLevel: 'Mid',
  hoursPerWeek: null,
  closesAt: null,
  skills: [],
})

/** Comma-separated in the UI, `string[]` on the wire. */
const skillsText = ref('')

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    categories.value = await categoriesApi.list()
    if (!form.categoryId && categories.value.length) form.categoryId = categories.value[0].id

    if (isEdit.value) {
      const job = await jobsApi.detail(jobId.value)
      if (!job.canEdit) throw Object.assign(new Error('You cannot edit this job.'), { status: 403 })
      form.title = job.title
      form.description = job.description
      form.categoryId = job.categoryId
      form.jobType = job.jobType
      form.region = job.region
      form.payType = job.payType
      form.payMin = Number(job.payMin)
      form.payMax = Number(job.payMax)
      form.currency = job.currency
      form.experienceLevel = job.experienceLevel
      form.hoursPerWeek = job.hoursPerWeek === null ? null : Number(job.hoursPerWeek)
      form.closesAt = job.closesAt ? job.closesAt.slice(0, 10) : null
      skillsText.value = (job.skills ?? []).join(', ')
    }
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    loading.value = false
  }
}

onMounted(() => void load())
watch(isEdit, () => void load())

async function submit(): Promise<void> {
  error.value = null
  fields.value = {}
  saving.value = true

  try {
    const payload: JobInput = {
      ...form,
      payMin: Number(form.payMin) || 0,
      payMax: Number(form.payMax) || 0,
      hoursPerWeek: form.hoursPerWeek === null || form.hoursPerWeek === ('' as never) ? null : Number(form.hoursPerWeek),
      skills: skillsText.value
        .split(',')
        .map((skill) => skill.trim())
        .filter(Boolean),
    }

    const message = isEdit.value ? await jobsApi.update(jobId.value, payload) : await jobsApi.create(payload)
    await router.push({ name: 'job-detail', params: { slug: message.slug } })
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
  <div class="mx-auto max-w-3xl px-4 py-8 sm:px-6">
    <RouterLink :to="{ name: 'dashboard' }" class="text-sm text-brand-600 hover:underline">← Back to dashboard</RouterLink>
    <h1 class="mt-3 text-2xl font-bold text-slate-900">{{ isEdit ? 'Edit job' : 'Post a job' }}</h1>
    <p class="mt-1 text-sm text-slate-500">
      New jobs are reviewed by moderation before they appear on the public board.
    </p>

    <Spinner v-if="loading" label="Loading…" />

    <form v-else class="mt-6 space-y-6" @submit.prevent="submit">
      <section class="card space-y-4">
        <div>
          <label class="label" for="jobTitle">Job title *</label>
          <input
            id="jobTitle"
            v-model="form.title"
            class="input"
            maxlength="160"
            placeholder="e.g. Virtual Assistant for e-commerce operations"
            required
          />
          <p v-if="fields.title" class="mt-1 text-xs text-rose-600">{{ fields.title[0] }}</p>
        </div>

        <div>
          <label class="label" for="jobDescription">Description *</label>
          <textarea
            id="jobDescription"
            v-model="form.description"
            class="input min-h-56"
            placeholder="Responsibilities, requirements, benefits, how to apply…"
            required
          />
          <p class="mt-1 text-xs text-slate-400">Plain text. Minimum length enforced server-side.</p>
          <p v-if="fields.description" class="mt-1 text-xs text-rose-600">{{ fields.description[0] }}</p>
        </div>

        <div class="grid gap-4 sm:grid-cols-2">
          <div>
            <label class="label" for="jobCategory">Category *</label>
            <select id="jobCategory" v-model="form.categoryId" class="input" required>
              <option disabled value="">Select a category</option>
              <option v-for="category in categories" :key="category.id" :value="category.id">{{ category.name }}</option>
            </select>
            <p v-if="fields.categoryId" class="mt-1 text-xs text-rose-600">{{ fields.categoryId[0] }}</p>
          </div>
          <div>
            <label class="label" for="jobSkills">Skills (comma separated)</label>
            <input id="jobSkills" v-model="skillsText" class="input" placeholder="Vue.js, TypeScript, Tailwind CSS" />
            <p v-if="fields.skills" class="mt-1 text-xs text-rose-600">{{ fields.skills[0] }}</p>
          </div>
        </div>
      </section>

      <section class="card space-y-4">
        <h2 class="font-semibold text-slate-900">Terms</h2>

        <div class="grid gap-4 sm:grid-cols-2">
          <div>
            <label class="label" for="jobType">Job type *</label>
            <select id="jobType" v-model="form.jobType" class="input">
              <option v-for="option in JOB_TYPES" :key="option.value" :value="option.value">{{ option.label }}</option>
            </select>
            <p v-if="fields.jobType" class="mt-1 text-xs text-rose-600">{{ fields.jobType[0] }}</p>
          </div>
          <div>
            <label class="label" for="region">Region *</label>
            <select id="region" v-model="form.region" class="input">
              <option v-for="option in REGIONS" :key="option.value" :value="option.value">{{ option.label }}</option>
            </select>
            <p v-if="fields.region" class="mt-1 text-xs text-rose-600">{{ fields.region[0] }}</p>
          </div>
          <div>
            <label class="label" for="experience">Experience level *</label>
            <select id="experience" v-model="form.experienceLevel" class="input">
              <option v-for="option in EXPERIENCE_LEVELS" :key="option.value" :value="option.value">{{ option.label }}</option>
            </select>
            <p v-if="fields.experienceLevel" class="mt-1 text-xs text-rose-600">{{ fields.experienceLevel[0] }}</p>
          </div>
          <div>
            <label class="label" for="hours">Hours per week (optional)</label>
            <input id="hours" v-model.number="form.hoursPerWeek" type="number" min="1" max="80" class="input" placeholder="e.g. 40" />
            <p v-if="fields.hoursPerWeek" class="mt-1 text-xs text-rose-600">{{ fields.hoursPerWeek[0] }}</p>
          </div>
        </div>

        <div class="grid gap-4 sm:grid-cols-4">
          <div>
            <label class="label" for="payType">Pay type *</label>
            <select id="payType" v-model="form.payType" class="input">
              <option v-for="option in PAY_TYPES" :key="option.value" :value="option.value">{{ option.label }}</option>
            </select>
          </div>
          <div>
            <label class="label" for="payMin">From *</label>
            <input id="payMin" v-model.number="form.payMin" type="number" min="0" step="0.5" class="input" required />
            <p v-if="fields.payMin" class="mt-1 text-xs text-rose-600">{{ fields.payMin[0] }}</p>
          </div>
          <div>
            <label class="label" for="payMax">To *</label>
            <input id="payMax" v-model.number="form.payMax" type="number" min="0" step="0.5" class="input" required />
            <p v-if="fields.payMax" class="mt-1 text-xs text-rose-600">{{ fields.payMax[0] }}</p>
          </div>
          <div>
            <label class="label" for="currency">Currency *</label>
            <select id="currency" v-model="form.currency" class="input">
              <option value="USD">USD</option>
              <option value="PHP">PHP</option>
              <option value="EUR">EUR</option>
            </select>
          </div>
        </div>

        <div class="max-w-xs">
          <label class="label" for="closesAt">Applications close (optional)</label>
          <input id="closesAt" v-model="form.closesAt" type="date" class="input" />
          <p v-if="fields.closesAt" class="mt-1 text-xs text-rose-600">{{ fields.closesAt[0] }}</p>
        </div>
      </section>

      <AlertBox v-if="error" :message="error" />

      <div class="flex gap-3">
        <button class="btn-primary px-6" type="submit" :disabled="saving">
          {{ saving ? 'Saving…' : isEdit ? 'Save changes' : 'Submit for review' }}
        </button>
        <RouterLink class="btn-secondary" :to="{ name: 'dashboard' }">Cancel</RouterLink>
      </div>
    </form>
  </div>
</template>
