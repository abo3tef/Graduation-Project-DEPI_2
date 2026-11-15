'use client';

import {useId, useState, type FormEvent} from 'react';
import {useLocale, useTranslations} from 'next-intl';
import {
  usePaymentErrorMessage,
  useUpdatePaymentStatus
} from '../hooks/usePayments';
import type {
  Payment,
  PaymentStatus,
  UpdatePaymentStatusInput
} from '../types/payment.types';
import {formatCurrency, formatDate} from '../utils/formatters';
import {nextStatuses} from '../utils/payment-rules';
import {DialogShell} from './DialogShell';
import {PaymentStatusBadge} from './PaymentStatusBadge';

const FIELD_CLASSES =
  'min-h-11 w-full rounded-xl border border-border bg-background px-3 text-start text-sm text-foreground transition-colors focus-visible:border-brand focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand';

const LABEL_CLASSES = 'mb-1.5 block text-xs font-semibold text-foreground';

const PRIMARY_BUTTON_CLASSES =
  'inline-flex h-11 items-center justify-center gap-2 rounded-xl border border-transparent bg-brand px-5 text-sm font-bold text-[#06211f] transition-opacity hover:opacity-90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand disabled:cursor-not-allowed disabled:opacity-60';

const SECONDARY_BUTTON_CLASSES =
  'inline-flex h-11 items-center justify-center gap-2 rounded-xl border border-border px-5 text-sm font-semibold text-foreground transition-colors hover:bg-surface-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand disabled:cursor-not-allowed disabled:opacity-40';

type SubmitStatus = (
  input: UpdatePaymentStatusInput,
  handlers: {
    onSuccess: () => void;
    onError: (error: unknown) => void;
  }
) => void;

type PaymentStatusDialogProps = {
  /** The selected payment, or null when the dialog is closed. */
  payment: Payment | null;
  onClose: () => void;
};

/**
 * Moves a payment through the transitions the API allows. The form is mounted
 * only while a payment is selected, so it always starts from the current status.
 */
export function PaymentStatusDialog({payment, onClose}: PaymentStatusDialogProps) {
  const t = useTranslations('Payments');
  const mutation = useUpdatePaymentStatus();

  return (
    <DialogShell
      open={payment !== null}
      onClose={onClose}
      title={t('statusDialog.title')}
      description={
        payment
          ? t('statusDialog.description', {patient: payment.patientName})
          : undefined
      }
      closeLabel={t('actions.close')}
      isBusy={mutation.isPending}
    >
      {payment ? (
        <PaymentStatusForm
          key={payment.id}
          payment={payment}
          isBusy={mutation.isPending}
          submit={mutation.mutate}
          onClose={onClose}
        />
      ) : null}
    </DialogShell>
  );
}

type PaymentStatusFormProps = {
  payment: Payment;
  isBusy: boolean;
  submit: SubmitStatus;
  onClose: () => void;
};

function PaymentStatusForm({
  payment,
  isBusy,
  submit,
  onClose
}: PaymentStatusFormProps) {
  const t = useTranslations('Payments');
  const locale = useLocale();
  const errorMessage = usePaymentErrorMessage();
  const statusId = useId();
  const allowed = nextStatuses(payment.status);

  const [status, setStatus] = useState<PaymentStatus | ''>(allowed[0] ?? '');
  const [formError, setFormError] = useState<string | null>(null);

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (status === '') {
      return;
    }

    setFormError(null);
    submit(
      {id: payment.id, status},
      {
        onSuccess: onClose,
        onError: (error) => setFormError(errorMessage(error))
      }
    );
  }

  return (
    <form onSubmit={handleSubmit} noValidate aria-label={t('statusDialog.title')} className="grid gap-4">
      {formError ? (
        <div
          role="alert"
          className="rounded-xl border border-red-400/40 bg-red-500/10 p-3 text-sm font-semibold text-red-300 [:root[data-theme=light]_&]:text-red-800"
        >
          {formError}
        </div>
      ) : null}

      <dl className="grid gap-3 rounded-xl border border-border bg-surface-2/40 p-3 text-sm sm:grid-cols-2">
        <div>
          <dt className="text-xs text-muted">{t('table.patient')}</dt>
          <dd dir="auto" className="font-semibold text-foreground">
            {payment.patientName}
          </dd>
        </div>
        <div>
          <dt className="text-xs text-muted">{t('table.amount')}</dt>
          <dd className="font-semibold tabular-nums text-foreground">
            {formatCurrency(payment.amount, locale)}
          </dd>
        </div>
        <div>
          <dt className="text-xs text-muted">{t('table.appointment')}</dt>
          <dd className="text-foreground">
            {formatDate(payment.appointmentDate, locale)}
          </dd>
        </div>
        <div>
          <dt className="text-xs text-muted">{t('statusDialog.currentStatus')}</dt>
          <dd>
            <PaymentStatusBadge status={payment.status} />
          </dd>
        </div>
      </dl>

      {allowed.length === 0 ? (
        <p
          role="note"
          className="rounded-xl border border-border bg-surface-2/40 p-3 text-sm text-muted"
        >
          {t('statusDialog.finalDescription')}
        </p>
      ) : (
        <div>
          <label htmlFor={statusId} className={LABEL_CLASSES}>
            {t('statusDialog.label')}
          </label>
          <select
            id={statusId}
            value={status}
            onChange={(event) => {
              const value = event.target.value;

              if (value === 'pending' || value === 'partial' || value === 'paid') {
                setStatus(value);
              }
            }}
            className={FIELD_CLASSES}
          >
            {allowed.map((item) => (
              <option key={item} value={item}>
                {t(`status.${item}`)}
              </option>
            ))}
          </select>
        </div>
      )}

      <div className="flex flex-wrap justify-end gap-2 pt-1">
        <button
          type="button"
          onClick={onClose}
          disabled={isBusy}
          className={SECONDARY_BUTTON_CLASSES}
        >
          {t('actions.cancel')}
        </button>
        {allowed.length > 0 ? (
          <button
            type="submit"
            disabled={isBusy || status === ''}
            aria-busy={isBusy || undefined}
            className={PRIMARY_BUTTON_CLASSES}
          >
            {isBusy ? t('actions.saving') : t('statusDialog.submit')}
          </button>
        ) : null}
      </div>
    </form>
  );
}
