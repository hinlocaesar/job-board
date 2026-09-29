<script setup lang="ts">
import { RouterLink } from 'vue-router'
import type { JobSearchItem } from '../../api/types'
import { formatPay, formatRelative, humanize } from '../../composables/useFormat'

defineProps<{ job: JobSearchItem }>()
</script>

<template>
  <!--
    One anchor per row. No card, no border, no button — the row separates itself
    with whitespace and a single hairline, and the title is the only thing that
    needs to be a link.
  -->
  <RouterLink
    :to="{ name: 'job-detail', params: { slug: job.slug } }"
    class="group block border-b border-slate-200 py-7 transition-colors hover:bg-slate-50/60 sm:px-4 sm:-mx-4 sm:rounded-xl"
  >
    <div class="flex flex-col gap-2 sm:flex-row sm:items-baseline sm:justify-between sm:gap-x-8">
      <div class="min-w-0 flex-1">
        <h3 class="text-[19px] font-semibold leading-snug tracking-tight text-slate-900 transition-colors group-hover:text-brand-600">
          {{ job.title }}
        </h3>
        <p class="mt-1 flex flex-wrap items-center gap-x-2 text-[15px] text-slate-600">
          <span>{{ job.companyName }}</span>
          <span class="text-slate-300" aria-hidden="true">·</span>
          <span>{{ job.categoryName }}</span>
          <span class="text-slate-300" aria-hidden="true">·</span>
          <span>{{ humanize(job.region) }}</span>
        </p>
      </div>

      <div class="flex shrink-0 items-baseline gap-4 sm:text-right">
        <p class="nums text-[17px] font-medium text-slate-900">{{ formatPay(job) }}</p>
        <p class="w-20 shrink-0 text-[13px] text-slate-500">{{ formatRelative(job.publishedAt) }}</p>
      </div>
    </div>

    <p class="mt-2.5 flex flex-wrap items-center gap-x-3 text-[13px] text-slate-500">
      <span>{{ humanize(job.jobType) }}</span>
      <span>{{ humanize(job.experienceLevel) }}</span>
      <span v-if="job.hoursPerWeek" class="nums">{{ job.hoursPerWeek }} hrs/week</span>
      <span v-if="job.skills?.length" class="truncate">
        {{ job.skills.slice(0, 4).join(', ') }}
        <template v-if="job.skills.length > 4"> +{{ job.skills.length - 4 }}</template>
      </span>
    </p>
  </RouterLink>
</template>
