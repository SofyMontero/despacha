import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { apiGet, apiSend, queryString } from '../api/client'
import { applyFieldErrors, messageOf } from '../api/errors'
import { limits, pageSize, type Customer, type CustomerSave, type Establishment, type Paged, type PhoneCheck } from '../api/types'
import { useSession } from '../auth/session'
import { Banner, Button, DataTable, Dialog, Field, Pagination, QueryBanner, SelectInput, StatusPill, Td, TextInput, Th } from '../components/ui'
import { formatDate } from '../lib/money'
import { isColombianPhone, phoneMessage, phoneWarningText } from '../lib/phone'
import { useListFilters } from '../lib/use-list-filters'

const schema = z.object({
  name: z.string().trim().min(1, 'Es obligatorio.').max(limits.name, `Máximo ${limits.name} caracteres.`),
  phone: z.string().trim().refine(isColombianPhone, phoneMessage),
  identification: z.string().trim().max(limits.identification, `Máximo ${limits.identification} caracteres.`),
  address: z.string().trim().max(limits.address, `Máximo ${limits.address} caracteres.`),
  zone: z.string().trim().max(limits.zone, `Máximo ${limits.zone} caracteres.`),
  establishmentId: z.string(),
})

type FormValues = z.infer<typeof schema>

export function CustomersPage() {
  const session = useSession()
  const tenantId = session.me?.tenantId
  const filters = useListFilters()
  const [zone, setZone] = useState('')
  const [establishmentId, setEstablishmentId] = useState('')
  const [editor, setEditor] = useState<Customer | 'new' | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const queryClient = useQueryClient()
  const canManage = session.has('customers.manage')

  const zones = useQuery({
    queryKey: ['zones', tenantId],
    enabled: tenantId != null,
    queryFn: () => apiGet<string[]>('/api/customers/zones'),
  })

  const establishments = useQuery({
    queryKey: ['establishments', 'options', tenantId],
    enabled: tenantId != null,
    queryFn: () => apiGet<Paged<Establishment>>(`/api/customers/establishments${queryString({ page: 1, pageSize: 100 })}`),
  })

  const query = useQuery({
    queryKey: ['customers', tenantId, filters.debounced, filters.active, zone, establishmentId, filters.page],
    enabled: tenantId != null,
    queryFn: () => apiGet<Paged<Customer>>(`/api/customers/customers${queryString({
      q: filters.debounced,
      active: filters.active,
      zone,
      establishmentId,
      page: filters.page,
      pageSize,
    })}`),
  })

  const status = useMutation({
    mutationFn: (row: Customer) => apiSend<Customer>(`/api/customers/customers/${row.id}/status`, 'PATCH', {
      isActive: !row.isActive,
      version: row.version,
    }),
    onSuccess: async () => {
      setActionError(null)
      await queryClient.invalidateQueries({ queryKey: ['customers', tenantId] })
    },
    onError: (error) => setActionError(messageOf(error)),
  })

  return (
    <section className="grid gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="font-display text-3xl">Clientes</h1>
        {canManage ? <Button onClick={() => setEditor('new')}>Nuevo cliente</Button> : null}
      </div>
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <Field label="Buscar"><TextInput value={filters.q} onChange={(event) => filters.setQ(event.target.value)} placeholder="Nombre, identificación o teléfono" /></Field>
        <Field label="Zona">
          <SelectInput value={zone} onChange={(event) => { setZone(event.target.value); filters.setPage(1) }}>
            <option value="">Todas</option>
            {zones.data?.map((item) => <option key={item} value={item}>{item}</option>)}
          </SelectInput>
        </Field>
        <Field label="Filtrar por establecimiento">
          <SelectInput value={establishmentId} onChange={(event) => { setEstablishmentId(event.target.value); filters.setPage(1) }}>
            <option value="">Todos</option>
            {establishments.data?.items.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
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
      {notice ? <Banner tone="warning">{notice}</Banner> : null}
      {actionError ? <Banner>{actionError}</Banner> : null}
      <QueryBanner error={query.error} />
      {query.isPending ? <p>Cargando…</p> : null}
      {query.data && query.data.items.length === 0 ? <p>No hay clientes.</p> : null}
      {query.data && query.data.items.length > 0 ? (
        <DataTable>
          <thead><tr><Th>Nombre</Th><Th>Teléfono</Th><Th>Establecimiento</Th><Th>Zona</Th><Th>Registro</Th><Th>Estado</Th><Th>Acciones</Th></tr></thead>
          <tbody>
            {query.data.items.map((row) => (
              <tr key={row.id}>
                <Td>{row.name}</Td>
                <Td>{row.phone}</Td>
                <Td>{row.establishmentName}</Td>
                <Td>{row.zone}</Td>
                <Td>{formatDate(row.registeredAtUtc)}</Td>
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
        <CustomerDialog
          customer={editor === 'new' ? null : editor}
          establishments={establishments.data?.items ?? []}
          zones={zones.data ?? []}
          onClose={() => setEditor(null)}
          onSaved={(saved) => {
            setNotice(saved.samePhoneCustomerIds.length > 0 ? phoneWarningText(saved.samePhoneCustomerIds) : null)
            void queryClient.invalidateQueries({ queryKey: ['customers', tenantId] })
            void queryClient.invalidateQueries({ queryKey: ['zones', tenantId] })
          }}
        />
      ) : null}
    </section>
  )
}

function CustomerDialog({
  customer,
  establishments,
  zones,
  onClose,
  onSaved,
}: {
  customer: Customer | null
  establishments: Establishment[]
  zones: string[]
  onClose: () => void
  onSaved: (saved: CustomerSave) => void
}) {
  const [formError, setFormError] = useState<string | null>(null)
  const [liveWarning, setLiveWarning] = useState<number[]>([])
  const [version, setVersion] = useState(customer?.version)
  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: customer?.name ?? '',
      phone: customer?.phone ?? '',
      identification: customer?.identification ?? '',
      address: customer?.address ?? '',
      zone: customer?.zone ?? '',
      establishmentId: customer?.establishmentId ? String(customer.establishmentId) : '',
    },
  })

  async function lookupPhone(value: string) {
    if (!isColombianPhone(value)) {
      setLiveWarning([])
      return
    }

    const result = await apiGet<PhoneCheck>(`/api/customers/customers/phone-check${queryString({ phone: value, excludeId: customer?.id })}`)
    setLiveWarning(result.samePhoneCustomerIds)
  }

  async function onSubmit(values: FormValues) {
    setFormError(null)
    try {
      const saved = await apiSend<CustomerSave>(customer ? `/api/customers/customers/${customer.id}` : '/api/customers/customers', customer ? 'PUT' : 'POST', {
        name: values.name.trim(),
        phone: values.phone.trim(),
        identification: values.identification.trim() || null,
        address: values.address.trim() || null,
        zone: values.zone.trim() || null,
        establishmentId: values.establishmentId ? Number(values.establishmentId) : null,
        ...(version === undefined ? { isActive: true } : { version }),
      })
      setVersion(saved.customer.version)
      onSaved(saved)
      onClose()
    } catch (error) {
      if (!applyFieldErrors(error, (name, fieldError) => form.setError(name as keyof FormValues, fieldError))) {
        setFormError(messageOf(error))
      }
    }
  }

  return (
    <Dialog title={customer ? 'Editar cliente' : 'Nuevo cliente'} onClose={onClose}>
      <form className="grid gap-3" noValidate onSubmit={form.handleSubmit(onSubmit)}>
        {formError ? <Banner>{formError}</Banner> : null}
        {version ? <p className="text-sm text-ink/70">Versión {version}</p> : null}
        <Field label="Nombre" error={form.formState.errors.name?.message}><TextInput {...form.register('name')} /></Field>
        <Field label="Teléfono" error={form.formState.errors.phone?.message}>
          <TextInput
            {...form.register('phone', {
              onBlur: (event) => { void lookupPhone(event.target.value).catch(() => undefined) },
            })}
          />
        </Field>
        {liveWarning.length > 0 ? <Banner tone="warning">{phoneWarningText(liveWarning)}</Banner> : null}
        <Field label="Identificación" error={form.formState.errors.identification?.message}><TextInput {...form.register('identification')} /></Field>
        <Field label="Dirección" error={form.formState.errors.address?.message}><TextInput {...form.register('address')} /></Field>
        <Field label="Zona del cliente" error={form.formState.errors.zone?.message}>
          <TextInput list="customer-zones" {...form.register('zone')} />
        </Field>
        <datalist id="customer-zones">
          {zones.map((item) => <option key={item} value={item} />)}
        </datalist>
        <Field label="Establecimiento" error={form.formState.errors.establishmentId?.message}>
          <SelectInput {...form.register('establishmentId')}>
            <option value="">Sin establecimiento</option>
            {establishments.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
          </SelectInput>
        </Field>
        <Button type="submit" disabled={form.formState.isSubmitting}>{form.formState.isSubmitting ? 'Guardando…' : 'Guardar'}</Button>
      </form>
    </Dialog>
  )
}
