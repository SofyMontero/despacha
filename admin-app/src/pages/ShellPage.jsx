import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { clearSession, getSession, opsApi } from '../api/opsClient.js'

export default function ShellPage() {
  const navigate = useNavigate()
  const session = getSession()
  const [empresas, setEmpresas] = useState([])
  const [roles, setRoles] = useState([])
  const [jornadas, setJornadas] = useState([])
  const [hint, setHint] = useState('')
  const [error, setError] = useState('')
  const rol = (session?.rol || '').toLowerCase()
  const esAdmin = rol === 'administrador' || rol === 'supervisor'

  useEffect(() => {
    let alive = true
    ;(async () => {
      try {
        const [e, r] = await Promise.all([opsApi.empresas(), opsApi.roles()])
        if (!alive) return
        setEmpresas(e.data || [])
        setRoles(r.data || [])
        setHint(e.hint || r.hint || '')
        if (esAdmin) {
          try {
            const j = await opsApi.jornadaListar(new Date().toISOString().slice(0, 10))
            if (alive) setJornadas(j.data || [])
          } catch {
            if (alive) setJornadas([])
          }
        }
      } catch (err) {
        if (alive) setError(err.message)
      }
    })()
    return () => {
      alive = false
    }
  }, [esAdmin])

  const logout = async () => {
    try {
      await opsApi.logout()
    } catch {
      /* ignore */
    }
    clearSession()
    navigate('/login', { replace: true })
  }

  return (
    <div className="ops-shell">
      <header className="ops-shell__head">
        <div>
          <p className="ops-eyebrow">Centro de operaciones</p>
          <h1>Hola, {session?.nombre || session?.usuario}</h1>
          <p className="ops-muted">
            Rol: {session?.rol || '—'} · Empresa: {session?.idEmpresa ?? '—'}
          </p>
        </div>
        <button type="button" className="ops-ghost" onClick={logout}>
          Salir
        </button>
      </header>

      {error && <p className="ops-error">{error}</p>}
      {hint && <p className="ops-warn">{hint}</p>}

      <section className="ops-grid">
        <article>
          <h2>Empresas</h2>
          {empresas.length === 0 ? (
            <p className="ops-muted">Sin datos (¿SQL Fase 1 ejecutado?).</p>
          ) : (
            <ul>
              {empresas.map((e) => (
                <li key={e.idEmpresa}>
                  <strong>{e.nombre}</strong>
                  <span className="ops-muted"> · {e.modeloNegocio}</span>
                </li>
              ))}
            </ul>
          )}
        </article>
        <article>
          <h2>Roles activos</h2>
          {roles.length === 0 ? (
            <p className="ops-muted">Sin roles.</p>
          ) : (
            <ul>
              {roles.map((r) => (
                <li key={r.idRol}>{r.nombre}</li>
              ))}
            </ul>
          )}
        </article>
        {esAdmin && (
          <article>
            <h2>Jornadas de hoy</h2>
            {jornadas.length === 0 ? (
              <p className="ops-muted">Sin jornadas hoy (o SQL Fase 2 pendiente).</p>
            ) : (
              <ul>
                {jornadas.map((j) => (
                  <li key={j.idJornada}>
                    #{j.idJornada} · {j.paseadorNombre || `paseador ${j.idPaseador}`} ·{' '}
                    {j.estado}
                  </li>
                ))}
              </ul>
            )}
          </article>
        )}
      </section>

      <p className="ops-foot">
        <Link className="ops-link" to="/paseador">
          Ir a jornada / visitas (paseador) →
        </Link>
      </p>
      <p className="ops-muted ops-foot">
        Admin PHP de producción no se reemplaza. Mapa en vivo llega en fases
        siguientes.
      </p>
    </div>
  )
}
