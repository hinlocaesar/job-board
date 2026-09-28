<script setup lang="ts">
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
  <nav v-if="totalPages > 1" class="flex flex-wrap items-center justify-center gap-1.5" aria-label="Pagination">
    <button class="btn-secondary" type="button" :disabled="disabled || page <= 1" @click="go(page - 1)">← Prev</button>

    <template v-for="(entry, index) in pages()" :key="`${entry}-${index}`">
      <span v-if="entry === '…'" class="px-2 text-slate-400">…</span>
      <button
        v-else
        type="button"
        class="h-9 min-w-9 rounded-lg px-3 text-sm font-medium"
        :class="entry === page ? 'bg-brand-600 text-white' : 'border border-slate-300 bg-white text-slate-700 hover:bg-slate-50'"
        :disabled="disabled"
        @click="go(entry)"
      >
        {{ entry }}
      </button>
    </template>

    <button class="btn-secondary" type="button" :disabled="disabled || page >= totalPages" @click="go(page + 1)">
      Next →
    </button>
  </nav>
</template>
