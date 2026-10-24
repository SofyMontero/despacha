import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { apiGet, apiSend, queryString } from '../api/client'
import { applyFieldErrors, messageOf } from '../api/errors'
import { limits, pageSize, type Category, type Paged, type Product } from '../api/types'
import { useSession } from '../auth/session'
import { useCatalogImage } from '../components/use-catalog-image'
import { Banner, Button, CatalogImage, DataTable, Dialog, Field, Pagination, QueryBanner, SelectInput, StatusPill, Td, TextInput, Th } from '../components/ui'
import { formatCop, formatMoneyInput, moneyError, parseMoney } from '../lib/money'
import { useListFilters } from '../lib/use-list-filters'

const schema = z.object({
  code: z.string().trim().min(1, 'Es obligatorio.').max(limits.code, `Máximo ${limits.code} caracteres.`),
  name: z.string().trim().min(1, 'Es obligatorio.').max(limits.productName, `Máximo ${limits.productName} caracteres.`),
  categoryId: z.string().min(1, 'Es obligatorio.'),
  unitPrice: z.string(),
  packPrice: z.string(),
  unitsPerPack: z.string(),
  sellsByUnit: z.boolean(),
}).superRefine((values, context) => {
  const unit = moneyError(values.unitPrice, true)
  if (unit) context.addIssue({ code: z.ZodIssueCode.custom, path: ['unitPrice'], message: unit })
  const pack = moneyError(values.packPrice, false)
  if (pack) context.addIssue({ code: z.ZodIssueCode.custom, path: ['packPrice'], message: pack })
  if (!values.unitsPerPack.trim()) {
    context.addIssue({ code: z.ZodIssueCode.custom, path: ['unitsPerPack'], message: 'Es obligatorio.' })
    return
  }

  const units = Number(values.unitsPerPack)
  if (!Number.isInteger(units) || units < 1 || units > limits.maxUnitsPerPack) {
    context.addIssue({ code: z.ZodIssueCode.custom, path: ['unitsPerPack'], message: `Debe estar entre 1 y ${limits.maxUnitsPerPack}.` })
  }
})

type FormValues = z.infer<typeof schema>

