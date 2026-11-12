/**
 * Types mirror the API contract in `backend/docs/api/appointments.md`.
 * Dates are `yyyy-MM-dd` and times `HH:mm:ss` strings in the clinic time zone;
 * they are never converted to `Date` objects for storage or transport.
 */
export const APPOINTMENT_STATUSES = [
  'Scheduled',
  'Confirmed',
  'Completed',
  'Cancelled',
  'NoShow'
] as const;
export type AppointmentStatus = (typeof APPOINTMENT_STATUSES)[number];

export function isAppointmentStatus(value: unknown): value is AppointmentStatus {
  return (APPOINTMENT_STATUSES as readonly unknown[]).includes(value);
}

/** Statuses that still hold their time slot (Cancelled and NoShow free it). */
export function holdsSlot(status: AppointmentStatus): boolean {
  return status !== 'Cancelled' && status !== 'NoShow';
}

export const REASON_MAX_LENGTH = 1000;
export const CANCELLATION_REASON_MAX_LENGTH = 500;
export const DEFAULT_PAGE_SIZE = 10;
export const MAX_PAGE_SIZE = 100;
export const PATIENT_SEARCH_MIN_LENGTH = 2;
export const PATIENT_SEARCH_LIMIT = 10;

export type Appointment = {
  readonly id: string;
  readonly patientId: string;
  readonly patientName: string;
  readonly patientPhone: string | null;
  readonly doctorId: string;
  readonly doctorName: string;
  /** yyyy-MM-dd, clinic local */
  readonly appointmentDate: string;
  /** HH:mm:ss, clinic local */
  readonly startTime: string;
  /** HH:mm:ss, clinic local; null only for legacy rows */
  readonly endTime: string | null;
  readonly reason: string | null;
  readonly status: AppointmentStatus;
  readonly notes: string | null;
  readonly cancellationReason: string | null;
  readonly createdBy: string;
  readonly createdAtUtc: string;
};

export type PagedResult<T> = {
  readonly items: readonly T[];
  readonly pageNumber: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly totalPages: number;
};

export type DoctorLookup = {
  readonly id: string;
  readonly fullName: string;
  readonly specialization: string | null;
};

export type PatientLookup = {
  readonly id: string;
  readonly fullName: string;
  readonly phone: string | null;
};

export type AppointmentFilters = {
  /** Empty string = all doctors in the caller's scope. */
  doctorId: string;
  status: AppointmentStatus | 'all';
  /** yyyy-MM-dd or '' */
  dateFrom: string;
  /** yyyy-MM-dd or '' */
  dateTo: string;
  pageNumber: number;
  pageSize: number;
};

export type BookAppointmentInput = {
  patientId: string;
  doctorId: string;
  /** yyyy-MM-dd */
  appointmentDate: string;
  /** HH:mm or HH:mm:ss */
  startTime: string;
  /** HH:mm or HH:mm:ss */
  endTime: string;
  reason: string;
};

export type RescheduleAppointmentInput = {
  appointmentDate: string;
  startTime: string;
  endTime: string;
};
