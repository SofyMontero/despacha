const fallbackTitles: Record<number, string> = {
  400: 'Los datos no son válidos.',
  401: 'La sesión expiró. Inicie sesión de nuevo.',
  403: 'No tiene permiso para esta acción.',
  404: 'No se encontró el registro.',
  409: 'El registro fue modificado por otra persona. Recargue e intente de nuevo.',
  429: 'Demasiados intentos. Espere un momento.',
  503: 'El almacenamiento de imágenes no está configurado en este ambiente.',
}

export class ApiError extends Error {
  readonly status: number
  readonly errors: Record<string, string[]>

  constructor(status: number, title: string, errors: Record<string, string[]> = {}) {
    super(title)
    this.name = 'ApiError'
    this.status = status
    this.errors = errors
  }
}

export function messageOf(error: unknown) {
  return error instanceof ApiError ? error.message : 'No se pudo completar la solicitud.'
}

export async function toApiError(response: Response) {
  const fallback = fallbackTitles[response.status] ?? 'No se pudo completar la solicitud.'
  const text = await response.text()
  if (!text) return new ApiError(response.status, fallback)

  try {
    const body = JSON.parse(text) as { title?: string; errors?: Record<string, string[]> }
    return new ApiError(response.status, body.title?.trim() || fallback, body.errors ?? {})
  } catch {
    return new ApiError(response.status, fallback)
  }
}

/** Copia los errores PascalCase del ValidationProblem a los campos del formulario. */
export function applyFieldErrors(
  error: unknown,
  setError: (name: string, error: { message: string }) => void,
) {
  if (!(error instanceof ApiError)) return false

  let applied = false
  for (const [key, messages] of Object.entries(error.errors)) {
    if (!messages?.length) continue
    const name = key.charAt(0).toLowerCase() + key.slice(1)
    setError(name, { message: messages.join(' ') })
    applied = true
  }

  return applied
}
