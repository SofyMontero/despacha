import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { expect, it, vi } from 'vitest'
import type { AuthResponse, Category, Customer, CustomerSave, Me, Membership, Paged, Product } from '../api/types'
import { AppProviders, AppRoutes } from '../app'
import { phoneMessage } from '../lib/phone'

type Call = { method: string; url: string; init: RequestInit }
type Route = {
  method: string
  test: (url: URL) => boolean
  handle: (url: URL, init: RequestInit) => Response | Promise<Response>
}

const adminPermissions = ['catalog.read', 'catalog.manage', 'customers.read', 'customers.manage']

function membership(tenantId: number, tenantName: string): Membership {
  return { tenantId, tenantCode: `t${tenantId}`, tenantName, role: 'admin' }
}

function profile(partial: Partial<Me> & { tenantId: number }): Me {
  return {
    userId: 1,
    login: 'ana',
    displayName: 'Ana',
    requiresPasswordChange: false,
    role: 'admin',
    permissions: adminPermissions,
    memberships: [membership(partial.tenantId, 'Alfa')],
    ...partial,
  }
}

function auth(token: string, tenantId: number | null, memberships: Membership[]): AuthResponse {
  return {
    accessToken: token,
    expiresIn: 900,
    refreshToken: null,
    tenantId,
    memberships,
    requiresPasswordChange: false,
  }
}

function page<T>(items: T[], total = items.length): Paged<T> {
  return { items, total, page: 1, pageSize: 20 }
}

function category(partial: Partial<Category> = {}): Category {
  return {
    id: 5,
    name: 'Licores',
    isActive: true,
    imageKey: null,
    imageUrl: null,
    version: 1,
    updatedAtUtc: '2026-10-24T12:00:00Z',
    ...partial,
  }
}

