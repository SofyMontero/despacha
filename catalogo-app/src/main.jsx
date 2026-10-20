import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { registerSW } from 'virtual:pwa-register'
import App from './App.jsx'
import { CartProvider } from './context/CartContext.jsx'
import { ClienteProvider } from './context/ClienteContext.jsx'
import './index.css'

registerSW({ immediate: true })

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <BrowserRouter basename="/sistema/app">
      <ClienteProvider>
        <CartProvider>
          <App />
        </CartProvider>
      </ClienteProvider>
    </BrowserRouter>
  </StrictMode>,
)
