import {apiRequest} from '@/lib/api';
import type {PagedResult} from '@/types/paging';
import {
  PAYABLE_APPOINTMENTS_LIMIT,
  type CreatePaymentInput,
  type Payment,
  type PaymentAppointmentOption,
  type PaymentFilters,
  type UpdatePaymentStatusInput
} from '../types/payment.types';
import {toApiPaymentMethod, toApiPaymentStatus} from './payment-enums';
import {
  readPayment,
  readPaymentPage,
  readPayableAppointments
} from './payment-readers';

const BASE_PATH = '/api/payments';

/**
 * The create request always produces a `Pending` payment. When the user asked
 * for "fully paid now", the follow-up status call may fail after the payment
 * already exists; the dialog reports that the payment was saved as pending.
 */
export class PaymentPartiallyRecordedError extends Error {
  readonly payment: Payment;
  readonly causeError: unknown;

  constructor(payment: Payment, causeError: unknown) {
    super('The payment was recorded as pending but could not be marked as paid.');
    this.name = 'PaymentPartiallyRecordedError';
    this.payment = payment;
    this.causeError = causeError;
  }
}

export function isPaymentPartiallyRecordedError(
  error: unknown
): error is PaymentPartiallyRecordedError {
  return error instanceof PaymentPartiallyRecordedError;
}

function searchQuery(search: string | undefined): string | undefined {
  const trimmed = search?.trim() ?? '';
  return trimmed === '' ? undefined : trimmed;
}

function notesBody(notes: string | undefined): string | null {
  const trimmed = notes?.trim() ?? '';
  return trimmed === '' ? null : trimmed;
}

export async function listPayments(
  filters: PaymentFilters,
  signal?: AbortSignal
): Promise<PagedResult<Payment>> {
  const data = await apiRequest<unknown>(BASE_PATH, {
    query: {
      search: searchQuery(filters.search),
      status:
        filters.status && filters.status !== 'all'
          ? toApiPaymentStatus(filters.status)
          : undefined,
      method:
        filters.method && filters.method !== 'all'
          ? toApiPaymentMethod(filters.method)
          : undefined,
      pageNumber: filters.page,
      pageSize: filters.pageSize
    },
    signal
  });

  return readPaymentPage(data);
}

export async function listPayableAppointments(
  signal?: AbortSignal
): Promise<readonly PaymentAppointmentOption[]> {
  const data = await apiRequest<unknown>(`${BASE_PATH}/payable-appointments`, {
    query: {limit: PAYABLE_APPOINTMENTS_LIMIT},
    signal
  });

  return readPayableAppointments(data);
}

export async function createPayment(
  input: CreatePaymentInput
): Promise<Payment> {
  const data = await apiRequest<unknown>(BASE_PATH, {
    method: 'POST',
    body: {
      appointmentId: input.appointmentId,
      amount: input.amount,
      method: toApiPaymentMethod(input.method),
      notes: notesBody(input.notes)
    }
  });
  const created = readPayment(data);

  if (!input.recordAsPaid || created.status === 'paid') {
    return created;
  }

  try {
    return await updatePaymentStatus({id: created.id, status: 'paid'});
  } catch (error) {
    throw new PaymentPartiallyRecordedError(created, error);
  }
}

export async function updatePaymentStatus(
  input: UpdatePaymentStatusInput
): Promise<Payment> {
  const data = await apiRequest<unknown>(
    `${BASE_PATH}/${encodeURIComponent(input.id)}/status`,
    {
      method: 'PUT',
      body: {status: toApiPaymentStatus(input.status)}
    }
  );

  return readPayment(data);
}
