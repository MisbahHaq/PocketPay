import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuth } from '@/context/AuthContext'
import { canManageCatalog, canManageUsers } from '@/types/api'

/**
 * Phase 1 landing page. Catalog, stock, purchasing and sales arrive in later phases;
 * this confirms authentication works end to end before that work starts.
 */
export function DashboardPage() {
  const { user } = useAuth()

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">
          Welcome back{user?.fullName ? `, ${user.fullName.split(' ')[0]}` : ''}
        </h1>
        <p className="text-sm text-muted-foreground">
          You are signed in. Phase 1 covers authentication; the inventory modules follow.
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle className="text-sm font-medium text-muted-foreground">Your account</CardTitle>
            <CardDescription className="text-xs">Active roles determine what you can do.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-1.5">
            {user?.roles.length ? (
              user.roles.map((role) => (
                <span
                  key={role}
                  className="rounded-full bg-accent px-2.5 py-1 text-xs text-accent-foreground"
                >
                  {role}
                </span>
              ))
            ) : (
              <Skeleton className="h-6 w-16" />
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-sm font-medium text-muted-foreground">Catalog</CardTitle>
            <CardDescription className="text-xs">Product records and pricing.</CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-sm">
              {canManageCatalog(user) ? 'You can manage the catalog.' : 'Read only.'}
            </p>
            <p className="mt-1 text-xs text-muted-foreground">Arrives in phase 2.</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-sm font-medium text-muted-foreground">Users</CardTitle>
            <CardDescription className="text-xs">Accounts and role assignment.</CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-sm">
              {canManageUsers(user) ? 'You can create accounts.' : 'Admin only.'}
            </p>
            <p className="mt-1 text-xs text-muted-foreground">Arrives in a later phase.</p>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}

interface PlaceholderPageProps {
  title: string
  description: string
}

/** Shared placeholder for the phases not yet built, so no link is ever dead. */
export function PlaceholderPage({ title, description }: PlaceholderPageProps) {
  return (
    <div className="space-y-2">
      <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
      <p className="text-sm text-muted-foreground">{description}</p>
      <Card className="mt-4 max-w-xl">
        <CardHeader>
          <CardTitle className="text-base">Not built yet</CardTitle>
          <CardDescription>
            This module arrives in a later phase. The link is present so navigation and
            role gating can be reviewed now rather than after the module lands.
          </CardDescription>
        </CardHeader>
      </Card>
    </div>
  )
}