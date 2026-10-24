import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { apiGet, apiSend, queryString } from '../api/client'
import { applyFieldErrors, messageOf } from '../api/errors'
import { limits, pageSize, type Paged, type Product, type Promotion } from '../api/types'
import { useSession } from '../auth/session'
import { useCatalogImage } from '../components/use-catalog-image'
import { Banner, Button, CatalogImage, DataTable, Dialog, Field, Pagination, QueryBanner, SelectInput, StatusPill, Td, TextArea, TextInput, Th } from '../components/ui'
import { formatCop, formatMoneyInput, moneyError, parseMoney } from '../lib/money'
import { useListFilters } from '../lib/use-list-filters'

const schema = z.object({
  title: z.string().trim().min(1, 'Es obligatorio.').max(limits.promotionTitle, `Máximo ${limits.promotionTitle} caracteres.`),
  description: z.string().trim().min(1, 'Es obligatorio.').max(limits.promotionDescription, `Máximo ${limits.promotionDescription} caracteres.`),
  code: z.string().trim().min(1, 'Es obligatorio.').max(limits.code).refine(
    (value) => /^\d+$/.test(value) && /[1-9]/.test(value),
    'Debe ser un código de producto numérico mayor que 0.',
  ),
  productId: z.string(),
  unitPrice: z.string(),
  packPrice: z.string(),
  priority: z.string().trim().refine((value) => /^-?\d+$/.test(value), 'Debe ser un número entero.'),
  sellsByUnit: z.boolean(),
}).superRefine((values, context) => {
  for (const field of ['unitPrice', 'packPrice'] as const) {
    const error = moneyError(values[field], false)
    if (error) context.addIssue({ code: z.ZodIssueCode.custom, path: [field], message: error })
  }
})

type FormValues = z.infer<typeof schema>

