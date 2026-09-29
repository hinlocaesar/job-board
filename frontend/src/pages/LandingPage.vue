<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { onServerPrefetch } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { propString, umbracoApi, type DeliveryContent } from '../api/umbraco'
import { renderRichText, richTextToText } from '../api/richText'
import AlertBox from '../components/common/AlertBox.vue'
import RichText from '../components/common/RichText.vue'
import Spinner from '../components/common/Spinner.vue'
import Icon from '../components/common/Icon.vue'
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
const ctaTo = computed(() => ({ name: 'jobs' as const }))

const assurances = [
  {
    title: 'Vetted talent',
    body: 'Profiles carry real skills, rates and verified experience — not a one-line pitch.',
  },
  {
    title: 'Reviewed listings',
    body: 'Our team reads every job before it reaches the board, so the noise stays out.',
  },
  {
    title: 'One-click apply',
    body: 'Apply with the profile you already built. No re-typing your résumé each time.',
  },
]
</script>

<template>
  <div>
    <Spinner v-if="loading" label="Loading page…" />

    <div v-else-if="error" class="shell py-20">
      <AlertBox :message="error">
        <p class="mt-2 text-[15px]">
          <RouterLink class="link" :to="{ name: 'home' }">Back to the homepage</RouterLink>
        </p>
      </AlertBox>
    </div>

    <template v-else-if="page">
      <!-- Centred hero, matching the homepage -->
      <section class="shell-wide section pt-16 sm:pt-24">
        <p class="kicker">Hire remote</p>
        <h1 class="display mt-5 text-[40px] sm:text-[64px]">
          {{ propString(page, 'headline') || page.name }}
        </h1>
        <p v-if="propString(page, 'subheadline')" class="muted mx-auto mt-6 max-w-2xl text-[19px] sm:text-[21px]">
          {{ propString(page, 'subheadline') }}
        </p>
        <div class="mt-9 flex flex-wrap justify-center gap-3">
          <RouterLink class="btn-primary btn-lg" :to="ctaTo">{{ ctaLabel }}</RouterLink>
          <RouterLink class="btn-secondary btn-lg" :to="{ name: 'faq' }">How it works</RouterLink>
        </div>
      </section>

      <section class="shell-prose py-16 sm:py-20">
        <RichText :html="renderRichText(page.properties?.body) || renderRichText(propString(page, 'body'))" />
      </section>

      <section class="section-alt section">
        <div class="shell">
          <h2 class="section-title text-center">Why candidates apply here</h2>
          <div class="mx-auto mt-12 grid max-w-4xl gap-10 sm:grid-cols-3">
            <div v-for="item in assurances" :key="item.title">
              <h3 class="text-[19px] font-semibold text-slate-900">{{ item.title }}</h3>
              <p class="muted mt-2 text-pretty text-[17px] leading-relaxed">{{ item.body }}</p>
            </div>
          </div>

          <div class="mt-16 text-center">
            <RouterLink class="btn-dark btn-lg" :to="ctaTo">
              {{ ctaLabel }}
              <Icon name="arrow-right" :size="16" />
            </RouterLink>
          </div>
        </div>
      </section>
    </template>
  </div>
</template>
