import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { apiGet, apiSend, queryString } from '../api/client'
import { messageOf } from '../api/errors'
import { pageSize, type Order, type Paged } from '../api/types'
import { useSession } from '../auth/session'
import { Banner, Button, DataTable, Dialog, Field, Pagination, QueryBanner, Td, TextInput, Th } from '../components/ui'
import { formatCop, formatDate } from '../lib/money'

const actions = [
  { action: 'approve', label: 'Aceptar', permission: 'orders.approve', from: ['1', 'pendiente'] },
  { action: 'reject', label: 'Rechazar', permission: 'orders.cancel', from: ['1', 'pendiente'] },
  { action: 'dispatch', label: 'Enviar', permission: 'orders.approve', from: ['2', 'aceptado'] },
  { action: 'deliverCash', label: 'Finalizar de contado', permission: 'orders.approve', from: ['enviado'] },
  { action: 'deliverCredit', label: 'Finalizar a crédito', permission: 'orders.approve', from: ['enviado'] },
] as const

function today() {
  const date = new Date()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}

export function OrdersPage() {
  const session = useSession()
  const tenantId = session.me?.tenantId
  const [from, setFrom] = useState(today)
  const [to, setTo] = useState(today)
  const [status, setStatus] = useState('pending')
  const [q, setQ] = useState('')
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<number | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const queryClient = useQueryClient()

  const query = useQuery({
    queryKey: ['orders', tenantId, from, to, status, q, page],
    enabled: tenantId != null,
    queryFn: () => apiGet<Paged<Order>>(`/api/orders${queryString({ from, to, status, q, page, pageSize })}`),
  })

  const detail = useQuery({
    queryKey: ['order', tenantId, selected],
    enabled: tenantId != null && selected != null,
    queryFn: () => apiGet<Order>(`/api/orders/${selected}`),
  })

  const transition = useMutation({
    mutationFn: (action: (typeof actions)[number]['action']) => {
      const path = action === 'reject' ? `/api/orders/${selected}/reject` : `/api/orders/${selected}/transition`
      const body = action === 'reject' ? undefined : { action }
      return apiSend<Order>(path, 'POST', body)
    },
    onSuccess: async () => {
      setActionError(null)
      await queryClient.invalidateQueries({ queryKey: ['orders', tenantId] })
      await queryClient.invalidateQueries({ queryKey: ['order', tenantId, selected] })
    },
    onError: (error) => setActionError(messageOf(error)),
  })

  return (
    <section className="grid gap-4">
      <h1 className="font-display text-3xl">Pedidos</h1>
      <div className="grid gap-3 sm:grid-cols-4">
        <Field label="Desde">
          <TextInput type="date" value={from} onChange={(event) => { setFrom(event.target.value); setPage(1) }} />
        </Field>
        <Field label="Hasta">
          <TextInput type="date" value={to} onChange={(event) => { setTo(event.target.value); setPage(1) }} />
        </Field>
        <Field label="Estado">
          <select className="w-full rounded-md border border-line bg-panel px-3 py-2" value={status} onChange={(event) => { setStatus(event.target.value); setPage(1) }}>
            <option value="">Todos</option>
            <option value="pending">Pendientes</option>
            <option value="approved">Aceptados</option>
            <option value="dispatch">Enviados</option>
            <option value="delivered">Finalizados</option>
            <option value="rejected">Rechazados</option>
          </select>
        </Field>
        <Field label="Número o teléfono">
          <TextInput value={q} onChange={(event) => { setQ(event.target.value); setPage(1) }} />
        </Field>
      </div>
      <QueryBanner error={query.error} />
      {query.isPending ? <p>Cargando…</p> : null}
      {query.data && query.data.items.length === 0 ? <p>No hay pedidos en este rango.</p> : null}
      {query.data && query.data.items.length > 0 ? (
        <DataTable>
          <thead>
            <tr><Th>Número</Th><Th>Fecha</Th><Th>Cliente</Th><Th>Total</Th><Th>Estado</Th><Th>Acciones</Th></tr>
          </thead>
          <tbody>
            {query.data.items.map((row) => (
              <tr key={row.id}>
                <Td>{row.number}</Td>
                <Td>{formatDate(row.createdAt)}</Td>
                <Td>{row.customerName ?? row.phone ?? '—'}</Td>
                <Td>{formatCop(row.total)}</Td>
                <Td>{row.statusLabel}</Td>
                <Td><Button variant="secondary" onClick={() => { setActionError(null); setSelected(row.id) }}>Ver</Button></Td>
              </tr>
            ))}
          </tbody>
        </DataTable>
      ) : null}
      {query.data ? <Pagination page={query.data.page} pageSize={query.data.pageSize} total={query.data.total} onPage={setPage} /> : null}

      {selected != null ? (
        <Dialog title={detail.data?.number ?? 'Pedido'} onClose={() => setSelected(null)}>
          {detail.isPending ? <p>Cargando…</p> : null}
          <QueryBanner error={detail.error} />
          {actionError ? <Banner>{actionError}</Banner> : null}
          {detail.data ? (
            <div className="grid gap-3">
              <p>{detail.data.statusLabel} · {formatDate(detail.data.createdAt)}</p>
              <p>{detail.data.customerName ?? 'Cliente sin nombre'}{detail.data.phone ? ` · ${detail.data.phone}` : ''}</p>
              {detail.data.address ? <p>{detail.data.address}</p> : null}
              {detail.data.note ? <p>Nota: {detail.data.note}</p> : null}
              <DataTable>
                <thead>
                  <tr><Th>Producto</Th><Th>Tipo</Th><Th>Cantidad</Th><Th>Precio</Th><Th>Subtotal</Th></tr>
                </thead>
                <tbody>
                  {detail.data.lines?.map((line) => (
                    <tr key={line.id}>
                      <Td>{line.name}</Td>
                      <Td>{line.kind}</Td>
                      <Td>{line.quantity}</Td>
                      <Td>{formatCop(line.unitPrice)}</Td>
                      <Td>{formatCop(line.subtotal)}</Td>
                    </tr>
                  ))}
                </tbody>
              </DataTable>
              <p className="font-semibold">Total {formatCop(detail.data.total)}</p>
              <div className="flex flex-wrap gap-2">
                {actions.filter((item) => session.has(item.permission) && (item.from as readonly string[]).includes(detail.data!.status)).map((item) => (
                  <Button key={item.action} disabled={transition.isPending} onClick={() => transition.mutate(item.action)}>{item.label}</Button>
                ))}
              </div>
            </div>
          ) : null}
        </Dialog>
      ) : null}
    </section>
  )
}
