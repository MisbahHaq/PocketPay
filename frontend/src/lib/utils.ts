import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

/**
 * Merges class names, letting a caller override a component's defaults.
 *
 * twMerge is what makes the last argument win: without it, `cn('p-2', 'p-4')` leaves both
 * classes in the output and the winner depends on stylesheet order, so a caller could not
 * reliably adjust spacing on a shared component.
 */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}