import type {PaymentMethod, PaymentStatus} from '../types/payment.types';

/**
 * The API stores and returns these enums as their PascalCase specification
 * labels (`"Cash"`, `"Paid"`), not as the lower-case UI values. Every request
 * body, filter value and response passes through this module, so the casing is
 * converted in one place.
 */
export const API_PAYMENT_METHODS = ['Cash', 'Card'] as const;
export type ApiPaymentMethod = (typeof API_PAYMENT_METHODS)[number];

export const API_PAYMENT_STATUSES = ['Pending', 'Partial', 'Paid'] as const;
export type ApiPaymentStatus = (typeof API_PAYMENT_STATUSES)[number];

const METHOD_TO_API: Readonly<Record<PaymentMethod, ApiPaymentMethod>> = {
  cash: 'Cash',
  card: 'Card'
};

const METHOD_FROM_API: Readonly<Record<ApiPaymentMethod, PaymentMethod>> = {
  Cash: 'cash',
  Card: 'card'
};

const STATUS_TO_API: Readonly<Record<PaymentStatus, ApiPaymentStatus>> = {
  pending: 'Pending',
  partial: 'Partial',
  paid: 'Paid'
};

const STATUS_FROM_API: Readonly<Record<ApiPaymentStatus, PaymentStatus>> = {
  Pending: 'pending',
  Partial: 'partial',
  Paid: 'paid'
};

export function toApiPaymentMethod(value: PaymentMethod): ApiPaymentMethod {
  return METHOD_TO_API[value];
}

export function toApiPaymentStatus(value: PaymentStatus): ApiPaymentStatus {
  return STATUS_TO_API[value];
}

export function fromApiPaymentMethod(value: ApiPaymentMethod): PaymentMethod {
  return METHOD_FROM_API[value];
}

export function fromApiPaymentStatus(value: ApiPaymentStatus): PaymentStatus {
  return STATUS_FROM_API[value];
}

export function isApiPaymentMethod(value: unknown): value is ApiPaymentMethod {
  return (
    typeof value === 'string' &&
    (API_PAYMENT_METHODS as readonly string[]).includes(value)
  );
}

export function isApiPaymentStatus(value: unknown): value is ApiPaymentStatus {
  return (
    typeof value === 'string' &&
    (API_PAYMENT_STATUSES as readonly string[]).includes(value)
  );
}
