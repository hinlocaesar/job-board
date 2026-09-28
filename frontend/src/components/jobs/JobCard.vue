<script setup lang="ts">
import { RouterLink } from 'vue-router'
import type { JobSearchItem } from '../../api/types'
import { formatPay, formatRelative, humanize } from '../../composables/useFormat'

defineProps<{ job: JobSearchItem }>()
</script>

<template>
  <article class="card transition hover:border-brand-300 hover:shadow-md">
    <div class="flex items-start justify-between gap-4">
      <div>
        <RouterLink
          :to="{ name: 'job-detail', params: { slug: job.slug } }"
          class="text-lg font-semibold text-slate-900 hover:text-brand-600"
        >
          {{ job.title }}
        </RouterLink>
        <p class="mt-0.5 text-sm text-slate-500">
          {{ job.companyName }} · {{ job.categoryName }}
        </p>
      </div>
      <span class="shrink-0 text-right text-sm font-semibold text-emerald-700">
        {{ formatPay(job) }}
      </span>
    </div>

    <div class="mt-3 flex flex-wrap items-center gap-2 text-xs text-slate-500">
      <span class="chip">{{ humanize(job.jobType) }}</span>
      <span class="chip">{{ humanize(job.experienceLevel) }}</span>
      <span class="chip">{{ humanize(job.region) }}</span>
      <span v-if="job.hoursPerWeek" class="chip">{{ job.hoursPerWeek }} hrs/week</span>
      <span class="text-slate-400">Posted {{ formatRelative(job.publishedAt) }}</span>
    </div>

    <div v-if="job.skills?.length" class="mt-3 flex flex-wrap gap-1.5">
      <span v-for="skill in job.skills.slice(0, 6)" :key="skill" class="chip bg-brand-50 text-brand-700">
        {{ skill }}
      </span>
      <span v-if="job.skills.length > 6" class="text-xs text-slate-400">+{{ job.skills.length - 6 }} more</span>
    </div>
  </article>
</template>
