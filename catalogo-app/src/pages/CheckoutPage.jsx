import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { api, formatMoney, newPedidoToken } from '../api/client.js'
import { useCart } from '../context/CartContext.jsx'
import { useCliente } from '../context/ClienteContext.jsx'
import { withCli } from '../components/Header.jsx'

export default function CheckoutPage() {
  const navigate = useNavigate()
  const { items, total, observaciones, clearCart } = useCart()
  const { telefono, setTelefono, clienteSeleccionado, setClienteSeleccionado } =
    useCliente()

  const [phoneInput, setPhoneInput] = useState(telefono || '')
  const [status, setStatus] = useState('')
  const [clientes, setClientes] = useState([])
  const [loadingLookup, setLoadingLookup] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')

  const [registro, setRegistro] = useState({
    nombre: '',
    telefono: telefono || '',
    direccion: '',
    bar: '',
    zona: '',
  })

  useEffect(() => {
    if (items.length === 0) {
      navigate(withCli('/', telefono), { replace: true })
    }
  }, [items, navigate, telefono])

  const lookupCliente = async (rawPhone) => {
    const phone = String(rawPhone || '').replace(/\D/g, '')
    if (!phone) {
      setError('Ingresa tu teléfono')
      return
    }
    setError('')
    setLoadingLookup(true)
    try {
      setTelefono(phone)
      const res = await api.cliente(phone)
      setStatus(res.status)
      setClientes(res.data || [])
      if (res.status === 'existe' && res.data?.[0]) {
        setClienteSeleccionado(res.data[0])
      } else {
        setClienteSeleccionado(null)
      }
      setRegistro((prev) => ({ ...prev, telefono: phone }))
    } catch (err) {
      setError(err.message || 'No se pudo verificar el cliente')
    } finally {
      setLoadingLookup(false)
    }
  }

  const registrar = async (e) => {
    e.preventDefault()
    setError('')
    try {
      const res = await api.registrarCliente({
        nombre: registro.nombre,
        telefono: registro.telefono || phoneInput,
        direccion: registro.direccion,
        bar: registro.bar,
        zona: registro.zona,
      })
      setClienteSeleccionado(res.data)
      setStatus('existe')
      setTelefono(res.data.telefono)
    } catch (err) {
      setError(err.message || 'No se pudo registrar')
    }
  }

  const enviarPedido = async () => {
    if (!clienteSeleccionado && status !== 'existe') {
      setError('Confirma o registra tu datos antes de enviar')
      return
    }

    const direccion =
      clienteSeleccionado?.direccion ||
      registro.direccion ||
      null

    setSubmitting(true)
    setError('')
    try {
      const pedidoToken = newPedidoToken()
      const payload = {
        pedidoToken,
        telefono: telefono || phoneInput,
        clienteId: clienteSeleccionado?.id || null,
        total,
        observaciones,
        direccion,
        productos: items.map((item) => ({
          id: item.id,
          nombre: item.nombre,
          tipo: item.tipo,
          cantidad: item.cantidad,
          precio_unitario: item.precioUnitario,
          subtotal: item.precioTotal,
        })),
      }

      const res = await api.crearPedido(payload)
      clearCart()
      navigate(withCli(`/pedido-ok?id=${res.idPedido}&total=${res.total}`, telefono), {
        replace: true,
      })
    } catch (err) {
      setError(err.message || 'Error al crear el pedido')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <section className="page checkout">
      <div className="page-toolbar">
        <Link to={withCli('/catalogo', telefono)} className="back-link">
          ← Seguir comprando
        </Link>
        <h1>Confirmar pedido</h1>
      </div>

      <div className="checkout-grid">
        <div className="panel">
          <h2>Resumen</h2>
          <ul className="cart-list">
            {items.map((item, index) => (
              <li
                key={`${item.id}-${index}`}
                className={`cart-item${item.esPromo ? ' cart-item--promo' : ''}`}
              >
                <div>
                  <strong>
                    {item.nombre}
                    {item.esPromo && (
                      <span className="cart-item__promo-tag">Promo</span>
                    )}
                  </strong>
                  <div className="muted small">
                    {item.cantidad} × {item.tipo}
                  </div>
                </div>
                <strong>{formatMoney(item.precioTotal)}</strong>
              </li>
            ))}
          </ul>
          <div className="drawer__total">
            <span>Total</span>
            <strong>{formatMoney(total)}</strong>
          </div>
          {observaciones && (
            <p className="muted small">Obs: {observaciones}</p>
          )}
        </div>

        <div className="panel">
          <h2>Tu teléfono</h2>
          <div className="inline-form">
            <input
              type="tel"
              value={phoneInput}
              onChange={(e) => setPhoneInput(e.target.value)}
              placeholder="Ej: 3001234567"
            />
            <button
              type="button"
              className="btn btn-primary"
              disabled={loadingLookup}
              onClick={() => lookupCliente(phoneInput)}
            >
              {loadingLookup ? 'Buscando…' : 'Continuar'}
            </button>
          </div>

          {status === 'existe' && clienteSeleccionado && (
            <div className="ok-box">
              <p>
                Hola <strong>{clienteSeleccionado.nombre}</strong>
              </p>
              {clienteSeleccionado.direccion && (
                <p className="muted small">{clienteSeleccionado.direccion}</p>
              )}
              <button
                type="button"
                className="btn btn-primary btn-block"
                disabled={submitting}
                onClick={enviarPedido}
              >
                {submitting ? 'Enviando…' : 'Enviar pedido'}
              </button>
            </div>
          )}

          {status === 'multiple' && (
            <div className="ok-box">
              <p>Hay varias cuentas con este número. Elige una:</p>
              <div className="choice-list">
                {clientes.map((c) => (
                  <button
                    key={c.id}
                    type="button"
                    className={`choice ${clienteSeleccionado?.id === c.id ? 'is-selected' : ''}`}
                    onClick={() => setClienteSeleccionado(c)}
                  >
                    <strong>{c.nombre}</strong>
                    <span className="muted small">{c.direccion || 'Sin dirección'}</span>
                  </button>
                ))}
              </div>
              <button
                type="button"
                className="btn btn-primary btn-block"
                disabled={!clienteSeleccionado || submitting}
                onClick={enviarPedido}
              >
                {submitting ? 'Enviando…' : 'Enviar pedido'}
              </button>
            </div>
          )}

          {status === 'no_existe' && (
            <form className="stack-form" onSubmit={registrar}>
              <p>Tu número no está registrado. Completa tus datos:</p>
              <label className="field">
                <span>Nombre</span>
                <input
                  required
                  value={registro.nombre}
                  onChange={(e) =>
                    setRegistro((p) => ({ ...p, nombre: e.target.value }))
                  }
                />
              </label>
              <label className="field">
                <span>Dirección</span>
                <input
                  required
                  value={registro.direccion}
                  onChange={(e) =>
                    setRegistro((p) => ({ ...p, direccion: e.target.value }))
                  }
                />
              </label>
              <label className="field">
                <span>Bar / negocio (opcional)</span>
                <input
                  value={registro.bar}
                  onChange={(e) =>
                    setRegistro((p) => ({ ...p, bar: e.target.value }))
                  }
                />
              </label>
              <label className="field">
                <span>Zona (opcional)</span>
                <input
                  value={registro.zona}
                  onChange={(e) =>
                    setRegistro((p) => ({ ...p, zona: e.target.value }))
                  }
                />
              </label>
              <button type="submit" className="btn btn-primary btn-block">
                Registrarme
              </button>
            </form>
          )}

          {error && <p className="error-text">{error}</p>}
        </div>
      </div>
    </section>
  )
}
