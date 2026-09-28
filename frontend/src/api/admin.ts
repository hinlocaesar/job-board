import { request } from './http'
import type { AuditDto, JobQueueDto, StatusResponse, UserSummaryDto } from './types'

export const adminApi = {
  /** Moderation queue, filtered by job status (Pending/Approved/Rejected/Flagged). */
  queue: (status = 'Pending', take = 50) =>
    request<JobQueueDto[]>('/admin/jobs', { query: { status, take } }),

  approve: (jobId: string) => request<StatusResponse>(`/admin/jobs/${jobId}/approve`, { method: 'POST' }),

  reject: (jobId: string, reason: string) =>
    request<StatusResponse>(`/admin/jobs/${jobId}/reject`, { method: 'POST', body: { reason } }),

  flag: (jobId: string, reason: string) =>
    request<StatusResponse>(`/admin/jobs/${jobId}/flag`, { method: 'POST', body: { reason } }),

  users: () => request<UserSummaryDto[]>('/admin/users'),

  ban: (userId: string, reason: string) =>
    request<StatusResponse>(`/admin/users/${userId}/ban`, { method: 'POST', body: { reason } }),

  unban: (userId: string) => request<StatusResponse>(`/admin/users/${userId}/unban`, { method: 'POST' }),

  audit: (take = 100) => request<AuditDto[]>('/admin/audit', { query: { take } }),
}