function product(partial: Partial<Product> = {}): Product {
  return {
    id: 8,
    code: 'A-1',
    name: 'Aguardiente',
    categoryId: 3,
    categoryName: 'Licores',
    unitPrice: 12000,
    packPrice: 0,
    unitsPerPack: 1,
    sellsByUnit: true,
    isActive: true,
    imageKey: null,
    imageUrl: null,
    version: 1,
    updatedAtUtc: '2026-10-24T12:00:00Z',
    ...partial,
  }
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

function problem(status: number, title: string, errors?: Record<string, string[]>) {
  return json(errors ? { title, errors } : { title }, status)
}

function tokenOf(init: RequestInit) {
  return new Headers(init.headers).get('Authorization')
}

function install(routes: Route[]) {
  const calls: Call[] = []
  vi.stubGlobal('fetch', async (input: RequestInfo | URL, init: RequestInit = {}) => {
    const href = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url
    const url = new URL(href)
    const method = (init.method ?? 'GET').toUpperCase()
    calls.push({ method, url: `${url.pathname}${url.search}`, init })
    const route = routes.find((item) => item.method === method && item.test(url))
    if (!route) return problem(500, `Sin simulación ${method} ${url.pathname}`)
    return route.handle(url, init)
  })
  return calls
}

function renderApp(path: string) {
  return render(
    <AppProviders>
      <MemoryRouter initialEntries={[path]}>
        <AppRoutes />
      </MemoryRouter>
    </AppProviders>,
  )
}

function sessionRoutes(user: Me, extra: Route[] = []): Route[] {
  return [
    {
      method: 'POST',
      test: (url) => url.pathname === '/api/auth/refresh',
      handle: () => json(auth('token-a', user.tenantId, user.memberships)),
    },
    {
      method: 'GET',
      test: (url) => url.pathname === '/api/auth/me',
      handle: () => json(user),
    },
    ...extra,
  ]
}

function catalogRoutes(products: Paged<Product>, categories: Paged<Category> = page([])): Route[] {
  return [
    {
      method: 'GET',
      test: (url) => url.pathname === '/api/catalog/categories',
      handle: () => json(categories),
    },
    {
      method: 'GET',
      test: (url) => url.pathname === '/api/catalog/products',
      handle: () => json(products),
    },
  ]
}

it('inicia y cierra sesión sin guardar el token en el navegador', async () => {
  const user = userEvent.setup()
  const alfa = profile({ tenantId: 1 })
  const calls = install([
    {
      method: 'POST',
      test: (url) => url.pathname === '/api/auth/refresh',
      handle: () => problem(401, 'La sesión expiró. Inicie sesión de nuevo.'),
    },
    {
      method: 'POST',
      test: (url) => url.pathname === '/api/auth/login',
      handle: (_url, init) => {
        expect(tokenOf(init)).toBeNull()
        expect(JSON.parse(String(init.body))).toEqual({ login: 'ana', password: 'secreta', client: 'web' })
        return json(auth('token-a', 1, alfa.memberships))
      },
    },
    {
      method: 'GET',
      test: (url) => url.pathname === '/api/auth/me',
      handle: (_url, init) => {
        expect(tokenOf(init)).toBe('Bearer token-a')
        return json(alfa)
      },
    },
    {
      method: 'POST',
      test: (url) => url.pathname === '/api/auth/logout',
      handle: () => new Response(null, { status: 204 }),
    },
  ])

  renderApp('/login')
  await user.type(await screen.findByLabelText('Usuario'), 'ana')
  await user.type(screen.getByLabelText('Contraseña'), 'secreta')
  await user.click(screen.getByRole('button', { name: 'Entrar' }))

  expect(await screen.findByRole('button', { name: 'Cerrar sesión' })).toBeInTheDocument()
  expect(screen.getByText('Empresa activa: Alfa.')).toBeInTheDocument()
  expect(localStorage.length).toBe(0)
  expect(sessionStorage.length).toBe(0)
  expect(calls.some((call) => call.init.credentials === 'include')).toBe(true)

  await user.click(screen.getByRole('button', { name: 'Cerrar sesión' }))
  expect(await screen.findByLabelText('Usuario')).toBeInTheDocument()
  expect(calls.some((call) => call.method === 'POST' && call.url === '/api/auth/logout')).toBe(true)
})

it('muestra el 401 de login y permanece en el formulario', async () => {
  const user = userEvent.setup()
  install([
    {
      method: 'POST',
      test: (url) => url.pathname === '/api/auth/refresh',
      handle: () => problem(401, 'La sesión expiró. Inicie sesión de nuevo.'),
    },
    {
      method: 'POST',
      test: (url) => url.pathname === '/api/auth/login',
      handle: () => problem(401, 'Usuario o contraseña inválidos, o cuenta bloqueada temporalmente.'),
    },
  ])

  renderApp('/login')
  await user.type(await screen.findByLabelText('Usuario'), 'ana')
  await user.type(screen.getByLabelText('Contraseña'), 'mala')
  await user.click(screen.getByRole('button', { name: 'Entrar' }))

  expect(await screen.findByRole('alert')).toHaveTextContent('Usuario o contraseña inválidos, o cuenta bloqueada temporalmente.')
  expect(screen.getByLabelText('Usuario')).toBeInTheDocument()
})

it('deja elegir la empresa cuando el token todavía no tiene una', async () => {
  const user = userEvent.setup()
  const memberships = [membership(1, 'Alfa'), membership(2, 'Beta')]
  install([
    {
      method: 'POST',
      test: (url) => url.pathname === '/api/auth/refresh',
      handle: () => problem(401, 'La sesión expiró. Inicie sesión de nuevo.'),
    },
    {
      method: 'POST',
      test: (url) => url.pathname === '/api/auth/login',
      handle: () => json(auth('token-a', null, memberships)),
    },
    {
      method: 'POST',
      test: (url) => url.pathname === '/api/auth/tenant',
      handle: (_url, init) => {
        expect(tokenOf(init)).toBe('Bearer token-a')
        expect(JSON.parse(String(init.body))).toEqual({ tenantId: 2 })
        return json(auth('token-b', 2, memberships))
      },
    },
    {
      method: 'GET',
      test: (url) => url.pathname === '/api/auth/me',
      handle: (_url, init) => {
        expect(tokenOf(init)).toBe('Bearer token-b')
        return json(profile({ tenantId: 2, memberships }))
      },
    },
  ])

  renderApp('/login')
  await user.type(await screen.findByLabelText('Usuario'), 'ana')
  await user.type(screen.getByLabelText('Contraseña'), 'secreta')
  await user.click(screen.getByRole('button', { name: 'Entrar' }))
  await user.click(await screen.findByRole('button', { name: 'Beta' }))

  expect(await screen.findByText('Empresa activa: Beta.')).toBeInTheDocument()
})

it('protege una ruta y vuelve al login si no hay sesión', async () => {
  install([
    {
      method: 'POST',
      test: (url) => url.pathname === '/api/auth/refresh',
      handle: () => problem(401, 'La sesión expiró. Inicie sesión de nuevo.'),
    },
  ])

  renderApp('/productos')
  expect(await screen.findByLabelText('Usuario')).toBeInTheDocument()
  expect(screen.queryByRole('heading', { name: 'Productos' })).not.toBeInTheDocument()
})

it('oculta acciones y secciones que el permiso no autoriza', async () => {
  const seller = profile({ tenantId: 1, role: 'seller', permissions: ['catalog.read'] })
  install(sessionRoutes(seller, catalogRoutes(page([]))))

  renderApp('/productos')
  expect(await screen.findByRole('heading', { name: 'Productos' })).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Nuevo producto' })).not.toBeInTheDocument()
  expect(screen.queryByRole('link', { name: 'Clientes' })).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Siguiente' })).not.toBeInTheDocument()
})

it('niega la pantalla de clientes sin customers.read', async () => {
  const seller = profile({ tenantId: 1, role: 'seller', permissions: ['catalog.read'] })
  const calls = install(sessionRoutes(seller))

  renderApp('/clientes')
  expect(await screen.findByRole('alert')).toHaveTextContent('No tiene permiso para ver clientes.')
  expect(calls.some((call) => call.url.startsWith('/api/customers'))).toBe(false)
})

it('muestra el 403 que devuelve la API aunque el menú permita la pantalla', async () => {
  const calls = install(sessionRoutes(profile({ tenantId: 1 }), [
    {
      method: 'GET',
      test: (url) => url.pathname === '/api/catalog/categories',
      handle: () => json(page([])),
    },
    {
      method: 'GET',
      test: (url) => url.pathname === '/api/catalog/products',
      handle: () => problem(403, 'No tiene permiso para esta acción.'),
    },
  ]))

  renderApp('/productos')
  expect(await screen.findByRole('alert')).toHaveTextContent('No tiene permiso para esta acción.')
  expect(calls.some((call) => call.method === 'GET' && call.url.startsWith('/api/catalog/products'))).toBe(true)
})

it('valida el producto y muestra el 400 de código repetido en el campo', async () => {
  const user = userEvent.setup()
  const calls = install(sessionRoutes(profile({ tenantId: 1 }), [
    ...catalogRoutes(page([]), page([category({ id: 3 })])),
    {
      method: 'POST',
      test: (url) => url.pathname === '/api/catalog/products',
      handle: () => problem(400, 'Los datos no son válidos.', { Code: ['Ya existe un producto con ese código.'] }),
    },
  ]))

  renderApp('/productos')
  await user.click(await screen.findByRole('button', { name: 'Nuevo producto' }))
  await user.click(screen.getByRole('button', { name: 'Guardar' }))
  expect(await screen.findAllByText('Es obligatorio.')).not.toHaveLength(0)
  expect(calls.some((call) => call.method === 'POST' && call.url === '/api/catalog/products')).toBe(false)

  await user.type(screen.getByLabelText('Código'), 'A-1')
  await user.type(screen.getByLabelText('Nombre'), 'Aguardiente')
  await user.selectOptions(screen.getByLabelText('Categoría'), '3')
  await user.type(screen.getByLabelText('Precio unidad'), '12000')
  await user.click(screen.getByRole('button', { name: 'Guardar' }))

  expect(await screen.findByText('Ya existe un producto con ese código.')).toBeInTheDocument()
  const post = calls.find((call) => call.method === 'POST' && call.url === '/api/catalog/products')
  expect(JSON.parse(String(post?.init.body))).toMatchObject({ code: 'A-1', unitPrice: 12000, isActive: true })
  expect(JSON.parse(String(post?.init.body)).version).toBeUndefined()
})

it('muestra el 409 de concurrencia al editar un producto', async () => {
  const user = userEvent.setup()
  const calls = install(sessionRoutes(profile({ tenantId: 1 }), [
    ...catalogRoutes(page([product()]), page([category({ id: 3 })])),
    {
      method: 'PUT',
      test: (url) => url.pathname === '/api/catalog/products/8',
      handle: () => problem(409, 'El registro fue modificado por otra persona. Recargue e intente de nuevo.'),
    },
  ]))

  renderApp('/productos')
  expect(await screen.findByText('Aguardiente')).toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'Editar' }))
  await user.clear(screen.getByLabelText('Nombre'))
  await user.type(screen.getByLabelText('Nombre'), 'Otro nombre')
  await user.click(screen.getByRole('button', { name: 'Guardar' }))

  expect(await screen.findByRole('alert')).toHaveTextContent('El registro fue modificado por otra persona. Recargue e intente de nuevo.')
  const put = calls.find((call) => call.method === 'PUT')
  expect(JSON.parse(String(put?.init.body)).version).toBe(1)
})

