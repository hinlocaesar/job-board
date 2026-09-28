import { request } from './http'
import type { CategoryDto, JobDetailDto, JobInput, JobMineDto, JobSearchQuery, JobSearchResult } from './types'

export const jobsApi = {
  search: (query: JobSearchQuery = {}) =>
    request<JobSearchResult>('/jobs', { query: query as Record<string, string | number | undefined>, auth: false }),

  detail: (slugOrId: string) => request<JobDetailDto>(`/jobs/${encodeURIComponent(slugOrId)}`, { auth: false }),

  /** Jobs owned by the signed-in employer (any status, incl. drafts). */
  mine: () => request<JobMineDto[]>('/jobs/mine'),

  create: (body: JobInput) => request<JobMessageShape>('/jobs', { method: 'POST', body }),

  update: (id: string, body: JobInput) => request<JobMessageShape>(`/jobs/${id}`, { method: 'PUT', body }),

  close: (id: string) => request<{ status: string }>(`/jobs/${id}/close`, { method: 'POST' }),
}

export const categoriesApi = {
  list: () => request<CategoryDto[]>('/categories', { auth: false }),
}

/** `POST /jobs` and `PUT /jobs/{id}` reply with `{ id, slug, status }`. */
export interface JobMessageShape {
  id?: string
  slug: string
  status: string
}
