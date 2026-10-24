import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { apiGet, apiSend, queryString } from '../api/client'
import { applyFieldErrors, messageOf } from '../api/errors'
import { limits, pageSize, type Establishment, type Paged } from '../api/types'
import { useSession } from '../auth/session'
import { Banner, Button, DataTable, Dialog, Field, Pagination, QueryBanner, SelectInput, StatusPill, Td, TextInput, Th } from '../components/ui'
import { useListFilters } from '../lib/use-list-filters'

const schema = z.object({
  name: z.string().trim().min(1, 'Es obligatorio.').max(limits.name, `Máximo ${limits.name} caracteres.`),
  address: z.string().trim().max(limits.address, `Máximo ${limits.address} caracteres.`),
})

type FormValues = z.infer<typeof schema>

export function EstablishmentsPage() {
  const session = useSession()
  const tenantId = session.me?.tenantId
  const filters = useListFilters()
  const [editor, setEditor] = useState<Establishment | 'new' | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const queryClient = useQueryClient()
  const canManage = session.has('customers.manage')

  const query = useQuery({
    queryKey: ['establishments', tenantId, filters.debounced, filters.active, filters.page],
    enabled: tenantId != null,
    queryFn: () => apiGet<Paged<Establishment>>(`/api/customers/establishments${queryString({
      q: filters.debounced,
      active: filters.active,
      page: filters.page,
      pageSize,
    })}`),
  })

  const status = useMutation({
    mutationFn: (row: Establishment) => apiSend<Establishment>(`/api/customers/establishments/${row.id}/status`, 'PATCH', {
      isActive: !row.isActive,
      version: row.version,
    }),
    onSuccess: async () => {
      setActionError(null)
      await queryClient.invalidateQueries({ queryKey: ['establishments', tenantId] })
    },
    onError: (error) => setActionError(messageOf(error)),
  })

  return (
    <section className="grid gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="font-display text-3xl">Establecimientos</h1>
        {canManage ? <Button onClick={() => setEditor('new')}>Nuevo establecimiento</Button> : null}
      </div>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Buscar"><TextInput value={filters.q} onChange={(event) => filters.setQ(event.target.value)} /></Field>
        <Field label="Estado">
          <SelectInput value={filters.active} onChange={(event) => filters.setActive(event.target.value as '' | 'true' | 'false')}>
            <option value="">Todos</option>
            <option value="true">Activos</option>
            <option value="false">Inactivos</option>
          </SelectInput>
        </Field>
      </div>
      {actionError ? <Banner>{actionError}</Banner> : null}
      <QueryBanner error={query.error} />
      {query.isPending ? <p>Cargando…</p> : null}
      {query.data && query.data.items.length === 0 ? <p>No hay establecimientos.</p> : null}
      {query.data && query.data.items.length > 0 ? (
        <DataTable>
          <thead><tr><Th>Nombre</Th><Th>Dirección</Th><Th>Estado</Th><Th>Acciones</Th></tr></thead>
          <tbody>
            {query.data.items.map((row) => (
              <tr key={row.id}>
                <Td>{row.name}</Td>
                <Td>{row.address}</Td>
                <Td><StatusPill active={row.isActive} /></Td>
                <Td>
                  {canManage ? (
                    <div className="flex flex-wrap gap-2">
                      <Button variant="secondary" onClick={() => setEditor(row)}>Editar</Button>
                      <Button variant="ghost" disabled={status.isPending} onClick={() => status.mutate(row)}>{row.isActive ? 'Desactivar' : 'Activar'}</Button>
                    </div>
                  ) : null}
                </Td>
              </tr>
            ))}
          </tbody>
        </DataTable>
      ) : null}
      {query.data ? <Pagination page={filters.page} pageSize={pageSize} total={query.data.total} onPage={filters.setPage} /> : null}
      {editor ? (
        <EstablishmentDialog
          establishment={editor === 'new' ? null : editor}
          onClose={() => setEditor(null)}
          onSaved={() => void queryClient.invalidateQueries({ queryKey: ['establishments', tenantId] })}
        />
      ) : null}
    </section>
  )
}

function EstablishmentDialog({ establishment, onClose, onSaved }: { establishment: Establishment | null; onClose: () => void; onSaved: () => void }) {
  const [formError, setFormError] = useState<string | null>(null)
  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: establishment?.name ?? '', address: establishment?.address ?? '' },
  })

  async function onSubmit(values: FormValues) {
    setFormError(null)
    try {
      await apiSend(establishment ? `/api/customers/establishments/${establishment.id}` : '/api/customers/establishments', establishment ? 'PUT' : 'POST', {
        name: values.name.trim(),
        address: values.address.trim() || null,
        ...(establishment ? { version: establishment.version } : {}),
      })
      onSaved()
      onClose()
    } catch (error) {
      if (!applyFieldErrors(error, (name, fieldError) => form.setError(name as keyof FormValues, fieldError))) {
        setFormError(messageOf(error))
      }
    }
  }

  return (
    <Dialog title={establishment ? 'Editar establecimiento' : 'Nuevo establecimiento'} onClose={onClose}>
      <form className="grid gap-3" noValidate onSubmit={form.handleSubmit(onSubmit)}>
        {formError ? <Banner>{formError}</Banner> : null}
        {establishment ? <p className="text-sm text-ink/70">Versión {establishment.version}</p> : null}
        <Field label="Nombre" error={form.formState.errors.name?.message}><TextInput {...form.register('name')} /></Field>
        <Field label="Dirección" error={form.formState.errors.address?.message}><TextInput {...form.register('address')} /></Field>
        <Button type="submit" disabled={form.formState.isSubmitting}>{form.formState.isSubmitting ? 'Guardando…' : 'Guardar'}</Button>
      </form>
    </Dialog>
  )
}
