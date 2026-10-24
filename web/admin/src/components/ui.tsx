import { useEffect, type ButtonHTMLAttributes, type ReactNode, type SelectHTMLAttributes } from 'react'
import { messageOf } from '../api/errors'

export function cx(...parts: Array<string | false | null | undefined>) {
  return parts.filter(Boolean).join(' ')
}

const controlClass = 'w-full rounded-md border border-line bg-panel px-3 py-2 text-ink'

export function Button({
  variant = 'primary',
  className,
  type = 'button',
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: 'primary' | 'secondary' | 'ghost' }) {
  const styles = {
    primary: 'bg-wine text-white hover:bg-wine-dark',
    secondary: 'border border-line bg-panel hover:bg-paper',
    ghost: 'hover:bg-paper',
  }[variant]

  return (
    <button
      type={type}
      {...props}
      className={cx('inline-flex items-center justify-center rounded-md px-3 py-2 text-sm font-semibold disabled:opacity-50', styles, className)}
    />
  )
}

export function Field({ label, error, children }: { label: string; error?: string; children: ReactNode }) {
  return (
    <div className="grid gap-1 text-sm">
      <label className="grid gap-1">
        <span className="font-medium">{label}</span>
        {children}
      </label>
      {error ? <span className="text-sm text-danger">{error}</span> : null}
    </div>
  )
}

export function TextInput(props: React.InputHTMLAttributes<HTMLInputElement>) {
  return <input {...props} className={cx(controlClass, props.className)} />
}

export function TextArea(props: React.TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea {...props} className={cx(controlClass, 'min-h-24', props.className)} />
}

export function SelectInput(props: SelectHTMLAttributes<HTMLSelectElement>) {
  return <select {...props} className={cx(controlClass, props.className)} />
}

export function Banner({ tone = 'danger', children }: { tone?: 'danger' | 'warning'; children: ReactNode }) {
  const role = tone === 'warning' ? 'status' : 'alert'
  return (
    <p role={role} className={cx('rounded-md px-3 py-2 text-sm', tone === 'warning' ? 'bg-[#f8f1de] text-[#6b4a12]' : 'bg-red-50 text-danger')}>
      {children}
    </p>
  )
}

export function QueryBanner({ error }: { error: unknown }) {
  if (!error) return null
  return <Banner>{messageOf(error)}</Banner>
}

export function Dialog({ title, onClose, children }: { title: string; onClose: () => void; children: ReactNode }) {
  useEffect(() => {
    function onKey(event: KeyboardEvent) {
      if (event.key === 'Escape') onClose()
    }

    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  return (
    <div className="fixed inset-0 z-40 flex items-end justify-center bg-ink/40 sm:items-center sm:p-4" onMouseDown={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="dialog-title"
        className="max-h-[92vh] w-full overflow-y-auto bg-panel p-4 shadow-xl sm:max-w-xl sm:rounded-lg"
        onMouseDown={(event) => event.stopPropagation()}
      >
        <div className="mb-4 flex items-start justify-between gap-3">
          <h2 id="dialog-title" className="font-display text-2xl">{title}</h2>
          <Button variant="ghost" aria-label="Cerrar formulario" onClick={onClose}>Cerrar</Button>
        </div>
        {children}
      </div>
    </div>
  )
}

export function Pagination({
  page,
  pageSize,
  total,
  onPage,
}: {
  page: number
  pageSize: number
  total: number
  onPage: (page: number) => void
}) {
  if (total <= pageSize) return null
  const pages = Math.ceil(total / pageSize)

  return (
    <div className="mt-4 flex flex-wrap items-center gap-3">
      <Button variant="secondary" disabled={page <= 1} onClick={() => onPage(page - 1)}>Anterior</Button>
      <span className="text-sm">Página {page} de {pages}</span>
      <Button variant="secondary" disabled={page >= pages} onClick={() => onPage(page + 1)}>Siguiente</Button>
    </div>
  )
}

export function StatusPill({ active }: { active: boolean }) {
  return (
    <span className={cx('rounded-full px-2 py-0.5 text-xs font-semibold', active ? 'bg-emerald-100 text-moss' : 'bg-stone-200 text-stone-700')}>
      {active ? 'Activo' : 'Inactivo'}
    </span>
  )
}

export function DataTable({ children }: { children: ReactNode }) {
  return (
    <div className="overflow-x-auto rounded-lg border border-line bg-panel">
      <table className="w-full min-w-[44rem] border-collapse text-left text-sm">{children}</table>
    </div>
  )
}

export function Th({ children }: { children: ReactNode }) {
  return <th className="px-3 py-2 text-xs font-semibold tracking-wide text-ink/60 uppercase">{children}</th>
}

export function Td({ children, className }: { children: ReactNode; className?: string }) {
  return <td className={cx('border-t border-line px-3 py-2 align-middle', className)}>{children}</td>
}

/**
 * Muestra solo la URL que devolvió el catálogo (`/media/...` o la URL heredada).
 * No sirve para armar rutas de documentos privados.
 */
export function CatalogImage({
  imageUrl,
  version,
  pending,
  error,
  canUpload,
  onSelect,
  onRemove,
}: {
  imageUrl: string | null
  version?: number
  pending: boolean
  error?: string
  canUpload: boolean
  onSelect: (file: File) => void
  onRemove: () => void
}) {
  return (
    <div className="grid gap-2">
      {imageUrl ? (
        <img src={imageUrl} alt="Imagen del catálogo" referrerPolicy="no-referrer" className="h-28 w-28 rounded-md object-cover" />
      ) : (
        <p className="text-sm text-ink/70">Sin imagen.</p>
      )}
      {version !== undefined ? <p className="text-sm text-ink/70">Versión {version}</p> : null}
      {canUpload ? (
        <Field label="Imagen">
          <input
            type="file"
            accept="image/jpeg,image/png,image/gif,image/webp"
            disabled={pending}
            className="text-sm"
            onChange={(event) => {
              const file = event.target.files?.[0]
              event.target.value = ''
              if (file) onSelect(file)
            }}
          />
        </Field>
      ) : (
        <p className="text-sm text-ink/70">Guarde el registro para cargar la imagen.</p>
      )}
      {canUpload && imageUrl ? (
        <Button variant="secondary" disabled={pending} onClick={onRemove}>Quitar imagen</Button>
      ) : null}
      {pending ? <p className="text-sm">Subiendo imagen…</p> : null}
      {error ? <Banner>{error}</Banner> : null}
    </div>
  )
}
