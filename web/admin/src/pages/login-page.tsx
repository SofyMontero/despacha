import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Navigate } from 'react-router-dom'
import { z } from 'zod'
import { messageOf } from '../api/errors'
import { useSession } from '../auth/session'
import { Loading } from '../components/layout'
import { Banner, Button, Field, TextInput } from '../components/ui'

const schema = z.object({
  login: z.string().trim().min(1, 'Es obligatorio.'),
  password: z.string().min(1, 'Es obligatorio.'),
})

type FormValues = z.infer<typeof schema>

export function LoginPage() {
  const session = useSession()
  const [error, setError] = useState<string | null>(null)
  const form = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { login: '', password: '' } })

  if (session.status === 'loading' || session.status === 'switching') return <Loading />
  if (session.status === 'ready') return <Navigate to="/" replace />

  async function onSubmit(values: FormValues) {
    setError(null)
    try {
      await session.login(values.login, values.password)
    } catch (caught) {
      setError(messageOf(caught))
    }
  }

  return (
    <main className="mx-auto grid min-h-screen max-w-md content-center gap-6 px-4">
      <div>
        <p className="font-display text-4xl text-wine">Despacha</p>
        <h1 className="mt-2 text-lg">Administración</h1>
      </div>
      {session.status === 'choose-tenant' ? (
        <section className="grid gap-3">
          <h2 className="font-display text-2xl">Elija la empresa</h2>
          {session.memberships.map((item) => (
            <Button key={item.tenantId} onClick={() => void session.selectTenant(item.tenantId).catch((caught: unknown) => setError(messageOf(caught)))}>
              {item.tenantName}
            </Button>
          ))}
          {error ? <Banner>{error}</Banner> : null}
        </section>
      ) : (
        <form className="grid gap-3 rounded-lg border border-line bg-panel p-4" noValidate onSubmit={form.handleSubmit(onSubmit)}>
          {error ? <Banner>{error}</Banner> : null}
          <Field label="Usuario" error={form.formState.errors.login?.message}>
            <TextInput autoComplete="username" {...form.register('login')} />
          </Field>
          <Field label="Contraseña" error={form.formState.errors.password?.message}>
            <TextInput type="password" autoComplete="current-password" {...form.register('password')} />
          </Field>
          <Button type="submit" disabled={form.formState.isSubmitting}>{form.formState.isSubmitting ? 'Entrando…' : 'Entrar'}</Button>
        </form>
      )}
    </main>
  )
}
