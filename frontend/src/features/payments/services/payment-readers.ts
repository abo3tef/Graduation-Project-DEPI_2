import {createInvalidResponseError, readPagedResult} from '@/lib/api';
import type {PagedResult} from '@/types/paging';
import {
  type Payment,
  type PaymentAppointmentOption
} from '../types/payment.types';
import {
  fromApiPaymentMethod,
  fromApiPaymentStatus,
  isApiPaymentMethod,
  isApiPaymentStatus
} from './payment-enums';

/**
 * The boundary where untrusted payment JSON becomes typed data. The API sends
 * enums as PascalCase labels (`Cash`, `Paid`); the UI keeps lower-case values,
 * so every response item is converted here.
 */
function fail(): never {
  throw createInvalidResponseError(200);
}

function record(value: unknown): Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : fail();
}

function text(value: unknown): string {
  return typeof value === 'string' ? value : fail();
}

function nullableText(value: unknown): string | null {
  return value === null || value === undefined ? null : text(value);
}

function amount(value: unknown): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fail();
}

export function readPayment(value: unknown): Payment {
  const source = record(value);
  const method = source.method;
  const status = source.status;

  return {
    id: text(source.id),
    patientId: text(source.patientId),
    patientName: text(source.patientName),
    doctorId: text(source.doctorId),
    appointmentId: text(source.appointmentId),
    appointmentDate: text(source.appointmentDate),
    amount: amount(source.amount),
    method: isApiPaymentMethod(method) ? fromApiPaymentMethod(method) : fail(),
    status: isApiPaymentStatus(status) ? fromApiPaymentStatus(status) : fail(),
    paidAt: nullableText(source.paidAt),
    notes: nullableText(source.notes)
  };
}

export function readPaymentPage(value: unknown): PagedResult<Payment> {
  return readPagedResult(value, readPayment);
}

export function readPayableAppointments(
  value: unknown
): readonly PaymentAppointmentOption[] {
  if (!Array.isArray(value)) {
    fail();
  }

  return (value as unknown[]).map((item) => {
    const source = record(item);

    return {
      id: text(source.id),
      patientId: text(source.patientId),
      patientName: text(source.patientName),
      doctorId: text(source.doctorId),
      appointmentDate: text(source.appointmentDate),
      startTime: text(source.startTime)
    };
  });
}
