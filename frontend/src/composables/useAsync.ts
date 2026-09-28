import { onMounted, onServerPrefetch, ref, type Ref } from 'vue'
import { errorMessage } from './useFormat'

export interface AsyncState<T> {
  data: Ref<T | null>
  error: Ref<string | null>
  loading: Ref<boolean>
  load: () => Promise<void>
}

/**
 * Loads data once during prerender (`onServerPrefetch`) and again in the
 * browser when no state was transferred — simple, predictable, no cache layer.
 */
export function useAsync<T>(loader: () => Promise<T>, immediate = true): AsyncState<T> {
  const data = ref<T | null>(null) as Ref<T | null>
  const error = ref<string | null>(null)
  const loading = ref(false)

  async function load(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      data.value = await loader()
    } catch (caught) {
      error.value = errorMessage(caught)
      data.value = null
    } finally {
      loading.value = false
    }
  }

  if (immediate) {
    onServerPrefetch(() => load())
    onMounted(() => {
      // Prerendered HTML arrives without JS state → refetch in the browser.
      if (data.value === null && !loading.value) void load()
    })
  }

  return { data, error, loading, load }
}
