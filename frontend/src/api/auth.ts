import { request } from './http'
import type {
  AuthResponse,
  ForgotPasswordRequest,
  LoginRequest,
  RefreshRequest,
  RegisterRequest,
  ResendVerificationRequest,
  ResetPasswordRequest,
  UserDto,
  VerifyEmailRequest,
} from './types'

export const authApi = {
  register: (body: RegisterRequest) =>
    request<UserDto>('/auth/register', { method: 'POST', body, auth: false }),

  login: (body: LoginRequest) => request<AuthResponse>('/auth/login', { method: 'POST', body, auth: false }),

  refresh: (body: RefreshRequest) =>
    request<AuthResponse>('/auth/refresh', { method: 'POST', body, auth: false }),

  verifyEmail: (body: VerifyEmailRequest) =>
    request<void>('/auth/verify-email', { method: 'POST', body, auth: false }),

  resendVerification: (body: ResendVerificationRequest) =>
    request<void>('/auth/resend-verification', { method: 'POST', body, auth: false }),

  forgotPassword: (body: ForgotPasswordRequest) =>
    request<void>('/auth/forgot-password', { method: 'POST', body, auth: false }),

  resetPassword: (body: ResetPasswordRequest) =>
    request<void>('/auth/reset-password', { method: 'POST', body, auth: false }),

  logout: (refreshToken: string) => request<void>('/auth/logout', { method: 'POST', body: { refreshToken } }),

  /** Development-only endpoint: the recent email outbox (`Email:Provider=Log`). */
  outbox: () => request<string[]>('/auth/outbox', { auth: false }),
}
