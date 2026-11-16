import {isApiError, type ApiError} from '@/lib/api';
import type {
  PaymentFormErrorKey,
  PaymentFormField
} from '../types/payment.types';

/** Keys of the `Payments.errors` messages. */
export type PaymentErrorKey =
  | 'network'
  | 'invalidResponse'
  | 'identity'
  | 'unauthorized'
  | 'forbidden'
  | 'notFound'
  | 'validation'
  | 'appointmentNotFound'
  | 'alreadyExists'
  | 'paymentNotFound'
  | 'paidFinal'
  | 'invalidTransition'
  | 'invalidAmount'
  | 'invalidMethod'
  | 'notesTooLong'
  | 'missingReference'
  | 'badRequest'
  | 'conflict'
  | 'server'
  | 'unknown';

/**
 * Stable API `code` → message key. Codes come from
 * `backend/docs/api/payments.md` and `DomainErrorCodes`.
 */
const CODE_TO_KEY: Readonly<Record<string, PaymentErrorKey>> = {
  'identity.invalid': 'identity',
  'payment.not_found': 'paymentNotFound',
  'payment.appointment_not_found': 'appointmentNotFound',
  'payment.already_exists': 'alreadyExists',
  'payment.invalid_status_transition': 'invalidTransition',
  'payment.paid_final': 'paidFinal',
  'payment.invalid_amount': 'invalidAmount',
  'payment.invalid_method': 'invalidMethod',
  'payment.notes_too_long': 'notesTooLong',
  'payment.missing_reference': 'missingReference',
  'validation.failed': 'validation',
  'domain.rule_violation': 'badRequest',
  'server.error': 'server'
};

function keyFromStatus(status: number): PaymentErrorKey {
  if (status === 0) {
    return 'network';
  }

  if (status === 400) {
    return 'badRequest';
  }

  if (status === 401) {
    return 'unauthorized';
  }

  if (status === 403) {
    return 'forbidden';
  }

  if (status === 404) {
    return 'notFound';
  }

  if (status === 409) {
    return 'conflict';
  }

  return status >= 500 ? 'server' : 'unknown';
}

/**
 * Chooses the message to show for any failure. The API `code` wins; otherwise
 * the HTTP status decides. Server English text is never shown to the user.
 */
export function getPaymentErrorKey(error: unknown): PaymentErrorKey {
  if (!isApiError(error)) {
    return 'unknown';
  }

  if (error.kind === 'network') {
    return 'network';
  }

  if (error.kind === 'invalid-response') {
    return 'invalidResponse';
  }

  if (error.code !== null && error.code in CODE_TO_KEY) {
    return CODE_TO_KEY[error.code] ?? keyFromStatus(error.status);
  }

  return keyFromStatus(error.status);
}

/** True for failures worth retrying: no connection or a server-side error. */
export function isRetryablePaymentError(error: unknown): boolean {
  return isApiError(error) && (error.status === 0 || error.status >= 500);
}

function requiredKeyFor(field: PaymentFormField): PaymentFormErrorKey {
  switch (field) {
    case 'appointmentId':
      return 'appointmentRequired';
    case 'amount':
      return 'amountRequired';
    case 'method':
      return 'methodRequired';
    case 'notes':
      return 'invalid';
  }
}

function fieldCodeToKey(
  field: PaymentFormField,
  code: string
): PaymentFormErrorKey {
  // FluentValidation's built-in codes; the amount rule is a `Must` predicate.
  if (code === 'NotEmptyValidator' || code === 'NotNullValidator') {
    return requiredKeyFor(field);
  }

  if (code === 'MaximumLengthValidator') {
    return field === 'notes' ? 'notesTooLong' : 'invalid';
  }

  if (code === 'PredicateValidator' && field === 'amount') {
    return 'amountOutOfRange';
  }

  if (code === 'EnumValidator' && field === 'method') {
    return 'methodRequired';
  }

  return 'invalid';
}

/**
 * Maps the API's per-field validator codes to form message keys. Fields the API
 * did not flag are omitted; framework 400s without codes fall back to `invalid`.
 */
export function mapPaymentFieldErrors(
  error: ApiError,
  fields: readonly PaymentFormField[]
): Partial<Record<PaymentFormField, PaymentFormErrorKey>> {
  const result: Partial<Record<PaymentFormField, PaymentFormErrorKey>> = {};

  for (const field of fields) {
    const codes = error.fieldErrorCodes[field];
    const messages = error.fieldErrors[field];

    if (codes && codes.length > 0) {
      result[field] = fieldCodeToKey(field, codes[0] ?? '');
    } else if (messages && messages.length > 0) {
      result[field] = 'invalid';
    }
  }

  return result;
}
