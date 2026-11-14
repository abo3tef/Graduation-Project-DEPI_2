'use client';

import {useTranslations} from 'next-intl';
import {FiCalendar, FiClock, FiUser} from 'react-icons/fi';
import type {TodayAppointmentSummary} from '../types/dashboard.types';
import {AppointmentStatusBadge} from './AppointmentStatusBadge';

type TodayAppointmentsTableProps = {
  appointments?: TodayAppointmentSummary[];
  isLoading?: boolean;
};

function formatTime(startTime: string, endTime: string | null): string {
  // startTime is formatted as "HH:mm:ss" or "HH:mm"
  const start = startTime.slice(0, 5);
  if (!endTime) {
    return start;
  }
  const end = endTime.slice(0, 5);
  return `${start} - ${end}`;
}

export function TodayAppointmentsTable({
  appointments = [],
  isLoading = false
}: TodayAppointmentsTableProps) {
  const t = useTranslations('dashboard');

  if (isLoading) {
    return (
      <div className="rounded-2xl border border-border bg-surface p-6 shadow-xs">
        <div className="mb-4 h-6 w-40 animate-pulse rounded-md bg-surface-2" />
        <div className="space-y-3">
          {Array.from({length: 4}).map((_, i) => (
            <div
              key={i}
              className="flex animate-pulse items-center justify-between rounded-xl bg-surface-2/60 p-4"
            >
              <div className="flex items-center gap-3">
                <div className="size-9 rounded-full bg-surface-2" />
                <div className="space-y-1.5">
                  <div className="h-4 w-28 rounded bg-surface-2" />
                  <div className="h-3 w-20 rounded bg-surface-2" />
                </div>
              </div>
              <div className="h-6 w-16 rounded-full bg-surface-2" />
            </div>
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="rounded-2xl border border-border bg-surface shadow-xs overflow-hidden">
      <div className="border-b border-border/80 px-6 py-4 flex items-center justify-between">
        <div>
          <h2 className="text-base font-bold text-foreground">
            {t('table.title')}
          </h2>
          <p className="text-xs text-muted">
            {t('table.subtitle')}
          </p>
        </div>
        <span className="inline-flex items-center gap-1.5 rounded-full bg-surface-2 px-3 py-1 text-xs font-semibold text-muted">
          <FiCalendar className="size-3.5 text-brand" />
          <span>{appointments.length}</span>
        </span>
      </div>

      {appointments.length === 0 ? (
        <div className="flex flex-col items-center justify-center px-6 py-12 text-center">
          <div className="mb-3 grid size-12 place-items-center rounded-2xl bg-surface-2 text-muted">
            <FiCalendar className="size-6 stroke-[1.5]" />
          </div>
          <p className="text-sm font-bold text-foreground">
            {t('table.empty')}
          </p>
          <p className="mt-1 text-xs text-muted max-w-sm">
            {t('table.emptyHint')}
          </p>
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-start text-xs text-foreground">
            <thead className="border-b border-border/60 bg-surface-2/50 text-muted uppercase tracking-wider">
              <tr>
                <th scope="col" className="px-6 py-3.5 font-bold text-start">
                  {t('table.patientName')}
                </th>
                <th scope="col" className="px-6 py-3.5 font-bold text-start">
                  {t('table.time')}
                </th>
                <th scope="col" className="px-6 py-3.5 font-bold text-start">
                  {t('table.reason')}
                </th>
                <th scope="col" className="px-6 py-3.5 font-bold text-start">
                  {t('table.status')}
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border/40">
              {appointments.map((apt) => (
                <tr
                  key={apt.appointmentId}
                  className="transition hover:bg-surface-2/30"
                >
                  <td className="whitespace-nowrap px-6 py-4">
                    <div className="flex items-center gap-2.5">
                      <div className="grid size-8 shrink-0 place-items-center rounded-full bg-brand/10 text-brand">
                        <FiUser className="size-4" />
                      </div>
                      <span className="font-bold text-foreground">
                        {apt.patientName}
                      </span>
                    </div>
                  </td>
                  <td className="whitespace-nowrap px-6 py-4 text-muted">
                    <div className="inline-flex items-center gap-1.5 font-medium dir-ltr">
                      <FiClock className="size-3.5 text-muted/80" />
                      <span>{formatTime(apt.startTime, apt.endTime)}</span>
                    </div>
                  </td>
                  <td className="px-6 py-4 text-muted">
                    <span className="line-clamp-1 max-w-xs">
                      {apt.reason || t('table.noReason')}
                    </span>
                  </td>
                  <td className="whitespace-nowrap px-6 py-4">
                    <AppointmentStatusBadge status={apt.status} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

