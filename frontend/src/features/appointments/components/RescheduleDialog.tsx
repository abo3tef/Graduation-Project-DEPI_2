'use client';

import {useState, type FormEvent} from 'react';
import {useTranslations} from 'next-intl';
import {
  focusFirstInvalid,
  useErrorMessage,
  useValidationMessage
} from '../hooks/useAppointmentMessages';
import {useRescheduleAppointment} from '../hooks/useAppointments';
import {mapFieldErrors} from '../services/appointment-errors';
import type {Appointment} from '../types/appointment.types';
import {getClinicNow, toHoursMinutes} from '../utils/date-time';
import {
  hasErrors,
  validateReschedule,
  type FormErrors,
  type ScheduleField
} from '../validation/appointment.validation';
import {isApiError} from '@/lib/api';
import {AppointmentSummary} from './AppointmentSummary';
import {Alert} from './primitives/Alert';
import {Button} from './primitives/Button';
import {Dialog} from './primitives/Dialog';
import {controlClass, Field, fieldAria} from './primitives/Field';

const FIELDS: readonly ScheduleField[] = ['appointmentDate', 'startTime', 'endTime'];

type RescheduleDialogProps = {
  /** The appointment to move; the dialog is open while this is set. */
  appointment: Appointment | null;
  onClose: () => void;
};

export function RescheduleDialog({appointment, onClose}: RescheduleDialogProps) {
  const t = useTranslations('Appointments.reschedule');

  return (
    <Dialog open={appointment !== null} onClose={onClose} title={t('title')} description={t('description')}>
      {appointment ? <RescheduleForm appointment={appointment} onClose={onClose} /> : null}
    </Dialog>
  );
}

function RescheduleForm({appointment, onClose}: {appointment: Appointment; onClose: () => void}) {
  const t = useTranslations('Appointments');
  const message = useValidationMessage();
  const errorMessage = useErrorMessage();
  const mutation = useRescheduleAppointment();
  const [date, setDate] = useState(appointment.appointmentDate);
  const [start, setStart] = useState(toHoursMinutes(appointment.startTime));
  const [end, setEnd] = useState(appointment.endTime ? toHoursMinutes(appointment.endTime) : '');
  const [errors, setErrors] = useState<FormErrors<ScheduleField>>({});
  const [formError, setFormError] = useState<string | null>(null);

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(null);

    const input = {appointmentDate: date, startTime: start, endTime: end};
    const found = validateReschedule(input, getClinicNow());
    setErrors(found);

    if (hasErrors(found)) {
      focusFirstInvalid(FIELDS.filter((field) => found[field]).map((field) => `reschedule-${field}`));
      return;
    }

    mutation.mutate(
      {id: appointment.id, input},
      {
        onSuccess: onClose,
        onError: (error) => {
          const fieldErrors = isApiError(error) ? mapFieldErrors(error, FIELDS) : {};
          setErrors(fieldErrors);

          if (hasErrors(fieldErrors)) {
            focusFirstInvalid(FIELDS.filter((field) => fieldErrors[field]).map((field) => `reschedule-${field}`));
          } else {
            setFormError(errorMessage(error));
          }
        }
      }
    );
  }

  return (
    <form onSubmit={handleSubmit} noValidate className="grid gap-4">
      <AppointmentSummary appointment={appointment} />

      {formError ? <Alert tone="error">{formError}</Alert> : null}

      <Field id="reschedule-appointmentDate" label={t('fields.date')} required error={message(errors.appointmentDate)}>
        <input
          type="date"
          value={date}
          min={getClinicNow().date}
          onChange={(event) => setDate(event.target.value)}
          className={controlClass}
          {...fieldAria('reschedule-appointmentDate', message(errors.appointmentDate))}
        />
      </Field>

      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="reschedule-startTime" label={t('fields.startTime')} required error={message(errors.startTime)}>
          <input
            type="time"
            value={start}
            onChange={(event) => setStart(event.target.value)}
            className={controlClass}
            {...fieldAria('reschedule-startTime', message(errors.startTime))}
          />
        </Field>
        <Field id="reschedule-endTime" label={t('fields.endTime')} required error={message(errors.endTime)}>
          <input
            type="time"
            value={end}
            onChange={(event) => setEnd(event.target.value)}
            className={controlClass}
            {...fieldAria('reschedule-endTime', message(errors.endTime))}
          />
        </Field>
      </div>

      <div className="flex flex-wrap justify-end gap-2">
        <Button onClick={onClose} disabled={mutation.isPending}>
          {t('actions.close')}
        </Button>
        <Button type="submit" variant="primary" loading={mutation.isPending}>
          {t('reschedule.submit')}
        </Button>
      </div>
    </form>
  );
}
