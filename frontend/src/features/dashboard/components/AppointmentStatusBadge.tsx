'use client';

import {useTranslations} from 'next-intl';

export type KnownAppointmentStatus =
  | 'Scheduled'
  | 'Confirmed'
  | 'Completed'
  | 'Cancelled'
  | 'NoShow';

const KNOWN_STATUSES: readonly KnownAppointmentStatus[] = [
  'Scheduled',
  'Confirmed',
  'Completed',
  'Cancelled',
  'NoShow'
];

function isKnownStatus(status: string): status is KnownAppointmentStatus {
  return (KNOWN_STATUSES as readonly string[]).includes(status);
}

type AppointmentStatusBadgeProps = {
  status: KnownAppointmentStatus | string;
};

const STATUS_STYLES: Record<KnownAppointmentStatus, string> = {
  Confirmed:
    'bg-emerald-500/10 text-emerald-500 dark:text-emerald-400 border border-emerald-500/20',
  Scheduled:
    'bg-sky-500/10 text-sky-600 dark:text-sky-400 border border-sky-500/20',
  Completed:
    'bg-brand-soft text-brand border border-brand/20',
  Cancelled:
    'bg-rose-500/10 text-rose-600 dark:text-rose-400 border border-rose-500/20',
  NoShow:
    'bg-amber-500/10 text-amber-600 dark:text-amber-400 border border-amber-500/20'
};

const DEFAULT_STYLE =
  'bg-surface-2 text-muted border border-border';

export function AppointmentStatusBadge({status}: AppointmentStatusBadgeProps) {
  const t = useTranslations('dashboard.statuses');

  const isKnown = isKnownStatus(status);
  const style = isKnown ? STATUS_STYLES[status] : DEFAULT_STYLE;
  const label = isKnown ? t(status) : status;

  return (
    <span
      className={`inline-flex items-center justify-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${style}`}
    >
      {label}
    </span>
  );
}
