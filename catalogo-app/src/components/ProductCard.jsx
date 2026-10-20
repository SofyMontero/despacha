import { useState } from 'react'
import { formatMoney } from '../api/client.js'
import { useCart } from '../context/CartContext.jsx'

export default function ProductCard({ product, esPromo = false }) {
  const { addItem } = useCart()
  const vendeUnidad = Boolean(product.vendeUnidad)
  const [tipo, setTipo] = useState(vendeUnidad ? '' : 'paca')
  const [cantidad, setCantidad] = useState(1)
  const [error, setError] = useState('')

  const nombre = product.nombre || product.titulo || 'Producto'
  const precioUnidad = product.precioUnidad ?? 0
  const precioPaca = product.precioPaca ?? 0

  const handleAdd = () => {
    if (!tipo) {
      setError('Elige unidad o paca')
      return
    }
    setError('')
    const precioUnitario = tipo === 'paca' ? precioPaca : precioUnidad
    addItem({
      id: product.id,
      nombre,
      tipo,
      cantidad,
      precioUnitario,
      esPromo,
    })
  }

  const fallbackImg =
    'https://multilicoreschapinero.com/sistema/assets/img/logoM.png'

  return (
    <article className={`product-card${esPromo ? ' product-card--promo' : ''}`}>
      <div className="product-card__media">
        <img
          src={product.imagen || fallbackImg}
          alt={nombre}
          loading="lazy"
          onError={(e) => {
            if (e.currentTarget.dataset.fallback === '1') return
            e.currentTarget.dataset.fallback = '1'
            e.currentTarget.src = fallbackImg
          }}
        />
        {esPromo && <span className="badge-promo">Promo</span>}
      </div>

      <div className="product-card__body">
        <h3>{nombre}</h3>
        {product.descripcion && (
          <p className="muted small">{product.descripcion}</p>
        )}

        <div className="price-block">
          {vendeUnidad && (
            <div className={tipo === 'unidad' ? 'is-active' : ''}>
              <span>Unidad</span>
              <strong>{formatMoney(precioUnidad)}</strong>
            </div>
          )}
          <div className={tipo === 'paca' ? 'is-active' : ''}>
            <span>Paca</span>
            <strong>{formatMoney(precioPaca)}</strong>
          </div>
        </div>

        <div className="product-card__controls">
          <label>
            Tipo
            <select value={tipo} onChange={(e) => setTipo(e.target.value)}>
              <option value="">Elegir</option>
              {vendeUnidad && <option value="unidad">Unidad</option>}
              <option value="paca">Paca</option>
            </select>
          </label>
          <label>
            Cant.
            <input
              type="number"
              min="1"
              value={cantidad}
              onChange={(e) => setCantidad(Math.max(1, Number(e.target.value) || 1))}
            />
          </label>
        </div>

        {error && <p className="error-text">{error}</p>}

        <button
          type="button"
          className={`btn btn-block ${esPromo ? 'btn-promo' : 'btn-primary'}`}
          onClick={handleAdd}
        >
          Agregar
        </button>
      </div>
    </article>
  )
}
