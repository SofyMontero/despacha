import { useNavigate } from 'react-router-dom'
import { formatMoney } from '../api/client.js'
import { useCart } from '../context/CartContext.jsx'
import { useCliente } from '../context/ClienteContext.jsx'
import { withCli } from './Header.jsx'

export default function CartDrawer() {
  const {
    items,
    total,
    cartOpen,
    setCartOpen,
    updateQty,
    removeItem,
    observaciones,
    setObservaciones,
  } = useCart()
  const { telefono } = useCliente()
  const navigate = useNavigate()

  if (!cartOpen) return null

  return (
    <div className="drawer-overlay" onClick={() => setCartOpen(false)}>
      <aside
        className="drawer"
        onClick={(e) => e.stopPropagation()}
        aria-label="Carrito"
      >
        <div className="drawer__head">
          <h2>Tu pedido</h2>
          <button type="button" className="icon-btn" onClick={() => setCartOpen(false)}>
            ✕
          </button>
        </div>

        {items.length === 0 ? (
          <p className="muted drawer__empty">El carrito está vacío</p>
        ) : (
          <ul className="cart-list">
            {items.map((item, index) => (
              <li
                key={`${item.id}-${item.tipo}-${index}`}
                className={`cart-item${item.esPromo ? ' cart-item--promo' : ''}`}
              >
                <div className="cart-item__top">
                  <div className="cart-item__info">
                    <strong className="cart-item__name">
                      {item.nombre}
                      {item.esPromo && (
                        <span className="cart-item__promo-tag">Promo</span>
                      )}
                    </strong>
                    <span className="cart-item__meta">
                      {item.tipo} · {formatMoney(item.precioUnitario)} c/u
                    </span>
                  </div>
                  <button
                    type="button"
                    className="cart-item__remove"
                    onClick={() => removeItem(index)}
                    aria-label="Quitar"
                  >
                    ✕
                  </button>
                </div>

                <div className="cart-item__bottom">
                  <div className="qty-stepper" role="group" aria-label="Cantidad">
                    <button
                      type="button"
                      className="qty-stepper__btn"
                      onClick={() => updateQty(index, Math.max(1, item.cantidad - 1))}
                      aria-label="Menos"
                    >
                      −
                    </button>
                    <span className="qty-stepper__value">{item.cantidad}</span>
                    <button
                      type="button"
                      className="qty-stepper__btn"
                      onClick={() => updateQty(index, item.cantidad + 1)}
                      aria-label="Más"
                    >
                      +
                    </button>
                  </div>
                  <strong className="cart-item__subtotal">
                    {formatMoney(item.precioTotal)}
                  </strong>
                </div>
              </li>
            ))}
          </ul>
        )}

        <label className="field drawer__notes">
          <span>Observaciones</span>
          <textarea
            rows={2}
            value={observaciones}
            onChange={(e) => setObservaciones(e.target.value)}
            placeholder="Ej: dejar en portería"
          />
        </label>

        <div className="drawer__foot">
          <div className="drawer__total">
            <span>Total</span>
            <strong>{formatMoney(total)}</strong>
          </div>
          <button
            type="button"
            className="btn btn-primary btn-block"
            disabled={items.length === 0}
            onClick={() => {
              setCartOpen(false)
              navigate(withCli('/checkout', telefono))
            }}
          >
            Continuar pedido
          </button>
        </div>
      </aside>
    </div>
  )
}
