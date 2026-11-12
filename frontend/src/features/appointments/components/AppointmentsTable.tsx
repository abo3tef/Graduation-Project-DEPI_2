'use client';

import {useTranslations} from 'next-intl';
import {useAppointmentFormatters} from '../hooks/useAppointmentFormatters';
import type {Appointment} from '../types/appointment.types';
import {getAllowedActions, type ActionRole, type AppointmentAction} from '../utils/status-actions';
import {AppointmentStatusBadge} from './AppointmentStatusBadge';
import {Button} from './primitives/Button';

type AppointmentsTableProps = {
  items: readonly Appointment[];
  role: ActionRole;
  onAction: (action: AppointmentAction, appointment: Appointment) => void;
  /** Dims the list while a refresh is in flight. */
  busy?: boolean;
};

const ACTION_VARIANT: Record<AppointmentAction, 'primary' | 'secondary' | 'danger'> = {
  confirm: 'primary',
  complete: 'primary',
  reschedule: 'secondary',
  noShow: 'secondary',
  cancel: 'danger'
};

/**
 * Table from `md` up, stacked cards below. Both render the same data and the same
 * row actions; the hidden one is removed from the accessibility tree by CSS.
 */
export function AppointmentsTable({items, role, onAction, busy = false}: AppointmentsTableProps) {
  const t = useTranslations('Appointments');
  const formatters = useAppointmentFormatters();

  function timeRange(appointment: Appointment) {
    return (
      <bdi>
        {formatters.time(appointment.startTime)}
        {appointment.endTime ? ` – ${formatters.time(appointment.endTime)}` : ''}
      </bdi>
    );
  }

  function actions(appointment: Appointment) {
    const allowed = getAllowedActions(appointment.status, role);

    if (allowed.length === 0) {
      return <span className="text-xs text-muted">{t('table.noActions')}</span>;
    }

    return (
      <div className="flex flex-wrap gap-2">
        {allowed.map((action) => (
          <Button
            key={action}
            size="sm"
            variant={ACTION_VARIANT[action]}
            aria-label={t('actions.withPatient', {
              action: t(`actions.${action}`),
              patient: appointment.patientName
            })}
            onClick={() => onAction(action, appointment)}
          >
            {t(`actions.${action}`)}
          </Button>
        ))}
      </div>
    );
  }

  return (
    <div aria-busy={busy || undefined} className={busy ? 'opacity-70 transition-opacity' : ''}>
      <table
        data-testid="appointments-table"
        className="hidden w-full border-separate border-spacing-0 overflow-hidden rounded-2xl border border-border bg-surface text-sm md:table"
      >
        <caption className="sr-only">{t('table.caption')}</caption>
        <thead className="bg-surface-2 text-xs text-muted">
          <tr>
            {(['date', 'time', 'patient', 'doctor', 'status', 'actions'] as const).map((column) => (
              <th key={column} scope="col" className="px-4 py-3 text-start font-bold">
                {t(`table.${column}`)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {items.map((appointment) => (
            <tr key={appointment.id} className="align-top">
              <td className="border-t border-border px-4 py-3">
                {formatters.date(appointment.appointmentDate)}
              </td>
              <td className="border-t border-border px-4 py-3">{timeRange(appointment)}</td>
              <td className="border-t border-border px-4 py-3">
                <p className="m-0 font-bold" dir="auto">
                  {appointment.patientName}
                </p>
                {appointment.patientPhone ? (
                  <p className="m-0 mt-0.5 text-xs text-muted" dir="ltr">
                    {appointment.patientPhone}
                  </p>
                ) : null}
                {appointment.reason ? (
                  <p className="m-0 mt-1 max-w-64 truncate text-xs text-muted" dir="auto" title={appointment.reason}>
                    {appointment.reason}
                  </p>
                ) : null}
              </td>
              <td className="border-t border-border px-4 py-3" dir="auto">
                {appointment.doctorName}
              </td>
              <td className="border-t border-border px-4 py-3">
                <AppointmentStatusBadge status={appointment.status} />
              </td>
              <td className="border-t border-border px-4 py-3">{actions(appointment)}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <ul data-testid="appointments-cards" className="m-0 grid list-none gap-3 p-0 md:hidden">
        {items.map((appointment) => (
          <li
            key={appointment.id}
            className="grid gap-3 rounded-2xl border border-border bg-surface p-4"
          >
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div>
                <p className="m-0 font-black" dir="auto">
                  {appointment.patientName}
                </p>
                {appointment.patientPhone ? (
                  <p className="m-0 mt-0.5 text-xs text-muted" dir="ltr">
                    {appointment.patientPhone}
                  </p>
                ) : null}
              </div>
              <AppointmentStatusBadge status={appointment.status} />
            </div>
            <p className="m-0 text-sm text-muted">
              {formatters.date(appointment.appointmentDate)} · {timeRange(appointment)}
            </p>
            <p className="m-0 text-sm" dir="auto">
              {appointment.doctorName}
            </p>
            {appointment.reason ? (
              <p className="m-0 text-xs text-muted" dir="auto">
                {appointment.reason}
              </p>
            ) : null}
            {actions(appointment)}
          </li>
        ))}
      </ul>
    </div>
  );
}