it('pide la página siguiente y oculta el control cuando no hace falta', async () => {
  const user = userEvent.setup()
  const calls = install(sessionRoutes(profile({ tenantId: 1 }), catalogRoutes(page([product()], 45))))

  renderApp('/productos')
  expect(await screen.findByText('Página 1 de 3')).toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'Siguiente' }))

  await waitFor(() => {
    expect(calls.some((call) => call.method === 'GET' && call.url.includes('/api/catalog/products') && call.url.includes('page=2'))).toBe(true)
  })
})

it('al cambiar de empresa deja de mostrar los datos de la anterior', async () => {
  const user = userEvent.setup()
  const memberships = [membership(1, 'Alfa'), membership(2, 'Beta')]
  const calls = install([
    {
      method: 'POST',
      test: (url) => url.pathname === '/api/auth/refresh',
      handle: () => json(auth('token-a', 1, memberships)),
    },
    {
      method: 'GET',
      test: (url) => url.pathname === '/api/auth/me',
      handle: (_url, init) => json(profile({
        tenantId: tokenOf(init) === 'Bearer token-b' ? 2 : 1,
        memberships,
      })),
    },
    {
      method: 'POST',
      test: (url) => url.pathname === '/api/auth/tenant',
      handle: (_url, init) => {
        expect(tokenOf(init)).toBe('Bearer token-a')
        return json(auth('token-b', 2, memberships))
      },
    },
    {
      method: 'GET',
      test: (url) => url.pathname === '/api/catalog/categories',
      handle: () => json(page([])),
    },
    {
      method: 'GET',
      test: (url) => url.pathname === '/api/catalog/products',
      handle: (_url, init) => json(page(tokenOf(init) === 'Bearer token-a' ? [product({ name: 'Solo alfa' })] : [])),
    },
  ])

  renderApp('/productos')
  expect(await screen.findByText('Solo alfa')).toBeInTheDocument()
  await user.selectOptions(screen.getByLabelText('Empresa'), '2')

  expect(await screen.findByText('No hay productos.')).toBeInTheDocument()
  expect(screen.queryByText('Solo alfa')).not.toBeInTheDocument()
  expect(calls.some((call) => call.method === 'GET' && call.url.includes('/api/catalog/products') && tokenOf(call.init) === 'Bearer token-b')).toBe(true)
})

