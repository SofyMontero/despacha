const API_BASE = import.meta.env.VITE_API_BASE || '/sistema/api'

async function request(path, options = {}) {
  const res = await fetch(`${API_BASE}${path}`, {
    headers: {
      Accept: 'application/json',
      ...(options.body ? { 'Content-Type': 'application/json' } : {}),
      ...options.headers,
    },
    ...options,
  })

  let data = null
  try {
    data = await res.json()
  } catch {
    data = null
  }

  if (!res.ok || data?.ok === false) {
    const message = data?.error || `Error HTTP ${res.status}`
    throw new Error(message)
  }

  return data
}

export const api = {
  categorias: () => request('/categorias.php'),
  productos: (params = {}) => {
    const q = new URLSearchParams()
    Object.entries(params).forEach(([k, v]) => {
      if (v !== undefined && v !== null && v !== '') q.set(k, String(v))
    })
    const qs = q.toString()
    return request(`/productos.php${qs ? `?${qs}` : ''}`)
  },
  promociones: (limit) =>
    request(`/promociones.php${limit ? `?limit=${limit}` : ''}`),
  sugerencias: (q) =>
    request(`/sugerencias.php?q=${encodeURIComponent(q)}`),
  cliente: (telefono) =>
    request(`/cliente.php?telefono=${encodeURIComponent(telefono)}`),
  registrarCliente: (payload) =>
    request('/cliente.php', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  crearPedido: (payload) =>
    request('/pedido.php', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
}

export function formatMoney(value) {
  return new Intl.NumberFormat('es-CO', {
    style: 'currency',
    currency: 'COP',
    maximumFractionDigits: 0,
  }).format(Number(value) || 0)
}

export function newPedidoToken() {
  if (crypto?.randomUUID) {
    return crypto.randomUUID().replace(/-/g, '')
  }
  return `${Date.now()}${Math.random().toString(16).slice(2)}`
}
