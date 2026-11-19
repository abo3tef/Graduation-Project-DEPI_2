/**
 * Control and button classes for the auth screens, so login, register, verify and reset cannot
 * drift apart visually. They mirror the medical-records form controls.
 */
export const controlClassName =
  'w-full rounded-xl border border-border bg-background/80 px-3.5 py-3 text-sm text-foreground outline-none transition-all placeholder:text-muted/60 focus:-translate-y-px focus:border-brand focus:ring-4 focus:ring-brand/15 aria-[invalid=true]:border-danger disabled:cursor-not-allowed disabled:opacity-60';

export const primaryButtonClassName =
  'inline-flex min-h-12 w-full items-center justify-center gap-2 rounded-xl bg-brand px-4 py-2 text-sm font-bold text-[#06211f] shadow-lg shadow-brand/15 transition-all hover:-translate-y-0.5 hover:shadow-xl hover:shadow-brand/20 disabled:cursor-not-allowed disabled:opacity-50';

export const secondaryButtonClassName =
  'inline-flex min-h-10 items-center justify-center gap-2 rounded-xl border border-border px-4 py-2 text-sm font-semibold text-muted transition-colors hover:text-foreground disabled:cursor-not-allowed disabled:opacity-50';

export const linkClassName = 'font-bold text-brand hover:underline';

export const hintClassName = 'mt-1 text-xs text-muted';

export const fieldErrorClassName = 'mt-1 text-xs font-semibold text-danger';

export const cardClassName =
  'rounded-3xl border border-border/80 bg-surface/95 p-6 shadow-2xl shadow-black/10 backdrop-blur sm:p-9';
