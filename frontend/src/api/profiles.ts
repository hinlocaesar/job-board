import { request } from './http'
import type { EmployerProfileDto, EmployerProfileInput, ProfileMessage, WorkerProfileDto, WorkerProfileInput } from './types'

export const profilesApi = {
  createWorker: (body: WorkerProfileInput) => request<ProfileMessage>('/profiles/worker', { method: 'POST', body }),

  updateWorker: (body: WorkerProfileInput) => request<ProfileMessage>('/profiles/worker', { method: 'PUT', body }),

  currentWorker: () => request<WorkerProfileDto>('/profiles/worker/me/current'),

  publicWorker: (slug: string) =>
    request<WorkerProfileDto>(`/profiles/worker/${encodeURIComponent(slug)}`, { auth: false }),

  createEmployer: (body: EmployerProfileInput) =>
    request<ProfileMessage>('/profiles/employer', { method: 'POST', body }),

  updateEmployer: (body: EmployerProfileInput) =>
    request<ProfileMessage>('/profiles/employer', { method: 'PUT', body }),

  currentEmployer: () => request<EmployerProfileDto>('/profiles/employer/me/current'),

  publicEmployer: (slug: string) =>
    request<EmployerProfileDto>(`/profiles/employer/${encodeURIComponent(slug)}`, { auth: false }),
}
