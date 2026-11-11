'use client';

import type {ReactNode} from 'react';
import {FiAlertTriangle, FiLoader} from 'react-icons/fi';

/** Skeleton shown while a doctor-scoped query is in flight. */
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
  children?: ReactNode;
};

/** Full-page message panel used for token, forbidden, not-found and failure states. */
export function StatePanel({
  title,
  description,
  tone = 'neutral',
  children
}: StatePanelProps) {
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

      {children ? <div className="mt-4 flex flex-wrap gap-2">{children}</div> : null}
    </section>
  );
}
