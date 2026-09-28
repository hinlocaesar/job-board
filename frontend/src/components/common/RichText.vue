<script setup lang="ts">
import { computed } from 'vue'
import DOMPurify from 'dompurify'

const props = defineProps<{ html?: string | null }>()

/**
 * Rich text from Umbraco is sanitised before it reaches the DOM (AGENTS.md).
 * During prerender there is no `window`, so the raw markup passes through and
 * the browser re-renders (and sanitises) it on hydration — identical output for
 * safe content, patched output for anything unsafe.
 */
const sanitized = computed(() => {
  const html = props.html ?? ''
  if (import.meta.env.SSR) return html

  return DOMPurify.sanitize(html, {
    ALLOWED_TAGS: [
      'p', 'h2', 'h3', 'h4', 'strong', 'em', 'u', 'a', 'ul', 'ol', 'li',
      'blockquote', 'br', 'hr', 'code', 'pre', 'img', 'figure', 'figcaption',
    ],
    ALLOWED_ATTR: ['href', 'title', 'target', 'rel', 'src', 'alt', 'width', 'height'],
    ALLOW_DATA_ATTR: false,
  })
})
</script>

<template>
  <!-- eslint-disable-next-line vue/no-v-html — sanitised above -->
  <div class="prose-basic" v-html="sanitized" />
</template>
