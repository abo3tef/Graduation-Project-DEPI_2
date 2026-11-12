'use client';

import {useState} from 'react';
import {useTranslations} from 'next-intl';
import {useErrorMessage} from '../hooks/useAppointmentMessages';
import {useUpdateAppointmentStatus, type ChangeableStatus} from '../hooks/useAppointments';
import type {Appointment} from '../types/appointment.types';
import {AppointmentSummary} from './AppointmentSummary';
import {Alert} from './primitives/Alert';
import {Button} from './primitives/Button';
import {Dialog} from './primitives/Dialog';

export type StatusDialogState = {
  appointment: Appointment;
  status: ChangeableStatus;
} | null;

type StatusDialogProps = {
  state: StatusDialogState;
  onClose: () => void;
};

const MESSAGE_KEY: Record<ChangeableStatus, 'confirm' | 'noShow' | 'complete'> = {
  Confirmed: 'confirm',
  NoShow: 'noShow',
  Completed: 'complete'
};

/** Confirmation step before Confirm / No-show / Complete. The server decides if it is allowed. */
export function StatusDialog({state, onClose}: StatusDialogProps) {
  const t = useTranslations('Appointments.statusDialog');
  const key = state ? MESSAGE_KEY[state.status] : 'confirm';

  return (
    <Dialog
      open={state !== null}
      onClose={onClose}
      title={t(`${key}.title`)}
      description={t(`${key}.description`)}
    >
      {state ? <StatusForm state={state} messageKey={key} onClose={onClose} /> : null}
    </Dialog>
  );
}

function StatusForm({
  state,
  messageKey,
  onClose
}: {
  state: NonNullable<StatusDialogState>;
  messageKey: 'confirm' | 'noShow' | 'complete';
  onClose: () => void;
}) {
  const t = useTranslations('Appointments');
  const errorMessage = useErrorMessage();
  const mutation = useUpdateAppointmentStatus();
  const [formError, setFormError] = useState<string | null>(null);

  function handleConfirm() {
    setFormError(null);
    mutation.mutate(
      {id: state.appointment.id, status: state.status},
      {onSuccess: onClose, onError: (error) => setFormError(errorMessage(error))}
    );
  }

  return (
    <div className="grid gap-4">
      <AppointmentSummary appointment={state.appointment} />

      {formError ? <Alert tone="error">{formError}</Alert> : null}

      <div className="flex flex-wrap justify-end gap-2">
        <Button onClick={onClose} disabled={mutation.isPending}>
          {t('actions.close')}
        </Button>
        <Button variant="primary" onClick={handleConfirm} loading={mutation.isPending}>
          {t(`statusDialog.${messageKey}.submit`)}
        </Button>
      </div>
    </div>
  );
}
