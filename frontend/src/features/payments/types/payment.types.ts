import {DEFAULT_PAGE_SIZE} from '@/types/paging';

/**
 * The UI model keeps lower-case labels because the translation keys and the
 * badge styles are keyed by them. The API serializes `PaymentStatus` and
 * `PaymentMethod` as PascalCase labels (`Paid`, `Cash`); `services/payment-enums.ts`
 * is the single boundary where the two representations are mapped.
 */
export const PAYMENT_STATUSES = ['pending', 'partial', 'paid'] as const;
export type PaymentStatus = (typeof PAYMENT_STATUSES)[number];

export const PAYMENT_METHODS = ['cash', 'card'] as const;
export type PaymentMethod = (typeof PAYMENT_METHODS)[number];

export function isPaymentStatus(value: unknown): value is PaymentStatus {
  return (PAYMENT_STATUSES as readonly unknown[]).includes(value);
}

export function isPaymentMethod(value: unknown): value is PaymentMethod {
  return (PAYMENT_METHODS as readonly unknown[]).includes(value);
}

export const PAYMENT_CURRENCY = 'EGP';

export const PAYMENT_AMOUNT_MIN = 0.01;
export const PAYMENT_AMOUNT_MAX = 1_000_000;
export const PAYMENT_NOTES_MAX_LENGTH = 500;

/** Matches `PaymentRules.DefaultPayableLimit` in the API. */
export const PAYABLE_APPOINTMENTS_LIMIT = 50;

export type Payment = {
  readonly id: string;
  readonly patientId: string;
  readonly patientName: string;
  readonly doctorId: string;
  readonly appointmentId: string;
  /** ISO date, e.g. 2026-11-12 */
  readonly appointmentDate: string;
  readonly amount: number;
  readonly method: PaymentMethod;
  readonly status: PaymentStatus;
  /** ISO date-time of the moment the payment reached `paid`; null before that. */
  readonly paidAt: string | null;
  readonly notes: string | null;
};

/** An appointment that can still be billed, for the create form picker. */
export type PaymentAppointmentOption = {
  readonly id: string;
  readonly patientId: string;
  readonly patientName: string;
  readonly doctorId: string;
  /** ISO date, e.g. 2026-11-12 */
  readonly appointmentDate: string;
  /** `HH:mm:ss` as served by the API. */
  readonly startTime: string;
};

export type CreatePaymentInput = {
  appointmentId: string;
  amount: number;
  method: PaymentMethod;
  notes?: string;
  /**
   * The API always creates the payment as `Pending`. When true the service
   * follows the POST with a status call that moves it to `Paid`.
   */
  recordAsPaid?: boolean;
};

export type UpdatePaymentStatusInput = {
  id: string;
  status: PaymentStatus;
};

/** The create form fields, in the order the API validates them. */
export const PAYMENT_FORM_FIELDS = [
  'appointmentId',
  'amount',
  'method',
  'notes'
] as const;

export type PaymentFormField = (typeof PAYMENT_FORM_FIELDS)[number];

/** Keys of the `Payments.form.errors` messages. */
export type PaymentFormErrorKey =
  | 'appointmentRequired'
  | 'amountRequired'
  | 'amountInvalid'
  | 'amountOutOfRange'
  | 'methodRequired'
  | 'notesTooLong'
  | 'invalid';

export type CreatePaymentFormErrors = Partial<
  Record<PaymentFormField, PaymentFormErrorKey>
>;

export type PaymentFilters = {
  search?: string;
  status?: PaymentStatus | 'all';
  method?: PaymentMethod | 'all';
  page: number;
  pageSize: number;
};

export const DEFAULT_PAYMENT_FILTERS: PaymentFilters = {
  search: '',
  status: 'all',
  method: 'all',
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE
};
