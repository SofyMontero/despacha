import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { apiGet, apiSend, queryString } from '../api/client'
import { applyFieldErrors, messageOf } from '../api/errors'
import { limits, pageSize, type Category, type Paged } from '../api/types'
import { useSession } from '../auth/session'
import { useCatalogImage } from '../components/use-catalog-image'
import { Banner, Button, CatalogImage, DataTable, Dialog, Field, Pagination, QueryBanner, StatusPill, Td, TextInput, Th } from '../components/ui'
import { useListFilters } from '../lib/use-list-filters'

const schema = z.object({
  name: z.string().trim().min(1, 'Es obligatorio.').max(limits.categoryName, `Máximo ${limits.categoryName} caracteres.`),
})

type FormValues = z.infer<typeof schema>

export function CategoriesPage() {
  const session = useSession()
  const tenantId = session.me?.tenantId
  const filters = useListFilters()
  const [editor, setEditor] = useState<Category | 'new' | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const queryClient = useQueryClient()
  const canManage = session.has('catalog.manage')

  const query = useQuery({
    queryKey: ['categories', tenantId, filters.debounced, filters.active, filters.page],
    enabled: tenantId != null,
    queryFn: () => apiGet<Paged<Category>>(`/api/catalog/categories${queryString({
      q: filters.debounced,
      active: filters.active,
      page: filters.page,
      pageSize,
    })}`),
  })

  const status = useMutation({
    mutationFn: (row: Category) => apiSend<Category>(`/api/catalog/categories/${row.id}/status`, 'PATCH', {
      isActive: !row.isActive,
      version: row.version,
    }),
    onSuccess: async () => {
      setActionError(null)
      await queryClient.invalidateQueries({ queryKey: ['categories', tenantId] })
    },
    onError: (error) => setActionError(messageOf(error)),
  })

  return (
    <section className="grid gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="font-display text-3xl">Categorías</h1>
        {canManage ? <Button onClick={() => setEditor('new')}>Nueva categoría</Button> : null}
      </div>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Buscar">
          <TextInput value={filters.q} onChange={(event) => filters.setQ(event.target.value)} />
        </Field>
        <Field label="Estado">
          <select className="w-full rounded-md border border-line bg-panel px-3 py-2" value={filters.active} onChange={(event) => filters.setActive(event.target.value as '' | 'true' | 'false')}>
            <option value="">Todos</option>
            <option value="true">Activas</option>
            <option value="false">Inactivas</option>
          </select>
        </Field>
      </div>
      {actionError ? <Banner>{actionError}</Banner> : null}
      <QueryBanner error={query.error} />
      {query.isPending ? <p>Cargando…</p> : null}
      {query.data && query.data.items.length === 0 ? <p>No hay categorías.</p> : null}
      {query.data && query.data.items.length > 0 ? (
        <DataTable>
          <thead>
            <tr><Th>Nombre</Th><Th>Estado</Th><Th>Acciones</Th></tr>
          </thead>
          <tbody>
            {query.data.items.map((row) => (
              <tr key={row.id}>
                <Td>{row.name}</Td>
                <Td><StatusPill active={row.isActive} /></Td>
                <Td>
                  {canManage ? (
                    <div className="flex flex-wrap gap-2">
                      <Button variant="secondary" onClick={() => setEditor(row)}>Editar</Button>
                      <Button variant="ghost" disabled={status.isPending} onClick={() => status.mutate(row)}>
                        {row.isActive ? 'Desactivar' : 'Activar'}
                      </Button>
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
        <CategoryDialog
          category={editor === 'new' ? null : editor}
          onClose={() => setEditor(null)}
          onSaved={() => void queryClient.invalidateQueries({ queryKey: ['categories', tenantId] })}
        />
      ) : null}
    </section>
  )
}

function CategoryDialog({ category, onClose, onSaved }: { category: Category | null; onClose: () => void; onSaved: () => void }) {
  const session = useSession()
  const tenantId = session.me?.tenantId
  const image = useCatalogImage<Category>(
    category ? `/api/catalog/categories/${category.id}/image` : null,
    { version: category?.version, imageUrl: category?.imageUrl ?? null },
    ['categories', tenantId],
  )
  const [formError, setFormError] = useState<string | null>(null)
  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: category?.name ?? '' },
  })

  async function onSubmit(values: FormValues) {
    setFormError(null)
    try {
      await apiSend<Category>(category ? `/api/catalog/categories/${category.id}` : '/api/catalog/categories', category ? 'PUT' : 'POST', {
        name: values.name.trim(),
        ...(category ? { version: image.version ?? category.version } : {}),
      })
      onSaved()
      onClose()
    } catch (error) {
      if (!applyFieldErrors(error, (name, fieldError) => form.setError(name as 'name', fieldError))) {
        setFormError(messageOf(error))
      }
    }
  }

  return (
    <Dialog title={category ? 'Editar categoría' : 'Nueva categoría'} onClose={onClose}>
      <form className="grid gap-3" noValidate onSubmit={form.handleSubmit(onSubmit)}>
        {formError ? <Banner>{formError}</Banner> : null}
        <Field label="Nombre" error={form.formState.errors.name?.message}>
          <TextInput {...form.register('name')} />
        </Field>
        <CatalogImage
          imageUrl={image.imageUrl}
          version={image.version}
          pending={image.pending}
          error={image.error}
          canUpload={category != null}
          onSelect={(file) => void image.upload(file)}
          onRemove={() => void image.remove()}
        />
        <Button type="submit" disabled={form.formState.isSubmitting}>
          {form.formState.isSubmitting ? 'Guardando…' : 'Guardar'}
        </Button>
      </form>
    </Dialog>
  )
}
