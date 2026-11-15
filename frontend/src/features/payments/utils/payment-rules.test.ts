import {describe, expect, it} from 'vitest';
import {
  hasFormErrors,
  nextStatuses,
  validatePaymentForm
} from './payment-rules';

describe('nextStatuses', () => {
  it('matches the API transitions and keeps Paid final', () => {
    expect(nextStatuses('pending')).toEqual(['partial', 'paid']);
    expect(nextStatuses('partial')).toEqual(['paid']);
    expect(nextStatuses('paid')).toEqual([]);
  });
});

describe('validatePaymentForm', () => {
  const valid = {
    appointmentId: 'apt-1',
    amount: '250.50',
    method: 'cash' as const,
    notes: 'First visit'
  };

  it('accepts a complete form, including Arabic-Indic digits', () => {
    expect(validatePaymentForm(valid)).toEqual({});
    expect(
      validatePaymentForm({...valid, amount: '٢٥٠٫٥٠'})
    ).toEqual({});
  });

  it('requires an appointment and an amount', () => {
    expect(validatePaymentForm({...valid, appointmentId: '   '})).toMatchObject({
      appointmentId: 'appointmentRequired'
    });
    expect(validatePaymentForm({...valid, amount: '   '})).toMatchObject({
      amount: 'amountRequired'
    });
  });

  it('rejects malformed and out-of-range amounts', () => {
    expect(validatePaymentForm({...valid, amount: 'abc'})).toMatchObject({
      amount: 'amountInvalid'
    });
    expect(validatePaymentForm({...valid, amount: '0'})).toMatchObject({
      amount: 'amountOutOfRange'
    });
    expect(validatePaymentForm({...valid, amount: '1000000.01'})).toMatchObject({
      amount: 'amountOutOfRange'
    });
    expect(validatePaymentForm({...valid, amount: '0.01'})).toEqual({});
    expect(validatePaymentForm({...valid, amount: '1000000'})).toEqual({});
  });

  it('rejects a method outside the allow-list', () => {
    expect(
      validatePaymentForm({
        ...valid,
        method: 'cheque' as unknown as 'cash'
      })
    ).toMatchObject({method: 'methodRequired'});
  });

  it('limits the notes length', () => {
    expect(validatePaymentForm({...valid, notes: 'a'.repeat(500)})).toEqual({});
    expect(validatePaymentForm({...valid, notes: 'a'.repeat(501)})).toMatchObject({
      notes: 'notesTooLong'
    });
  });

  it('reports whether any error exists', () => {
    expect(hasFormErrors({})).toBe(false);
    expect(hasFormErrors({amount: 'amountInvalid'})).toBe(true);
  });
});
