import type {ButtonHTMLAttributes} from 'react';

type Variant = 'primary' | 'secondary' | 'danger' | 'ghost';

const VARIANT_CLASSES: Record<Variant, string> = {
  primary: 'border-transparent bg-brand text-[#06211f] hover:opacity-90',
  secondary: 'border-border bg-transparent text-foreground hover:border-brand hover:text-brand',
  danger:
    'border-transparent bg-red-500 text-white hover:bg-red-600 focus-visible:outline-red-400',
  ghost: 'border-transparent bg-transparent text-muted hover:text-foreground'
};

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: Variant;
  /** Shows a spinner and blocks repeated clicks. */
  loading?: boolean;
  size?: 'sm' | 'md';
};

/** Feature-local copy of the established button primitive (see the appointments feature). */
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
