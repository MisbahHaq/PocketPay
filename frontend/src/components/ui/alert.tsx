import * as React from 'react'
import { AlertCircle, CheckCircle2 } from 'lucide-react'

import { cn } from '@/lib/utils'

type AlertVariant = 'destructive' | 'success'

const Alert = React.forwardRef<
  HTMLDivElement,
  React.ComponentProps<'div'> & { variant?: AlertVariant }
>(({ className, variant = 'destructive', ...props }, ref) => (
  <div
    ref={ref}
    role="alert"
    aria-live="assertive"
    className={cn(
      'flex items-start gap-2 rounded-md border p-3 text-sm',
      variant === 'destructive'
        ? 'border-destructive/40 bg-destructive/5 text-destructive'
        : 'border-border bg-muted text-foreground',
      className,
    )}
    {...props}
  />
))
Alert.displayName = 'Alert'

const AlertIcon = React.forwardRef<HTMLDivElement, React.ComponentProps<'div'> & { variant?: AlertVariant }>(
  ({ className, variant = 'destructive', ...props }, ref) => (
    <div ref={ref} className={cn('mt-0.5 shrink-0', className)} {...props}>
      {variant === 'destructive' ? (
        <AlertCircle className="size-4" aria-hidden="true" />
      ) : (
        <CheckCircle2 className="size-4" aria-hidden="true" />
      )}
    </div>
  ),
)
AlertIcon.displayName = 'AlertIcon'

export { Alert, AlertIcon }