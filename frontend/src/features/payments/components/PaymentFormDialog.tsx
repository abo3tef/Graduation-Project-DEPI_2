'use client';

import {useId, useState, type FormEvent} from 'react';
import {useLocale, useTranslations} from 'next-intl';
import {FiCheck} from 'react-icons/fi';
import {isApiError} from '@/lib/api';
import {
  useCreatePayment,
  usePayableAppointments,
  usePaymentErrorMessage
} from '../hooks/usePayments';
import {mapPaymentFieldErrors} from '../services/payment-errors';
import {isPaymentPartiallyRecordedError} from '../services/payments.service';
import {
  PAYMENT_AMOUNT_MAX,
  PAYMENT_AMOUNT_MIN,
  PAYMENT_FORM_FIELDS,
  PAYMENT_METHODS,
  PAYMENT_NOTES_MAX_LENGTH,
  isPaymentMethod,
  type CreatePaymentFormErrors,
  type CreatePaymentInput,
  type PaymentAppointmentOption,
  type PaymentFormErrorKey,
  type PaymentFormField,
  type PaymentMethod
} from '../types/payment.types';
import {formatDate, formatTime, parseAmountInput} from '../utils/formatters';
import {hasFormErrors, validatePaymentForm} from '../utils/payment-rules';
import {DialogShell} from './DialogShell';

const FIELD_CLASSES =
  'min-h-11 w-full rounded-xl border border-border bg-background px-3 text-start text-sm text-foreground transition-colors placeholder:text-muted hover:border-brand/50 focus-visible:border-brand focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand disabled:cursor-not-allowed disabled:opacity-60 aria-[invalid=true]:border-red-400';

const LABEL_CLASSES = 'mb-1.5 block text-xs font-semibold text-foreground';

const ERROR_CLASSES = 'mt-1 text-xs font-bold text-red-400';

const PRIMARY_BUTTON_CLASSES =
  'inline-flex h-11 items-center justify-center gap-2 rounded-xl border border-transparent bg-brand px-5 text-sm font-bold text-[#06211f] transition-opacity hover:opacity-90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand disabled:cursor-not-allowed disabled:opacity-60';

const SECONDARY_BUTTON_CLASSES =
  'inline-flex h-11 items-center justify-center gap-2 rounded-xl border border-border px-5 text-sm font-semibold text-foreground transition-colors hover:bg-surface-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand disabled:cursor-not-allowed disabled:opacity-40';

type SubmitPayment = (
  input: CreatePaymentInput,
  handlers: {
    onSuccess: () => void;
    onError: (error: unknown) => void;
  }
) => void;

type PaymentFormFieldsProps = {
  options: readonly PaymentAppointmentOption[];
  isLoading: boolean;
  hasError: boolean;
  onRetry: () => void;
  isBusy: boolean;
  submit: SubmitPayment;
  onClose: () => void;
};

type PaymentFormDialogProps = {
  open: boolean;
  onClose: () => void;
};

/**
 * Records a payment against a payable appointment. The form fields are mounted
 * only while the dialog is open, so every opening starts with a clean form.
 */
export function PaymentFormDialog({open, onClose}: PaymentFormDialogProps) {
  const t = useTranslations('Payments.form');
  const tActions = useTranslations('Payments.actions');
  const payable = usePayableAppointments(open);
  const mutation = useCreatePayment();

  return (
    <DialogShell
      open={open}
      onClose={onClose}
      title={t('title')}
      description={t('description')}
      closeLabel={tActions('close')}
      isBusy={mutation.isPending}
    >
      {open ? (
        <PaymentFormFields
          options={payable.data ?? []}
          isLoading={payable.isPending}
          hasError={payable.isError}
          onRetry={() => void payable.refetch()}
          isBusy={mutation.isPending}
          submit={mutation.mutate}
          onClose={onClose}
        />
      ) : null}
    </DialogShell>
  );
}

