/** Selectable option lists (mirrors the API enums, snake_case accepted). */
export const JOB_TYPES = [
  { value: 'FullTime', label: 'Full-time' },
  { value: 'PartTime', label: 'Part-time' },
  { value: 'Contract', label: 'Contract' },
  { value: 'Freelance', label: 'Freelance' },
  { value: 'Internship', label: 'Internship' },
]

export const EXPERIENCE_LEVELS = [
  { value: 'Junior', label: 'Junior' },
  { value: 'Mid', label: 'Mid level' },
  { value: 'Senior', label: 'Senior' },
  { value: 'Lead', label: 'Lead' },
]

export const PAY_TYPES = [
  { value: 'Hourly', label: 'Hourly' },
  { value: 'Monthly', label: 'Monthly' },
  { value: 'FixedPrice', label: 'Fixed price' },
]

export const REGIONS = [
  { value: 'Worldwide', label: 'Worldwide' },
  { value: 'PhilippinesOnly', label: 'Philippines only' },
  { value: 'Custom', label: 'Custom region' },
]

export const AVAILABILITY_OPTIONS = [
  { value: 'FullTime', label: 'Available full-time' },
  { value: 'PartTime', label: 'Available part-time' },
  { value: 'Freelance', label: 'Freelance' },
  { value: 'ProjectBased', label: 'Project-based' },
]

export const RATE_PERIOD_OPTIONS = [
  { value: 'Hour', label: 'per hour' },
  { value: 'Day', label: 'per day' },
  { value: 'Month', label: 'per month' },
]

/** `FullTime` → `Full time` (for values that arrive raw from the API). */
export function humanize(value: string | null | undefined): string {
  if (!value) return '—'
  const spaced = value.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/_/g, ' ')
  return spaced.charAt(0).toUpperCase() + spaced.slice(1)
}

function money(value: number, currency: string | null | undefined): string {
  try {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: currency || 'USD',
      maximumFractionDigits: value % 1 === 0 ? 0 : 2,
    }).format(value)
  } catch {
    return `${currency} ${value}`
  }
}

/** `$35 – $55 /hour`, `₱30,000 – ₱45,000 /month`. */
export function formatPay(job: {
  payType?: string | null
  payMin?: number | null
  payMax?: number | null
  currency?: string | null
}): string {
  const { payMin, payMax, currency, payType } = job
  if (!payMin && !payMax) return 'Negotiable'

  const suffix =
    payType === 'Hourly' ? 'hour' : payType === 'Monthly' ? 'month' : payType === 'FixedPrice' ? 'project' : ''

  const low = payMin ? money(payMin, currency) : ''
  const high = payMax ? money(payMax, currency) : ''
  const range = low && high && low !== high ? `${low} – ${high}` : low || high
  return suffix ? `${range} /${suffix}` : range
}

export function formatDate(value: string | null | undefined, options?: Intl.DateTimeFormatOptions): string {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  return date.toLocaleDateString('en-US', options ?? { year: 'numeric', month: 'short', day: 'numeric' })
}

/** "3 days ago" style label for job cards. */
export function formatRelative(value: string | null | undefined): string {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  const days = Math.floor((Date.now() - date.getTime()) / 86_400_000)
  if (days <= 0) return 'Today'
  if (days === 1) return 'Yesterday'
  if (days < 7) return `${days} days ago`
  if (days < 30) return `${Math.floor(days / 7)} week${days < 14 ? '' : 's'} ago`
  return formatDate(value)
}

/** Turns a problem+json error into a single user-facing message. */
export function errorMessage(error: unknown, fallback = 'Something went wrong. Please try again.'): string {
  if (!error) return fallback
  if (error instanceof Error && error.message) return error.message
  return fallback
}
