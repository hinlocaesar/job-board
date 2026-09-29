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
  <div class="border-b border-slate-200">
    <div class="shell py-4">
      <nav class="breadcrumb" aria-label="Breadcrumb">
        <RouterLink :to="{ name: 'home' }">Home</RouterLink>
        <span class="breadcrumb-sep" aria-hidden="true">›</span>
        <RouterLink :to="{ name: 'blog' }">Blog</RouterLink>
        <span class="breadcrumb-sep" aria-hidden="true">›</span>
        <span class="truncate text-slate-900">{{ propString(post, 'title') || post?.name || 'Post' }}</span>
      </nav>
    </div>
  </div>

  <div class="shell-prose py-16 sm:py-24">
    <RouterLink :to="{ name: 'blog' }" class="arrow-link text-[15px]">
      <Icon name="arrow-left" :size="15" />
      All posts
    </RouterLink>

    <Spinner v-if="loading" label="Loading post…" />

    <AlertBox v-else-if="error" :message="error" />

    <article v-else-if="post">
      <header class="mt-8">
        <h1 class="display text-[38px] sm:text-[52px]">
          {{ propString(post, 'title') || post.name }}
        </h1>
        <p class="mt-5 flex flex-wrap items-center gap-x-2.5 text-[15px] text-slate-500">
          <time>{{ formatDate(propString(post, 'publishDate') || post.updateDate) }}</time>
          <template v-if="propString(post, 'author')">
            <span class="text-slate-300" aria-hidden="true">·</span>
            <span>{{ propString(post, 'author') }}</span>
          </template>
        </p>
      </header>

      <div class="mt-10">
        <RichText :html="body" />
      </div>
    </article>
  </div>
</template>
