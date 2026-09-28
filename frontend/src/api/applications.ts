import { request } from './http'
import type { ApplicantDto, MyApplicationDto, StatusResponse } from './types'

export const applicationsApi = {
  apply: (jobId: string, coverLetter: string) =>
    request<StatusResponse>(`/jobs/${jobId}/apply`, { method: 'POST', body: { coverLetter } }),

  mine: () => request<MyApplicationDto[]>('/applications/mine'),

  withdraw: (id: string) => request<StatusResponse>(`/applications/${id}/withdraw`, { method: 'POST' }),

  /** Employer view of one job's applicants (contact details included). */
  applicants: (jobId: string) => request<ApplicantDto[]>(`/jobs/${jobId}/applications`),

  /** Employer decision: Shortlisted | Rejected (reason required for Rejected). */
  decide: (id: string, status: string, rejectionReason?: string) =>
    request<StatusResponse>(`/applications/${id}`, { method: 'PATCH', body: { status, rejectionReason } }),
}
