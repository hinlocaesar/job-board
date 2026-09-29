<script setup lang="ts">
/** Apple's empty states are quiet: a title, a sentence, and an optional action. */
defineProps<{ title: string; message?: string }>()
</script>

<template>
  <div class="rounded-2xl bg-slate-100 px-6 py-16 text-center">
    <h3 class="text-[21px] font-semibold text-slate-900">{{ title }}</h3>
    <p v-if="message" class="mx-auto mt-2 max-w-md text-pretty text-[17px] text-slate-600">{{ message }}</p>
    <div class="mt-7 flex flex-wrap justify-center gap-3"><slot /></div>
  </div>
</template>
