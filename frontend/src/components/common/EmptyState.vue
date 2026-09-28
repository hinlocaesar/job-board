<script setup lang="ts">
defineProps<{ title: string; message?: string }>()
</script>

<template>
  <div class="rounded-xl border border-dashed border-slate-300 bg-slate-50 px-6 py-12 text-center">
    <h3 class="text-base font-semibold text-slate-700">{{ title }}</h3>
    <p v-if="message" class="mx-auto mt-1 max-w-md text-sm text-slate-500">{{ message }}</p>
    <div class="mt-4"><slot /></div>
  </div>
</template>
