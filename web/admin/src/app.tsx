import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useState, type ReactNode } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { SessionProvider, useSession } from './auth/session'
import { Forbidden, Layout, Loading } from './components/layout'
import { CategoriesPage } from './pages/categories-page'
import { CustomersPage } from './pages/customers-page'
import { EstablishmentsPage } from './pages/establishments-page'
import { HomePage } from './pages/home-page'
import { LoginPage } from './pages/login-page'
import { ProductsPage } from './pages/products-page'
import { OrdersPage } from './pages/orders-page'
import { PromotionsPage } from './pages/promotions-page'

export function AppProviders({ children }: { children: ReactNode }) {
  const [client] = useState(() => new QueryClient({
    defaultOptions: {
      queries: {
        retry: (failureCount, error) => {
          const status = typeof error === 'object' && error && 'status' in error ? Number(error.status) : 500
          return status >= 500 && failureCount < 1
        },
      },
    },
  }))

  return (
    <QueryClientProvider client={client}>
      <SessionProvider>{children}</SessionProvider>
    </QueryClientProvider>
  )
}

function Protected() {
  const session = useSession()
  if (session.status === 'loading') return <Loading />
  if (session.status === 'anonymous' || session.status === 'choose-tenant') return <Navigate to="/login" replace />
  return <Layout />
}

function Permit({ permission, what, children }: { permission: string; what: string; children: ReactNode }) {
  const session = useSession()
  if (!session.has(permission)) return <Forbidden what={what} />
  return children
}

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<Protected />}>
        <Route path="/" element={<HomePage />} />
        <Route path="/categorias" element={<Permit permission="catalog.read" what="categorías"><CategoriesPage /></Permit>} />
        <Route path="/productos" element={<Permit permission="catalog.read" what="productos"><ProductsPage /></Permit>} />
        <Route path="/promociones" element={<Permit permission="catalog.read" what="promociones"><PromotionsPage /></Permit>} />
        <Route path="/clientes" element={<Permit permission="customers.read" what="clientes"><CustomersPage /></Permit>} />
        <Route path="/establecimientos" element={<Permit permission="customers.read" what="establecimientos"><EstablishmentsPage /></Permit>} />
        <Route path="/pedidos" element={<Permit permission="orders.read" what="pedidos"><OrdersPage /></Permit>} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

export function App() {
  return (
    <AppProviders>
      <BrowserRouter>
        <AppRoutes />
      </BrowserRouter>
    </AppProviders>
  )
}
