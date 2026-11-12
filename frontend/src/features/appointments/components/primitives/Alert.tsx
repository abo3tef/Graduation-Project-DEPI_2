import type {ReactNode} from 'react';

type Tone = 'error' | 'info' | 'warning';

const TONE_CLASSES: Record<Tone, string> = {
  error:
    'border-red-400/40 bg-red-500/10 text-red-300 [:root[data-theme=light]_&]:text-red-800',
  info: 'border-brand/30 bg-brand-soft text-brand [:root[data-theme=light]_&]:text-brand-strong',
  warning:
    'border-amber-400/40 bg-amber-400/10 text-amber-300 [:root[data-theme=light]_&]:text-amber-800'
};

type AlertProps = {
  tone?: Tone;
  title?: string;
  children?: ReactNode;
  action?: ReactNode;
  className?: string;
};

/** Errors are announced immediately (`alert`); other tones politely (`status`). */
export function Alert({tone = 'info', title, children, action, className = ''}: AlertProps) {
  return (
    <div
      role={tone === 'error' ? 'alert' : 'status'}
      className={`flex flex-wrap items-start justify-between gap-3 rounded-xl border p-4 text-sm ${TONE_CLASSES[tone]} ${className}`.trim()}
    >
      <div className="grid gap-1">
        {title ? <p className="m-0 font-black">{title}</p> : null}
        {children ? <div className="leading-6">{children}</div> : null}
      </div>
      {action}
    </div>
  );
}
