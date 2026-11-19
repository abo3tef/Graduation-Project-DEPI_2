'use client';

import {useLocale, useTranslations} from 'next-intl';
import {FiCalendar, FiClock, FiUser} from 'react-icons/fi';
import {toUtcDate, type Appointment} from '@/features/appointments';
import {AppointmentStatusBadge} from './AppointmentStatusBadge';

type UpcomingAppointmentsPanelProps = {
  appointments: readonly Appointment[];
  isLoading?: boolean;
};

function formatTime(value: string): string {
  return value.slice(0, 5);
}

export function UpcomingAppointmentsPanel({
  appointments,
  isLoading = false
}: UpcomingAppointmentsPanelProps) {
  const t = useTranslations('dashboard');
  const locale = useLocale();

  if (isLoading) {
    return (
      <div className="rounded-2xl border border-border bg-surface p-6 shadow-xs">
        <div className="h-6 w-48 animate-pulse rounded bg-surface-2" />
        <div className="mt-5 grid gap-3 sm:grid-cols-2">
          {Array.from({length: 4}).map((_, index) => (
            <div key={index} className="h-20 animate-pulse rounded-xl bg-surface-2/70" />
          ))}
        </div>
      </div>
    );
  }

  return (
    <section className="rounded-2xl border border-border bg-surface shadow-xs">
      <div className="flex items-center justify-between gap-3 border-b border-border/80 px-6 py-4">
        <div>
          <h2 className="text-base font-bold">{t('upcoming.title')}</h2>
          <p className="text-xs text-muted">{t('upcoming.subtitle')}</p>
        </div>
        <span className="inline-flex items-center gap-1.5 rounded-full bg-brand-soft px-3 py-1 text-xs font-bold text-brand">
          <FiCalendar className="size-3.5" aria-hidden />
          {appointments.length}
        </span>
      </div>

      {appointments.length === 0 ? (
        <div className="px-6 py-10 text-center">
          <FiCalendar className="mx-auto size-8 text-muted" aria-hidden />
          <p className="mt-3 text-sm font-bold">{t('upcoming.empty')}</p>
          <p className="mt-1 text-xs text-muted">{t('upcoming.emptyHint')}</p>
        </div>
      ) : (
        <div className="grid gap-3 p-4 sm:grid-cols-2">
          {appointments.map((appointment) => (
            <article key={appointment.id} className="rounded-xl border border-border/80 bg-background/40 p-4 transition hover:border-brand/50">
              <div className="flex items-start justify-between gap-3">
                <div className="flex min-w-0 items-center gap-2.5">
                  <span className="grid size-9 shrink-0 place-items-center rounded-full bg-brand/10 text-brand">
                    <FiUser className="size-4" aria-hidden />
                  </span>
                  <div className="min-w-0">
                    <p className="truncate text-sm font-bold">{appointment.patientName}</p>
                    <p className="text-xs text-muted">
                      {new Intl.DateTimeFormat(locale, {day: 'numeric', month: 'short'}).format(
                        toUtcDate(appointment.appointmentDate)
                      )}
                    </p>
                  </div>
                </div>
                <AppointmentStatusBadge status={appointment.status} />
              </div>
              <div className="mt-3 flex items-center justify-between gap-3 text-xs text-muted">
                <span className="inline-flex items-center gap-1.5" dir="ltr">
                  <FiClock className="size-3.5" aria-hidden />
                  {formatTime(appointment.startTime)}
                  {appointment.endTime ? ` - ${formatTime(appointment.endTime)}` : ''}
                </span>
                <span className="truncate">{appointment.reason || t('table.noReason')}</span>
              </div>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}
