<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { useSeo } from '../composables/useSeo'

useSeo({ title: 'Page not found', noindex: true })
</script>

<template>
  <div class="mx-auto max-w-xl px-4 py-20 text-center sm:px-6">
    <p class="text-sm font-semibold uppercase tracking-wide text-brand-600">404</p>
    <h1 class="mt-2 text-3xl font-bold text-slate-900">We couldn't find that page</h1>
    <p class="mt-3 text-slate-600">
      The link may be out of date, or the job/profile was removed. Try searching the job board.
    </p>
    <div class="mt-6 flex flex-wrap justify-center gap-3">
      <RouterLink class="btn-primary" :to="{ name: 'jobs' }">Browse jobs</RouterLink>
      <RouterLink class="btn-secondary" :to="{ name: 'home' }">Homepage</RouterLink>
    </div>
  </div>
</template>
