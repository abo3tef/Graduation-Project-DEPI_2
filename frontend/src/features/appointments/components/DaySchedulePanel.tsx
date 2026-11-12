'use client';

import {useTranslations} from 'next-intl';
import {useAppointmentFormatters} from '../hooks/useAppointmentFormatters';
import {useErrorMessage} from '../hooks/useAppointmentMessages';
import {useDaySchedule} from '../hooks/useAppointments';
import {holdsSlot} from '../types/appointment.types';
import {intervalsOverlap, isValidIsoDate, isValidTime, timeToMinutes} from '../utils/date-time';
import {AppointmentStatusBadge} from './AppointmentStatusBadge';
import {Alert} from './primitives/Alert';
import {SkeletonRows} from './primitives/Skeleton';

type DaySchedulePanelProps = {
  doctorId: string;
  date: string;
  /** The interval being booked; used only to flag likely conflicts. */
  start: string;
  end: string;
};

/**
 * The chosen doctor's appointments on the chosen day, so the secretary can pick a
 * free time. Conflicts with the typed interval are highlighted as a HINT using the
 * same rule as the server; the server still has the final say (409).
 */
export function DaySchedulePanel({doctorId, date, start, end}: DaySchedulePanelProps) {
  const t = useTranslations('Appointments.daySchedule');
  const formatters = useAppointmentFormatters();
  const errorMessage = useErrorMessage();
  const schedule = useDaySchedule(doctorId, date);

  const intervalKnown =
    isValidTime(start) && isValidTime(end) && timeToMinutes(end) > timeToMinutes(start);

  let body;

  if (doctorId === '' || !isValidIsoDate(date)) {
    body = <p className="m-0 text-sm text-muted">{t('pick')}</p>;
  } else if (schedule.isPending) {
    body = <SkeletonRows rows={3} label={t('loading')} />;
  } else if (schedule.isError) {
    body = <Alert tone="error">{errorMessage(schedule.error)}</Alert>;
  } else {
    const items = schedule.data.items;
    const conflicts = intervalKnown
      ? items.filter(
          (item) =>
            holdsSlot(item.status) &&
            item.endTime !== null &&
            intervalsOverlap(start, end, item.startTime, item.endTime)
        )
      : [];

    body = (
      <div className="grid gap-3">
        {conflicts.length > 0 ? <Alert tone="warning">{t('overlapWarning')}</Alert> : null}

        {items.length === 0 ? (
          <p className="m-0 text-sm text-muted">{t('empty')}</p>
        ) : (
          <ul className="m-0 grid list-none gap-2 p-0">
            {items.map((item) => {
              const conflicting = conflicts.includes(item);
              const frees = !holdsSlot(item.status);

              return (
                <li
                  key={item.id}
                  className={`flex flex-wrap items-center justify-between gap-2 rounded-xl border px-3 py-2 text-sm ${
                    conflicting ? 'border-amber-400/60 bg-amber-400/10' : 'border-border bg-surface-2'
                  } ${frees ? 'opacity-70' : ''}`}
                >
                  <div className="grid gap-0.5">
                    <bdi className="font-bold">
                      {formatters.time(item.startTime)}
                      {item.endTime ? ` – ${formatters.time(item.endTime)}` : ''}
                    </bdi>
                    <span className="text-xs text-muted" dir="auto">
                      {item.patientName}
                    </span>
                    {frees ? <span className="text-[11px] text-muted">{t('slotFree')}</span> : null}
                  </div>
                  <AppointmentStatusBadge status={item.status} />
                </li>
              );
            })}
          </ul>
        )}
      </div>
    );
  }

  return (
    <aside aria-labelledby="day-schedule-title" className="grid content-start gap-3 rounded-2xl border border-border bg-surface p-4">
      <h2 id="day-schedule-title" className="m-0 text-base font-black">
        {t('title')}
      </h2>
      {body}
    </aside>
  );
}
