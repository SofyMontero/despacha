import { useQueryClient } from '@tanstack/react-query'
import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { apiLogin, apiLogout, apiMe, apiSelectTenant, refreshSession, setAccessToken, setSessionExpiredHandler } from '../api/client'
import { ApiError } from '../api/errors'
import type { AuthResponse, Me, Membership } from '../api/types'

export type SessionStatus = 'loading' | 'anonymous' | 'choose-tenant' | 'switching' | 'ready'

type SessionValue = {
  status: SessionStatus
  me: Me | null
  memberships: Membership[]
  login: (login: string, password: string) => Promise<void>
  selectTenant: (tenantId: number) => Promise<void>
  logout: () => Promise<void>
  reloadMe: () => Promise<void>
  has: (...permissions: string[]) => boolean
}

const SessionContext = createContext<SessionValue | null>(null)

export function SessionProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<SessionStatus>('loading')
  const [me, setMe] = useState<Me | null>(null)
  const [memberships, setMemberships] = useState<Membership[]>([])

  useEffect(() => {
    let cancelled = false
    setSessionExpiredHandler(() => {
      if (cancelled) return
      setAccessToken(null)
      setMe(null)
      setStatus('anonymous')
    })

    void (async () => {
      try {
        const auth = await refreshSession()
        if (cancelled) return
        if (!auth?.accessToken) {
          setStatus('anonymous')
          return
        }

        await adopt(auth, () => cancelled)
      } catch {
        if (!cancelled) {
          setAccessToken(null)
          setStatus('anonymous')
        }
      }
    })()

    return () => {
      cancelled = true
      setSessionExpiredHandler(null)
    }
  }, [])

  async function adopt(auth: AuthResponse, cancelled: () => boolean) {
    setMemberships(auth.memberships)
    if (!auth.tenantId) {
      if (!cancelled()) setStatus(auth.memberships.length > 0 ? 'choose-tenant' : 'anonymous')
      if (auth.memberships.length === 0) setAccessToken(null)
      return
    }

    const profile = await apiMe()
    if (cancelled()) return
    setMe(profile)
    setMemberships(profile.memberships)
    setStatus('ready')
  }

  async function login(loginName: string, password: string) {
    const auth = await apiLogin(loginName, password)
    if (!auth.tenantId) {
      setMemberships(auth.memberships)
      if (auth.memberships.length === 0) {
        setAccessToken(null)
        throw new ApiError(403, 'Su usuario no tiene una empresa activa.')
      }

      setStatus('choose-tenant')
      return
    }

    const profile = await apiMe()
    setMe(profile)
    setMemberships(profile.memberships)
    setStatus('ready')
  }

  async function selectTenant(tenantId: number) {
    setStatus('switching')
    try {
      const auth = await apiSelectTenant(tenantId)
      queryClient.clear()
      const profile = await apiMe()
      setMe(profile)
      setMemberships(auth.memberships.length > 0 ? auth.memberships : profile.memberships)
      setStatus('ready')
    } catch (error) {
      if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
        setAccessToken(null)
        setMe(null)
        setStatus('anonymous')
      } else {
        setStatus(me ? 'ready' : 'choose-tenant')
      }

      throw error
    }
  }

  async function logout() {
    try {
      await apiLogout()
    } catch {
      setAccessToken(null)
    }

    queryClient.clear()
    setMe(null)
    setMemberships([])
    setStatus('anonymous')
  }

  async function reloadMe() {
    const profile = await apiMe()
    setMe(profile)
    setMemberships(profile.memberships)
  }

  const value: SessionValue = {
    status,
    me,
    memberships,
    login,
    selectTenant,
    logout,
    reloadMe,
    has: (...permissions) => permissions.every((permission) => me?.permissions.includes(permission) === true),
  }

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>
}

export function useSession() {
  const value = useContext(SessionContext)
  if (!value) throw new Error('useSession debe usarse dentro de SessionProvider.')
  return value
}
