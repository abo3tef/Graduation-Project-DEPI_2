'use client';

import {useTranslations} from 'next-intl';
import {useAppointmentFormatters} from '../hooks/useAppointmentFormatters';
import type {Appointment} from '../types/appointment.types';

type AppointmentSummaryProps = {
  appointment: Appointment;
};

/** Who, with whom and when: shown inside the action dialogs so the user confirms the right row. */
export function AppointmentSummary({appointment}: AppointmentSummaryProps) {
  const t = useTranslations('Appointments.fields');
  const formatters = useAppointmentFormatters();

  return (
    <dl className="m-0 grid gap-1 rounded-xl border border-border bg-surface-2 p-3 text-sm">
      <div className="flex flex-wrap gap-x-2">
        <dt className="sr-only">{t('patient')}</dt>
        <dd className="m-0 font-black" dir="auto">
          {appointment.patientName}
        </dd>
      </div>
      <div className="flex flex-wrap gap-x-2 text-muted">
        <dt className="sr-only">{t('doctor')}</dt>
        <dd className="m-0" dir="auto">
          {appointment.doctorName}
        </dd>
      </div>
      <div className="flex flex-wrap gap-x-2 text-muted">
        <dt className="sr-only">{t('when')}</dt>
        <dd className="m-0">
          {formatters.date(appointment.appointmentDate)} ·{' '}
          <bdi>
            {formatters.time(appointment.startTime)}
            {appointment.endTime ? ` – ${formatters.time(appointment.endTime)}` : ''}
          </bdi>
        </dd>
      </div>
    </dl>
  );
}
