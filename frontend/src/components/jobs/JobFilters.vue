<script setup lang="ts">
import { ref, watch } from 'vue'
import type { CategoryDto } from '../../api/types'
import type { JobSearchQuery } from '../../api/types'
import { EXPERIENCE_LEVELS, JOB_TYPES, PAY_TYPES } from '../../composables/useFormat'

const props = defineProps<{
  modelValue: JobSearchQuery
  categories: CategoryDto[]
  total?: number
}>()

const emit = defineEmits<{ 'update:modelValue': [value: JobSearchQuery] }>()

const search = ref(props.modelValue.q ?? '')

// Debounced free-text search; other filters apply immediately.
let timer: ReturnType<typeof setTimeout> | undefined
watch(search, (value) => {
  clearTimeout(timer)
  timer = setTimeout(() => update({ q: value.trim() || undefined }), 350)
})

function update(patch: Partial<JobSearchQuery>): void {
  emit('update:modelValue', { ...props.modelValue, ...patch, page: 1 })
}

function clearAll(): void {
  search.value = ''
  emit('update:modelValue', { q: undefined, category: undefined, jobType: undefined, experience: undefined, minPay: undefined, payType: undefined, sort: 'newest', page: 1, pageSize: props.modelValue.pageSize })
}

const sortOptions = [
  { value: 'newest', label: 'Newest first' },
  { value: 'pay-desc', label: 'Highest pay' },
  { value: 'pay-asc', label: 'Lowest pay' },
]

const hasFilters = () =>
  Boolean(
    props.modelValue.q ||
      props.modelValue.category ||
      props.modelValue.jobType ||
      props.modelValue.experience ||
      props.modelValue.minPay ||
      props.modelValue.payType,
  )
</script>

<template>
  <div class="card space-y-4">
    <div class="relative">
      <input
        v-model="search"
        type="search"
        class="input pl-9"
        placeholder="Search title, skill or keyword…"
        aria-label="Search jobs"
        @keydown.enter.prevent
      />
      <span class="pointer-events-none absolute left-3 top-2.5 text-slate-400">⌕</span>
      <p v-if="typeof total === 'number'" class="mt-1 text-xs text-slate-400">{{ total }} job{{ total === 1 ? '' : 's' }} found</p>
    </div>

    <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
      <div>
        <label class="label" for="f-category">Category</label>
        <select
          id="f-category"
          class="input"
          :value="modelValue.category ?? ''"
          @change="update({ category: ($event.target as HTMLSelectElement).value || undefined })"
        >
          <option value="">All categories</option>
          <option v-for="category in categories" :key="category.id" :value="category.slug">{{ category.name }}</option>
        </select>
      </div>

      <div>
        <label class="label" for="f-jobtype">Job type</label>
        <select
          id="f-jobtype"
          class="input"
          :value="modelValue.jobType ?? ''"
          @change="update({ jobType: ($event.target as HTMLSelectElement).value || undefined })"
        >
          <option value="">Any type</option>
          <option v-for="option in JOB_TYPES" :key="option.value" :value="option.value">{{ option.label }}</option>
        </select>
      </div>

      <div>
        <label class="label" for="f-experience">Experience</label>
        <select
          id="f-experience"
          class="input"
          :value="modelValue.experience ?? ''"
          @change="update({ experience: ($event.target as HTMLSelectElement).value || undefined })"
        >
          <option value="">Any level</option>
          <option v-for="option in EXPERIENCE_LEVELS" :key="option.value" :value="option.value">{{ option.label }}</option>
        </select>
      </div>

      <div>
        <label class="label" for="f-paytype">Pay basis</label>
        <select
          id="f-paytype"
          class="input"
          :value="modelValue.payType ?? ''"
          @change="update({ payType: ($event.target as HTMLSelectElement).value || undefined })"
        >
          <option value="">Any</option>
          <option v-for="option in PAY_TYPES" :key="option.value" :value="option.value">{{ option.label }}</option>
        </select>
      </div>

      <div>
        <label class="label" for="f-minpay">Min. pay (hourly/monthly)</label>
        <input
          id="f-minpay"
          type="number"
          min="0"
          class="input"
          placeholder="e.g. 15"
          :value="modelValue.minPay ?? ''"
          @change="update({ minPay: Number(($event.target as HTMLInputElement).value) || undefined })"
        />
      </div>

      <div>
        <label class="label" for="f-sort">Sort by</label>
        <select
          id="f-sort"
          class="input"
          :value="modelValue.sort ?? 'newest'"
          @change="update({ sort: ($event.target as HTMLSelectElement).value })"
        >
          <option v-for="option in sortOptions" :key="option.value" :value="option.value">{{ option.label }}</option>
        </select>
      </div>
    </div>

    <div class="flex items-center justify-between">
      <button v-if="hasFilters()" class="text-sm font-medium text-brand-600 hover:underline" type="button" @click="clearAll">
        Clear filters
      </button>
    </div>
  </div>
</template>
