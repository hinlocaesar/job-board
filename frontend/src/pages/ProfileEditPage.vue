<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { onServerPrefetch } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { profilesApi } from '../api/profiles'
import { meApi } from '../api/account'
import type { ExperienceInput, FileDto, SkillInput, WorkerProfileInput } from '../api/types'
import AlertBox from '../components/common/AlertBox.vue'
import Spinner from '../components/common/Spinner.vue'
import { AVAILABILITY_OPTIONS, RATE_PERIOD_OPTIONS, errorMessage } from '../composables/useFormat'
import { useSeo } from '../composables/useSeo'
import { useAuthStore } from '../stores/auth'

useSeo({ title: 'My profile', noindex: true })

const auth = useAuthStore()
const router = useRouter()

const existing = ref(false)
const loading = ref(true)
const saving = ref(false)
const error = ref<string | null>(null)
const notice = ref<string | null>(null)
const fields = ref<Record<string, string[]>>({})

const resumeFile = ref<FileDto | null>(null)
const uploading = ref(false)

const form = reactive<WorkerProfileInput>({
  headline: '',
  summary: '',
  country: 'Philippines',
  city: '',
  timeZone: 'Asia/Manila',
  yearsOfExperience: 0,
  rateMin: 0,
  rateMax: 0,
  currency: 'USD',
  ratePeriod: 'Hour',
  availability: 'FullTime',
  isPublic: true,
  resumeFileId: null,
  skills: [],
  experiences: [],
})

function fillForm(profile: NonNullable<Awaited<ReturnType<typeof profilesApi.currentWorker>>>): void {
  form.headline = profile.headline
  form.summary = profile.summary ?? ''
  form.country = profile.country
  form.city = profile.city ?? ''
  form.timeZone = profile.timeZone ?? ''
  form.yearsOfExperience = profile.yearsOfExperience
  form.rateMin = profile.rateMin
  form.rateMax = profile.rateMax
  form.currency = profile.currency
  form.ratePeriod = profile.ratePeriod
  form.availability = profile.availability
  form.isPublic = profile.isPublic
  form.resumeFileId = profile.resumeFileId ?? null
  form.skills = (profile.skills ?? []).map((skill) => ({
    name: skill.name,
    level: Number(skill.level),
    yearsExperience: Number(skill.yearsExperience) || 0,
  })) as SkillInput[]
  form.experiences = (profile.experiences ?? []).map((experience) => ({
    title: experience.title,
    companyName: experience.companyName,
    location: experience.location ?? '',
    startDate: experience.startDate,
    endDate: experience.endDate ?? '',
    isCurrent: experience.isCurrent,
    description: experience.description ?? '',
    sortOrder: Number(experience.sortOrder) || 0,
  })) as ExperienceInput[]
}

async function load(): Promise<void> {
  loading.value = true
  try {
    const profile = await profilesApi.currentWorker()
    existing.value = true
    fillForm(profile)
  } catch {
    existing.value = false // 404 → first-time setup
  } finally {
    loading.value = false
  }
}

onServerPrefetch(() => load())
onMounted(() => void load())

function addSkill(): void {
  form.skills.push({ name: '', level: 3, yearsExperience: 0 })
}
function removeSkill(index: number): void {
  form.skills.splice(index, 1)
}

function addExperience(): void {
  form.experiences.push({
    title: '',
    companyName: '',
    location: '',
    startDate: '',
    endDate: '',
    isCurrent: false,
    description: '',
    sortOrder: form.experiences.length,
  })
}
function removeExperience(index: number): void {
  form.experiences.splice(index, 1)
}

async function uploadResume(event: Event): Promise<void> {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  uploading.value = true
  error.value = null
  try {
    resumeFile.value = await meApi.uploadResume(file)
    form.resumeFileId = resumeFile.value.id
    notice.value = `Uploaded ${resumeFile.value.fileName} (${Math.round(resumeFile.value.sizeBytes / 1024)} KB). Save your profile to keep it.`
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    uploading.value = false
    input.value = ''
  }
}

async function downloadResume(): Promise<void> {
  if (!form.resumeFileId) return
  try {
    const blob = await meApi.resumeBlob(form.resumeFileId)
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = resumeFile.value?.fileName ?? 'resume'
    link.click()
    URL.revokeObjectURL(url)
  } catch (caught) {
    error.value = errorMessage(caught)
  }
}

const skillsForSubmit = computed(() =>
  form.skills
    .filter((skill) => skill.name.trim())
    .map((skill) => ({
      name: skill.name.trim(),
      level: Number(skill.level) || 3,
      yearsExperience: Number(skill.yearsExperience) || 0,
    })),
)

