import { useEffect } from 'react'
import { Outlet, useSearchParams } from 'react-router-dom'
import Header from './Header.jsx'
import Footer from './Footer.jsx'
import CartDrawer from './CartDrawer.jsx'
import InstallPrompt from './InstallPrompt.jsx'
import { useCliente } from '../context/ClienteContext.jsx'

export default function Layout() {
  const [params] = useSearchParams()
  const { setTelefono } = useCliente()

  useEffect(() => {
    const idCli = params.get('idCli')
    if (idCli) setTelefono(idCli)
  }, [params, setTelefono])

  return (
    <div className="app-shell">
      <Header />
      <main className="app-main">
        <Outlet />
      </main>
      <Footer />
      <CartDrawer />
      <InstallPrompt />
    </div>
  )
}
