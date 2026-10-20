import { Link, useSearchParams } from 'react-router-dom'
import { formatMoney } from '../api/client.js'
import { useCliente } from '../context/ClienteContext.jsx'
import { withCli } from '../components/Header.jsx'

export default function PedidoOkPage() {
  const [params] = useSearchParams()
  const { telefono } = useCliente()
  const id = params.get('id')
  const total = params.get('total')

  return (
    <section className="page success-page">
      <div className="success-card">
        <div className="success-card__icon">✓</div>
        <h1>¡Pedido enviado!</h1>
        {id && <p className="lede">Pedido #{id}</p>}
        {total && <p className="total-line">{formatMoney(total)}</p>}
        <p className="muted">
          Te confirmamos por WhatsApp. Gracias por comprar en Multilicores.
        </p>
        <Link to={withCli('/', telefono)} className="btn btn-primary">
          Volver al catálogo
        </Link>
      </div>
    </section>
  )
}
