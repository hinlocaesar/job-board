<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { onServerPrefetch } from 'vue'
import { RouterLink } from 'vue-router'
import { propString, routeSlug, umbracoApi, type DeliveryContent } from '../api/umbraco'
import { richTextToText } from '../api/richText'
import AlertBox from '../components/common/AlertBox.vue'
import EmptyState from '../components/common/EmptyState.vue'
import Spinner from '../components/common/Spinner.vue'
import Icon from '../components/common/Icon.vue'
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

function author(post: DeliveryContent): string {
  return propString(post, 'author')
}
</script>

<template>
  <div class="border-b border-slate-200">
    <div class="shell py-4">
      <nav class="breadcrumb" aria-label="Breadcrumb">
        <RouterLink :to="{ name: 'home' }">Home</RouterLink>
        <span class="breadcrumb-sep" aria-hidden="true">›</span>
        <span class="font-medium text-slate-900">Blog</span>
      </nav>
    </div>
  </div>

  <div class="shell py-16 sm:py-24">
    <div class="max-w-2xl">
      <h1 class="display text-[44px] sm:text-[64px]">Blog</h1>
      <p class="muted mt-5 text-[19px] sm:text-[21px]">
        Guides on remote hiring, managing distributed teams and working from the Philippines.
      </p>
    </div>

    <div class="mt-14">
      <Spinner v-if="loading" label="Loading posts…" />

      <AlertBox v-else-if="error" :message="`Could not load the blog: ${error}`">
        <p class="mt-2 text-[15px]">Make sure the Umbraco instance is running, then reload.</p>
      </AlertBox>

      <EmptyState
        v-else-if="posts.length === 0"
        title="Nothing published yet"
        message="Check back soon for new articles."
      />

      <div v-else class="max-w-3xl">
        <article
          v-for="post in posts"
          :key="post.id"
          class="group border-b border-slate-200 py-9 first:border-t first:pt-0"
        >
          <p class="flex flex-wrap items-center gap-x-2.5 text-[13px] text-slate-500">
            <time>{{ formatDate(publishDate(post)) }}</time>
            <template v-if="author(post)">
              <span class="text-slate-300" aria-hidden="true">·</span>
              <span>{{ author(post) }}</span>
            </template>
          </p>

          <RouterLink :to="{ name: 'blog-post', params: { slug: routeSlug(post) } }" class="block">
            <h2
              class="mt-2 text-[26px] font-semibold leading-snug tracking-tight text-slate-900 transition-colors group-hover:text-brand-600 sm:text-[30px]"
            >
              {{ postTitle(post) }}
            </h2>
          </RouterLink>

          <p class="muted mt-3 text-pretty text-[17px] leading-relaxed">{{ excerpt(post) }}</p>

          <RouterLink
            class="arrow-link mt-4 text-[15px]"
            :to="{ name: 'blog-post', params: { slug: routeSlug(post) } }"
          >
            Read more
            <Icon name="arrow-right" :size="15" />
          </RouterLink>
        </article>
      </div>
    </div>
  </div>
</template>
