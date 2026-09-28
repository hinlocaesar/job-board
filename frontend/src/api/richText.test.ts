import { describe, expect, it } from 'vitest'
import { renderRichText, richTextToText } from './richText'

describe('renderRichText', () => {
  it('passes plain strings through', () => {
    expect(renderRichText('<p>hi</p>')).toBe('<p>hi</p>')
    expect(renderRichText(null)).toBe('')
    expect(renderRichText(undefined)).toBe('')
  })

  it('prefers the markup property when present', () => {
    expect(renderRichText({ markup: '<p>from markup</p>' })).toBe('<p>from markup</p>')
  })

  it('renders a Delivery API node tree', () => {
    const html = renderRichText({
      nodeType: 'paragraph',
      content: [
        { nodeType: 'text', value: 'Hello ' },
        {
          nodeType: 'link',
          url: '/jobs',
          content: [{ nodeType: 'text', value: 'jobs' }],
        },
      ],
    })

    expect(html).toContain('<p>')
    expect(html).toContain('Hello ')
    expect(html).toContain('<a href="/jobs">jobs</a>')
  })

  it('renders headings with their level and escapes text', () => {
    const html = renderRichText({
      nodeType: 'heading',
      level: 3,
      content: [{ nodeType: 'text', value: '<script>alert(1)</script>' }],
    })

    expect(html).toBe('<h3>&lt;script&gt;alert(1)&lt;/script&gt;</h3>')
  })

  it('renders lists and auto links', () => {
    const html = renderRichText({
      nodeType: 'list',
      content: [{ nodeType: 'listItem', content: [{ nodeType: 'text', value: 'one' }] }],
    })
    expect(html).toBe('<ul><li>one</li></ul>')

    const link = renderRichText({
      nodeType: 'autoLink',
      url: 'https://example.com',
      queryString: '?a=1',
      target: '_blank',
      content: [{ nodeType: 'text', value: 'site' }],
    })
    expect(link).toContain('href="https://example.com?a=1"')
    expect(link).toContain('rel="noopener nofollow"')
  })

  it('handles an untitled wrapper array', () => {
    const html = renderRichText({ content: [{ nodeType: 'text', value: 'plain' }] })
    expect(html).toBe('plain')
  })

  it('returns empty string for unknown shapes', () => {
    expect(renderRichText({ totally: 'different' })).toBe('')
    expect(renderRichText(42)).toBe('')
  })
})

describe('richTextToText', () => {
  it('strips tags and truncates with an ellipsis', () => {
    const text = richTextToText('<p><strong>Long</strong> body text here</p>')
    expect(text).toBe('Long body text here')

    const long = richTextToText('<p>' + 'x'.repeat(400) + '</p>', 50)
    expect(long).toHaveLength(50)
    expect(long.endsWith('…')).toBe(true)
  })
})
