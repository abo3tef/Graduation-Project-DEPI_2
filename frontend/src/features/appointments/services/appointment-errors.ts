import {isApiError, type ApiError} from '@/lib/api';
import type {ValidationKey} from '../validation/appointment.validation';

/** Keys of the `Appointments.errors` messages. */
export type AppointmentErrorKey =
  | 'network'
  | 'invalidResponse'
  | 'identity'
  | 'completeRequiresDoctor'
  | 'doctorNotFound'
  | 'patientNotFound'
  | 'appointmentNotFound'
  | 'overlap'
  | 'doctorUnavailable'
  | 'scheduleBusy'
  | 'validation'
  | 'timeNotInTimeZone'
  | 'inPast'
  | 'invalidInterval'
  | 'endTimeRequired'
  | 'notOpen'
  | 'invalidTransition'
  | 'cancellationReasonRequired'
  | 'cancellationReasonTooLong'
  | 'forbidden'
  | 'notFound'
  | 'conflict'
  | 'badRequest'
  | 'server'
  | 'unknown';

/** API `code` → message key. Codes are listed in `backend/docs/api/appointments.md`. */
const CODE_TO_KEY: Readonly<Record<string, AppointmentErrorKey>> = {
  'identity.invalid': 'identity',
  'appointment.complete_requires_doctor': 'completeRequiresDoctor',
  'doctor.not_found': 'doctorNotFound',
  'patient.not_found': 'patientNotFound',
  'appointment.not_found': 'appointmentNotFound',
  'appointment.overlap': 'overlap',
  'doctor.unavailable': 'doctorUnavailable',
  'schedule.busy': 'scheduleBusy',
  'validation.failed': 'validation',
  'appointment.time_not_in_time_zone': 'timeNotInTimeZone',
  'appointment.in_the_past': 'inPast',
  'appointment.invalid_interval': 'invalidInterval',
  'appointment.end_time_required': 'endTimeRequired',
  'appointment.not_open': 'notOpen',
  'appointment.invalid_status_transition': 'invalidTransition',
  'appointment.use_cancel_operation': 'invalidTransition',
  'appointment.cancellation_reason_required': 'cancellationReasonRequired',
  'appointment.cancellation_reason_too_long': 'cancellationReasonTooLong',
  'appointment.missing_reference': 'badRequest',
  'domain.rule_violation': 'badRequest',
  'server.error': 'server'
};

function keyFromStatus(status: number): AppointmentErrorKey {
  if (status === 0) {
    return 'network';
  }

  if (status === 400) {
    return 'badRequest';
  }

  if (status === 403) {
    return 'forbidden';
  }

  if (status === 404) {
    return 'notFound';
  }

  if (status === 409) {
    return 'conflict';
  }

  return status >= 500 ? 'server' : 'unknown';
}

/**
 * Chooses the message to show for any failure. The API `code` wins; otherwise
 * the HTTP status decides. Server English text is never shown to the user.
 */
export function getAppointmentErrorKey(error: unknown): AppointmentErrorKey {
  if (!isApiError(error)) {
    return 'unknown';
  }

  if (error.kind === 'network') {
    return 'network';
  }

  if (error.kind === 'invalid-response') {
    return 'invalidResponse';
  }

  if (error.code !== null && error.code in CODE_TO_KEY) {
    return CODE_TO_KEY[error.code] ?? keyFromStatus(error.status);
  }

  return keyFromStatus(error.status);
}

/** True for failures worth retrying: no connection or a server-side error. */
export function isRetryableError(error: unknown): boolean {
  return isApiError(error) && (error.status === 0 || error.status >= 500);
}

const VALIDATOR_CODE_TO_KEY: Readonly<Record<string, ValidationKey>> = {
  NotEmptyValidator: 'required',
  NotNullValidator: 'required',
  'appointment.time_not_in_time_zone': 'timeNotInTimeZone'
};

function fieldCodeToKey(field: string, code: string): ValidationKey {
  const known = VALIDATOR_CODE_TO_KEY[code];

  if (known) {
    return known;
  }

  if (code === 'MaximumLengthValidator') {
    return field === 'cancellationReason' ? 'cancelReasonTooLong' : 'reasonTooLong';
  }

  return 'invalid';
}

/**
 * Maps the API's per-field validator codes to validation message keys for the
 * given form fields. Fields the API did not flag are omitted. A field without
 * codes (for example the framework's own 400s) falls back to `invalid`.
 */
export function mapFieldErrors<TField extends string>(
  error: ApiError,
  fields: readonly TField[]
): Partial<Record<TField, ValidationKey>> {
  const result: Partial<Record<TField, ValidationKey>> = {};

  for (const field of fields) {
    const codes = error.fieldErrorCodes[field];
    const messages = error.fieldErrors[field];

    if (codes && codes.length > 0) {
      result[field] = fieldCodeToKey(field, codes[0] ?? '');
    } else if (messages && messages.length > 0) {
      result[field] = 'invalid';
    }
  }

  return result;
}
