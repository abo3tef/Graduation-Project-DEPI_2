'use client';

import {useEffect, useId, useRef, type ButtonHTMLAttributes, type ReactNode} from 'react';

/**
 * Small presentational pieces owned by the patients feature, so it does not deep-import another
 * feature's internals (AGENTS.md section 6.2). Logical spacing only, so Arabic (RTL) and
 * English (LTR) share one layout.
 */

export const controlClass =
  'min-h-10 w-full rounded-lg border border-border bg-background px-3 text-start text-sm text-foreground transition placeholder:text-muted focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-brand disabled:cursor-not-allowed disabled:opacity-60 aria-[invalid=true]:border-red-400';

type Variant = 'primary' | 'secondary' | 'danger' | 'ghost';

const VARIANT_CLASSES: Record<Variant, string> = {
  primary: 'border-transparent bg-brand text-[#06211f] hover:opacity-90',
  secondary: 'border-border bg-transparent text-foreground hover:border-brand hover:text-brand',
  danger: 'border-transparent bg-red-500 text-white hover:bg-red-600 focus-visible:outline-red-400',
  ghost: 'border-transparent bg-transparent text-muted hover:text-foreground'
};

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: Variant;
  loading?: boolean;
  size?: 'sm' | 'md';
};

export function Button({
  variant = 'secondary',
  loading = false,
  size = 'md',
  disabled,
  className = '',
  type = 'button',
  children,
  ...rest
}: ButtonProps) {
  const sizeClasses = size === 'sm' ? 'min-h-8 px-3 text-xs' : 'min-h-10 px-5 text-sm';

  return (
    <button
      type={type}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      className={`inline-flex items-center justify-center gap-2 rounded-full border font-bold transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand disabled:cursor-not-allowed disabled:opacity-60 ${sizeClasses} ${VARIANT_CLASSES[variant]} ${className}`.trim()}
      {...rest}
    >
      {loading ? (
        <span
          aria-hidden="true"
          className="size-3.5 animate-spin rounded-full border-2 border-current border-t-transparent"
        />
      ) : null}
      {children}
    </button>
  );
}

export function errorId(id: string) {
  return `${id}-error`;
}

/** aria props for a control rendered inside a Field. */
export function fieldAria(id: string, error?: string | null) {
  return {
    id,
    'aria-invalid': error ? true : undefined,
    'aria-describedby': error ? errorId(id) : undefined
  } as const;
}

type FieldProps = {
  id: string;
  label: string;
  required?: boolean;
  hint?: string;
  error?: string | null;
  children: ReactNode;
};

export function Field({id, label, required, hint, error, children}: FieldProps) {
  return (
    <div className="grid gap-1.5">
      <label htmlFor={id} className="text-xs font-bold text-foreground">
        {label}
        {required ? (
          <span aria-hidden="true" className="ms-1 text-red-400">
            *
          </span>
        ) : null}
      </label>
      {children}
      {hint && !error ? <p className="m-0 text-[11px] leading-5 text-muted">{hint}</p> : null}
      {error ? (
        <p id={errorId(id)} role="alert" className="m-0 text-xs font-bold text-red-400">
          {error}
        </p>
      ) : null}
    </div>
  );
}

type AlertProps = {
  tone?: 'error' | 'info';
  title?: string;
  children?: ReactNode;
  action?: ReactNode;
};

export function Alert({tone = 'info', title, children, action}: AlertProps) {
  const toneClass =
    tone === 'error'
      ? 'border-red-400/40 bg-red-500/10 text-red-300 [:root[data-theme=light]_&]:text-red-800'
      : 'border-brand/30 bg-brand-soft text-brand [:root[data-theme=light]_&]:text-brand-strong';

  return (
    <div
      role={tone === 'error' ? 'alert' : 'status'}
      className={`flex flex-wrap items-start justify-between gap-3 rounded-xl border p-4 text-sm ${toneClass}`}
    >
      <div className="grid gap-1">
        {title ? <p className="m-0 font-black">{title}</p> : null}
        {children ? <div className="leading-6">{children}</div> : null}
      </div>
      {action}
    </div>
  );
}

type DialogProps = {
  open: boolean;
  onClose: () => void;
  title: string;
  description?: string;
  children: ReactNode;
};

/** Accessible modal on the native dialog element (focus trap, Escape and focus return are the browser's). */
export function Dialog({open, onClose, title, description, children}: DialogProps) {
  const ref = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  const descriptionId = useId();

  useEffect(() => {
    const dialog = ref.current;

    if (!dialog) {
      return;
    }

    if (open && !dialog.open) {
      dialog.showModal();
    } else if (!open && dialog.open) {
      dialog.close();
    }
  }, [open]);

  return (
    <dialog
      ref={ref}
      aria-labelledby={titleId}
      aria-describedby={description ? descriptionId : undefined}
      onClose={onClose}
      onClick={(event) => {
        if (event.target === event.currentTarget) {
          event.currentTarget.close();
        }
      }}
      className="m-auto w-[min(34rem,calc(100%-2rem))] rounded-2xl border border-border bg-surface p-0 text-foreground shadow-2xl backdrop:bg-black/60"
    >
      {open ? (
        <div className="grid gap-4 p-5 sm:p-6">
          <header className="grid gap-1">
            <h2 id={titleId} className="m-0 text-lg font-black">
              {title}
            </h2>
            {description ? (
              <p id={descriptionId} className="m-0 text-sm leading-6 text-muted">
                {description}
              </p>
            ) : null}
          </header>
          {children}
        </div>
      ) : null}
    </dialog>
  );
}
