import { useState } from 'react'
import { Link, NavLink, Outlet } from 'react-router-dom'
import { Boxes, LayoutDashboard, LogOut, ShoppingCart, Truck, Users } from 'lucide-react'

import { useAuth } from '@/context/AuthContext'
import { canManageUsers } from '@/types/api'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Separator } from '@/components/ui/separator'

/**
 * Navigation entries carry the roles allowed to see them.
 *
 * This only hides links. Every one of these routes is protected server-side by a policy,
 * so removing an entry here is presentation, not security. Keeping both in step is the
 * point: a visible-but-forbidden link is a worse experience than an absent one.
 */
const navigation = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard, roles: null },
  { to: '/products', label: 'Products', icon: Boxes, roles: null },
  { to: '/stock', label: 'Stock', icon: Boxes, roles: null },
  { to: '/purchasing', label: 'Purchasing', icon: Truck, roles: null },
  { to: '/sales', label: 'Sales', icon: ShoppingCart, roles: null },
  { to: '/users', label: 'Users', icon: Users, roles: ['Admin'] },
] as const

export function AppLayout() {
  const { user, signOut } = useAuth()
  const [signingOut, setSigningOut] = useState(false)

  const visibleNavigation = navigation.filter(
    (item) => item.roles === null || (user && canManageUsers(user)),
  )

  const initials =
    user?.fullName
      .split(' ')
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() ?? '')
      .join('') || '?'

  async function handleSignOut() {
    setSigningOut(true)
    try {
      await signOut()
    } finally {
      setSigningOut(false)
    }
  }

  return (
    <div className="flex min-h-screen bg-muted/30">
      <aside className="hidden w-60 shrink-0 flex-col border-r border-border bg-background md:flex">
        <div className="flex h-14 items-center gap-2 border-b border-border px-4">
          <Boxes className="size-5" aria-hidden="true" />
          <span className="text-sm font-semibold">Inventory</span>
        </div>

        <nav className="flex-1 space-y-1 p-3">
          {visibleNavigation.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === '/'}
              className={({ isActive }) =>
                [
                  'flex items-center gap-2 rounded-md px-3 py-2 text-sm transition-colors',
                  isActive
                    ? 'bg-accent font-medium text-accent-foreground'
                    : 'text-muted-foreground hover:bg-accent/60 hover:text-accent-foreground',
                ].join(' ')
              }
            >
              <item.icon className="size-4" aria-hidden="true" />
              {item.label}
            </NavLink>
          ))}
        </nav>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-14 items-center justify-end gap-3 border-b border-border bg-background px-4">
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" className="h-9 gap-2 px-2">
                <Avatar className="size-7">
                  <AvatarFallback>{initials}</AvatarFallback>
                </Avatar>
                <span className="hidden text-sm sm:inline">{user?.fullName}</span>
              </Button>
            </DropdownMenuTrigger>

            <DropdownMenuContent align="end" className="w-56">
              <DropdownMenuLabel>
                <div className="text-sm font-medium">{user?.fullName}</div>
                <div className="truncate text-xs font-normal text-muted-foreground">
                  {user?.email}
                </div>
              </DropdownMenuLabel>

              <Separator />

              <div className="flex flex-wrap gap-1 px-2 py-1.5">
                {user?.roles.map((role) => (
                  <span
                    key={role}
                    className="rounded-full bg-accent px-2 py-0.5 text-xs text-accent-foreground"
                  >
                    {role}
                  </span>
                ))}
              </div>

              <Separator />

              <DropdownMenuItem
                onSelect={() => void handleSignOut()}
                disabled={signingOut}
                className="text-destructive focus:text-destructive"
              >
                <LogOut className="size-4" aria-hidden="true" />
                {signingOut ? 'Signing out…' : 'Sign out'}
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </header>

        <main className="flex-1 p-6">
          <Outlet />
        </main>
      </div>
    </div>
  )
}

export function NotFoundPage() {
  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-2 px-4 text-center">
      <p className="text-sm font-medium text-muted-foreground">404</p>
      <h1 className="text-2xl font-semibold">Page not found</h1>
      <p className="text-sm text-muted-foreground">
        The page you asked for does not exist.
      </p>
      <Button asChild variant="outline" className="mt-4">
        <Link to="/">Back to dashboard</Link>
      </Button>
    </main>
  )
}