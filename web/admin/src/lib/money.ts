const maxPrice = 9_999_999_999.99

/** Acepta 45000.50, 45000,50 y 45.000 como miles. */
export function parseMoney(input: string): number | null {
  const value = input.trim()
  if (!value || value.startsWith('-')) return null

  let normalized = value
  if (value.includes(',')) {
    normalized = value.replaceAll('.', '').replace(',', '.')
  } else if (/^\d{1,3}(\.\d{3})+$/.test(value)) {
    normalized = value.replaceAll('.', '')
  }

  if (!/^\d+(\.\d{1,2})?$/.test(normalized)) return null

  const amount = Number(normalized)
  if (!Number.isFinite(amount) || amount > maxPrice) return null
  return amount
}

export function moneyError(value: string, mustBePositive: boolean) {
  const trimmed = value.trim()
  if (!trimmed) return mustBePositive ? 'Es obligatorio.' : null
  if (trimmed.startsWith('-')) return 'No puede ser negativo.'

  const amount = parseMoney(trimmed)
  if (amount === null) return 'Use un valor con hasta 2 decimales.'
  if (mustBePositive && amount <= 0) return 'Debe ser mayor que 0.'
  if (amount < 0) return 'No puede ser negativo.'
  return null
}

export function formatMoneyInput(value: number) {
  return value.toFixed(2).replace(/\.00$/, '').replace(/(\.\d)0$/, '$1')
}

const cop = new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP' })

export function formatCop(value: number) {
  return cop.format(value)
}

const dateFormat = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })

export function formatDate(value: string | null) {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return dateFormat.format(date)
}
