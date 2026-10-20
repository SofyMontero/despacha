import { NavLink, useSearchParams } from 'react-router-dom'
import { useCart } from '../context/CartContext.jsx'
import { useCliente } from '../context/ClienteContext.jsx'
import { CLIENT } from '../brand.js'
import MonteblancoMark from './MonteblancoMark.jsx'

function withCli(path, telefono) {
  if (!telefono) return path
  const sep = path.includes('?') ? '&' : '?'
  return `${path}${sep}idCli=${encodeURIComponent(telefono)}`
}

export default function Header() {
  const { count, setCartOpen } = useCart()
  const { telefono } = useCliente()
  const [params] = useSearchParams()
  const idCli = params.get('idCli') || telefono

  return (
    <header className="site-header">
      <div className="site-header__inner">
        <NavLink to={withCli('/', idCli)} className="brand">
          <img src={CLIENT.logo} alt={CLIENT.name} className="brand__logo" />
          <div className="brand__copy">
            <span className="brand__name">{CLIENT.name}</span>
            <span className="brand__product">
              <MonteblancoMark size={14} />
              <span>by Monteblanco</span>
            </span>
          </div>
        </NavLink>

        <nav className="site-nav" aria-label="Principal">
          <NavLink to={withCli('/', idCli)} end>
            Categorías
          </NavLink>
          <NavLink to={withCli('/promociones', idCli)} className="nav-promo">
            Promos
          </NavLink>
          <NavLink to={withCli('/catalogo', idCli)}>Productos</NavLink>
        </nav>

        <button
          type="button"
          className="cart-btn"
          onClick={() => setCartOpen(true)}
          aria-label="Abrir carrito"
        >
          <span className="cart-btn__icon" aria-hidden>
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M6 6h15l-1.5 9h-12z" />
              <circle cx="9" cy="20" r="1" />
              <circle cx="18" cy="20" r="1" />
              <path d="M6 6 5 3H2" />
            </svg>
          </span>
          {count > 0 && <span className="cart-btn__badge">{count}</span>}
        </button>
      </div>
    </header>
  )
}

export { withCli }
