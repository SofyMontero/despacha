import { PRODUCT } from '../brand.js'
import MonteblancoMark from './MonteblancoMark.jsx'

export default function Footer() {
  return (
    <footer className="site-footer">
      <div className="site-footer__inner">
        <div className="powered-by">
          <span className="powered-by__label">Producto de</span>
          <div className="powered-by__brand">
            <MonteblancoMark size={22} />
            <div className="powered-by__text">
              <strong>{PRODUCT.name}</strong>
              <span>{PRODUCT.tagline}</span>
            </div>
          </div>
        </div>
      </div>
    </footer>
  )
}
