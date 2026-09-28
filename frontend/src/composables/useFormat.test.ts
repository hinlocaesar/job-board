import { describe, expect, it } from 'vitest'
import {
  errorMessage,
  formatDate,
  formatPay,
  formatRelative,
  humanize,
} from './useFormat'

describe('humanize', () => {
  it('splits camelCase enum values', () => {
    expect(humanize('FullTime')).toBe('Full Time')
    expect(humanize('FixedPrice')).toBe('Fixed Price')
    expect(humanize('AsiaPacific')).toBe('Asia Pacific')
  })

  it('handles snake_case, null and empty values', () => {
    expect(humanize('part_time')).toBe('Part time')
    expect(humanize(null)).toBe('—')
    expect(humanize(undefined)).toBe('—')
    expect(humanize('')).toBe('—')
  })
})

describe('formatPay', () => {
  it('returns a formatted range with the period suffix', () => {
    expect(
      formatPay({ payType: 'Hourly', payMin: 35, payMax: 55, currency: 'USD' }),
    ).toBe('$35 – $55 /hour')

    expect(
      formatPay({ payType: 'Monthly', payMin: 30000, payMax: 45000, currency: 'PHP' }),
    ).toBe('₱30,000 – ₱45,000 /month')
  })

  it('shows a single value when min and max match', () => {
    expect(
      formatPay({ payType: 'Hourly', payMin: 25, payMax: 25, currency: 'USD' }),
    ).toBe('$25 /hour')
  })

  it('falls back to one side when only one bound exists', () => {
    expect(formatPay({ payType: 'Monthly', payMin: 1000, payMax: null, currency: 'USD' })).toBe(
      '$1,000 /month',
    )
    expect(formatPay({ payType: 'Monthly', payMin: 0, payMax: 2000, currency: 'USD' })).toBe(
      '$2,000 /month',
    )
  })

  it('returns "Negotiable" without any bounds', () => {
    expect(formatPay({ payType: 'Hourly', payMin: 0, payMax: 0, currency: 'USD' })).toBe(
      'Negotiable',
    )
    expect(formatPay({})).toBe('Negotiable')
  })

  it('appends no suffix for unknown pay types and tolerates bad currencies', () => {
    expect(formatPay({ payType: 'Weird', payMin: 10, payMax: 20, currency: 'USD' })).toBe(
      '$10 – $20',
    )
    expect(formatPay({ payType: 'Hourly', payMin: 10, payMax: 20, currency: 'NOPE' })).toContain(
      '10',
    )
  })
})

describe('formatDate', () => {
  it('formats ISO dates and rejects junk', () => {
    expect(formatDate('2026-01-15T00:00:00Z')).toMatch(/Jan 1[45], 2026/)
    expect(formatDate(null)).toBe('—')
    expect(formatDate(undefined)).toBe('—')
    expect(formatDate('not-a-date')).toBe('—')
  })
})

describe('formatRelative', () => {
  const now = Date.now()
  const daysAgo = (days: number) => new Date(now - days * 86_400_000).toISOString()

  it('describes recency in plain language', () => {
    expect(formatRelative(daysAgo(0))).toBe('Today')
    expect(formatRelative(daysAgo(1))).toBe('Yesterday')
    expect(formatRelative(daysAgo(3))).toBe('3 days ago')
    expect(formatRelative(daysAgo(10))).toBe('1 week ago')
    expect(formatRelative(daysAgo(40))).not.toBe('')
    expect(formatRelative(null)).toBe('')
  })
})

describe('errorMessage', () => {
  it('unwraps Error instances', () => {
    expect(errorMessage(new Error('boom'))).toBe('boom')
    expect(errorMessage(undefined, 'fallback')).toBe('fallback')
    expect(errorMessage('plain string')).toBe('Something went wrong. Please try again.')
  })
})
