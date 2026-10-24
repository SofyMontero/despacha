import { useEffect, useState, type FormEvent } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { apiSend } from '../api/client'
import { messageOf } from '../api/errors'
import { useSession } from '../auth/session'
import { Banner, Button, cx, SelectInput } from './ui'

const sections = [
  { to: '/categorias', label: 'Categorías', permission: 'catalog.read' },
  { to: '/productos', label: 'Productos', permission: 'catalog.read' },
  { to: '/promociones', label: 'Promociones', permission: 'catalog.read' },
  { to: '/clientes', label: 'Clientes', permission: 'customers.read' },
  { to: '/establecimientos', label: 'Establecimientos', permission: 'customers.read' },
]

export function Layout() {
  const session = useSession()
  const me = session.me
  const [open, setOpen] = useState(false)
  const [switchError, setSwitchError] = useState<string | null>(null)
  const { pathname } = useLocation()
  const tenantName = me?.memberships.find((item) => item.tenantId === me.tenantId)?.tenantName

  useEffect(() => {
    setOpen(false)
  }, [pathname])

  if (!me) return null

  async function changeTenant(tenantId: number) {
    setSwitchError(null)
    try {
      await session.selectTenant(tenantId)
    } catch (error) {
      setSwitchError(messageOf(error))
    }
  }

  return (
    <div className="min-h-screen md:grid md:grid-cols-[16rem_1fr]">
      {open ? <button type="button" aria-label="Cerrar menú" className="fixed inset-0 z-20 bg-ink/40 md:hidden" onClick={() => setOpen(false)} /> : null}
      <nav
        aria-label="Secciones"
        className={cx(
          'fixed inset-y-0 left-0 z-30 flex w-64 transform flex-col bg-ink text-paper transition md:static md:translate-x-0',
          open ? 'translate-x-0' : '-translate-x-full',
        )}
      >
        <div className="px-5 py-6">
          <p className="font-display text-2xl">Despacha</p>
          <p className="text-sm text-paper/70">Administración</p>
        </div>
        {me.requiresPasswordChange ? (
          <p className="px-5 text-sm text-paper/80">Cambie su contraseña para continuar.</p>
        ) : (
          <ul className="grid gap-1 px-3">
            {sections.filter((item) => session.has(item.permission)).map((item) => (
              <li key={item.to}>
                <NavLink
                  to={item.to}
                  onClick={() => setOpen(false)}
                  className={({ isActive }) => cx('block rounded-md px-3 py-2 text-sm', isActive ? 'bg-white/15' : 'hover:bg-white/10')}
                >
                  {item.label}
                </NavLink>
              </li>
            ))}
          </ul>
        )}
      </nav>
      <div className="min-w-0">
        <header className="flex flex-wrap items-center gap-3 border-b border-line bg-panel px-4 py-3">
          <Button variant="secondary" className="md:hidden" onClick={() => setOpen(true)}>Abrir menú</Button>
          <div className="min-w-0 flex-1">
            {me.memberships.length > 1 ? (
              <label className="grid gap-1 text-sm">
                <span className="font-medium">Empresa</span>
                <SelectInput
                  aria-label="Empresa"
                  value={me.tenantId ?? ''}
                  disabled={session.status === 'switching'}
                  onChange={(event) => void changeTenant(Number(event.target.value))}
                >
                  {me.memberships.map((item) => (
                    <option key={item.tenantId} value={item.tenantId}>{item.tenantName}</option>
                  ))}
                </SelectInput>
              </label>
            ) : (
              <p className="truncate text-sm font-semibold">{tenantName}</p>
            )}
          </div>
          <p className="text-sm">{me.displayName}{me.role ? ` · ${me.role}` : ''}</p>
          <Button variant="secondary" onClick={() => void session.logout()}>Cerrar sesión</Button>
        </header>
        <main className="mx-auto grid max-w-6xl gap-4 px-4 py-6">
          {switchError ? <Banner>{switchError}</Banner> : null}
          {session.status === 'switching' ? <p>Cambiando de empresa…</p> : me.requiresPasswordChange ? <PasswordChange /> : <Outlet />}
        </main>
      </div>
    </div>
  )
}

function PasswordChange() {
  const session = useSession()
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    if (newPassword.length < 10) {
      setError('Mínimo 10 caracteres.')
      return
    }

    setPending(true)
    try {
      await apiSend<void>('/api/auth/password/change', 'POST', { currentPassword, newPassword })
      await session.reloadMe()
    } catch (caught) {
      setError(messageOf(caught))
    } finally {
      setPending(false)
    }
  }

  return (
    <form className="grid max-w-md gap-3" onSubmit={(event) => void submit(event)} noValidate>
      <h1 className="font-display text-3xl">Cambie su contraseña</h1>
      <p className="text-sm">La cuenta tiene una contraseña temporal. El servidor vuelve a validarla.</p>
      {error ? <Banner>{error}</Banner> : null}
      <label className="grid gap-1 text-sm">
        Contraseña actual
        <input type="password" autoComplete="current-password" className="rounded-md border border-line px-3 py-2" value={currentPassword} onChange={(event) => setCurrentPassword(event.target.value)} />
      </label>
      <label className="grid gap-1 text-sm">
        Contraseña nueva
        <input type="password" autoComplete="new-password" className="rounded-md border border-line px-3 py-2" value={newPassword} onChange={(event) => setNewPassword(event.target.value)} />
      </label>
      <button type="submit" disabled={pending} className="rounded-md bg-wine px-3 py-2 text-sm font-semibold text-white disabled:opacity-50">
        {pending ? 'Guardando…' : 'Guardar contraseña'}
      </button>
    </form>
  )
}

export function Forbidden({ what }: { what: string }) {
  return <p role="alert">No tiene permiso para ver {what}.</p>
}

export function Loading() {
  return <p className="p-6">Cargando…</p>
}
