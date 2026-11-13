import {createInvalidResponseError, readPagedResult} from '@/lib/api';
import type {PagedResult} from '@/types/paging';
import {
  isAppointmentStatus,
  type Appointment,
  type DoctorLookup,
  type PatientLookup
} from '../types/appointment.types';

/**
 * The boundary where untrusted API JSON becomes typed data. The project has no
 * schema library, so these small readers check exactly the fields the screens
 * use and reject anything else as an invalid response.
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

export function readAppointment(value: unknown): Appointment {
  const source = record(value);
  const status = source.status;

  return {
    id: text(source.id),
    patientId: text(source.patientId),
    patientName: text(source.patientName),
    patientPhone: nullableText(source.patientPhone),
    doctorId: text(source.doctorId),
    doctorName: text(source.doctorName),
    appointmentDate: text(source.appointmentDate),
    startTime: text(source.startTime),
    endTime: nullableText(source.endTime),
    reason: nullableText(source.reason),
    status: isAppointmentStatus(status) ? status : fail(),
    notes: nullableText(source.notes),
    cancellationReason: nullableText(source.cancellationReason),
    createdBy: text(source.createdBy),
    createdAtUtc: text(source.createdAtUtc)
  };
}

export function readAppointmentPage(value: unknown): PagedResult<Appointment> {
  return readPagedResult(value, readAppointment);
}

export function readDoctorLookups(value: unknown): readonly DoctorLookup[] {
  if (!Array.isArray(value)) {
    fail();
  }

  return (value as unknown[]).map((item) => {
    const source = record(item);

    return {
      id: text(source.id),
      fullName: text(source.fullName),
      specialization: nullableText(source.specialization)
    };
  });
}

export function readPatientLookups(value: unknown): readonly PatientLookup[] {
  if (!Array.isArray(value)) {
    fail();
  }

  return (value as unknown[]).map((item) => {
    const source = record(item);

    return {
      id: text(source.id),
      fullName: text(source.fullName),
      phone: nullableText(source.phone)
    };
  });
}
