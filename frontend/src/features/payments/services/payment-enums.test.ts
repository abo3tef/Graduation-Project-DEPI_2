import {describe, expect, it} from 'vitest';
import {
  API_PAYMENT_METHODS,
  API_PAYMENT_STATUSES,
  fromApiPaymentMethod,
  fromApiPaymentStatus,
  isApiPaymentMethod,
  isApiPaymentStatus,
  toApiPaymentMethod,
  toApiPaymentStatus
} from './payment-enums';
import {
  PAYMENT_METHODS,
  PAYMENT_STATUSES
} from '../types/payment.types';

describe('payment enum boundary', () => {
  it('maps every UI method to the PascalCase API label and back', () => {
    for (const method of PAYMENT_METHODS) {
      const api = toApiPaymentMethod(method);

      expect(API_PAYMENT_METHODS).toContain(api);
      expect(fromApiPaymentMethod(api)).toBe(method);
    }
  });

  it('maps every UI status to the PascalCase API label and back', () => {
    for (const status of PAYMENT_STATUSES) {
      const api = toApiPaymentStatus(status);

      expect(API_PAYMENT_STATUSES).toContain(api);
      expect(fromApiPaymentStatus(api)).toBe(status);
    }
  });

  it('rejects the other casing and unknown values', () => {
    expect(isApiPaymentMethod('Cash')).toBe(true);
    expect(isApiPaymentMethod('cash')).toBe(false);
    expect(isApiPaymentMethod('Cheque')).toBe(false);
    expect(isApiPaymentMethod(1)).toBe(false);

    expect(isApiPaymentStatus('Paid')).toBe(true);
    expect(isApiPaymentStatus('paid')).toBe(false);
    expect(isApiPaymentStatus(null)).toBe(false);
  });
});
