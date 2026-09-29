<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { onServerPrefetch } from 'vue'
import { RouterLink } from 'vue-router'
import { propString, umbracoApi, type DeliveryContent } from '../api/umbraco'
import { renderRichText } from '../api/richText'
import AlertBox from '../components/common/AlertBox.vue'
import RichText from '../components/common/RichText.vue'
import Spinner from '../components/common/Spinner.vue'
import EmptyState from '../components/common/EmptyState.vue'
import Icon from '../components/common/Icon.vue'
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
  <div class="border-b border-slate-200">
    <div class="shell py-4">
      <nav class="breadcrumb" aria-label="Breadcrumb">
        <RouterLink :to="{ name: 'home' }">Home</RouterLink>
        <span class="breadcrumb-sep" aria-hidden="true">›</span>
        <span class="font-medium text-slate-900">Help &amp; FAQ</span>
      </nav>
    </div>
  </div>

  <div class="shell-prose py-16 sm:py-24">
    <h1 class="display text-[40px] sm:text-[56px]">
      {{ propString(page, 'heading') || 'Help & FAQ' }}
    </h1>
    <p class="muted mt-5 text-[19px]">
      {{ propString(page, 'intro') || 'Everything you need to know about using JobBoard.' }}
    </p>

    <div class="mt-12">
      <Spinner v-if="loading" label="Loading FAQ…" />

      <AlertBox v-else-if="error" :message="error">
        <p class="mt-2 text-[15px]">
          <RouterLink class="link" :to="{ name: 'jobs' }">Browse jobs instead</RouterLink>
        </p>
      </AlertBox>

      <template v-else>
        <!-- Rich intro from the CMS, if present -->
        <RichText
          v-if="renderRichText(page?.properties?.body)"
          :html="renderRichText(page?.properties?.body)"
        />

        <div v-if="faqs.length" class="mt-10">
          <section v-for="(faq, index) in faqs" :key="index" class="border-b border-slate-200">
            <h2>
              <button
                class="flex w-full items-center justify-between gap-6 py-5 text-left"
                type="button"
                :aria-expanded="openIndex === index"
                @click="openIndex = openIndex === index ? null : index"
              >
                <span
                  class="text-[19px] font-medium transition-colors"
                  :class="openIndex === index ? 'text-brand-600' : 'text-slate-900'"
                >
                  {{ faq.question }}
                </span>
                <span class="shrink-0 text-slate-400">
                  <Icon :name="openIndex === index ? 'minus' : 'plus'" :size="18" />
                </span>
              </button>
            </h2>
            <div v-if="openIndex === index" class="pb-6">
              <RichText :html="faq.answerHtml" />
            </div>
          </section>
        </div>

        <EmptyState
          v-else-if="page"
          class="mt-10"
          title="No questions yet"
          message="Nothing has been added to this page so far."
        />

        <div class="mt-12 flex flex-wrap gap-3">
          <RouterLink class="btn-primary" :to="{ name: 'jobs' }">Browse jobs</RouterLink>
          <RouterLink class="btn-secondary" :to="{ name: 'register' }">Create a profile</RouterLink>
        </div>
      </template>
    </div>
  </div>
</template>
