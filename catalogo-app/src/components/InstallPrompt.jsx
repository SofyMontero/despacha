import { useEffect, useState } from 'react'

export default function InstallPrompt() {
  const [deferred, setDeferred] = useState(null)
  const [visible, setVisible] = useState(false)

  useEffect(() => {
    const dismissed = sessionStorage.getItem('ml_pwa_dismiss')
    if (dismissed) return undefined

    const onPrompt = (e) => {
      e.preventDefault()
      setDeferred(e)
      setVisible(true)
    }

    window.addEventListener('beforeinstallprompt', onPrompt)
    return () => window.removeEventListener('beforeinstallprompt', onPrompt)
  }, [])

  if (!visible || !deferred) return null

  return (
    <div className="install-banner" role="dialog" aria-label="Instalar app">
      <div className="install-banner__copy">
        <strong>Instala Multilicores</strong>
        <span>Acceso rápido desde tu pantalla de inicio</span>
      </div>
      <div className="install-banner__actions">
        <button
          type="button"
          className="btn btn-ghost"
          onClick={() => {
            sessionStorage.setItem('ml_pwa_dismiss', '1')
            setVisible(false)
          }}
        >
          Ahora no
        </button>
        <button
          type="button"
          className="btn btn-primary"
          onClick={async () => {
            deferred.prompt()
            await deferred.userChoice
            setDeferred(null)
            setVisible(false)
          }}
        >
          Instalar
        </button>
      </div>
    </div>
  )
}
