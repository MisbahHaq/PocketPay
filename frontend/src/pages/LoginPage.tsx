import { useMutation } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useLocation, useNavigate } from 'react-router-dom'

import { useAuth } from '@/context/AuthContext'
import { Alert, AlertIcon } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ApiError, ValidationError } from '@/lib/api'

/**
 * Mirrors the server's rules so obvious mistakes are caught without a round trip.
 * The server remains the authority: an identical rule here is for feedback speed, not
 * for security, and this schema must never be treated as validation that matters.
 */
const loginSchema = z.object({
  email: z.string().min(1, 'Email is required.').email('Enter a valid email address.'),
  password: z.string().min(1, 'Password is required.'),
})

type LoginForm = z.infer<typeof loginSchema>

export function LoginPage() {
  const { signIn } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()

  const from = (location.state as { from?: { pathname: string } } | null)?.from?.pathname ?? '/'

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LoginForm>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  })

  const mutation = useMutation({
    mutationFn: ({ email, password }: LoginForm) => signIn(email, password),
  })

  function onSubmit(values: LoginForm) {
    mutation.reset()

    return signIn(values.email, values.password).then(
      () => navigate(from, { replace: true }),
      (error: unknown) => {
        if (error instanceof ValidationError) {
          // Attach each message to its field so it appears beside the input.
          for (const [field, messages] of Object.entries(error.errors)) {
            if (field === 'email' || field === 'password') {
              setError(field, { message: messages[0] })
            }
          }
          return
        }

        if (error instanceof ApiError) {
          // 401 here is always "wrong credentials". The API answers identically for an
          // unknown account, so there is nothing more specific to show.
          mutation.error = error
        }
      },
    )
  }

  const failureMessage =
    mutation.error instanceof ApiError
      ? mutation.error.status === 0
        ? mutation.error.message
        : 'Sign-in failed. Check your email and password.'
      : null

  return (
    <main className="flex min-h-screen items-center justify-center bg-muted/40 px-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>Inventory Management</CardTitle>
          <CardDescription>Sign in to continue</CardDescription>
        </CardHeader>

        <CardContent>
          <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
            {failureMessage && (
              <Alert>
                <AlertIcon />
                <span>{failureMessage}</span>
              </Alert>
            )}

            <div className="space-y-2">
              <Label htmlFor="email">Email</Label>
              <Input
                id="email"
                type="email"
                autoComplete="username"
                autoFocus
                aria-invalid={errors.email !== undefined}
                {...register('email')}
              />
              {errors.email && (
                <p role="alert" className="text-xs text-destructive">
                  {errors.email.message}
                </p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="password">Password</Label>
              <Input
                id="password"
                type="password"
                autoComplete="current-password"
                aria-invalid={errors.password !== undefined}
                {...register('password')}
              />
              {errors.password && (
                <p role="alert" className="text-xs text-destructive">
                  {errors.password.message}
                </p>
              )}
            </div>

            <Button type="submit" className="w-full" disabled={isSubmitting || mutation.isPending}>
              {isSubmitting || mutation.isPending ? 'Signing in…' : 'Sign in'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </main>
  )
}