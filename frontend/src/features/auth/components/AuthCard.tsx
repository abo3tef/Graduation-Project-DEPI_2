'use client';

import type {ReactNode} from 'react';
import {cardClassName, primaryButtonClassName} from './auth-styles';

type AuthCardProps = {
  title: string;
  subtitle?: string;
  children: ReactNode;
  /** Links under the form (for example "create an account" or "back to sign in"). */
  footer?: ReactNode;
};

/** The shared shell of every auth screen: one heading, one short explanation, one action area. */
export function AuthCard({title, subtitle, children, footer}: AuthCardProps) {
  return (
    <section className={`${cardClassName} relative overflow-hidden`}>
      <div className="pointer-events-none absolute -end-24 -top-24 size-48 rounded-full bg-brand/10 blur-3xl" aria-hidden />
      <div className="relative">
        <div className="mb-5 flex items-center gap-3">
          <span className="grid size-11 place-items-center rounded-2xl bg-brand text-[#06211f] shadow-lg shadow-brand/20">
            <span className="text-lg font-black">M</span>
          </span>
          <div>
            <p className="text-xs font-black uppercase tracking-[0.18em] text-brand">Medilog</p>
            <p className="text-[11px] text-muted">Smart medical records</p>
          </div>
        </div>
        <h1 className="text-2xl font-black tracking-tight text-foreground">{title}</h1>
        {subtitle ? <p className="mt-1 text-sm leading-6 text-muted">{subtitle}</p> : null}
      </div>
      <div className="relative mt-7">{children}</div>
      {footer ? <div className="mt-6 text-center text-xs text-muted">{footer}</div> : null}
    </section>
  );
}

type FormAlertProps = {
  children: ReactNode;
  tone?: 'error' | 'success' | 'info';
};

const TONE_CLASSES = {
  error: 'border-danger/30 bg-danger/10 text-danger',
  success: 'border-brand/40 bg-brand-soft text-foreground',
  info: 'border-border bg-surface-2 text-muted'
} as const;

/** A translated, announced message. It never renders server text. */
export function FormAlert({children, tone = 'error'}: FormAlertProps) {
  return (
    <p
      role={tone === 'error' ? 'alert' : 'status'}
      className={`mb-4 rounded-xl border px-3 py-2 text-xs font-semibold ${TONE_CLASSES[tone]}`}
    >
      {children}
    </p>
  );
}

type SubmitButtonProps = {
  pending: boolean;
  label: string;
  pendingLabel: string;
};

/** The form's primary action; disabled and re-labelled while the request is in flight. */
export function SubmitButton({pending, label, pendingLabel}: SubmitButtonProps) {
  return (
    <button type="submit" disabled={pending} className={primaryButtonClassName}>
      {pending ? pendingLabel : label}
    </button>
  );
}
