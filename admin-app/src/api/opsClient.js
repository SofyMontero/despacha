const API_BASE = import.meta.env.VITE_OPS_API_BASE || '/sistema/api/ops'
const STORAGE_KEY = 'ml_ops_session'

export function getSession() {
  try {
    return JSON.parse(sessionStorage.getItem(STORAGE_KEY) || 'null')
  } catch {
    return null
  }
}

export function setSession(data) {
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(data))
}

export function clearSession() {
  sessionStorage.removeItem(STORAGE_KEY)
}

async function request(path, options = {}) {
  const session = getSession()
  const headers = {
    'Content-Type': 'application/json',
    ...(options.headers || {}),
  }
  if (session?.token) {
    headers.Authorization = `Bearer ${session.token}`
  }

  const res = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers,
  })
  const data = await res.json().catch(() => ({}))
  if (!res.ok || data.ok === false) {
    throw new Error(data.error || `Error HTTP ${res.status}`)
  }
  return data
}

export const opsApi = {
  health: () => request('/index.php'),
  schema: () => request('/schema.php'),
  login: (usuario, password) =>
    request('/auth.php?action=login', {
      method: 'POST',
      body: JSON.stringify({ action: 'login', usuario, password }),
    }),
  me: () => request('/auth.php?action=me'),
  logout: () =>
    request('/auth.php?action=logout', { method: 'POST', body: '{}' }),
  empresas: () => request('/empresas.php'),
  roles: () => request('/roles.php'),
  jornadaActual: () => request('/jornadas.php?action=actual'),
  jornadaListar: (fecha) =>
    request(`/jornadas.php?action=listar&fecha=${encodeURIComponent(fecha || '')}`),
  jornadaIniciar: (lat, lng) =>
    request('/jornadas.php?action=iniciar', {
      method: 'POST',
      body: JSON.stringify({ action: 'iniciar', lat, lng }),
    }),
  jornadaFinalizar: (idJornada, lat, lng) =>
    request('/jornadas.php?action=finalizar', {
      method: 'POST',
      body: JSON.stringify({ action: 'finalizar', idJornada, lat, lng }),
    }),

  visitasPorJornada: (idJornada) =>
    request(`/visitas.php?action=por_jornada&idJornada=${encodeURIComponent(idJornada)}`),
  visitaRegistrar: (payload) =>
    request('/visitas.php?action=registrar', {
      method: 'POST',
      body: JSON.stringify({ action: 'registrar', ...payload }),
    }),
  pedidoDesdeVisita: (payload) =>
    request('/pedido_visita.php?action=crear', {
      method: 'POST',
      body: JSON.stringify({ action: 'crear', ...payload }),
    }),
}
