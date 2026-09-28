<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { onServerPrefetch } from 'vue'
import { RouterLink } from 'vue-router'
import { propString, umbracoApi, type DeliveryContent } from '../api/umbraco'
import { renderRichText } from '../api/richText'
import AlertBox from '../components/common/AlertBox.vue'
import RichText from '../components/common/RichText.vue'
import Spinner from '../components/common/Spinner.vue'
import { siteOrigin, useSeo } from '../composables/useSeo'

useSeo({
  title: 'Help & FAQ',
  description: 'Answers about creating a profile, applying to jobs, posting roles and privacy on JobBoard.',
})

interface FaqItem {
  question: string
  answerHtml: string
}

const page = ref<DeliveryContent | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)
const openIndex = ref<number | null>(0)

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    page.value = await umbracoApi.firstByRoute('/faq')
    if (!page.value) error.value = 'The FAQ page has not been created in the CMS yet.'
    else {
      useSeo({
        title: propString(page.value, 'metaTitle') || 'Help & FAQ',
        description: propString(page.value, 'metaDescription') || undefined,
        canonical: `${siteOrigin()}/faq`,
      })
    }
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

/**
 * Reads the `faqItems` Block List as returned by the Delivery API v2:
 * `{ items: [{ content: { contentType, id, properties: { question, answer } } }] }`.
 */
const faqs = computed<FaqItem[]>(() => {
  const raw = page.value?.properties?.faqItems
  const list = Array.isArray(raw) ? raw : Array.isArray((raw as { items?: unknown[] })?.items) ? (raw as { items: unknown[] }).items : []

  return list
    .map((entry) => {
      const block = (entry ?? {}) as { content?: { properties?: Record<string, unknown> } }
      const properties = block.content?.properties ?? (entry as Record<string, unknown>)
      const question = typeof properties.question === 'string' ? properties.question : ''
      return { question, answerHtml: renderRichText(properties.answer) }
    })
    .filter((entry) => entry.question)
})
</script>

<template>
  <div class="mx-auto max-w-3xl px-4 py-8 sm:px-6">
    <header class="border-b border-slate-200 pb-6">
      <h1 class="text-3xl font-bold text-slate-900">{{ propString(page, 'heading') || 'Help & FAQ' }}</h1>
      <p class="mt-2 text-slate-600">{{ propString(page, 'intro') || 'Everything you need to know about using JobBoard.' }}</p>
    </header>

    <div class="mt-6">
      <Spinner v-if="loading" label="Loading FAQ…" />

      <AlertBox v-else-if="error" :message="error">
        <p class="mt-2 text-sm">
          <RouterLink class="font-medium underline" :to="{ name: 'jobs' }">Browse jobs instead →</RouterLink>
        </p>
      </AlertBox>

      <template v-else>
        <!-- Rich intro from the CMS, if present -->
        <RichText v-if="renderRichText(page?.properties?.body)" :html="renderRichText(page?.properties?.body)" />

        <div v-if="faqs.length" class="mt-4 divide-y divide-slate-200 rounded-xl border border-slate-200">
          <section v-for="(faq, index) in faqs" :key="index">
            <button
              class="flex w-full items-center justify-between gap-4 px-5 py-4 text-left"
              type="button"
              @click="openIndex = openIndex === index ? null : index"
            >
              <span class="font-medium text-slate-900">{{ faq.question }}</span>
              <span class="text-slate-400">{{ openIndex === index ? '−' : '+' }}</span>
            </button>
            <div v-if="openIndex === index" class="px-5 pb-5">
              <RichText :html="faq.answerHtml" />
            </div>
          </section>
        </div>

        <p v-else-if="page" class="mt-6 rounded-lg bg-slate-50 px-4 py-6 text-center text-sm text-slate-500">
          No questions have been added to this page yet.
        </p>
      </template>
    </div>
  </div>
</template>
