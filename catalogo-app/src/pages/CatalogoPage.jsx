import { useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api } from '../api/client.js'
import ProductCard from '../components/ProductCard.jsx'
import { withCli } from '../components/Header.jsx'
import { useCliente } from '../context/ClienteContext.jsx'

export default function CatalogoPage() {
  const { telefono } = useCliente()
  const [params, setParams] = useSearchParams()
  const categoria = params.get('categoria') || ''
  const nombre = params.get('nombre') || 'Productos'
  const page = Math.max(1, Number(params.get('page') || 1))

  const [products, setProducts] = useState([])
  const [meta, setMeta] = useState({ totalPages: 1 })
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let alive = true
    ;(async () => {
      try {
        setLoading(true)
        const res = await api.productos({ categoria, page, limit: 12 })
        if (!alive) return
        setProducts(res.data || [])
        setMeta(res.meta || { totalPages: 1 })
      } catch (err) {
        if (alive) setError(err.message || 'Error al cargar productos')
      } finally {
        if (alive) setLoading(false)
      }
    })()
    return () => {
      alive = false
    }
  }, [categoria, page])

  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase()
    if (!term) return products
    return products.filter((p) =>
      (p.nombre || '')
        .toLowerCase()
        .normalize('NFD')
        .replace(/[\u0300-\u036f]/g, '')
        .includes(
          term.normalize('NFD').replace(/[\u0300-\u036f]/g, ''),
        ),
    )
  }, [products, search])

  const goPage = (next) => {
    const nextParams = new URLSearchParams(params)
    nextParams.set('page', String(next))
    if (telefono) nextParams.set('idCli', telefono)
    setParams(nextParams)
  }

  return (
    <section className="page">
      <div className="page-toolbar">
        <Link to={withCli('/', telefono)} className="back-link">
          ← Categorías
        </Link>
        <h1>{nombre}</h1>
      </div>

      <div className="search-bar">
        <input
          type="search"
          placeholder="Buscar en esta página…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </div>

      {loading && <p className="muted">Cargando productos…</p>}
      {error && <p className="error-text">{error}</p>}

      {!loading && filtered.length === 0 && (
        <p className="muted">No hay productos para mostrar.</p>
      )}

      <div className="product-grid">
        {filtered.map((product) => (
          <ProductCard key={product.id} product={product} />
        ))}
      </div>

      {meta.totalPages > 1 && (
        <div className="pager">
          <button
            type="button"
            className="btn btn-ghost"
            disabled={page <= 1}
            onClick={() => goPage(page - 1)}
          >
            Anterior
          </button>
          <span>
            {page} / {meta.totalPages}
          </span>
          <button
            type="button"
            className="btn btn-ghost"
            disabled={page >= meta.totalPages}
            onClick={() => goPage(page + 1)}
          >
            Siguiente
          </button>
        </div>
      )}
    </section>
  )
}
