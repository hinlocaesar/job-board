<script setup lang="ts">
import { RouterLink } from 'vue-router'
import Icon from '../components/common/Icon.vue'
import { useSeo } from '../composables/useSeo'

useSeo({ title: 'Page not found', noindex: true })
</script>

<template>
  <div class="shell-wide section py-32 sm:py-44">
    <p class="text-[19px] font-medium text-brand-600">404</p>
    <h1 class="display mt-4 text-[40px] sm:text-[64px]">This page has moved on.</h1>
    <p class="muted mx-auto mt-6 max-w-xl text-[19px]">
      The link may be out of date, or the job or profile was removed. Try searching the job board.
    </p>

    <form class="mx-auto mt-9 max-w-lg" role="search" action="/jobs">
      <div class="flex gap-2">
        <label class="sr-only" for="notfound-q">Search jobs</label>
        <div class="relative flex-1">
          <span class="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-slate-400">
            <Icon name="search" :size="17" />
          </span>
          <input id="notfound-q" name="q" type="search" class="input rounded-full py-3 pl-11" placeholder="Search jobs" />
        </div>
        <button class="btn-dark px-7" type="submit">Search</button>
      </div>
    </form>

    <div class="mt-7 flex flex-wrap justify-center gap-3">
      <RouterLink class="btn-primary" :to="{ name: 'home' }">Back to homepage</RouterLink>
      <RouterLink class="btn-ghost" :to="{ name: 'faq' }">Help &amp; FAQ</RouterLink>
    </div>
  </div>
</template>
