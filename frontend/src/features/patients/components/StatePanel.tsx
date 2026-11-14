'use client';

import type {ReactNode} from 'react';
import {FiAlertTriangle, FiLoader} from 'react-icons/fi';
import {Button} from './primitives/Button';

/** Panel shown while a doctor-scoped query is in flight. */
export function LoadingPanel({label}: {label: string}) {
  return (
    <div
      role="status"
      aria-live="polite"
      aria-busy="true"
      className="flex flex-col gap-3 rounded-2xl border border-border bg-surface p-6"
    >
      <p className="flex items-center gap-2 text-sm font-semibold text-muted">
        <FiLoader aria-hidden className="size-4 animate-spin text-brand" />
        {label}
      </p>
      <div className="h-3 w-2/3 animate-pulse rounded-full bg-surface-2" />
      <div className="h-3 w-1/2 animate-pulse rounded-full bg-surface-2" />
      <div className="h-3 w-3/4 animate-pulse rounded-full bg-surface-2" />
    </div>
  );
}

type StatePanelProps = {
  title: string;
  description?: string;
  tone?: 'neutral' | 'danger';
  action?: ReactNode;
};

/** Full-page message panel used for invalid-id, forbidden, not-found and error states. */
export function StatePanel({title, description, tone = 'neutral', action}: StatePanelProps) {
  const isDanger = tone === 'danger';

  return (
    <section
      role={isDanger ? 'alert' : 'status'}
      className={
        isDanger
          ? 'rounded-2xl border border-danger/40 bg-danger/10 p-6'
          : 'rounded-2xl border border-border bg-surface p-6'
      }
    >
      <h2
        className={
          isDanger
            ? 'flex items-center gap-2 text-lg font-bold text-danger'
            : 'flex items-center gap-2 text-lg font-bold text-foreground'
        }
      >
        <FiAlertTriangle
          aria-hidden
          className={isDanger ? 'size-5 text-danger' : 'size-5 text-brand'}
        />
        {title}
      </h2>

      {description ? (
        <p className="mt-2 max-w-2xl text-sm leading-relaxed text-muted">
          {description}
        </p>
      ) : null}

      {action ? <div className="mt-4 flex flex-wrap gap-2">{action}</div> : null}
    </section>
  );
}

type ErrorPanelProps = {
  title: string;
  description: string;
  retryLabel: string;
  onRetry: () => void;
};

/** Error state with a retry action; retry is offered for every failure (harmless when pointless). */
export function ErrorPanel({title, description, retryLabel, onRetry}: ErrorPanelProps) {
  return (
    <StatePanel
      tone="danger"
      title={title}
      description={description}
      action={<Button onClick={onRetry}>{retryLabel}</Button>}
    />
  );
}
