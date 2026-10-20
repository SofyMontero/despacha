import { Navigate, Route, Routes } from 'react-router-dom'
import Layout from './components/Layout.jsx'
import CategoriasPage from './pages/CategoriasPage.jsx'
import CatalogoPage from './pages/CatalogoPage.jsx'
import PromocionesPage from './pages/PromocionesPage.jsx'
import CheckoutPage from './pages/CheckoutPage.jsx'
import PedidoOkPage from './pages/PedidoOkPage.jsx'

export default function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<CategoriasPage />} />
        <Route path="catalogo" element={<CatalogoPage />} />
        <Route path="promociones" element={<PromocionesPage />} />
        <Route path="checkout" element={<CheckoutPage />} />
        <Route path="pedido-ok" element={<PedidoOkPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}
