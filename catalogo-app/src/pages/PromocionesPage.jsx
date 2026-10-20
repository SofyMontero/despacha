import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client.js'
import ProductCard from '../components/ProductCard.jsx'
import { withCli } from '../components/Header.jsx'
import { useCliente } from '../context/ClienteContext.jsx'

export default function PromocionesPage() {
  const { telefono } = useCliente()
  const [promos, setPromos] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let alive = true
    ;(async () => {
      try {
        setLoading(true)
        const res = await api.promociones()
        if (!alive) return
        setPromos(res.data || [])
      } catch (err) {
        if (alive) setError(err.message || 'Error al cargar promociones')
      } finally {
        if (alive) setLoading(false)
      }
    })()
    return () => {
      alive = false
    }
  }, [])

  return (
    <section className="page page-promo">
      <div className="page-toolbar">
        <Link to={withCli('/', telefono)} className="back-link">
          ← Categorías
        </Link>
        <h1>Promociones</h1>
      </div>

      {loading && <p className="muted">Cargando ofertas…</p>}
      {error && <p className="error-text">{error}</p>}
      {!loading && promos.length === 0 && (
        <p className="muted">No hay promociones activas.</p>
      )}

      <div className="product-grid">
        {promos.map((promo) => (
          <ProductCard
            key={promo.id}
            esPromo
            product={{
              ...promo,
              nombre: promo.titulo,
            }}
          />
        ))}
      </div>
    </section>
  )
}