function PaymentFormFields({
  options,
  isLoading,
  hasError,
  onRetry,
  isBusy,
  submit,
  onClose
}: PaymentFormFieldsProps) {
  const t = useTranslations('Payments.form');
  const tMethod = useTranslations('Payments.method');
  const tActions = useTranslations('Payments.actions');
  const locale = useLocale();
  const errorMessage = usePaymentErrorMessage();
  const baseId = useId();

  const [appointmentId, setAppointmentId] = useState('');
  const [amount, setAmount] = useState('');
  const [method, setMethod] = useState<PaymentMethod>('cash');
  const [notes, setNotes] = useState('');
  const [recordAsPaid, setRecordAsPaid] = useState(false);
  const [errors, setErrors] = useState<CreatePaymentFormErrors>({});
  const [formError, setFormError] = useState<string | null>(null);

  const fieldId = (field: PaymentFormField) => `${baseId}-${field}`;
  const defaultAppointmentId =
    options.length === 1 ? (options[0]?.id ?? '') : '';
  const selectedAppointmentId = appointmentId || defaultAppointmentId;
  const hasNoAppointments = !isLoading && !hasError && options.length === 0;

  function fieldError(key: PaymentFormErrorKey | undefined): string | null {
    if (!key) {
      return null;
    }

    if (key === 'amountOutOfRange') {
      return t('errors.amountOutOfRange', {
        min: PAYMENT_AMOUNT_MIN,
        max: PAYMENT_AMOUNT_MAX
      });
    }

    if (key === 'notesTooLong') {
      return t('errors.notesTooLong', {max: PAYMENT_NOTES_MAX_LENGTH});
    }

    return t(`errors.${key}`);
  }

  function focusFirstInvalid(found: CreatePaymentFormErrors) {
    const target = PAYMENT_FORM_FIELDS.filter((field) => found[field])
      .map(fieldId)
      .map((id) => document.getElementById(id))
      .find((element) => element !== null);

    target?.focus();
  }

  function clearError(field: PaymentFormField) {
    setErrors((current) => ({...current, [field]: undefined}));
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(null);

    const found = validatePaymentForm({
      appointmentId: selectedAppointmentId,
      amount,
      method,
      notes
    });

    if (hasFormErrors(found)) {
      setErrors(found);
      focusFirstInvalid(found);
      return;
    }

    const parsedAmount = parseAmountInput(amount);

    if (parsedAmount === null) {
      setErrors({amount: 'amountInvalid'});
      focusFirstInvalid({amount: 'amountInvalid'});
      return;
    }

    setErrors({});
    submit(
      {
        appointmentId: selectedAppointmentId,
        amount: parsedAmount,
        method,
        notes,
        recordAsPaid
      },
      {
        onSuccess: onClose,
        onError: (error) => {
          // The payment exists as `Pending`; the hook already warned the user.
          if (isPaymentPartiallyRecordedError(error)) {
            onClose();
            return;
          }

          const fieldErrors = isApiError(error)
            ? mapPaymentFieldErrors(error, PAYMENT_FORM_FIELDS)
            : {};

          if (hasFormErrors(fieldErrors)) {
            setErrors(fieldErrors);
            focusFirstInvalid(fieldErrors);
          } else {
            setFormError(errorMessage(error));
          }
        }
      }
    );
  }

  return (
    <form onSubmit={handleSubmit} noValidate aria-label={t('title')} className="grid gap-4">
      {formError ? (
        <div
          role="alert"
          className="rounded-xl border border-red-400/40 bg-red-500/10 p-3 text-sm font-semibold text-red-300 [:root[data-theme=light]_&]:text-red-800"
        >
          {formError}
        </div>
      ) : null}

      {hasError ? (
        <div
          role="alert"
          className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-red-400/40 bg-red-500/10 p-3 text-sm text-red-300 [:root[data-theme=light]_&]:text-red-800"
        >
          <span>{t('appointmentsError')}</span>
          <button
            type="button"
            onClick={onRetry}
            className="inline-flex h-9 items-center justify-center rounded-lg border border-current px-3 text-xs font-semibold"
          >
            {tActions('retry')}
          </button>
        </div>
      ) : null}

      {hasNoAppointments ? (
        <p
          role="note"
          className="rounded-xl border border-amber-400/40 bg-amber-400/10 p-3 text-sm text-amber-300 [:root[data-theme=light]_&]:text-amber-800"
        >
          {t('noAppointments')}
        </p>
      ) : null}

      <div>
        <label htmlFor={fieldId('appointmentId')} className={LABEL_CLASSES}>
          {t('appointmentLabel')}
        </label>
        <select
          id={fieldId('appointmentId')}
          value={selectedAppointmentId}
          disabled={isLoading || hasError || options.length === 0}
          aria-invalid={errors.appointmentId ? true : undefined}
          aria-describedby={
            errors.appointmentId ? `${fieldId('appointmentId')}-error` : undefined
          }
          onChange={(event) => {
            setAppointmentId(event.target.value);
            clearError('appointmentId');
          }}
          className={FIELD_CLASSES}
        >
          <option value="">
            {isLoading ? t('appointmentLoading') : t('appointmentPlaceholder')}
          </option>
          {options.map((option) => (
            <option key={option.id} value={option.id}>
              {option.patientName} — {formatDate(option.appointmentDate, locale)}{' '}
              {formatTime(option.startTime, locale)}
            </option>
          ))}
        </select>
        {errors.appointmentId ? (
          <p
            id={`${fieldId('appointmentId')}-error`}
            role="alert"
            className={ERROR_CLASSES}
          >
            {fieldError(errors.appointmentId)}
          </p>
        ) : null}
      </div>

      <div>
        <label htmlFor={fieldId('amount')} className={LABEL_CLASSES}>
          {t('amountLabel')}
        </label>
        <input
          id={fieldId('amount')}
          type="text"
          dir="ltr"
          inputMode="decimal"
          autoComplete="off"
          value={amount}
          placeholder={t('amountPlaceholder')}
          aria-invalid={errors.amount ? true : undefined}
          aria-describedby={errors.amount ? `${fieldId('amount')}-error` : undefined}
          onChange={(event) => {
            setAmount(event.target.value);
            clearError('amount');
          }}
          className={FIELD_CLASSES}
        />
        {errors.amount ? (
          <p id={`${fieldId('amount')}-error`} role="alert" className={ERROR_CLASSES}>
            {fieldError(errors.amount)}
          </p>
        ) : null}
      </div>

      <div>
        <label htmlFor={fieldId('method')} className={LABEL_CLASSES}>
          {t('methodLabel')}
        </label>
        <select
          id={fieldId('method')}
          value={method}
          aria-invalid={errors.method ? true : undefined}
          aria-describedby={errors.method ? `${fieldId('method')}-error` : undefined}
          onChange={(event) => {
            const value = event.target.value;

            if (isPaymentMethod(value)) {
              setMethod(value);
              clearError('method');
            }
          }}
          className={FIELD_CLASSES}
        >
          {PAYMENT_METHODS.map((item) => (
            <option key={item} value={item}>
              {tMethod(item)}
            </option>
          ))}
        </select>
        {errors.method ? (
          <p id={`${fieldId('method')}-error`} role="alert" className={ERROR_CLASSES}>
            {fieldError(errors.method)}
          </p>
        ) : null}
      </div>

      <div>
        <div className="flex items-baseline justify-between gap-3">
          <label htmlFor={fieldId('notes')} className={LABEL_CLASSES}>
            {t('notesLabel')}
          </label>
          <span className="mb-1.5 text-[11px] text-muted" dir="auto">
            {t('notesCounter', {count: notes.trim().length, max: PAYMENT_NOTES_MAX_LENGTH})}
          </span>
        </div>
        <textarea
          id={fieldId('notes')}
          rows={3}
          value={notes}
          placeholder={t('notesPlaceholder')}
          aria-invalid={errors.notes ? true : undefined}
          aria-describedby={errors.notes ? `${fieldId('notes')}-error` : undefined}
          onChange={(event) => {
            setNotes(event.target.value);
            clearError('notes');
          }}
          className={`${FIELD_CLASSES} py-2`}
        />
        {errors.notes ? (
          <p id={`${fieldId('notes')}-error`} role="alert" className={ERROR_CLASSES}>
            {fieldError(errors.notes)}
          </p>
        ) : null}
      </div>

      <label className="flex cursor-pointer items-start gap-3 rounded-xl border border-border bg-surface-2/40 p-3">
        <input
          type="checkbox"
          checked={recordAsPaid}
          onChange={(event) => setRecordAsPaid(event.target.checked)}
          className="mt-0.5 size-4 accent-brand"
        />
        <span className="grid gap-0.5">
          <span className="flex items-center gap-1.5 text-sm font-semibold text-foreground">
            <FiCheck aria-hidden="true" className="size-4 text-brand" />
            {t('recordAsPaidLabel')}
          </span>
          <span className="text-xs text-muted">{t('recordAsPaidHint')}</span>
        </span>
      </label>

      <div className="flex flex-wrap justify-end gap-2 pt-1">
        <button
          type="button"
          onClick={onClose}
          disabled={isBusy}
          className={SECONDARY_BUTTON_CLASSES}
        >
          {tActions('cancel')}
        </button>
        <button
          type="submit"
          disabled={isBusy || options.length === 0}
          aria-busy={isBusy || undefined}
          className={PRIMARY_BUTTON_CLASSES}
        >
          {isBusy ? tActions('saving') : t('submit')}
        </button>
      </div>
    </form>
  );
}
