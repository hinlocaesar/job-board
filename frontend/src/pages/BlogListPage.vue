<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { onServerPrefetch } from 'vue'
import { RouterLink } from 'vue-router'
import { propString, routeSlug, umbracoApi, type DeliveryContent } from '../api/umbraco'
import { richTextToText } from '../api/richText'
import AlertBox from '../components/common/AlertBox.vue'
import EmptyState from '../components/common/EmptyState.vue'
import Spinner from '../components/common/Spinner.vue'
import { formatDate } from '../composables/useFormat'
import { useSeo } from '../composables/useSeo'

useSeo({
  title: 'Blog',
  description: 'Remote-work insights, hiring guides and productivity tips for distributed teams.',
})

const posts = ref<DeliveryContent[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const envelope = await umbracoApi.children('/blog', 50)
    posts.value = envelope.items ?? []
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Could not reach the CMS.'
  } finally {
    loading.value = false
  }
}

onServerPrefetch(() => load())
onMounted(() => {
  if (loading.value && posts.value.length === 0 && !error.value) void load()
})

function excerpt(post: DeliveryContent): string {
  return propString(post, 'excerpt') || richTextToText(post.properties?.body) || propString(post, 'metaDescription')
}

function postTitle(post: DeliveryContent): string {
  return propString(post, 'title') || propString(post, 'pageTitle') || post.name
}

function publishDate(post: DeliveryContent): string {
  return propString(post, 'publishDate') || post.updateDate
}
</script>

<template>
  <div class="mx-auto max-w-4xl px-4 py-8 sm:px-6">
    <header class="border-b border-slate-200 pb-6">
      <h1 class="text-3xl font-bold text-slate-900">Blog</h1>
      <p class="mt-2 text-slate-600">Guides on remote hiring, managing distributed teams and working from the Philippines.</p>
    </header>

    <div class="mt-6">
      <Spinner v-if="loading" label="Loading posts…" />

      <AlertBox v-else-if="error" :message="`Could not load the blog: ${error}`">
        <p class="mt-2 text-sm">Make sure the Umbraco instance is running, then reload.</p>
      </AlertBox>

      <EmptyState v-else-if="posts.length === 0" title="Nothing published yet" message="Check back soon for new articles." />

      <div v-else class="space-y-6">
        <article v-for="post in posts" :key="post.id" class="card">
          <RouterLink :to="{ name: 'blog-post', params: { slug: routeSlug(post) } }">
            <h2 class="text-xl font-semibold text-slate-900 hover:text-brand-600">
              {{ postTitle(post) }}
            </h2>
          </RouterLink>
          <p class="mt-1 text-xs text-slate-400">
            {{ formatDate(publishDate(post)) }}
            <span v-if="propString(post, 'author')"> · {{ propString(post, 'author') }}</span>
          </p>
          <p class="mt-2 text-slate-600">{{ excerpt(post) }}</p>
          <RouterLink
            class="mt-3 inline-block text-sm font-medium text-brand-600 hover:underline"
            :to="{ name: 'blog-post', params: { slug: routeSlug(post) } }"
          >
            Read more →
          </RouterLink>
        </article>
      </div>
    </div>
  </div>
</template>
