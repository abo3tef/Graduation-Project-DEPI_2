/**
 * Shared control and button classes for the medical-records forms, so the visit, diagnosis and
 * medication forms cannot drift apart visually.
 */
export const controlClassName =
  'w-full rounded-xl border border-border bg-background px-3 py-2 text-sm text-foreground outline-none transition-colors focus:border-brand focus:ring-2 focus:ring-brand/30 aria-[invalid=true]:border-danger';

export const textareaClassName = `${controlClassName} min-h-24 resize-y`;

export const primaryButtonClassName =
  'inline-flex items-center justify-center gap-2 rounded-xl bg-brand px-4 py-2 text-sm font-bold text-[#06211f] transition-opacity hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50';

export const secondaryButtonClassName =
  'inline-flex items-center justify-center gap-2 rounded-xl border border-border px-4 py-2 text-sm font-semibold text-muted transition-colors hover:text-foreground disabled:cursor-not-allowed disabled:opacity-50';

export const dangerButtonClassName =
  'inline-flex items-center justify-center gap-2 rounded-xl bg-danger px-4 py-2 text-sm font-bold text-background transition-opacity hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50';

export const sectionActionClassName =
  'inline-flex items-center gap-2 rounded-xl border border-brand/50 px-3 py-2 text-xs font-semibold text-brand transition-colors hover:bg-brand-soft disabled:cursor-not-allowed disabled:opacity-50';

export const deleteActionClassName =
  'inline-flex items-center gap-2 rounded-xl border border-danger/40 px-3 py-2 text-xs font-semibold text-danger transition-colors hover:bg-danger/10 disabled:cursor-not-allowed disabled:opacity-50';
