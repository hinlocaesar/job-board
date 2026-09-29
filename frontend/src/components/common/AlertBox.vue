<script setup lang="ts">
import { computed } from 'vue'
import Icon from './Icon.vue'

const props = withDefaults(
  defineProps<{
    type?: 'error' | 'success' | 'info'
    title?: string
    message?: string
  }>(),
  { type: 'error' },
)

const ICONS = { error: 'shield', success: 'check', info: 'inbox' } as const

const styles = computed(() => {
  switch (props.type) {
    case 'success':
      return { box: 'bg-emerald-50 text-emerald-900', icon: 'text-emerald-600' }
    case 'info':
      return { box: 'bg-brand-50 text-brand-900', icon: 'text-brand-600' }
    default:
      return { box: 'bg-rose-50 text-rose-900', icon: 'text-rose-600' }
  }
})
</script>

<template>
  <div
    v-if="message"
    class="rounded-2xl px-5 py-4 text-[15px]"
    :class="styles.box"
    role="alert"
  >
    <div class="flex gap-3">
      <span :class="styles.icon" class="mt-0.5 shrink-0">
        <Icon :name="ICONS[type]" :size="18" />
      </span>
      <div class="min-w-0 flex-1">
        <p v-if="title" class="font-semibold">{{ title }}</p>
        <p class="text-pretty">{{ message }}</p>
        <slot />
      </div>
    </div>
  </div>
</template>
