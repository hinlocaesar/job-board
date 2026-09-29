<script setup lang="ts">
import Icon from './Icon.vue'

const props = defineProps<{ page: number; totalPages: number; disabled?: boolean }>()
const emit = defineEmits<{ 'page-change': [page: number] }>()

/** A compact window around the current page, always with first/last. */
function pages(): (number | '…')[] {
  const { page, totalPages } = props
  if (totalPages <= 7) return Array.from({ length: totalPages }, (_, i) => i + 1)

  const values = new Set<number>([1, totalPages])
  for (let p = page - 1; p <= page + 1; p += 1) {
    if (p >= 1 && p <= totalPages) values.add(p)
  }

  const sorted = [...values].sort((a, b) => a - b)
  const output: (number | '…')[] = []
  let previous = 0
  for (const value of sorted) {
    if (value - previous > 1) output.push('…')
    output.push(value)
    previous = value
  }
  return output
}

function go(page: number): void {
  if (props.disabled) return
  if (page < 1 || page > props.totalPages || page === props.page) return
  emit('page-change', page)
}
</script>

<template>
  <nav
    v-if="totalPages > 1"
    class="flex flex-wrap items-center justify-center gap-1.5"
    aria-label="Pagination"
  >
    <button
      class="btn-ghost btn-sm"
      type="button"
      :disabled="disabled || page <= 1"
      aria-label="Previous page"
      @click="go(page - 1)"
    >
      <Icon name="chevron-right" :size="15" class="rotate-180" />
      Prev
    </button>

    <template v-for="(entry, index) in pages()" :key="`${entry}-${index}`">
      <span v-if="entry === '…'" class="grid h-8 w-8 place-items-center text-slate-400">…</span>
      <button
        v-else
        type="button"
        class="nums grid h-8 min-w-8 place-items-center rounded-full px-2 text-[14px] font-medium transition-colors"
        :class="entry === page ? 'bg-slate-900 text-white' : 'text-slate-600 hover:bg-slate-100'"
        :disabled="disabled"
        :aria-current="entry === page ? 'page' : undefined"
        @click="go(entry)"
      >
        {{ entry }}
      </button>
    </template>

    <button
      class="btn-ghost btn-sm"
      type="button"
      :disabled="disabled || page >= totalPages"
      aria-label="Next page"
      @click="go(page + 1)"
    >
      Next
      <Icon name="chevron-right" :size="15" />
    </button>
  </nav>
</template>