it('cierra la sesión si una petición recibe 401 y la renovación falla', async () => {
  let refreshes = 0
  install([
    {
      method: 'POST',
      test: (url) => url.pathname === '/api/auth/refresh',
      handle: () => {
        refreshes += 1
        if (refreshes === 1) return json(auth('token-a', 1, [membership(1, 'Alfa')]))
        return problem(401, 'La sesión expiró. Inicie sesión de nuevo.')
      },
    },
    {
      method: 'GET',
      test: (url) => url.pathname === '/api/auth/me',
      handle: () => json(profile({ tenantId: 1 })),
    },
    {
      method: 'GET',
      test: (url) => url.pathname === '/api/catalog/categories',
      handle: () => json(page([])),
    },
    {
      method: 'GET',
      test: (url) => url.pathname === '/api/catalog/products',
      handle: () => problem(401, 'La sesión expiró. Inicie sesión de nuevo.'),
    },
  ])

  renderApp('/productos')
  expect(await screen.findByLabelText('Usuario')).toBeInTheDocument()
  expect(screen.queryByRole('heading', { name: 'Productos' })).not.toBeInTheDocument()
  expect(refreshes).toBeGreaterThan(1)
})

it('sube la imagen y adopta la versión que devuelve la API', async () => {
  const user = userEvent.setup()
  install(sessionRoutes(profile({ tenantId: 1 }), [
    {
      method: 'GET',
      test: (url) => url.pathname === '/api/catalog/categories',
      handle: () => json(page([category()])),
    },
    {
      method: 'PUT',
      test: (url) => url.pathname === '/api/catalog/categories/5/image',
      handle: (_url, init) => {
        expect(init.body).toBeInstanceOf(FormData)
        expect((init.body as FormData).get('file')).toBeInstanceOf(File)
        expect(new Headers(init.headers).get('Content-Type')).toBeNull()
        return json(category({
          version: 2,
          imageKey: 't1/catalog/categories/abc.png',
          imageUrl: '/media/t1/catalog/categories/abc.png',
        }))
      },
    },
  ]))

  renderApp('/categorias')
  expect(await screen.findByText('Licores')).toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'Editar' }))
  expect(screen.getByText('Versión 1')).toBeInTheDocument()
  await user.upload(screen.getByLabelText('Imagen'), new File(['img'], 'foto.png', { type: 'image/png' }))

  expect(await screen.findByText('Versión 2')).toBeInTheDocument()
  expect(screen.getByRole('img', { name: 'Imagen del catálogo' })).toHaveAttribute('src', '/media/t1/catalog/categories/abc.png')
})

