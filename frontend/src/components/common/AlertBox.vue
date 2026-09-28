<script setup lang="ts">
defineProps<{
  type?: 'error' | 'success' | 'info'
  title?: string
  message?: string
}>()
</script>

<template>
  <div
    v-if="message"
    class="rounded-lg border px-4 py-3 text-sm"
    :class="{
      'border-rose-200 bg-rose-50 text-rose-700': (type ?? 'error') === 'error',
      'border-emerald-200 bg-emerald-50 text-emerald-700': type === 'success',
      'border-sky-200 bg-sky-50 text-sky-700': type === 'info',
    }"
    role="alert"
  >
    <p v-if="title" class="mb-1 font-semibold">{{ title }}</p>
    <p>{{ message }}</p>
    <slot />
  </div>
</template>
