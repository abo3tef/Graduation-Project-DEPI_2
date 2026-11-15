import {describe, expect, it} from 'vitest';
import {ApiError, createInvalidResponseError, kindFromStatus} from '@/lib/api';
import arMessages from '@/messages/ar.json';
import enMessages from '@/messages/en.json';
import {
  PAYMENT_FORM_FIELDS,
  type PaymentFormErrorKey
} from '../types/payment.types';
import {
  getPaymentErrorKey,
  isRetryablePaymentError,
  mapPaymentFieldErrors,
  type PaymentErrorKey
} from './payment-errors';

function apiError(
  status: number,
  code: string | null = null,
  fieldErrorCodes: Readonly<Record<string, readonly string[]>> = {},
  fieldErrors: Readonly<Record<string, readonly string[]>> = {}
) {
  return new ApiError({
    kind: kindFromStatus(status),
    status,
    code,
    fieldErrorCodes,
    fieldErrors
  });
}

describe('getPaymentErrorKey', () => {
  it.each([
    ['payment.not_found', 'paymentNotFound'],
    ['payment.appointment_not_found', 'appointmentNotFound'],
    ['payment.already_exists', 'alreadyExists'],
    ['payment.invalid_status_transition', 'invalidTransition'],
    ['payment.paid_final', 'paidFinal'],
    ['payment.invalid_amount', 'invalidAmount'],
    ['payment.invalid_method', 'invalidMethod'],
    ['payment.notes_too_long', 'notesTooLong'],
    ['payment.missing_reference', 'missingReference'],
    ['identity.invalid', 'identity'],
    ['validation.failed', 'validation'],
    ['domain.rule_violation', 'badRequest'],
    ['server.error', 'server']
  ] as const)('maps %s to %s', (code, expected) => {
    expect(getPaymentErrorKey(apiError(409, code))).toBe(expected);
  });

  it('falls back to the HTTP status when the code is unknown or absent', () => {
    expect(getPaymentErrorKey(apiError(400))).toBe('badRequest');
    expect(getPaymentErrorKey(apiError(401))).toBe('unauthorized');
    expect(getPaymentErrorKey(apiError(403))).toBe('forbidden');
    expect(getPaymentErrorKey(apiError(404))).toBe('notFound');
    expect(getPaymentErrorKey(apiError(409))).toBe('conflict');
    expect(getPaymentErrorKey(apiError(418))).toBe('unknown');
    expect(getPaymentErrorKey(apiError(500, 'server.error'))).toBe('server');
    expect(getPaymentErrorKey(apiError(400, 'not.a.payment.code'))).toBe('badRequest');
  });

  it('handles failures without a usable response', () => {
    expect(getPaymentErrorKey(new ApiError({kind: 'network', status: 0}))).toBe('network');
    expect(getPaymentErrorKey(createInvalidResponseError(200))).toBe('invalidResponse');
    expect(getPaymentErrorKey(new Error('boom'))).toBe('unknown');
    expect(getPaymentErrorKey(null)).toBe('unknown');
  });
});

describe('isRetryablePaymentError', () => {
  it('retries only network and server failures', () => {
    expect(isRetryablePaymentError(new ApiError({kind: 'network', status: 0}))).toBe(true);
    expect(isRetryablePaymentError(apiError(503))).toBe(true);
    expect(isRetryablePaymentError(apiError(400))).toBe(false);
    expect(isRetryablePaymentError(apiError(409))).toBe(false);
    expect(isRetryablePaymentError(new Error('boom'))).toBe(false);
  });
});

describe('mapPaymentFieldErrors', () => {
  it('maps the FluentValidation codes to form message keys', () => {
    const error = apiError(400, 'validation.failed', {
      appointmentId: ['NotEmptyValidator'],
      amount: ['PredicateValidator'],
      method: ['EnumValidator'],
      notes: ['MaximumLengthValidator']
    });

    expect(mapPaymentFieldErrors(error, PAYMENT_FORM_FIELDS)).toEqual({
      appointmentId: 'appointmentRequired',
      amount: 'amountOutOfRange',
      method: 'methodRequired',
      notes: 'notesTooLong'
    });
  });

  it('maps NotNull to the field-specific required message', () => {
    const error = apiError(400, 'validation.failed', {
      amount: ['NotNullValidator'],
      method: ['NotNullValidator']
    });

    expect(mapPaymentFieldErrors(error, PAYMENT_FORM_FIELDS)).toEqual({
      amount: 'amountRequired',
      method: 'methodRequired'
    });
  });

  it('falls back to invalid for unknown codes and code-less field errors', () => {
    expect(
      mapPaymentFieldErrors(apiError(400, 'validation.failed', {notes: ['SomeValidator']}), [
        'notes'
      ])
    ).toEqual({notes: 'invalid'});

    expect(
      mapPaymentFieldErrors(apiError(400, null, {}, {amount: ['is required']}), ['amount'])
    ).toEqual({amount: 'invalid'});
  });

  it('omits fields the API did not flag', () => {
    expect(mapPaymentFieldErrors(apiError(409, 'payment.paid_final'), PAYMENT_FORM_FIELDS)).toEqual(
      {}
    );
  });
});

describe('Payment messages', () => {
  const ERROR_KEYS = {
    network: true,
    invalidResponse: true,
    identity: true,
    unauthorized: true,
    forbidden: true,
    notFound: true,
    validation: true,
    appointmentNotFound: true,
    alreadyExists: true,
    paymentNotFound: true,
    paidFinal: true,
    invalidTransition: true,
    invalidAmount: true,
    invalidMethod: true,
    notesTooLong: true,
    missingReference: true,
    badRequest: true,
    conflict: true,
    server: true,
    unknown: true
  } satisfies Record<PaymentErrorKey, true>;

  const FORM_ERROR_KEYS = {
    appointmentRequired: true,
    amountRequired: true,
    amountInvalid: true,
    amountOutOfRange: true,
    methodRequired: true,
    notesTooLong: true,
    invalid: true
  } satisfies Record<PaymentFormErrorKey, true>;

  it.each([
    ['en', enMessages],
    ['ar', arMessages]
  ] as const)('%s defines a message for every error key', (_name, locale) => {
    expect(Object.keys(locale.Payments.errors).sort()).toEqual(Object.keys(ERROR_KEYS).sort());
    expect(Object.keys(locale.Payments.form.errors).sort()).toEqual(
      Object.keys(FORM_ERROR_KEYS).sort()
    );
  });
});
