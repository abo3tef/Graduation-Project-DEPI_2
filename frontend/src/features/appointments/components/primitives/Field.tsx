import type {ReactNode} from 'react';

/** Shared look of text inputs, selects and textareas (logical spacing, theme tokens). */
export const controlClass =
  'min-h-10 w-full rounded-lg border border-border bg-background px-3 text-start text-sm text-foreground transition placeholder:text-muted focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-brand disabled:cursor-not-allowed disabled:opacity-60 aria-[invalid=true]:border-red-400';

type FieldProps = {
  /** The id of the control; the label, hint and error are wired to it. */
  id: string;
  label: string;
  required?: boolean;
  hint?: string;
  /** Already translated error text, or null/undefined when valid. */
  error?: string | null;
  /** Right-aligned helper such as a character counter. */
  aside?: string;
  children: ReactNode;
};

export function hintId(id: string) {
  return `${id}-hint`;
}

export function errorId(id: string) {
  return `${id}-error`;
}

/** `aria-*` props for a control rendered inside a `Field`. */
export function fieldAria(id: string, error?: string | null, hint?: string) {
  const describedBy = [error ? errorId(id) : null, hint ? hintId(id) : null]
    .filter(Boolean)
    .join(' ');

  return {
    id,
    'aria-invalid': error ? true : undefined,
    'aria-describedby': describedBy || undefined
  } as const;
}

export function Field({id, label, required, hint, error, aside, children}: FieldProps) {
  return (
    <div className="grid gap-1.5">
      <div className="flex items-baseline justify-between gap-3">
        <label htmlFor={id} className="text-xs font-bold text-foreground">
          {label}
          {required ? (
            <span aria-hidden="true" className="ms-1 text-red-400">
              *
            </span>
          ) : null}
        </label>
        {aside ? (
          <span className="text-[11px] text-muted" dir="auto">
            {aside}
          </span>
        ) : null}
      </div>
      {children}
      {hint && !error ? (
        <p id={hintId(id)} className="m-0 text-[11px] leading-5 text-muted">
          {hint}
        </p>
      ) : null}
      {error ? (
        <p id={errorId(id)} role="alert" className="m-0 text-xs font-bold text-red-400">
          {error}
        </p>
      ) : null}
    </div>
  );
}
