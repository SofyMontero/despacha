import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { opsApi } from '../api/opsClient.js'

function readGps() {
  return new Promise((resolve) => {
    if (!navigator.geolocation) {
      resolve({ lat: null, lng: null })
      return
    }
    navigator.geolocation.getCurrentPosition(
      (pos) =>
        resolve({
          lat: pos.coords.latitude,
          lng: pos.coords.longitude,
        }),
      () => resolve({ lat: null, lng: null }),
      { enableHighAccuracy: true, timeout: 10000, maximumAge: 15000 },
    )
  })
}

function mañanaISO() {
  const d = new Date()
  d.setDate(d.getDate() + 1)
  return d.toISOString().slice(0, 10)
}

export default function PaseadorPage() {
  const [jornada, setJornada] = useState(null)
  const [visitas, setVisitas] = useState([])
  const [idCliente, setIdCliente] = useState('')
  const [resultado, setResultado] = useState('no_requiere_mercancia')
  const [notas, setNotas] = useState('')
  const [error, setError] = useState('')
  const [msg, setMsg] = useState('')
  const [loading, setLoading] = useState(false)

  // Pedido desde visita
  const [idVisitaPedido, setIdVisitaPedido] = useState('')
  const [fechaEntrega, setFechaEntrega] = useState(mañanaISO())
  const [prodId, setProdId] = useState('')
  const [prodNombre, setProdNombre] = useState('')
  const [prodTipo, setProdTipo] = useState('unidad')
  const [prodCant, setProdCant] = useState('1')
  const [prodPrecio, setProdPrecio] = useState('')
  const [lineas, setLineas] = useState([])

  const visitasSinPedido = useMemo(
    () => visitas.filter((v) => !v.idPedido),
    [visitas],
  )

  const totalPedido = useMemo(
    () => lineas.reduce((acc, l) => acc + l.cantidad * l.precio_unitario, 0),
    [lineas],
  )

  const refresh = async () => {
    const res = await opsApi.jornadaActual()
    setJornada(res.data || null)
    if (res.data?.idJornada) {
      const v = await opsApi.visitasPorJornada(res.data.idJornada)
      setVisitas(v.data || [])
    } else {
      setVisitas([])
    }
  }

  useEffect(() => {
    let alive = true
    ;(async () => {
      try {
        await refresh()
      } catch (err) {
        if (alive) setError(err.message)
      }
    })()
    return () => {
      alive = false
    }
  }, [])

  const iniciar = async () => {
    setError('')
    setMsg('')
    setLoading(true)
    try {
      const gps = await readGps()
      await opsApi.jornadaIniciar(gps.lat, gps.lng)
      await refresh()
      setMsg('Jornada iniciada')
    } catch (err) {
      setError(err.message)
    } finally {
      setLoading(false)
    }
  }

  const finalizar = async () => {
    setError('')
    setMsg('')
    setLoading(true)
    try {
      const gps = await readGps()
      await opsApi.jornadaFinalizar(jornada?.idJornada, gps.lat, gps.lng)
      await refresh()
      setMsg('Jornada finalizada')
    } catch (err) {
      setError(err.message)
    } finally {
      setLoading(false)
    }
  }

  const registrarVisita = async (e) => {
    e.preventDefault()
    setError('')
    setMsg('')
    setLoading(true)
    try {
      const gps = await readGps()
      const res = await opsApi.visitaRegistrar({
        idJornada: jornada?.idJornada,
        idCliente: Number(idCliente),
        resultado,
        notas,
        lat: gps.lat,
        lng: gps.lng,
      })
      setIdCliente('')
      setNotas('')
      await refresh()
      if (res.data?.idVisita) {
        setIdVisitaPedido(String(res.data.idVisita))
      }
      setMsg('Visita registrada')
    } catch (err) {
      setError(err.message)
    } finally {
      setLoading(false)
    }
  }

  const addLinea = (e) => {
    e.preventDefault()
    const id = Number(prodId)
    const cantidad = Number(prodCant)
    const precio = Number(prodPrecio)
    if (!id || !prodNombre.trim() || cantidad <= 0 || Number.isNaN(precio)) {
      setError('Completa id, nombre, cantidad y precio del producto')
      return
    }
    setLineas((prev) => [
      ...prev,
      {
        id,
        nombre: prodNombre.trim(),
        tipo: prodTipo,
        cantidad,
        precio_unitario: precio,
      },
    ])
    setProdId('')
    setProdNombre('')
    setProdCant('1')
    setProdPrecio('')
    setError('')
  }

  const crearPedido = async (e) => {
    e.preventDefault()
    setError('')
    setMsg('')
    setLoading(true)
    try {
      const res = await opsApi.pedidoDesdeVisita({
        idVisita: Number(idVisitaPedido),
        productos: lineas,
        total: totalPedido,
        fechaEntregaAcordada: fechaEntrega,
        observaciones: notas,
      })
      setLineas([])
      await refresh()
      setMsg(`Pedido #${res.data?.idPedido} creado (entrega ${fechaEntrega})`)
    } catch (err) {
      setError(err.message)
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="ops-shell">
      <header className="ops-shell__head">
        <div>
          <p className="ops-eyebrow">Paseador</p>
          <h1>Jornada y visitas</h1>
          <p className="ops-muted">
            <Link to="/">← Ops</Link>
          </p>
        </div>
      </header>

      {error && <p className="ops-error">{error}</p>}
      {msg && <p className="ops-warn">{msg}</p>}

      <section className="ops-grid">
        <article>
          <h2>Jornada</h2>
          {!jornada ? (
            <>
              <p className="ops-muted">No hay jornada en curso.</p>
              <button type="button" disabled={loading} onClick={iniciar}>
                Iniciar jornada
              </button>
            </>
          ) : (
            <>
              <p>
                <strong>#{jornada.idJornada}</strong> · {jornada.estado}
              </p>
              <p className="ops-muted">Inicio: {jornada.iniciadaEn}</p>
              <button type="button" className="ops-ghost" disabled={loading} onClick={finalizar}>
                Finalizar jornada
              </button>
            </>
          )}
        </article>

        <article>
          <h2>Registrar visita</h2>
          {!jornada ? (
            <p className="ops-muted">Inicia una jornada primero.</p>
          ) : (
            <form className="ops-form" onSubmit={registrarVisita}>
              <label>
                <span>ID cliente (tienda)</span>
                <input
                  value={idCliente}
                  onChange={(e) => setIdCliente(e.target.value)}
                  required
                  inputMode="numeric"
                />
              </label>
              <label>
                <span>Resultado</span>
                <select value={resultado} onChange={(e) => setResultado(e.target.value)}>
                  <option value="no_requiere_mercancia">No requiere mercancía</option>
                  <option value="pedido_creado">Pedido creado (luego arma el pedido abajo)</option>
                  <option value="no_atendido">No atendido</option>
                  <option value="otro">Otro</option>
                </select>
              </label>
              <label>
                <span>Notas</span>
                <input value={notas} onChange={(e) => setNotas(e.target.value)} />
              </label>
              <button type="submit" disabled={loading}>
                Guardar visita
              </button>
            </form>
          )}
        </article>
      </section>

      <section className="ops-grid">
        <article>
          <h2>Pedido desde visita</h2>
          {!jornada ? (
            <p className="ops-muted">Requiere jornada activa.</p>
          ) : visitasSinPedido.length === 0 ? (
            <p className="ops-muted">No hay visitas sin pedido. Registra una visita primero.</p>
          ) : (
            <>
              <form className="ops-form" onSubmit={addLinea}>
                <label>
                  <span>Visita</span>
                  <select
                    value={idVisitaPedido}
                    onChange={(e) => setIdVisitaPedido(e.target.value)}
                    required
                  >
                    <option value="">Seleccione…</option>
                    {visitasSinPedido.map((v) => (
                      <option key={v.idVisita} value={v.idVisita}>
                        #{v.idVisita} · cliente {v.idCliente}
                        {v.nombreCliente ? ` (${v.nombreCliente})` : ''}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  <span>Fecha entrega acordada</span>
                  <input
                    type="date"
                    value={fechaEntrega}
                    onChange={(e) => setFechaEntrega(e.target.value)}
                    required
                  />
                </label>
                <label>
                  <span>ID producto</span>
                  <input value={prodId} onChange={(e) => setProdId(e.target.value)} inputMode="numeric" />
                </label>
                <label>
                  <span>Nombre</span>
                  <input value={prodNombre} onChange={(e) => setProdNombre(e.target.value)} />
                </label>
                <label>
                  <span>Tipo</span>
                  <select value={prodTipo} onChange={(e) => setProdTipo(e.target.value)}>
                    <option value="unidad">unidad</option>
                    <option value="media">media</option>
                    <option value="promo">promo</option>
                  </select>
                </label>
                <label>
                  <span>Cantidad</span>
                  <input value={prodCant} onChange={(e) => setProdCant(e.target.value)} inputMode="numeric" />
                </label>
                <label>
                  <span>Precio unitario</span>
                  <input value={prodPrecio} onChange={(e) => setProdPrecio(e.target.value)} inputMode="decimal" />
                </label>
                <button type="submit" disabled={loading}>
                  Agregar línea
                </button>
              </form>

              {lineas.length > 0 && (
                <form className="ops-form" onSubmit={crearPedido} style={{ marginTop: '1rem' }}>
                  <ul>
                    {lineas.map((l, i) => (
                      <li key={`${l.id}-${i}`}>
                        {l.nombre} × {l.cantidad} · ${l.precio_unitario}
                      </li>
                    ))}
                  </ul>
                  <p>
                    <strong>Total: ${totalPedido.toLocaleString('es-CO')}</strong>
                  </p>
                  <button type="submit" disabled={loading || !idVisitaPedido}>
                    Crear pedido (no despacha aún)
                  </button>
                </form>
              )}
            </>
          )}
        </article>

        <article>
          <h2>Visitas de la jornada</h2>
          {visitas.length === 0 ? (
            <p className="ops-muted">Sin visitas aún.</p>
          ) : (
            <ul>
              {visitas.map((v) => (
                <li key={v.idVisita}>
                  #{v.idVisita} · cliente {v.idCliente}
                  {v.nombreCliente ? ` (${v.nombreCliente})` : ''} · {v.resultado}
                  {v.idPedido ? ` · pedido #${v.idPedido}` : ''}
                </li>
              ))}
            </ul>
          )}
        </article>
      </section>
    </div>
  )
}
