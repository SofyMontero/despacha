import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client.js'
import { useCliente } from '../context/ClienteContext.jsx'
import { withCli } from '../components/Header.jsx'

export default function CategoriasPage() {
  const { telefono } = useCliente()
  const [categorias, setCategorias] = useState([])
  const [promos, setPromos] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let alive = true
    ;(async () => {
      try {
        setLoading(true)
        const [catRes, promoRes] = await Promise.all([
          api.categorias(),
          api.promociones(3),
        ])
        if (!alive) return
        setCategorias(catRes.data || [])
        setPromos(promoRes.data || [])
      } catch (err) {
        if (alive) setError(err.message || 'Error al cargar')
      } finally {
        if (alive) setLoading(false)
      }
    })()
    return () => {
      alive = false
    }
  }, [])

  return (
    <section className="page">
      <div className="page-hero">
        <p className="eyebrow">Catálogo Multilicores</p>
        <h1>Elige tu categoría</h1>
        <p className="lede">
          Explora licores y arma tu pedido. Te confirmamos por WhatsApp.
        </p>
      </div>

      {promos.length > 0 && (
        <Link to={withCli('/promociones', telefono)} className="promo-banner">
          <div>
            <strong>Promociones activas</strong>
            <span>Mira las ofertas de hoy</span>
          </div>
          <span className="promo-banner__cta">Ver →</span>
        </Link>
      )}

      {loading && <p className="muted">Cargando categorías…</p>}
      {error && <p className="error-text">{error}</p>}

      <div className="category-grid">
        {categorias.map((cat) => (
          <Link
            key={cat.id}
            to={withCli(
              `/catalogo?categoria=${cat.id}&nombre=${encodeURIComponent(cat.nombre)}`,
              telefono,
            )}
            className="category-card"
          >
            <img
              src={cat.imagen}
              alt={cat.nombre}
              loading="lazy"
              onError={(e) => {
                if (e.currentTarget.dataset.fallback === '1') return
                e.currentTarget.dataset.fallback = '1'
                e.currentTarget.src =
                  'https://multilicoreschapinero.com/sistema/assets/img/logoM.png'
              }}
            />
            <span>{cat.nombre}</span>
          </Link>
        ))}
      </div>
    </section>
  )
}
