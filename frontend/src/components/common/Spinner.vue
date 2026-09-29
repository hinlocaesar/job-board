<script setup lang="ts">
withDefaults(defineProps<{ label?: string }>(), { label: 'Loading…' })
</script>

<template>
  <div class="flex items-center justify-center gap-3 py-16 text-slate-500" role="status">
    <span class="size-4 animate-spin rounded-full border-2 border-slate-200 border-t-slate-500" />
    <span class="text-[15px]">{{ label }}</span>
  </div>
</template>
