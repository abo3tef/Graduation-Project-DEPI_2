import {MAX_PAGE_SIZE} from '@/types/paging';
import type {PagedResult} from '@/types/paging';
import {
  PAYMENT_AMOUNT_MAX,
  PAYMENT_AMOUNT_MIN,
  PAYMENT_NOTES_MAX_LENGTH,
  isPaymentMethod,
  isPaymentStatus
} from '../types/payment.types';
import type {
  CreatePaymentInput,
  Payment,
  PaymentAppointmentOption,
  PaymentFilters,
  UpdatePaymentStatusInput
} from '../types/payment.types';
import {MOCK_APPOINTMENTS, MOCK_PAYMENTS} from './payments.mock';



export type PaymentErrorCode =
  | 'NETWORK'
  | 'INVALID_INPUT'
  | 'APPOINTMENT_NOT_FOUND'
  | 'APPOINTMENT_ALREADY_PAID'
  | 'PAYMENT_NOT_FOUND';

export class PaymentServiceError extends Error {
  readonly code: PaymentErrorCode;

  constructor(code: PaymentErrorCode) {
    super(code);
    this.name = 'PaymentServiceError';
    this.code = code;
  }
}

export function isPaymentServiceError(
  error: unknown
): error is PaymentServiceError {
  return error instanceof PaymentServiceError;
}


const MOCK_LATENCY_MS = 400;


const SHOULD_FAIL = process.env.NEXT_PUBLIC_PAYMENTS_MOCK_FAIL === 'true';


let store: readonly Payment[] = MOCK_PAYMENTS;
let nextPaymentNumber = MOCK_PAYMENTS.length + 1;

async function simulateRequest(): Promise<void> {
  await new Promise<void>((resolve) => setTimeout(resolve, MOCK_LATENCY_MS));

  if (SHOULD_FAIL) {
    throw new PaymentServiceError('NETWORK');
  }
}


function normalize(value: string): string {
  return value.trim().toLocaleLowerCase();
}

function clampInteger(value: number, min: number, max: number): number {
  if (!Number.isFinite(value)) {
    return min;
  }

  return Math.min(Math.max(Math.trunc(value), min), max);
}

export async function listPayments(
  filters: PaymentFilters
): Promise<PagedResult<Payment>> {
  await simulateRequest();

  const pageSize = clampInteger(filters.pageSize, 1, MAX_PAGE_SIZE);
  const search = normalize(filters.search ?? '');

  const filtered = store.filter((payment) => {
    if (filters.status && filters.status !== 'all' && payment.status !== filters.status) {
      return false;
    }

    if (filters.method && filters.method !== 'all' && payment.method !== filters.method) {
      return false;
    }

    if (search === '') {
      return true;
    }

    return (
      normalize(payment.patientName).includes(search) ||
      normalize(payment.id).includes(search) ||
      normalize(payment.appointmentId).includes(search)
    );
  });

  
  const sorted = [...filtered].sort((a, b) => {
    const byDate = b.paidAt.localeCompare(a.paidAt);
    return byDate !== 0 ? byDate : b.id.localeCompare(a.id);
  });

  const totalCount = sorted.length;
  const totalPages = Math.ceil(totalCount / pageSize);
  const page = totalPages === 0 ? 1 : clampInteger(filters.page, 1, totalPages);
  const start = (page - 1) * pageSize;

  return {
    items: sorted.slice(start, start + pageSize),
    page,
    pageSize,
    totalCount,
    totalPages
  };
}


export async function listPayableAppointments(): Promise<
  readonly PaymentAppointmentOption[]
> {
  await simulateRequest();

  const paidAppointmentIds = new Set(store.map((payment) => payment.appointmentId));

  return MOCK_APPOINTMENTS.filter((appointment) => !paidAppointmentIds.has(appointment.id));
}


export async function createPayment(input: CreatePaymentInput): Promise<Payment> {
  await simulateRequest();

  const notes = input.notes?.trim();

  const isValid =
    Number.isFinite(input.amount) &&
    input.amount >= PAYMENT_AMOUNT_MIN &&
    input.amount <= PAYMENT_AMOUNT_MAX &&
    isPaymentMethod(input.method) &&
    (notes === undefined || notes.length <= PAYMENT_NOTES_MAX_LENGTH);

  if (!isValid) {
    throw new PaymentServiceError('INVALID_INPUT');
  }

  const appointment = MOCK_APPOINTMENTS.find((item) => item.id === input.appointmentId);

  if (!appointment) {
    throw new PaymentServiceError('APPOINTMENT_NOT_FOUND');
  }

  if (store.some((payment) => payment.appointmentId === appointment.id)) {
    throw new PaymentServiceError('APPOINTMENT_ALREADY_PAID');
  }

  const payment: Payment = {
    id: `pay-${String(nextPaymentNumber).padStart(3, '0')}`,
    patientName: appointment.patientName,
    appointmentId: appointment.id,
    appointmentDate: appointment.appointmentDate,
    amount: input.amount,
    method: input.method,
    
    status: 'paid',
    paidAt: new Date().toISOString(),
    ...(notes ? {notes} : {})
  };

  nextPaymentNumber += 1;
  store = [payment, ...store];

  return payment;
}

export async function updatePaymentStatus(
  input: UpdatePaymentStatusInput
): Promise<Payment> {
  await simulateRequest();

  if (!isPaymentStatus(input.status)) {
    throw new PaymentServiceError('INVALID_INPUT');
  }

  const existing = store.find((payment) => payment.id === input.id);

  if (!existing) {
    throw new PaymentServiceError('PAYMENT_NOT_FOUND');
  }

  const updated: Payment = {...existing, status: input.status};
  store = store.map((payment) => (payment.id === updated.id ? updated : payment));

  return updated;
}