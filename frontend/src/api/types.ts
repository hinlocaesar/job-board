import type { components } from './generated/marketplace'

// ---------------------------------------------------------------------------
// Everything below is derived from the OpenAPI-generated types
// (regenerate with: pnpm dlx openapi-typescript <api>/openapi/v1.json
//  -o src/api/generated/marketplace.ts).
// ---------------------------------------------------------------------------

type Schemas = components['schemas']

/**
 * The generator widens `int32`/`decimal` response fields to `number | string`
 * (ASP.NET model-binding style). The API always serialises them as numbers, so
 * tighten them back.
 */
type AsNumbers<T> = {
  [K in keyof T as [number] extends [T[K]] ? K : never]: Exclude<T[K], string>
} & {
  [K in keyof T as [number] extends [T[K]] ? never : K]: T[K]
}

export type ApplicantDto = Schemas['ApplicantDto']
export type ApplicationInput = Schemas['ApplicationInput']
export type AuditDto = Schemas['AuditDto']
export type AuthResponse = Schemas['AuthResponse']
export type CategoryDto = Schemas['CategoryDto']
export type DeleteAccountInput = Schemas['DeleteAccountInput']
export type EmployerProfileDto = Schemas['EmployerProfileDto']
export type EmployerProfileInput = Schemas['EmployerProfileInput']
export type ExperienceDto = AsNumbers<Schemas['ExperienceDto']>
export type ExperienceInput = Schemas['ExperienceInput']
export type FileDto = AsNumbers<Schemas['FileDto']>
export type ForgotPasswordRequest = Schemas['ForgotPasswordRequest']
export type JobDetailDto = AsNumbers<Schemas['JobDetailDto']>
export type JobInput = Schemas['JobInput']
export type JobMessage = Schemas['JobMessage']
export type JobMineDto = AsNumbers<Schemas['JobMineDto']>
export type JobQueueDto = AsNumbers<Schemas['JobQueueDto']>
export type LoginRequest = Schemas['LoginRequest']
export type MeDto = Schemas['MeDto']
export type ModerationInput = Schemas['ModerationInput']
export type MyApplicationDto = Schemas['MyApplicationDto']
export type ProfileMessage = Schemas['ProfileMessage']
export type RefreshRequest = Schemas['RefreshRequest']
export type RegisterRequest = Schemas['RegisterRequest']
export type ResendVerificationRequest = Schemas['ResendVerificationRequest']
export type ResetPasswordRequest = Schemas['ResetPasswordRequest']
export type SkillInput = Schemas['SkillInput']
export type UserDto = Schemas['UserDto']
export type UserSummaryDto = Schemas['UserSummaryDto']
export type VerifyEmailRequest = Schemas['VerifyEmailRequest']
export type WorkerProfileDto = Omit<AsNumbers<Schemas['WorkerProfileDto']>, 'skills' | 'experiences'> & {
  skills: WorkerSkillDto[]
  experiences: ExperienceDto[]
}
export type WorkerProfileInput = Schemas['WorkerProfileInput']
export type WorkerSkillDto = AsNumbers<Schemas['WorkerSkillDto']>

export type JobSearchItem = AsNumbers<Schemas['JobSearchItem']>
export type JobSearchResult = Omit<AsNumbers<Schemas['JobSearchResult']>, 'totalPages' | 'items'> & {
  totalPages: number
  items: JobSearchItem[]
}

/** Query string accepted by `GET /api/jobs`. */
export interface JobSearchQuery {
  q?: string
  category?: string
  jobType?: string
  experience?: string
  minPay?: number
  payType?: string
  currency?: string
  sort?: string
  page?: number
  pageSize?: number
}

/** Status-change endpoints (apply/withdraw/approve…) return small anonymous objects. */
export interface StatusResponse {
  id?: string
  status: string
  reason?: string | null
}

/** Endpoints that only acknowledge. */
export interface AckResponse {
  accepted?: boolean
  message?: string
}

export type Role = 'admin' | 'employer' | 'worker'
