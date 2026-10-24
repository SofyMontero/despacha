import { Link } from 'react-router-dom'
import { useSession } from '../auth/session'

const links = [
  { to: '/categorias', label: 'Categorías', permission: 'catalog.read' },
  { to: '/productos', label: 'Productos', permission: 'catalog.read' },
  { to: '/promociones', label: 'Promociones', permission: 'catalog.read' },
  { to: '/clientes', label: 'Clientes', permission: 'customers.read' },
  { to: '/establecimientos', label: 'Establecimientos', permission: 'customers.read' },
]

export function HomePage() {
  const session = useSession()
  const tenant = session.me?.memberships.find((item) => item.tenantId === session.me?.tenantId)

  return (
    <section className="grid gap-4">
      <h1 className="font-display text-3xl">Administración</h1>
      <p>Empresa activa: {tenant?.tenantName ?? 'sin empresa'}.</p>
      <ul className="grid gap-2 sm:grid-cols-2">
        {links.filter((item) => session.has(item.permission)).map((item) => (
          <li key={item.to}>
            <Link className="block rounded-lg border border-line bg-panel px-4 py-3 hover:border-wine" to={item.to}>{item.label}</Link>
          </li>
        ))}
      </ul>
    </section>
  )
}
