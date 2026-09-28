import { describe, expect, it } from 'vitest'
import { prop, propString, routeSlug, type DeliveryContent } from './umbraco'

const post: DeliveryContent = {
  id: 'fcb2d3f9-3041-4ce9-b40b-134f9025eaa2',
  name: '5 Tasks You Can Delegate to a VA This Week',
  contentType: 'blogPost',
  createDate: '2026-09-28T08:36:46.4757802Z',
  updateDate: '2026-09-28T08:36:46.5155379Z',
  route: {
    path: '/blog/5-tasks-you-can-delegate-to-a-va-this-week/',
    queryString: null,
    startItem: { id: 'aa568b0b-e5bf-4991-b73d-b7962bed533b', path: 'home' },
  },
  properties: {
    title: '5 Tasks You Can Delegate to a VA This Week',
    author: 'John Dela Cruz',
    publishDate: '2026-09-10T00:00:00',
    // Rich text arrives as { markup, blocks } with RichTextOutputAsJson = false.
    body: { markup: '<p>Delegation fails when it is vague.</p>', blocks: [] },
    metaDescription: '',
  },
}

describe('routeSlug', () => {
  it('takes the last segment of route.path and drops slashes', () => {
    expect(routeSlug(post)).toBe('5-tasks-you-can-delegate-to-a-va-this-week')
  })

  it('is safe with missing content or routes', () => {
    expect(routeSlug(null)).toBe('')
    expect(routeSlug({ ...post, route: undefined })).toBe('')
    expect(routeSlug({ ...post, route: { path: null } })).toBe('')
  })
})

describe('prop', () => {
  it('returns the value or the fallback', () => {
    expect(prop(post, 'author', 'unknown')).toBe('John Dela Cruz')
    expect(prop(post, 'nope', 'fallback')).toBe('fallback')
    expect(prop(null, 'author', 'fallback')).toBe('fallback')
  })

  it('treats empty strings as missing', () => {
    expect(prop(post, 'metaDescription', 'fallback')).toBe('fallback')
  })
})

describe('propString', () => {
  it('unwraps rich text markup', () => {
    expect(propString(post, 'body')).toBe('<p>Delegation fails when it is vague.</p>')
  })

  it('returns plain strings unchanged and falls back otherwise', () => {
    expect(propString(post, 'title', 'fallback')).toBe('5 Tasks You Can Delegate to a VA This Week')
    expect(propString(post, 'missing', 'fallback')).toBe('fallback')
    expect(propString(null, 'title', 'fallback')).toBe('fallback')
    expect(propString(post, 'body')).not.toBe('fallback')
  })
})
