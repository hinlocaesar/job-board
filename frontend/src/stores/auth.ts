import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { authApi } from '../api/auth'
import { session } from '../api/session'
import type { AuthResponse, Role, UserDto } from '../api/types'

/**
 * Single source of truth for the signed-in user. Tokens themselves live in
 * `session` (localStorage) so the HTTP layer can attach them without a circular
 * import on this store.
 */
export const useAuthStore = defineStore('auth', () => {
  const user = ref<UserDto | null>(session.current?.user ?? null)

  const isAuthenticated = computed(() => user.value !== null)
  const emailVerified = computed(() => user.value?.emailVerified === true)
  const roles = computed<Role[]>(() => (user.value?.roles as Role[]) ?? [])

  function hasRole(role: Role): boolean {
    return roles.value.includes(role)
  }

  const isWorker = computed(() => hasRole('worker'))
  const isEmployer = computed(() => hasRole('employer'))
  const isAdmin = computed(() => hasRole('admin'))

  /** Re-hydrate from storage before the first navigation. */
  function restore(): void {
    const stored = session.load()
    user.value = stored?.user ?? null
  }

  function applyAuth(response: AuthResponse): UserDto {
    session.save({
      accessToken: response.accessToken,
      refreshToken: response.refreshToken,
      user: response.user,
    })
    user.value = response.user
    return response.user
  }

  async function login(email: string, password: string): Promise<UserDto> {
    return applyAuth(await authApi.login({ email, password }))
  }

  async function register(input: {
    email: string
    password: string
    fullName: string
    role: 'worker' | 'employer'
    acceptPrivacy: boolean
    privacyPolicyVersion: string
    marketingConsent: boolean
  }): Promise<UserDto> {
    // Registration only creates the account; the session starts at login.
    return authApi.register(input)
  }

  /** After verifying the e-mail address, sign in automatically. */
  async function verifyAndLogin(email: string, code: string, password: string): Promise<UserDto> {
    await authApi.verifyEmail({ email, code })
    return login(email, password)
  }

  async function logout(): Promise<void> {
    const refreshToken = session.current?.refreshToken
    try {
      if (refreshToken) await authApi.logout(refreshToken)
    } catch {
      // Best effort — local state is cleared either way.
    } finally {
      session.clear()
      user.value = null
    }
  }

  return {
    user,
    isAuthenticated,
    emailVerified,
    roles,
    hasRole,
    isWorker,
    isEmployer,
    isAdmin,
    restore,
    applyAuth,
    login,
    register,
    verifyAndLogin,
    logout,
  }
})