export function ProductsPage() {
  const session = useSession()
  const tenantId = session.me?.tenantId
  const filters = useListFilters()
  const { setPage } = filters
  const [categoryId, setCategoryId] = useState('')
  const [editor, setEditor] = useState<Product | 'new' | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const queryClient = useQueryClient()
  const canManage = session.has('catalog.manage')

  useEffect(() => {
    setPage(1)
  }, [categoryId, setPage])

  const categories = useQuery({
    queryKey: ['categories', 'options', tenantId],
    enabled: tenantId != null,
    queryFn: () => apiGet<Paged<Category>>(`/api/catalog/categories${queryString({ page: 1, pageSize: 100 })}`),
  })

  const query = useQuery({
    queryKey: ['products', tenantId, filters.debounced, filters.active, categoryId, filters.page],
    enabled: tenantId != null,
    queryFn: () => apiGet<Paged<Product>>(`/api/catalog/products${queryString({
      q: filters.debounced,
      active: filters.active,
      categoryId,
      page: filters.page,
      pageSize,
    })}`),
  })

  const status = useMutation({
    mutationFn: (row: Product) => apiSend<Product>(`/api/catalog/products/${row.id}/status`, 'PATCH', {
      isActive: !row.isActive,
      version: row.version,
    }),
    onSuccess: async () => {
      setActionError(null)
      await queryClient.invalidateQueries({ queryKey: ['products', tenantId] })
    },
    onError: (error) => setActionError(messageOf(error)),
  })

  return (
    <section className="grid gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="font-display text-3xl">Productos</h1>
        {canManage ? <Button onClick={() => setEditor('new')}>Nuevo producto</Button> : null}
      </div>
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <Field label="Buscar">
          <TextInput value={filters.q} onChange={(event) => filters.setQ(event.target.value)} />
        </Field>
        <Field label="Filtrar por categoría">
          <SelectInput value={categoryId} onChange={(event) => setCategoryId(event.target.value)}>
            <option value="">Todas</option>
            {categories.data?.items.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
          </SelectInput>
        </Field>
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
      {query.data && query.data.items.length === 0 ? <p>No hay productos.</p> : null}
      {query.data && query.data.items.length > 0 ? (
        <DataTable>
          <thead>
            <tr>
              <Th>Código</Th><Th>Nombre</Th><Th>Categoría</Th><Th>Unidad</Th><Th>Paca</Th><Th>Estado</Th><Th>Acciones</Th>
            </tr>
          </thead>
          <tbody>
            {query.data.items.map((row) => (
              <tr key={row.id}>
                <Td>{row.code}</Td>
                <Td>{row.name}</Td>
                <Td>{row.categoryName}</Td>
                <Td>{formatCop(row.unitPrice)}</Td>
                <Td>{formatCop(row.packPrice)}</Td>
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
        <ProductDialog
          product={editor === 'new' ? null : editor}
          categories={categories.data?.items ?? []}
          truncated={(categories.data?.total ?? 0) > (categories.data?.items.length ?? 0)}
          onClose={() => setEditor(null)}
          onSaved={() => void queryClient.invalidateQueries({ queryKey: ['products', tenantId] })}
        />
      ) : null}
    </section>
  )
}

function ProductDialog({
  product,
  categories,
  truncated,
  onClose,
  onSaved,
}: {
  product: Product | null
  categories: Category[]
  truncated: boolean
  onClose: () => void
  onSaved: () => void
}) {
  const session = useSession()
  const tenantId = session.me?.tenantId
  const image = useCatalogImage<Product>(
    product ? `/api/catalog/products/${product.id}/image` : null,
    { version: product?.version, imageUrl: product?.imageUrl ?? null },
    ['products', tenantId],
  )
  const [formError, setFormError] = useState<string | null>(null)
  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      code: product?.code ?? '',
      name: product?.name ?? '',
      categoryId: product?.categoryId ? String(product.categoryId) : '',
      unitPrice: product ? formatMoneyInput(product.unitPrice) : '',
      packPrice: product ? formatMoneyInput(product.packPrice) : '0',
      unitsPerPack: String(product?.unitsPerPack ?? 1),
      sellsByUnit: product?.sellsByUnit ?? true,
    },
  })

  async function onSubmit(values: FormValues) {
    setFormError(null)
    const unitPrice = parseMoney(values.unitPrice)
    const packPrice = values.packPrice.trim() ? parseMoney(values.packPrice) : 0
    if (unitPrice === null || packPrice === null) return

    try {
      await apiSend<Product>(product ? `/api/catalog/products/${product.id}` : '/api/catalog/products', product ? 'PUT' : 'POST', {
        code: values.code.trim(),
        name: values.name.trim(),
        categoryId: Number(values.categoryId),
        unitPrice,
        packPrice,
        unitsPerPack: Number(values.unitsPerPack),
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
    <Dialog title={product ? 'Editar producto' : 'Nuevo producto'} onClose={onClose}>
      <form className="grid gap-3" noValidate onSubmit={form.handleSubmit(onSubmit)}>
        {formError ? <Banner>{formError}</Banner> : null}
        <Field label="Código" error={form.formState.errors.code?.message}>
          <TextInput {...form.register('code')} />
        </Field>
        <Field label="Nombre" error={form.formState.errors.name?.message}>
          <TextInput {...form.register('name')} />
        </Field>
        <Field label="Categoría" error={form.formState.errors.categoryId?.message}>
          <SelectInput {...form.register('categoryId')}>
            <option value="">Seleccione</option>
            {categories.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
          </SelectInput>
        </Field>
        {truncated ? <p className="text-sm">La lista muestra las primeras 100 categorías.</p> : null}
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Precio unidad" error={form.formState.errors.unitPrice?.message}>
            <TextInput inputMode="decimal" {...form.register('unitPrice')} />
          </Field>
          <Field label="Precio paca" error={form.formState.errors.packPrice?.message}>
            <TextInput inputMode="decimal" {...form.register('packPrice')} />
          </Field>
        </div>
        <Field label="Unidades por paca" error={form.formState.errors.unitsPerPack?.message}>
          <TextInput inputMode="numeric" {...form.register('unitsPerPack')} />
        </Field>
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" {...form.register('sellsByUnit')} />
          Se vende por unidad
        </label>
        <CatalogImage
          imageUrl={image.imageUrl}
          version={image.version}
          pending={image.pending}
          error={image.error}
          canUpload={product != null}
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
