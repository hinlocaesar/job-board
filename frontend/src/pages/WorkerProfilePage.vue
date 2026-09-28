<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { onServerPrefetch } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { profilesApi } from '../api/profiles'
import type { WorkerProfileDto } from '../api/types'
import AlertBox from '../components/common/AlertBox.vue'
import Spinner from '../components/common/Spinner.vue'
import { siteOrigin, useSeo } from '../composables/useSeo'
import { formatDate, errorMessage, humanize, RATE_PERIOD_OPTIONS } from '../composables/useFormat'
import { useAuthStore } from '../stores/auth'

const route = useRoute()
const auth = useAuthStore()

const slug = computed(() => String(route.params.slug))
const profile = ref<WorkerProfileDto | null>(null)
const error = ref<string | null>(null)
const loading = ref(true)

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    profile.value = await profilesApi.publicWorker(slug.value)
    const location = [profile.value.city, profile.value.country].filter(Boolean).join(', ')
    useSeo({
      title: `${profile.value.headline} — ${location || 'Remote professional'}`,
      description: (profile.value.summary ?? '').replace(/\s+/g, ' ').slice(0, 155),
      canonical: `${siteOrigin()}/u/${profile.value.slug}`,
      ogType: 'profile',
    })
  } catch (caught) {
    profile.value = null
    error.value = errorMessage(caught, 'This profile does not exist or is not public.')
  } finally {
    loading.value = false
  }
}

onServerPrefetch(() => load())
onMounted(() => void load())

const rateLabel = computed(() => {
  if (!profile.value) return ''
  const period = RATE_PERIOD_OPTIONS.find((option) => option.value === profile.value?.ratePeriod)?.label ?? ''
  return `${profile.value.currency} ${profile.value.rateMin}–${profile.value.rateMax} ${period}`
})

const contactVisible = computed(() => Boolean(profile.value?.contactEmail || profile.value?.contactPhone))
</script>

<template>
  <div class="mx-auto max-w-4xl px-4 py-8 sm:px-6">
    <Spinner v-if="loading" label="Loading profile…" />

    <AlertBox v-else-if="error" :message="error">
      <template #default>
        <p class="mt-2">
          <RouterLink :to="{ name: 'jobs' }" class="font-medium underline">Browse jobs instead →</RouterLink>
        </p>
      </template>
    </AlertBox>

    <template v-else-if="profile">
      <header class="card">
        <div class="flex flex-wrap items-start justify-between gap-4">
          <div>
            <div class="flex items-center gap-3">
              <span class="grid h-14 w-14 place-items-center rounded-full bg-brand-100 text-xl font-bold text-brand-700">
                {{ profile.headline.charAt(0) }}
              </span>
              <div>
                <h1 class="text-xl font-bold text-slate-900">{{ profile.headline }}</h1>
                <p class="text-sm text-slate-500">
                  {{ [profile.city, profile.country].filter(Boolean).join(', ') }}
                  <span v-if="profile.timeZone"> · {{ profile.timeZone }}</span>
                </p>
              </div>
            </div>

            <div class="mt-4 flex flex-wrap gap-2 text-xs text-slate-500">
              <span class="chip">{{ profile.yearsOfExperience }} yrs experience</span>
              <span class="chip">{{ humanize(profile.availability) }}</span>
              <span class="chip bg-emerald-50 text-emerald-700">{{ rateLabel }}</span>
              <span v-if="!profile.isPublic" class="chip bg-amber-50 text-amber-700">Hidden profile</span>
            </div>
          </div>

          <div v-if="auth.isEmployer" class="text-right text-sm">
            <p v-if="contactVisible" class="text-slate-600">
              <a v-if="profile.contactEmail" :href="`mailto:${profile.contactEmail}`" class="text-brand-600 hover:underline">
                {{ profile.contactEmail }}
              </a>
            </p>
            <p v-if="profile.contactPhone" class="text-slate-600">{{ profile.contactPhone }}</p>
          </div>
        </div>

        <p v-if="profile.summary" class="mt-4 whitespace-pre-line border-t border-slate-100 pt-4 text-slate-700">
          {{ profile.summary }}
        </p>

        <p v-if="!contactVisible" class="mt-3 text-xs text-slate-400">
          Contact details are private and are only shared with signed-in employers.
        </p>
      </header>

      <div class="mt-6 grid gap-6 lg:grid-cols-[1fr_300px]">
        <section class="card">
          <h2 class="font-semibold text-slate-900">Experience</h2>
          <div v-if="profile.experiences.length" class="mt-4 space-y-5">
            <article v-for="experience in profile.experiences" :key="experience.id" class="border-l-2 border-brand-100 pl-4">
              <h3 class="font-medium text-slate-900">{{ experience.title }}</h3>
              <p class="text-sm text-slate-500">
                {{ experience.companyName }}
                <span v-if="experience.location"> · {{ experience.location }}</span>
              </p>
              <p class="text-xs text-slate-400">
                {{ formatDate(experience.startDate, { month: 'short', year: 'numeric' }) }} –
                {{ experience.isCurrent ? 'Present' : formatDate(experience.endDate, { month: 'short', year: 'numeric' }) }}
              </p>
              <p v-if="experience.description" class="mt-1 text-sm text-slate-600">{{ experience.description }}</p>
            </article>
          </div>
          <p v-else class="mt-2 text-sm text-slate-500">No work experience listed yet.</p>
        </section>

        <aside class="space-y-4">
          <div class="card">
            <h2 class="font-semibold text-slate-900">Skills</h2>
            <ul v-if="profile.skills.length" class="mt-3 space-y-2">
              <li v-for="skill in profile.skills" :key="skill.name" class="flex items-center justify-between gap-2 text-sm">
                <span class="text-slate-700">{{ skill.name }}</span>
                <span class="flex gap-0.5" :title="`Level ${skill.level} of 5`">
                  <span
                    v-for="dot in 5"
                    :key="dot"
                    class="h-2 w-2 rounded-full"
                    :class="dot <= skill.level ? 'bg-brand-500' : 'bg-slate-200'"
                  />
                </span>
              </li>
            </ul>
            <p v-else class="mt-2 text-sm text-slate-500">No skills listed yet.</p>
          </div>

          <div class="card text-sm">
            <h2 class="font-semibold text-slate-900">Hire this professional</h2>
            <p class="mt-2 text-slate-600">Post a job and shortlist candidates that fit your role.</p>
            <RouterLink class="btn-primary mt-3 w-full" :to="{ name: 'job-new' }">Post a job</RouterLink>
          </div>
        </aside>
      </div>
    </template>
  </div>
</template>
