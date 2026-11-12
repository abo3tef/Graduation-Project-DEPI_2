import {useTranslations} from 'next-intl';
import type {AppointmentStatus} from '../types/appointment.types';

/**
 * Colors come from the project's theme tokens or Tailwind palette. The app
 * switches themes with `data-theme` on <html> (not the `dark:` variant), so the
 * light-theme overrides use the `[:root[data-theme=light]_&]` variant, as in the
 * payments badge. `Record<AppointmentStatus, string>` makes TypeScript fail here
 * if a status is added to the contract without being styled.
 */
const STATUS_STYLES: Record<AppointmentStatus, string> = {
  Scheduled:
    'border-sky-400/40 bg-sky-400/15 text-sky-300 [:root[data-theme=light]_&]:border-sky-500/40 [:root[data-theme=light]_&]:bg-sky-100 [:root[data-theme=light]_&]:text-sky-800',
  Confirmed:
    'border-brand/30 bg-brand-soft text-brand [:root[data-theme=light]_&]:text-brand-strong',
  Completed:
    'border-emerald-400/40 bg-emerald-400/15 text-emerald-300 [:root[data-theme=light]_&]:border-emerald-500/40 [:root[data-theme=light]_&]:bg-emerald-100 [:root[data-theme=light]_&]:text-emerald-800',
  Cancelled:
    'border-red-400/40 bg-red-400/10 text-red-300 [:root[data-theme=light]_&]:border-red-500/40 [:root[data-theme=light]_&]:bg-red-100 [:root[data-theme=light]_&]:text-red-800',
  NoShow:
    'border-amber-400/40 bg-amber-400/15 text-amber-300 [:root[data-theme=light]_&]:border-amber-500/40 [:root[data-theme=light]_&]:bg-amber-100 [:root[data-theme=light]_&]:text-amber-800'
};

type AppointmentStatusBadgeProps = {
  status: AppointmentStatus;
  className?: string;
};

export function AppointmentStatusBadge({status, className = ''}: AppointmentStatusBadgeProps) {
  const t = useTranslations('Appointments.status');

  return (
    <span
      className={`inline-flex items-center gap-1.5 whitespace-nowrap rounded-full border px-2.5 py-1 text-xs font-bold ${STATUS_STYLES[status]} ${className}`.trim()}
    >
      {/* Decorative dot: the meaning is carried by the text, never by color alone. */}
      <span aria-hidden="true" className="size-1.5 rounded-full bg-current" />
      {t(status)}
    </span>
  );
}
