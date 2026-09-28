/**
 * Renders Umbraco rich-text values to HTML.
 *
 * The Delivery API can return rich text either as an HTML string or as a JSON
 * node tree (depending on version/expand options) — handle both, then let
 * `RichText.vue` sanitise the result with DOMPurify before display.
 */

interface RichNode {
  nodeType?: string
  value?: string
  markup?: string
  content?: RichNode[]
  url?: string
  queryString?: string
  target?: string
  level?: number
  style?: Array<{ alias?: string; value?: string }>
}

function escapeHtml(value: string): string {
  return value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;')
}

function renderChildren(node: RichNode): string {
  return (node.content ?? []).map(renderNode).join('')
}

function renderNode(node: RichNode): string {
  const type = (node.nodeType ?? '').toLowerCase()

  switch (type) {
    case 'text':
      return escapeHtml(node.value ?? '')
    case 'paragraph':
      return `<p>${renderChildren(node)}</p>`
    case 'heading':
      return `<h${node.level ?? 2}>${renderChildren(node)}</h${node.level ?? 2}>`
    case 'quote':
      return `<blockquote>${renderChildren(node)}</blockquote>`
    case 'list':
      return `<ul>${renderChildren(node)}</ul>`
    case 'listitem':
      return `<li>${renderChildren(node)}</li>`
    case 'link':
    case 'autolink': {
      const href = [node.url, node.queryString].filter(Boolean).join('')
      const target = node.target === '_blank' ? ' target="_blank" rel="noopener nofollow"' : ''
      return `<a href="${escapeHtml(href ?? '#')}"${target}>${renderChildren(node)}</a>`
    }
    case 'raw':
      // Trusted CMS-authored raw block; DOMPurify sanitises it downstream.
      return node.value ?? ''
    case 'linebreak':
      return '<br>'
    default:
      return renderChildren(node) || escapeHtml(node.value ?? '')
  }
}

export function renderRichText(value: unknown): string {
  if (value === null || value === undefined) return ''

  if (typeof value === 'string') return value

  if (typeof value === 'object') {
    const node = value as RichNode
    if (typeof node.markup === 'string') return node.markup as unknown as string
    if (node.nodeType || node.content) return renderNode(node)

    // `{ content: [...] }` wrapper without an explicit nodeType
    if (Array.isArray((value as { content?: unknown }).content)) {
      return ((value as { content: RichNode[] }).content ?? []).map(renderNode).join('')
    }
  }

  return ''
}

/** Plain-text excerpt (for meta descriptions and card previews). */
export function richTextToText(value: unknown, maxLength = 180): string {
  const html = renderRichText(value)
  const text = html
    .replace(/<[^>]+>/g, ' ')
    .replace(/&nbsp;/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
  return text.length > maxLength ? `${text.slice(0, maxLength - 1)}…` : text
}
