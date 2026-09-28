import type { UserDto } from './types'

/** Where the tokens + current user live (localStorage on the client, memory during prerender). */
export interface PersistedSession {
  accessToken: string
  refreshToken: string
  user: UserDto
}

const STORAGE_KEY = 'jobboard.session'

let state: PersistedSession | null = null

function canUseStorage(): boolean {
  return !import.meta.env.SSR && typeof localStorage !== 'undefined'
}

export const session = {
  get current(): PersistedSession | null {
    return state
  },

  get accessToken(): string | null {
    return state?.accessToken ?? null
  },

  load(): PersistedSession | null {
    if (state) return state
    if (!canUseStorage()) return null
    try {
      const raw = localStorage.getItem(STORAGE_KEY)
      if (raw) state = JSON.parse(raw) as PersistedSession
    } catch {
      // Corrupt storage should never brick the app.
      localStorage.removeItem(STORAGE_KEY)
    }
    return state
  },

  save(next: PersistedSession): void {
    state = next
    if (canUseStorage()) localStorage.setItem(STORAGE_KEY, JSON.stringify(next))
  },

  updateTokens(accessToken: string, refreshToken: string): void {
    if (!state) return
    state = { ...state, accessToken, refreshToken }
    if (canUseStorage()) localStorage.setItem(STORAGE_KEY, JSON.stringify(state))
  },

  updateUser(user: UserDto): void {
    if (!state) return
    state = { ...state, user }
    if (canUseStorage()) localStorage.setItem(STORAGE_KEY, JSON.stringify(state))
  },

  clear(): void {
    state = null
    if (canUseStorage()) localStorage.removeItem(STORAGE_KEY)
  },
}
