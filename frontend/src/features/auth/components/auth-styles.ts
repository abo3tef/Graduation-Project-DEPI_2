/**
 * Control and button classes for the auth screens, so login, register, verify and reset cannot
 * drift apart visually. They mirror the medical-records form controls.
 */
export const controlClassName =
  'w-full rounded-xl border border-border bg-background px-3 py-2 text-sm text-foreground outline-none transition-colors focus:border-brand focus:ring-2 focus:ring-brand/30 aria-[invalid=true]:border-danger disabled:cursor-not-allowed disabled:opacity-60';

export const primaryButtonClassName =
  'inline-flex min-h-10 w-full items-center justify-center gap-2 rounded-xl bg-brand px-4 py-2 text-sm font-bold text-[#06211f] transition-opacity hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50';

export const secondaryButtonClassName =
  'inline-flex min-h-10 items-center justify-center gap-2 rounded-xl border border-border px-4 py-2 text-sm font-semibold text-muted transition-colors hover:text-foreground disabled:cursor-not-allowed disabled:opacity-50';

export const linkClassName = 'font-bold text-brand hover:underline';

export const hintClassName = 'mt-1 text-xs text-muted';

export const fieldErrorClassName = 'mt-1 text-xs font-semibold text-danger';

export const cardClassName =
  'rounded-2xl border border-border bg-surface p-6 shadow-xs sm:p-8';
