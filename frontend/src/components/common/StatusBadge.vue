<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{ status: string }>()

/** Soft fills, no rings — Apple uses colour sparingly, so these stay muted. */
const styles: Record<string, string> = {
  // Jobs
  Pending: 'bg-amber-100 text-amber-800',
  Published: 'bg-emerald-100 text-emerald-800',
  Approved: 'bg-emerald-100 text-emerald-800',
  Rejected: 'bg-rose-100 text-rose-800',
  Flagged: 'bg-orange-100 text-orange-800',
  Draft: 'bg-slate-200 text-slate-700',
  Closed: 'bg-slate-200 text-slate-600',
  // Applications
  Submitted: 'bg-brand-100 text-brand-800',
  Shortlisted: 'bg-violet-100 text-violet-800',
  Withdrawn: 'bg-slate-200 text-slate-600',
  // Accounts
  Active: 'bg-emerald-100 text-emerald-800',
  Banned: 'bg-rose-100 text-rose-800',
  Suspended: 'bg-rose-100 text-rose-800',
}

const style = computed(() => styles[props.status] ?? 'bg-slate-200 text-slate-700')
</script>

<template>
  <span class="badge" :class="style">{{ status }}</span>
</template>
