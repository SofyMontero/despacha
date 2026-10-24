import type { AuthResponse } from './types'
import { toApiError } from './errors'

let accessToken: string | null = null
let refreshInFlight: Promise<AuthResponse | null> | null = null
let onExpired: (() => void) | null = null

export function setAccessToken(token: string | null) {
  accessToken = token
}

export function getAccessToken() {
  return accessToken
}

export function setSessionExpiredHandler(handler: (() => void) | null) {
  onExpired = handler
}

export function resetApiState() {
  accessToken = null
  refreshInFlight = null
  onExpired = null
}

function resolve(path: string) {
  return new URL(path, window.location.origin).href
}

function skipsAccessToken(path: string) {
  return path.startsWith('/api/auth/login')
    || path.startsWith('/api/auth/refresh')
    || path.startsWith('/api/auth/logout')
}

async function raw(path: string, init: RequestInit, json: boolean) {
  const headers = new Headers(init.headers)
  headers.set('Accept', 'application/json')
  if (json) headers.set('Content-Type', 'application/json')
  if (!skipsAccessToken(path) && accessToken) headers.set('Authorization', `Bearer ${accessToken}`)

  return fetch(resolve(path), { ...init, headers, credentials: 'include' })
}

async function parse<T>(response: Response): Promise<T> {
  if (!response.ok) throw await toApiError(response)
  if (response.status === 204) return undefined as T

  const text = await response.text()
  if (!text) return undefined as T
  return JSON.parse(text) as T
}

async function refreshOnce() {
  const response = await raw('/api/auth/refresh', { method: 'POST', body: '{}' }, true)
  if (!response.ok) return null

  const body = (await response.json()) as AuthResponse
  accessToken = body.accessToken
  return body
}

/** Una sola renovación en vuelo: React StrictMode no debe rotar el refresh dos veces. */
export function refreshSession() {
  refreshInFlight ??= refreshOnce().finally(() => {
    refreshInFlight = null
  })
  return refreshInFlight
}

async function request<T>(path: string, init: RequestInit, json: boolean, allowRefresh: boolean): Promise<T> {
  const response = await raw(path, init, json)
  if (response.status === 401 && !skipsAccessToken(path)) {
    if (allowRefresh) {
      const renewed = await refreshSession()
      if (renewed) return request<T>(path, init, json, false)
    }

    accessToken = null
    onExpired?.()
  }

  return parse<T>(response)
}

export function apiGet<T>(path: string) {
  return request<T>(path, { method: 'GET' }, false, true)
}

export function apiSend<T>(path: string, method: 'POST' | 'PUT' | 'PATCH' | 'DELETE', body?: unknown) {
  return request<T>(
    path,
    { method, body: body === undefined ? undefined : JSON.stringify(body) },
    body !== undefined,
    true,
  )
}

export function apiUpload<T>(path: string, file: File) {
  const body = new FormData()
  body.append('file', file)
  return request<T>(path, { method: 'PUT', body }, false, true)
}

export async function apiLogin(login: string, password: string) {
  const response = await raw('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ login, password, client: 'web' }),
  }, true)
  const body = await parse<AuthResponse>(response)
  accessToken = body.accessToken
  return body
}

export async function apiSelectTenant(tenantId: number) {
  const response = await raw('/api/auth/tenant', {
    method: 'POST',
    body: JSON.stringify({ tenantId }),
  }, true)
  const body = await parse<AuthResponse>(response)
  accessToken = body.accessToken
  return body
}

export async function apiLogout() {
  const response = await raw('/api/auth/logout', { method: 'POST', body: '{}' }, true)
  await parse<void>(response)
  accessToken = null
}

export function apiMe() {
  return apiGet<import('./types').Me>('/api/auth/me')
}

export function queryString(params: Record<string, string | number | boolean | null | undefined>) {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === '') continue
    search.set(key, String(value))
  }

  const text = search.toString()
  return text ? `?${text}` : ''
}
