<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { onServerPrefetch } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { propString, umbracoApi, type DeliveryContent } from '../api/umbraco'
import { renderRichText, richTextToText } from '../api/richText'
import AlertBox from '../components/common/AlertBox.vue'
import RichText from '../components/common/RichText.vue'
import Spinner from '../components/common/Spinner.vue'
import { formatDate } from '../composables/useFormat'
import { siteOrigin, useSeo } from '../composables/useSeo'

const route = useRoute()

const post = ref<DeliveryContent | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

const slug = computed(() => String(route.params.slug))

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    post.value = await umbracoApi.firstByRoute(`/blog/${slug.value}`)
    if (!post.value) {
      error.value = 'This post does not exist (or is not published).'
      return
    }

    const title = propString(post.value, 'metaTitle') || propString(post.value, 'title') || post.value.name
    const description =
      propString(post.value, 'metaDescription') ||
      propString(post.value, 'excerpt') ||
      richTextToText(post.value.properties?.body)
    useSeo({
      title,
      description: description || undefined,
      canonical: `${siteOrigin()}/blog/${slug.value}`,
      ogType: 'article',
    })
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Could not reach the CMS.'
  } finally {
    loading.value = false
  }
}

onServerPrefetch(() => load())
onMounted(() => {
  if (!post.value && !error.value) void load()
})

const body = computed(() => {
  if (!post.value) return ''
  return (
    renderRichText(post.value.properties?.body) ||
    renderRichText(propString(post.value, 'body')) ||
    renderRichText(post.value.properties?.content)
  )
})
</script>

<template>
  <div class="mx-auto max-w-3xl px-4 py-8 sm:px-6">
    <RouterLink :to="{ name: 'blog' }" class="text-sm text-brand-600 hover:underline">← All posts</RouterLink>

    <Spinner v-if="loading" label="Loading post…" />

    <AlertBox v-else-if="error" :message="error" />

    <article v-else-if="post">
      <header class="mt-4 border-b border-slate-200 pb-6">
        <h1 class="text-3xl font-bold text-slate-900">
          {{ propString(post, 'title') || post.name }}
        </h1>
        <p class="mt-2 text-sm text-slate-400">
          {{ formatDate(propString(post, 'publishDate') || post.updateDate) }}
          <span v-if="propString(post, 'author')"> · {{ propString(post, 'author') }}</span>
        </p>
      </header>

      <div class="mt-6">
        <RichText :html="body" />
      </div>
    </article>
  </div>
</template>