export function PromotionsPage() {
  const session = useSession()
  const tenantId = session.me?.tenantId
  const filters = useListFilters()
  const [editor, setEditor] = useState<Promotion | 'new' | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const queryClient = useQueryClient()
  const canManage = session.has('catalog.manage')

  const products = useQuery({
    queryKey: ['products', 'options', tenantId],
    enabled: tenantId != null,
    queryFn: () => apiGet<Paged<Product>>(`/api/catalog/products${queryString({ page: 1, pageSize: 100 })}`),
  })

  const query = useQuery({
    queryKey: ['promotions', tenantId, filters.debounced, filters.active, filters.page],
    enabled: tenantId != null,
    queryFn: () => apiGet<Paged<Promotion>>(`/api/catalog/promotions${queryString({
      q: filters.debounced,
      active: filters.active,
      page: filters.page,
      pageSize,
    })}`),
  })

  const status = useMutation({
    mutationFn: (row: Promotion) => apiSend<Promotion>(`/api/catalog/promotions/${row.id}/status`, 'PATCH', {
      isActive: !row.isActive,
      version: row.version,
    }),
    onSuccess: async () => {
      setActionError(null)
      await queryClient.invalidateQueries({ queryKey: ['promotions', tenantId] })
    },
    onError: (error) => setActionError(messageOf(error)),
  })

  return (
    <section className="grid gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="font-display text-3xl">Promociones</h1>
        {canManage ? <Button onClick={() => setEditor('new')}>Nueva promoción</Button> : null}
      </div>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Buscar"><TextInput value={filters.q} onChange={(event) => filters.setQ(event.target.value)} /></Field>
        <Field label="Estado">
          <SelectInput value={filters.active} onChange={(event) => filters.setActive(event.target.value as '' | 'true' | 'false')}>
            <option value="">Todas</option>
            <option value="true">Activas</option>
            <option value="false">Inactivas</option>
          </SelectInput>
        </Field>
      </div>
      {actionError ? <Banner>{actionError}</Banner> : null}
      <QueryBanner error={query.error} />
      {query.isPending ? <p>Cargando…</p> : null}
      {query.data && query.data.items.length === 0 ? <p>No hay promociones.</p> : null}
      {query.data && query.data.items.length > 0 ? (
        <DataTable>
          <thead><tr><Th>Título</Th><Th>Código</Th><Th>Prioridad</Th><Th>Unidad</Th><Th>Estado</Th><Th>Acciones</Th></tr></thead>
          <tbody>
            {query.data.items.map((row) => (
              <tr key={row.id}>
                <Td>{row.title}</Td>
                <Td>{row.code}</Td>
                <Td>{row.priority}</Td>
                <Td>{formatCop(row.unitPrice)}</Td>
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
        <PromotionDialog
          promotion={editor === 'new' ? null : editor}
          products={products.data?.items ?? []}
          onClose={() => setEditor(null)}
          onSaved={() => void queryClient.invalidateQueries({ queryKey: ['promotions', tenantId] })}
        />
      ) : null}
    </section>
  )
}

function PromotionDialog({
  promotion,
  products,
  onClose,
  onSaved,
}: {
  promotion: Promotion | null
  products: Product[]
  onClose: () => void
  onSaved: () => void
}) {
  const session = useSession()
  const image = useCatalogImage<Promotion>(
    promotion ? `/api/catalog/promotions/${promotion.id}/image` : null,
    { version: promotion?.version, imageUrl: promotion?.imageUrl ?? null },
    ['promotions', session.me?.tenantId],
  )
  const [formError, setFormError] = useState<string | null>(null)
  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      title: promotion?.title ?? '',
      description: promotion?.description ?? '',
      code: promotion?.code ?? '',
      productId: promotion?.productId ? String(promotion.productId) : '',
      unitPrice: promotion ? formatMoneyInput(promotion.unitPrice) : '0',
      packPrice: promotion ? formatMoneyInput(promotion.packPrice) : '0',
      priority: String(promotion?.priority ?? 0),
      sellsByUnit: promotion?.sellsByUnit ?? true,
    },
  })

  async function onSubmit(values: FormValues) {
    setFormError(null)
    const unitPrice = values.unitPrice.trim() ? parseMoney(values.unitPrice) : 0
    const packPrice = values.packPrice.trim() ? parseMoney(values.packPrice) : 0
    if (unitPrice === null || packPrice === null) return

    try {
      await apiSend(promotion ? `/api/catalog/promotions/${promotion.id}` : '/api/catalog/promotions', promotion ? 'PUT' : 'POST', {
        title: values.title.trim(),
        description: values.description.trim(),
        code: values.code.trim(),
        productId: values.productId ? Number(values.productId) : null,
        unitPrice,
        packPrice,
        priority: Number(values.priority),
        sellsByUnit: values.sellsByUnit,
        ...(image.version === undefined ? { isActive: true } : { version: image.version }),
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
    <Dialog title={promotion ? 'Editar promoción' : 'Nueva promoción'} onClose={onClose}>
      <form className="grid gap-3" noValidate onSubmit={form.handleSubmit(onSubmit)}>
        {formError ? <Banner>{formError}</Banner> : null}
        <Field label="Título" error={form.formState.errors.title?.message}><TextInput {...form.register('title')} /></Field>
        <Field label="Descripción" error={form.formState.errors.description?.message}><TextArea {...form.register('description')} /></Field>
        <Field label="Código" error={form.formState.errors.code?.message}><TextInput {...form.register('code')} /></Field>
        <Field label="Producto">
          <SelectInput {...form.register('productId')}>
            <option value="">Sin producto enlazado</option>
            {products.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
          </SelectInput>
        </Field>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Precio unidad" error={form.formState.errors.unitPrice?.message}><TextInput inputMode="decimal" {...form.register('unitPrice')} /></Field>
          <Field label="Precio paca" error={form.formState.errors.packPrice?.message}><TextInput inputMode="decimal" {...form.register('packPrice')} /></Field>
        </div>
        <Field label="Prioridad" error={form.formState.errors.priority?.message}><TextInput inputMode="numeric" {...form.register('priority')} /></Field>
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" {...form.register('sellsByUnit')} />
          Se vende por unidad
        </label>
        <CatalogImage imageUrl={image.imageUrl} version={image.version} pending={image.pending} error={image.error} canUpload={promotion != null} onSelect={(file) => void image.upload(file)} onRemove={() => void image.remove()} />
        <Button type="submit" disabled={form.formState.isSubmitting}>{form.formState.isSubmitting ? 'Guardando…' : 'Guardar'}</Button>
      </form>
    </Dialog>
  )
}
