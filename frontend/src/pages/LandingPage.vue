<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { onServerPrefetch } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { propString, umbracoApi, type DeliveryContent } from '../api/umbraco'
import { renderRichText, richTextToText } from '../api/richText'
import AlertBox from '../components/common/AlertBox.vue'
import RichText from '../components/common/RichText.vue'
import Spinner from '../components/common/Spinner.vue'
import { siteOrigin, useSeo } from '../composables/useSeo'

const route = useRoute()

const page = ref<DeliveryContent | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

const slug = computed(() => `/${String(route.params.slug)}`)

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    page.value = await umbracoApi.firstByRoute(slug.value)
    if (!page.value) {
      error.value = 'This landing page does not exist (or is not published).'
      return
    }

    useSeo({
      title: propString(page.value, 'metaTitle') || propString(page.value, 'headline') || page.value.name,
      description: propString(page.value, 'metaDescription') || richTextToText(page.value.properties?.body) || undefined,
      canonical: `${siteOrigin()}${slug.value}`,
    })
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Could not reach the CMS.'
  } finally {
    loading.value = false
  }
}

onServerPrefetch(() => load())
onMounted(() => {
  if (!page.value && !error.value) void load()
})

const ctaLabel = computed(() => propString(page.value, 'ctaLabel') || 'Browse jobs')
const ctaTo = computed(() => ({ name: 'jobs' as const }))</script>

<template>
  <div>
    <Spinner v-if="loading" label="Loading page…" />

    <div v-else-if="error" class="mx-auto max-w-3xl px-4 py-12 sm:px-6">
      <AlertBox :message="error">
        <p class="mt-2 text-sm">
          <RouterLink class="font-medium underline" :to="{ name: 'home' }">Back to the homepage →</RouterLink>
        </p>
      </AlertBox>
    </div>

    <template v-else-if="page">
      <section class="border-b border-slate-200 bg-gradient-to-b from-brand-50 to-white">
        <div class="mx-auto max-w-4xl px-4 py-14 sm:px-6">
          <h1 class="text-3xl font-bold text-slate-900 sm:text-4xl">
            {{ propString(page, 'headline') || page.name }}
          </h1>
          <p v-if="propString(page, 'subheadline')" class="mt-3 max-w-2xl text-lg text-slate-600">
            {{ propString(page, 'subheadline') }}
          </p>
          <div class="mt-6 flex flex-wrap gap-3">
            <RouterLink class="btn-primary px-6 py-3 text-base" :to="ctaTo">{{ ctaLabel }}</RouterLink>
            <RouterLink class="btn-secondary px-6 py-3 text-base" :to="{ name: 'faq' }">How it works</RouterLink>
          </div>
        </div>
      </section>

      <section class="mx-auto max-w-3xl px-4 py-10 sm:px-6">
        <RichText :html="renderRichText(page.properties?.body) || renderRichText(propString(page, 'body'))" />

        <div class="mt-8 grid gap-4 sm:grid-cols-3">
          <div class="card text-center">
            <p class="text-2xl">👩‍💻</p>
            <h2 class="mt-2 font-semibold text-slate-900">Vetted talent</h2>
            <p class="mt-1 text-sm text-slate-500">Profiles with skills, rates and verified experience.</p>
          </div>
          <div class="card text-center">
            <p class="text-2xl">🛡️</p>
            <h2 class="mt-2 font-semibold text-slate-900">Reviewed listings</h2>
            <p class="mt-1 text-sm text-slate-500">Every job is moderated before it goes live.</p>
          </div>
          <div class="card text-center">
            <p class="text-2xl">⚡</p>
            <h2 class="mt-2 font-semibold text-slate-900">One-click apply</h2>
            <p class="mt-1 text-sm text-slate-500">Apply with your profile and resume in seconds.</p>
          </div>
        </div>

        <div class="mt-10">
          <RouterLink class="btn-primary px-6 py-3" :to="ctaTo">{{ ctaLabel }} →</RouterLink>
        </div>
      </section>
    </template>
  </div>
</template>
