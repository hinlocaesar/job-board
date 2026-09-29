import { beforeEach, describe, expect, it } from 'vitest'
import { getSeo, renderHeadHtml, resetSeo, siteOrigin, useSeo } from './useSeo'

describe('useSeo', () => {
  beforeEach(() => {
    resetSeo()
  })

  it('collects title/description/canonical for the rendered head', () => {
    useSeo({ title: 'Remote jobs', description: 'Find work', canonical: 'https://jobboard.test/jobs' })

    expect(getSeo().title).toBe('Remote jobs')
    const html = renderHeadHtml()
    expect(html).toContain('<title>Remote jobs · Filipino VA</title>')
    expect(html).toContain('<meta name="description" content="Find work">')
    expect(html).toContain('<link rel="canonical" href="https://jobboard.test/jobs">')
    expect(html).toContain('og:url" content="https://jobboard.test/jobs"')
    expect(html).toContain('name="robots" content="index, follow"')
  })

  it('marks private pages as noindex', () => {
    useSeo({ title: 'Log in', noindex: true })
    expect(renderHeadHtml()).toContain('name="robots" content="noindex, nofollow"')
  })

  it('does not append the site name twice', () => {
    // The brand contains a space, so the check is a plain substring match —
    // a title already carrying it must be returned untouched.
    useSeo({ title: 'FAQ — Hiring Filipino VAs | Filipino VA' })
    expect(renderHeadHtml()).toContain('<title>FAQ — Hiring Filipino VAs | Filipino VA</title>')
  })

  it('still appends when the title only reads like the brand', () => {
    useSeo({ title: 'How to hire remote help' })
    expect(renderHeadHtml()).toContain('<title>How to hire remote help · Filipino VA</title>')
  })

  it('escapes HTML in tags', () => {
    useSeo({ title: '<script>alert(1)</script>', description: 'a & b' })
    const html = renderHeadHtml()
    expect(html).not.toContain('<script>')
    expect(html).toContain('&lt;script&gt;')
    expect(html).toContain('a &amp; b')
  })

  it('falls back to a default title and merges repeated calls', () => {
    useSeo({ title: 'First' })
    useSeo({ description: 'Second' })
    const html = renderHeadHtml()
    expect(html).toContain('<title>First · Filipino VA</title>')
    expect(html).toContain('content="Second"')
  })

  it('reset clears the collected head', () => {
    useSeo({ title: 'Gone' })
    resetSeo()
    expect(getSeo()).toEqual({})
  })
})

describe('siteOrigin', () => {
  it('is always an absolute origin', () => {
    expect(siteOrigin()).toMatch(/^https?:\/\//)
  })
})
