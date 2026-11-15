import {
  PAYMENT_AMOUNT_MAX,
  PAYMENT_AMOUNT_MIN,
  PAYMENT_NOTES_MAX_LENGTH,
  isPaymentMethod,
  type CreatePaymentFormErrors,
  type PaymentMethod,
  type PaymentStatus
} from '../types/payment.types';
import {parseAmountInput} from './formatters';

/**
 * Status changes the API allows (F7-C1, Q-21). `Paid` is final, so the table
 * does not offer a status action for a paid payment.
 */
const ALLOWED_TRANSITIONS: Readonly<Record<PaymentStatus, readonly PaymentStatus[]>> = {
  pending: ['partial', 'paid'],
  partial: ['paid'],
  paid: []
};

export function nextStatuses(status: PaymentStatus): readonly PaymentStatus[] {
  return ALLOWED_TRANSITIONS[status];
}

export type PaymentFormValues = {
  appointmentId: string;
  /** Raw text from the amount input, parsed with `parseAmountInput`. */
  amount: string;
  method: PaymentMethod;
  notes: string;
};

/**
 * Fast client-side checks that mirror the server's shape rules. The backend
 * remains the authority; the dialog also maps its per-field errors.
 */
export function validatePaymentForm(
  values: PaymentFormValues
): CreatePaymentFormErrors {
  const errors: CreatePaymentFormErrors = {};
  const amountText = values.amount.trim();

  if (values.appointmentId.trim() === '') {
    errors.appointmentId = 'appointmentRequired';
  }

  if (amountText === '') {
    errors.amount = 'amountRequired';
  } else {
    const parsed = parseAmountInput(amountText);

    if (parsed === null) {
      errors.amount = 'amountInvalid';
    } else if (parsed < PAYMENT_AMOUNT_MIN || parsed > PAYMENT_AMOUNT_MAX) {
      errors.amount = 'amountOutOfRange';
    }
  }

  if (!isPaymentMethod(values.method)) {
    errors.method = 'methodRequired';
  }

  if (values.notes.trim().length > PAYMENT_NOTES_MAX_LENGTH) {
    errors.notes = 'notesTooLong';
  }

  return errors;
}

export function hasFormErrors(errors: Readonly<Record<string, unknown>>): boolean {
  return Object.keys(errors).length > 0;
}
