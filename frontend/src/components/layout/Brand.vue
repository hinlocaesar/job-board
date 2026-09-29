<script setup lang="ts">
import { RouterLink } from 'vue-router'

/** Plain wordmark, no coloured tile — Apple's nav is typographic, not logo-heavy. */
defineProps<{ compact?: boolean }>()
</script>

<template>
  <RouterLink
    :to="{ name: 'home' }"
    class="text-[17px] font-semibold tracking-tight text-slate-900"
    aria-label="Filipino VA — home"
  >
    Filipino <span class="text-slate-500">VA</span>
  </RouterLink>
</template>
