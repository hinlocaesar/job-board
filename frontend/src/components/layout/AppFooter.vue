<script setup lang="ts">
import { RouterLink } from 'vue-router'
</script>

<template>
  <footer class="border-t border-slate-200 bg-slate-50">
    <div class="mx-auto grid max-w-6xl gap-8 px-4 py-10 sm:grid-cols-2 sm:px-6 lg:grid-cols-4">
      <div>
        <div class="flex items-center gap-2 font-semibold text-slate-900">
          <span class="grid h-7 w-7 place-items-center rounded-lg bg-brand-600 text-xs font-bold text-white">JB</span>
          JobBoard
        </div>
        <p class="mt-3 text-sm text-slate-500">
          Remote jobs for Filipino talent. Hire vetted virtual assistants, developers, designers and writers.
        </p>
      </div>

      <div>
        <h3 class="text-sm font-semibold text-slate-900">For job seekers</h3>
        <ul class="mt-3 space-y-2 text-sm text-slate-600">
          <li><RouterLink :to="{ name: 'jobs' }" class="hover:text-brand-600">Browse jobs</RouterLink></li>
          <li><RouterLink :to="{ name: 'register' }" class="hover:text-brand-600">Create a profile</RouterLink></li>
          <li><RouterLink :to="{ name: 'applications' }" class="hover:text-brand-600">My applications</RouterLink></li>
        </ul>
      </div>

      <div>
        <h3 class="text-sm font-semibold text-slate-900">For employers</h3>
        <ul class="mt-3 space-y-2 text-sm text-slate-600">
          <li><RouterLink :to="{ name: 'job-new' }" class="hover:text-brand-600">Post a job</RouterLink></li>
          <li><RouterLink :to="{ name: 'dashboard' }" class="hover:text-brand-600">Dashboard</RouterLink></li>
          <li><RouterLink :to="{ name: 'employer-profile' }" class="hover:text-brand-600">Company profile</RouterLink></li>
        </ul>
      </div>

      <div>
        <h3 class="text-sm font-semibold text-slate-900">Resources</h3>
        <ul class="mt-3 space-y-2 text-sm text-slate-600">
          <li><RouterLink :to="{ name: 'blog' }" class="hover:text-brand-600">Blog</RouterLink></li>
          <li><RouterLink :to="{ name: 'faq' }" class="hover:text-brand-600">Help &amp; FAQ</RouterLink></li>
          <li><RouterLink :to="{ name: 'landing', params: { slug: 'hire-filipino-virtual-assistants' } }" class="hover:text-brand-600">Hire Filipino VAs</RouterLink></li>
        </ul>
      </div>
    </div>

    <div class="border-t border-slate-200 py-4 text-center text-xs text-slate-400">
      © {{ new Date().getFullYear() }} JobBoard. Built with Umbraco + Vue 3.
    </div>
  </footer>
</template>
