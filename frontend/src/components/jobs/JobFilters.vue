<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { CategoryDto, JobSearchQuery } from '../../api/types'
import { EXPERIENCE_LEVELS, JOB_TYPES, PAY_TYPES } from '../../composables/useFormat'
import Icon from '../common/Icon.vue'

const props = defineProps<{
  modelValue: JobSearchQuery
  categories: CategoryDto[]
  total?: number
}>()

const emit = defineEmits<{ 'update:modelValue': [value: JobSearchQuery] }>()

const search = ref(props.modelValue.q ?? '')

// Debounced free-text search; the pills and selects apply immediately.
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
  emit('update:modelValue', {
    q: undefined,
    category: undefined,
    jobType: undefined,
    experience: undefined,
    minPay: undefined,
    payType: undefined,
    sort: 'newest',
    page: 1,
    pageSize: props.modelValue.pageSize,
  })
}

/** Pill row: "All" plus one pill per category. */
const categoryPills = computed(() => [
  { value: '', label: 'All' },
  ...props.categories.map((c) => ({ value: c.slug, label: c.name })),
])

const sortOptions = [
  { value: 'newest', label: 'Most recent' },
  { value: 'pay-desc', label: 'Highest pay' },
  { value: 'pay-asc', label: 'Lowest pay' },
]

const activeCount = computed(
  () =>
    [
      props.modelValue.q,
      props.modelValue.category,
      props.modelValue.jobType,
      props.modelValue.experience,
      props.modelValue.minPay,
      props.modelValue.payType,
    ].filter(Boolean).length,
)
</script>

<template>
  <div>
    <!-- Search + sort -->
    <div class="flex flex-wrap items-center gap-3">
      <div class="relative min-w-0 flex-1">
        <span class="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-slate-400">
          <Icon name="search" :size="17" />
        </span>
        <label class="sr-only" for="filter-search">Search jobs</label>
        <input
          id="filter-search"
          v-model="search"
          type="search"
          class="input rounded-full py-3 pl-11"
          placeholder="Search by title, skill or keyword"
          @keydown.enter.prevent
        />
      </div>

      <button v-if="activeCount" class="btn-ghost btn-sm" type="button" @click="clearAll">
        Clear filters
      </button>

      <!-- Full width on mobile so it cannot squeeze the search field. -->
      <div class="w-full sm:w-44">
        <label class="sr-only" for="filter-sort">Sort by</label>
        <select id="filter-sort" class="input rounded-full py-2.5 text-[14px]" :value="modelValue.sort ?? 'newest'" @change="update({ sort: ($event.target as HTMLSelectElement).value })">
          <option v-for="option in sortOptions" :key="option.value" :value="option.value">
            {{ option.label }}
          </option>
        </select>
      </div>
    </div>

    <!-- Category pills -->
    <div class="nav-scroll mt-4 flex gap-2 overflow-x-auto pb-1">
      <button
        v-for="pill in categoryPills"
        :key="pill.value || 'all'"
        type="button"
        class="shrink-0 rounded-full px-4 py-1.5 text-[14px] font-medium transition-colors"
        :class="
          (modelValue.category ?? '') === pill.value
            ? 'bg-slate-900 text-white'
            : 'bg-slate-100 text-slate-700 hover:bg-slate-200'
        "
        :aria-pressed="(modelValue.category ?? '') === pill.value"
        @click="update({ category: pill.value || undefined })"
      >
        {{ pill.label }}
      </button>
    </div>

    <!-- Refinements -->
    <div class="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
      <div>
        <label class="label" for="f-jobtype">Job type</label>
        <select
          id="f-jobtype"
          class="input rounded-full py-2.5 text-[14px]"
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
          class="input rounded-full py-2.5 text-[14px]"
          :value="modelValue.experience ?? ''"
          @change="update({ experience: ($event.target as HTMLSelectElement).value || undefined })"
        >
          <option value="">Any level</option>
          <option v-for="option in EXPERIENCE_LEVELS" :key="option.value" :value="option.value">
            {{ option.label }}
          </option>
        </select>
      </div>

      <div>
        <label class="label" for="f-paytype">Pay basis</label>
        <select
          id="f-paytype"
          class="input rounded-full py-2.5 text-[14px]"
          :value="modelValue.payType ?? ''"
          @change="update({ payType: ($event.target as HTMLSelectElement).value || undefined })"
        >
          <option value="">Any</option>
          <option v-for="option in PAY_TYPES" :key="option.value" :value="option.value">{{ option.label }}</option>
        </select>
      </div>

      <div>
        <label class="label" for="f-minpay">Minimum pay</label>
        <input
          id="f-minpay"
          type="number"
          min="0"
          class="input nums rounded-full py-2.5 text-[14px]"
          placeholder="Any"
          :value="modelValue.minPay ?? ''"
          @change="update({ minPay: Number(($event.target as HTMLInputElement).value) || undefined })"
        />
      </div>
    </div>
  </div>
</template>
