import { cn } from '@/lib/utils'

function Skeleton({ className, ...props }: React.ComponentProps<'div'>) {
  return (
    <div
      className={cn('animate-pulse rounded-md bg-muted', className)}
      // Decorative: a skeleton conveys no text, so announcing it would be noise for
      // screen reader users.
      aria-hidden="true"
      {...props}
    />
  )
}

export { Skeleton }