import { createContext, useCallback, useContext, useMemo, useState } from 'react'

const ClienteContext = createContext(null)
const STORAGE_KEY = 'ml_telefono'

export function ClienteProvider({ children }) {
  const [telefono, setTelefonoState] = useState(() => {
    return sessionStorage.getItem(STORAGE_KEY) || ''
  })
  const [clienteSeleccionado, setClienteSeleccionado] = useState(null)

  const setTelefono = useCallback((value) => {
    const next = String(value || '').replace(/\D/g, '')
    setTelefonoState(next)
    if (next) sessionStorage.setItem(STORAGE_KEY, next)
    else sessionStorage.removeItem(STORAGE_KEY)
  }, [])

  const value = useMemo(
    () => ({
      telefono,
      setTelefono,
      clienteSeleccionado,
      setClienteSeleccionado,
    }),
    [telefono, setTelefono, clienteSeleccionado],
  )

  return (
    <ClienteContext.Provider value={value}>{children}</ClienteContext.Provider>
  )
}

export function useCliente() {
  const ctx = useContext(ClienteContext)
  if (!ctx) throw new Error('useCliente debe usarse dentro de ClienteProvider')
  return ctx
}
