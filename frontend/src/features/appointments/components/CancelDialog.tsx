'use client';

import {useState, type FormEvent} from 'react';
import {useTranslations} from 'next-intl';
import {isApiError} from '@/lib/api';
import {
  focusFirstInvalid,
  useErrorMessage,
  useValidationMessage
} from '../hooks/useAppointmentMessages';
import {useCancelAppointment} from '../hooks/useAppointments';
import {mapFieldErrors} from '../services/appointment-errors';
import {CANCELLATION_REASON_MAX_LENGTH, type Appointment} from '../types/appointment.types';
import {validateCancellationReason, type ValidationKey} from '../validation/appointment.validation';
import {AppointmentSummary} from './AppointmentSummary';
import {Alert} from './primitives/Alert';
import {Button} from './primitives/Button';
import {Dialog} from './primitives/Dialog';
import {controlClass, Field, fieldAria} from './primitives/Field';

const REASON_ID = 'cancel-reason';

type CancelDialogProps = {
  /** The appointment to cancel; the dialog is open while this is set. */
  appointment: Appointment | null;
  onClose: () => void;
};

export function CancelDialog({appointment, onClose}: CancelDialogProps) {
  const t = useTranslations('Appointments.cancel');

  return (
    <Dialog open={appointment !== null} onClose={onClose} title={t('title')} description={t('description')}>
      {appointment ? <CancelForm appointment={appointment} onClose={onClose} /> : null}
    </Dialog>
  );
}

function CancelForm({appointment, onClose}: {appointment: Appointment; onClose: () => void}) {
  const t = useTranslations('Appointments');
  const message = useValidationMessage();
  const errorMessage = useErrorMessage();
  const mutation = useCancelAppointment();
  const [reason, setReason] = useState('');
  const [error, setError] = useState<ValidationKey | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(null);

    const found = validateCancellationReason(reason);
    setError(found);

    if (found) {
      focusFirstInvalid([REASON_ID]);
      return;
    }

    mutation.mutate(
      {id: appointment.id, reason},
      {
        onSuccess: onClose,
        onError: (failure) => {
          const mapped = isApiError(failure) ? mapFieldErrors(failure, ['cancellationReason'] as const) : {};

          if (mapped.cancellationReason) {
            setError(mapped.cancellationReason);
            focusFirstInvalid([REASON_ID]);
          } else {
            setFormError(errorMessage(failure));
          }
        }
      }
    );
  }

  const length = reason.trim().length;

  return (
    <form onSubmit={handleSubmit} noValidate className="grid gap-4">
      <AppointmentSummary appointment={appointment} />

      {formError ? <Alert tone="error">{formError}</Alert> : null}

      <Field
        id={REASON_ID}
        label={t('cancel.reasonLabel')}
        required
        hint={t('cancel.reasonHint')}
        error={message(error)}
        aside={t('cancel.counter', {count: length, max: CANCELLATION_REASON_MAX_LENGTH})}
      >
        <textarea
          value={reason}
          rows={4}
          onChange={(event) => setReason(event.target.value)}
          className={`${controlClass} py-2`}
          {...fieldAria(REASON_ID, message(error), t('cancel.reasonHint'))}
        />
      </Field>

      <div className="flex flex-wrap justify-end gap-2">
        <Button onClick={onClose} disabled={mutation.isPending}>
          {t('cancel.keep')}
        </Button>
        <Button type="submit" variant="danger" loading={mutation.isPending}>
          {t('cancel.submit')}
        </Button>
      </div>
    </form>
  );
}
