import { Navigate, Route, Routes } from 'react-router-dom'
import LoginPage from './pages/LoginPage.jsx'
import ShellPage from './pages/ShellPage.jsx'
import PaseadorPage from './pages/PaseadorPage.jsx'
import { getSession } from './api/opsClient.js'

function RequireAuth({ children }) {
  if (!getSession()?.token) {
    return <Navigate to="/login" replace />
  }
  return children
}

export default function App() {
  return (
    <Routes>
      <Route path="login" element={<LoginPage />} />
      <Route
        path="/"
        element={
          <RequireAuth>
            <ShellPage />
          </RequireAuth>
        }
      />
      <Route
        path="paseador"
        element={
          <RequireAuth>
            <PaseadorPage />
          </RequireAuth>
        }
      />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
