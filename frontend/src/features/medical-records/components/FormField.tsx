'use client';

import type {ReactNode} from 'react';

export type FormControlProps = {
  id: string;
  'aria-invalid': boolean;
  'aria-describedby': string | undefined;
  'aria-required': boolean | undefined;
};

type FormFieldProps = {
  id: string;
  label: string;
  hint?: string;
  error?: string | null;
  required?: boolean;
  className?: string;
  /** Render prop so the control always receives the accessibility wiring. */
  children: (control: FormControlProps) => ReactNode;
};

/**
 * Label + hint + error wrapper. The control is rendered through a render prop so `id`,
 * `aria-invalid`, `aria-required` and `aria-describedby` cannot drift apart.
 */
export function FormField({
  id,
  label,
  hint,
  error,
  required = false,
  className,
  children
}: FormFieldProps) {
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = error ? `${id}-error` : undefined;
  const describedBy =
    [hintId, errorId].filter((value): value is string => Boolean(value)).join(' ') ||
    undefined;

  return (
    <div className={className ?? 'flex flex-col gap-1.5'}>
      <label htmlFor={id} className="text-sm font-medium text-foreground">
        {label}
        {required ? (
          <span aria-hidden className="ms-1 text-danger">
            *
          </span>
        ) : null}
      </label>

      {children({
        id,
        'aria-invalid': Boolean(error),
        'aria-describedby': describedBy,
        'aria-required': required || undefined
      })}

      {hint ? (
        <p id={hintId} className="text-xs text-muted">
          {hint}
        </p>
      ) : null}

      {error ? (
        <p id={errorId} role="alert" className="text-xs font-medium text-danger">
          {error}
        </p>
      ) : null}
    </div>
  );
}