it('advierte el teléfono repetido y rechaza uno inválido sin llamar a la API', async () => {
  const user = userEvent.setup()
  const saved: Customer = {
    id: 9,
    name: 'Bar Central',
    identification: null,
    phone: '573001234567',
    address: null,
    zone: null,
    establishmentId: null,
    establishmentName: null,
    registeredAtUtc: '2026-10-24T12:00:00Z',
    isActive: true,
    version: 1,
    updatedAtUtc: '2026-10-24T12:00:00Z',
  }
  const calls = install(sessionRoutes(profile({ tenantId: 1 }), [
    {
      method: 'GET',
      test: (url) => url.pathname === '/api/customers/customers/phone-check',
      handle: () => json({ isValid: true, phone: '573001234567', samePhoneCustomerIds: [] }),
    },
    {
      method: 'GET',
      test: (url) => url.pathname === '/api/customers/customers',
      handle: () => json(page([])),
    },
    {
      method: 'GET',
      test: (url) => url.pathname === '/api/customers/zones',
      handle: () => json([]),
    },
    {
      method: 'GET',
      test: (url) => url.pathname === '/api/customers/establishments',
      handle: () => json(page([])),
    },
    {
      method: 'POST',
      test: (url) => url.pathname === '/api/customers/customers',
      handle: () => json({ customer: saved, samePhoneCustomerIds: [4] } satisfies CustomerSave),
    },
  ]))

  renderApp('/clientes')
  await user.click(await screen.findByRole('button', { name: 'Nuevo cliente' }))
  await user.type(screen.getByLabelText('Nombre'), 'Bar Central')
  await user.type(screen.getByLabelText('Teléfono'), '12345')
  await user.click(screen.getByRole('button', { name: 'Guardar' }))

  expect(await screen.findByText(phoneMessage)).toBeInTheDocument()
  expect(calls.some((call) => call.method === 'POST' && call.url === '/api/customers/customers')).toBe(false)

  await user.clear(screen.getByLabelText('Teléfono'))
  await user.type(screen.getByLabelText('Teléfono'), '3001234567')
  await user.click(screen.getByRole('button', { name: 'Guardar' }))

  expect(await screen.findByText('Este teléfono también lo usa el cliente 4.')).toBeInTheDocument()
  const post = calls.find((call) => call.method === 'POST' && call.url === '/api/customers/customers')
  expect(JSON.parse(String(post?.init.body)).phone).toBe('3001234567')
})
