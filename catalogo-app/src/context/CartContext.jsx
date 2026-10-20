import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useState,
} from 'react'

const CartContext = createContext(null)
const STORAGE_KEY = 'ml_carrito'
const OBS_KEY = 'ml_observaciones'

function loadCart() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? JSON.parse(raw) : []
  } catch {
    return []
  }
}

export function CartProvider({ children }) {
  const [items, setItems] = useState(loadCart)
  const [observaciones, setObservaciones] = useState(
    () => sessionStorage.getItem(OBS_KEY) || '',
  )
  const [cartOpen, setCartOpen] = useState(false)

  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(items))
  }, [items])

  useEffect(() => {
    sessionStorage.setItem(OBS_KEY, observaciones)
  }, [observaciones])

  const addItem = ({ id, nombre, tipo, cantidad, precioUnitario, esPromo = false }) => {
    const qty = Math.max(1, Number(cantidad) || 1)
    const unit = Number(precioUnitario) || 0

    setItems((prev) => {
      const idx = prev.findIndex(
        (p) => p.id === id && p.tipo === tipo && Boolean(p.esPromo) === Boolean(esPromo),
      )
      if (idx >= 0) {
        const next = [...prev]
        const nuevaCantidad = next[idx].cantidad + qty
        next[idx] = {
          ...next[idx],
          cantidad: nuevaCantidad,
          precioTotal: next[idx].precioUnitario * nuevaCantidad,
        }
        return next
      }
      return [
        ...prev,
        {
          id,
          nombre,
          tipo,
          cantidad: qty,
          precioUnitario: unit,
          precioTotal: unit * qty,
          esPromo,
        },
      ]
    })
    setCartOpen(true)
  }

  const updateQty = (index, cantidad) => {
    const qty = Math.max(1, Number(cantidad) || 1)
    setItems((prev) =>
      prev.map((item, i) =>
        i === index
          ? { ...item, cantidad: qty, precioTotal: item.precioUnitario * qty }
          : item,
      ),
    )
  }

  const removeItem = (index) => {
    setItems((prev) => prev.filter((_, i) => i !== index))
  }

  const clearCart = () => {
    setItems([])
    setObservaciones('')
    localStorage.removeItem(STORAGE_KEY)
    sessionStorage.removeItem(OBS_KEY)
  }

  const total = useMemo(
    () => items.reduce((sum, item) => sum + (item.precioTotal || 0), 0),
    [items],
  )

  const count = useMemo(
    () => items.reduce((sum, item) => sum + item.cantidad, 0),
    [items],
  )

  const value = useMemo(
    () => ({
      items,
      total,
      count,
      observaciones,
      setObservaciones,
      cartOpen,
      setCartOpen,
      addItem,
      updateQty,
      removeItem,
      clearCart,
    }),
    [items, total, count, observaciones, cartOpen],
  )

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>
}

export function useCart() {
  const ctx = useContext(CartContext)
  if (!ctx) throw new Error('useCart debe usarse dentro de CartProvider')
  return ctx
}
