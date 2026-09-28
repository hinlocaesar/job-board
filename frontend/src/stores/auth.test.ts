import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAuthStore } from '../stores/auth'
import { session } from '../api/session'

vi.mock('../api/auth', () => ({
  authApi: {
    login: vi.fn(),
    register: vi.fn(),
    verifyEmail: vi.fn(),
    resendVerification: vi.fn(),
    forgotPassword: vi.fn(),
    resetPassword: vi.fn(),
    refresh: vi.fn(),
    logout: vi.fn(),
    outbox: vi.fn(),
  },
}))

vi.mock('../api/http', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api/http')>()
  return { ...actual, request: vi.fn() }
})

import { authApi } from '../api/auth'
import type { AuthResponse, UserDto } from '../api/types'

const user: UserDto = {
  id: '9f1b2c3d-0000-0000-0000-00000000000a',
  email: 'worker@jobboard.local',
  fullName: 'Dana Worker',
  roles: ['worker'],
  emailVerified: true,
  accountStatus: 'Active',
}

const authResponse: AuthResponse = {
  accessToken: 'access-token',
  refreshToken: 'refresh-token',
  user,
}

describe('useAuthStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    session.clear()
    vi.clearAllMocks()
  })

  it('starts anonymous', () => {
    const auth = useAuthStore()
    expect(auth.isAuthenticated).toBe(false)
    expect(auth.roles).toEqual([])
    expect(auth.isWorker).toBe(false)
  })

  it('logs in, persists the session and exposes roles', async () => {
    vi.mocked(authApi.login).mockResolvedValue(authResponse)
    const auth = useAuthStore()

    const returned = await auth.login('worker@jobboard.local', 'Worker123!')

    expect(authApi.login).toHaveBeenCalledWith({
      email: 'worker@jobboard.local',
      password: 'Worker123!',
    })
    expect(returned.fullName).toBe('Dana Worker')
    expect(auth.isAuthenticated).toBe(true)
    expect(auth.isWorker).toBe(true)
    expect(auth.isAdmin).toBe(false)

    const persisted = session.load()
    expect(persisted?.accessToken).toBe('access-token')
    expect(persisted?.refreshToken).toBe('refresh-token')
    expect(persisted?.user.fullName).toBe('Dana Worker')
  })

  it('rehydrates from storage on restore()', () => {
    const auth = useAuthStore()
    expect(auth.isAuthenticated).toBe(false) // nothing stored yet

    session.save({ accessToken: 'a', refreshToken: 'r', user })
    auth.restore()
    expect(auth.isAuthenticated).toBe(true)
    expect(auth.emailVerified).toBe(true)
  })

  it('registers without opening a session', async () => {
    vi.mocked(authApi.register).mockResolvedValue(user)
    const auth = useAuthStore()

    await auth.register({
      email: 'new@jobboard.local',
      password: 'Passw0rd!',
      fullName: 'New User',
      role: 'worker',
      acceptPrivacy: true,
      privacyPolicyVersion: '2026-09-01',
      marketingConsent: false,
    })

    expect(authApi.register).toHaveBeenCalled()
    expect(auth.isAuthenticated).toBe(false)
  })

  it('clears local state on logout even if the API call fails', async () => {
    session.save({ accessToken: 'a', refreshToken: 'r', user })
    const auth = useAuthStore()
    auth.restore()

    vi.mocked(authApi.logout).mockRejectedValue(new Error('network down'))
    await auth.logout()

    expect(authApi.logout).toHaveBeenCalledWith('r')
    expect(auth.isAuthenticated).toBe(false)
    expect(session.current).toBeNull()
  })

  it('hasRole checks every assigned role', () => {
    session.save({
      accessToken: 'a',
      refreshToken: 'r',
      user: { ...user, roles: ['employer', 'admin'] },
    })
    const auth = useAuthStore()
    auth.restore()
    expect(auth.isEmployer).toBe(true)
    expect(auth.isAdmin).toBe(true)
    expect(auth.isWorker).toBe(false)
    expect(auth.hasRole('admin')).toBe(true)
  })
})
