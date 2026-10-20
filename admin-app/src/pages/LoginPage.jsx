import { useEffect, useState } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { getSession, opsApi, setSession } from '../api/opsClient.js'

export default function LoginPage() {
  const navigate = useNavigate()
  const [usuario, setUsuario] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const [schema, setSchema] = useState(null)

  useEffect(() => {
    let alive = true
    opsApi
      .schema()
      .then((res) => {
        if (alive) setSchema(res)
      })
      .catch(() => {
        if (alive) setSchema({ fase1Listo: false, nota: 'No se pudo leer /ops/schema.php (¿subiste el archivo?)' })
      })
    return () => {
      alive = false
    }
  }, [])

  if (getSession()?.token) {
    return <Navigate to="/" replace />
  }

  const onSubmit = async (e) => {
    e.preventDefault()
    setError('')
    setLoading(true)
    try {
      const res = await opsApi.login(usuario, password)
      setSession(res.data)
      navigate('/', { replace: true })
    } catch (err) {
      setError(err.message || 'No se pudo iniciar sesión')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="ops-login">
      <form className="ops-login__panel" onSubmit={onSubmit}>
        <p className="ops-eyebrow">Variante B</p>
        <h1>Ops Multilicores</h1>
        <p className="ops-muted">Centro de operaciones (paralelo al admin PHP).</p>
        {schema && (
          <p className={schema.fase1Listo ? 'ops-muted' : 'ops-error'}>
            {schema.fase1Listo
              ? 'Esquema Fase 1 listo.'
              : schema.nota || 'Falta ejecutar el SQL de Fase 1 en la BD.'}
          </p>
        )}
        <label>
          <span>Usuario</span>
          <input
            value={usuario}
            onChange={(e) => setUsuario(e.target.value)}
            autoComplete="username"
            required
          />
        </label>
        <label>
          <span>Clave</span>
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
            required
          />
        </label>
        {error && <p className="ops-error">{error}</p>}
        <button type="submit" disabled={loading}>
          {loading ? 'Entrando…' : 'Entrar'}
        </button>
      </form>
    </div>
  )
}