async function save(): Promise<void> {
  error.value = null
  fields.value = {}
  saving.value = true
  try {
    const payload: WorkerProfileInput = {
      ...form,
      yearsOfExperience: Number(form.yearsOfExperience),
      rateMin: Number(form.rateMin),
      rateMax: Number(form.rateMax),
      skills: skillsForSubmit.value,
      experiences: form.experiences
        .filter((experience) => experience.title.trim() && experience.companyName.trim())
        .map((experience, index) => ({ ...experience, sortOrder: index })),
    }

    const message = existing.value
      ? await profilesApi.updateWorker(payload)
      : await profilesApi.createWorker(payload)

    notice.value = existing.value ? 'Profile saved.' : 'Profile created.'
    existing.value = true
    await router.push({ name: 'worker-profile', params: { slug: message.slug } })
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
    <div class="flex items-start justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold text-slate-900">My worker profile</h1>
        <p class="mt-1 text-sm text-slate-500">
          Your profile is what employers see when you apply.
          <span v-if="auth.user && !auth.emailVerified" class="text-amber-600">
            Verify your e-mail to unlock all features.
          </span>
        </p>
      </div>
    </div>

    <Spinner v-if="loading" label="Loading profile…" />

    <form v-else class="mt-6 space-y-6" @submit.prevent="save">
      <section class="card space-y-4">
        <h2 class="font-semibold text-slate-900">Basics</h2>

        <div>
          <label class="label" for="headline">Headline</label>
          <input
            id="headline"
            v-model="form.headline"
            class="input"
            placeholder="Senior Frontend Developer (Vue 3 / TypeScript)"
            maxlength="140"
            required
          />
          <p class="mt-1 text-xs text-slate-400">Shown as your public title — max 140 characters.</p>
          <p v-if="fields.headline" class="mt-1 text-xs text-rose-600">{{ fields.headline[0] }}</p>
        </div>

        <div>
          <label class="label" for="summary">Summary</label>
          <textarea id="summary" v-model="form.summary" class="input min-h-28" placeholder="A short intro about your experience…" />
          <p v-if="fields.summary" class="mt-1 text-xs text-rose-600">{{ fields.summary[0] }}</p>
        </div>

        <div class="grid gap-4 sm:grid-cols-3">
          <div>
            <label class="label" for="country">Country</label>
            <input id="country" v-model="form.country" class="input" required />
          </div>
          <div>
            <label class="label" for="city">City</label>
            <input id="city" v-model="form.city" class="input" />
          </div>
          <div>
            <label class="label" for="timeZone">Time zone</label>
            <input id="timeZone" v-model="form.timeZone" class="input" placeholder="Asia/Manila" />
          </div>
        </div>

        <div>
          <label class="label" for="years">Years of experience</label>
          <input id="years" v-model.number="form.yearsOfExperience" type="number" min="0" max="60" class="input sm:w-40" />
          <p v-if="fields.yearsOfExperience" class="mt-1 text-xs text-rose-600">{{ fields.yearsOfExperience[0] }}</p>
        </div>
      </section>

      <section class="card space-y-4">
        <h2 class="font-semibold text-slate-900">Rates &amp; availability</h2>

        <div class="grid gap-4 sm:grid-cols-4">
          <div>
            <label class="label" for="rateMin">Min. rate</label>
            <input id="rateMin" v-model.number="form.rateMin" type="number" min="0" step="0.5" class="input" />
          </div>
          <div>
            <label class="label" for="rateMax">Max. rate</label>
            <input id="rateMax" v-model.number="form.rateMax" type="number" min="0" step="0.5" class="input" />
          </div>
          <div>
            <label class="label" for="currency">Currency</label>
            <select id="currency" v-model="form.currency" class="input">
              <option value="USD">USD</option>
              <option value="PHP">PHP</option>
              <option value="EUR">EUR</option>
            </select>
          </div>
          <div>
            <label class="label" for="ratePeriod">Period</label>
            <select id="ratePeriod" v-model="form.ratePeriod" class="input">
              <option v-for="option in RATE_PERIOD_OPTIONS" :key="option.value" :value="option.value">
                {{ option.label }}
              </option>
            </select>
          </div>
        </div>
        <p v-if="fields.rateMin" class="text-xs text-rose-600">{{ fields.rateMin[0] }}</p>

        <div class="grid gap-4 sm:grid-cols-2">
          <div>
            <label class="label" for="availability">Availability</label>
            <select id="availability" v-model="form.availability" class="input">
              <option v-for="option in AVAILABILITY_OPTIONS" :key="option.value" :value="option.value">
                {{ option.label }}
              </option>
            </select>
          </div>
          <label class="flex items-end gap-2 pb-2 text-sm text-slate-600">
            <input v-model="form.isPublic" type="checkbox" />
            Make my profile public (appear in search, hide contact details)
          </label>
        </div>
      </section>

      <section class="card space-y-4">
        <div class="flex items-center justify-between">
          <h2 class="font-semibold text-slate-900">Skills</h2>
          <button class="btn-secondary text-xs" type="button" @click="addSkill">+ Add skill</button>
        </div>

        <p v-if="!form.skills.length" class="text-sm text-slate-500">No skills yet — add at least a few.</p>

        <div v-for="(skill, index) in form.skills" :key="index" class="grid gap-3 sm:grid-cols-[1fr_120px_120px_auto]">
          <input v-model="skill.name" class="input" placeholder="Skill name (e.g. Vue.js)" />
          <select v-model.number="skill.level" class="input">
            <option :value="1">Level 1</option>
            <option :value="2">Level 2</option>
            <option :value="3">Level 3</option>
            <option :value="4">Level 4</option>
            <option :value="5">Level 5</option>
          </select>
          <input v-model.number="skill.yearsExperience" type="number" min="0" max="60" class="input" placeholder="Years" />
          <button class="text-sm text-rose-600 hover:underline" type="button" @click="removeSkill(index)">Remove</button>
        </div>
        <p v-if="fields.skills" class="text-xs text-rose-600">{{ fields.skills[0] }}</p>
      </section>

      <section class="card space-y-4">
        <div class="flex items-center justify-between">
          <h2 class="font-semibold text-slate-900">Work experience</h2>
          <button class="btn-secondary text-xs" type="button" @click="addExperience">+ Add experience</button>
        </div>

        <p v-if="!form.experiences.length" class="text-sm text-slate-500">Optional, but it helps you stand out.</p>

        <div v-for="(experience, index) in form.experiences" :key="index" class="space-y-3 rounded-lg border border-slate-200 p-4">
          <div class="grid gap-3 sm:grid-cols-2">
            <input v-model="experience.title" class="input" placeholder="Job title" />
            <input v-model="experience.companyName" class="input" placeholder="Company" />
          </div>
          <div class="grid gap-3 sm:grid-cols-3">
            <input v-model="experience.location" class="input" placeholder="Location" />
            <div>
              <label class="label" :for="`start-${index}`">Start</label>
              <input :id="`start-${index}`" v-model="experience.startDate" type="date" class="input" />
            </div>
            <div>
              <label class="label" :for="`end-${index}`">End</label>
              <input :id="`end-${index}`" v-model="experience.endDate" type="date" class="input" :disabled="experience.isCurrent" />
            </div>
          </div>
          <label class="flex items-center gap-2 text-sm text-slate-600">
            <input v-model="experience.isCurrent" type="checkbox" /> I still work here
          </label>
          <textarea v-model="experience.description" class="input" rows="2" placeholder="What did you do?" />
          <button class="text-sm text-rose-600 hover:underline" type="button" @click="removeExperience(index)">Remove</button>
        </div>
        <p v-if="fields.experiences" class="text-xs text-rose-600">{{ fields.experiences[0] }}</p>
      </section>

      <section class="card space-y-3">
        <h2 class="font-semibold text-slate-900">Resume</h2>
        <p class="text-sm text-slate-500">PDF, DOC, DOCX or RTF — max 5 MB.</p>

        <div class="flex flex-wrap items-center gap-3">
          <input type="file" accept=".pdf,.doc,.docx,.rtf,application/pdf" class="text-sm" @change="uploadResume" />
          <span v-if="uploading" class="text-sm text-slate-500">Uploading…</span>
          <button v-if="form.resumeFileId" class="btn-secondary text-xs" type="button" @click="downloadResume">
            Download current resume
          </button>
        </div>
        <p v-if="resumeFile" class="text-xs text-slate-500">
          {{ resumeFile.fileName }} · {{ Math.round(resumeFile.sizeBytes / 1024) }} KB
        </p>
        <p v-if="fields.resumeFileId" class="text-xs text-rose-600">{{ fields.resumeFileId[0] }}</p>
      </section>

      <AlertBox v-if="error" :message="error" />
      <AlertBox v-if="notice" type="success" :message="notice" />

      <div class="flex gap-3">
        <button class="btn-primary px-6" type="submit" :disabled="saving">
          {{ saving ? 'Saving…' : existing ? 'Save profile' : 'Create profile' }}
        </button>
        <RouterLink class="btn-secondary" :to="{ name: 'jobs' }">Cancel</RouterLink>
      </div>
    </form>
  </div>
</template>
