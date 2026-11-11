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


export const DEFAULT_PAGE_SIZE = 10;
export const MAX_PAGE_SIZE = 50;


export type Payment = {
  readonly id: string;
  readonly patientName: string;
  readonly appointmentId: string;
  /** ISO date, e.g. 2026-11-12 */
  readonly appointmentDate: string;
  readonly amount: number;
  readonly method: PaymentMethod;
  readonly status: PaymentStatus;
  /** ISO date-time */
  readonly paidAt: string;
  readonly notes?: string;
};


export type PaymentAppointmentOption = {
  readonly id: string;
  readonly patientName: string;

  readonly appointmentDate: string;
  
  readonly startTime: string;
};


export type CreatePaymentInput = {
  appointmentId: string;
  amount: number;
  method: PaymentMethod;
  notes?: string;
};

export type UpdatePaymentStatusInput = {
  id: string;
  status: PaymentStatus;
};


export type CreatePaymentFormErrors = Partial<
  Record<keyof CreatePaymentInput, string>
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


export type PagedResult<T> = {
  readonly items: readonly T[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly totalPages: number;
};