import {isApiError, type ApiError} from '@/lib/api';

/** Keys of the `Patients.errors` messages. */
export type PatientErrorKey =
  | 'network'
  | 'invalidResponse'
  | 'identity'
  | 'forbidden'
  | 'notFound'
  | 'badRequest'
  | 'server'
  | 'unknown';

/** API `code` → message key. Codes are listed in `PATIENT_MEDICAL_API.md` §4. */
const CODE_TO_KEY: Readonly<Record<string, PatientErrorKey>> = {
  'auth.unauthorized': 'identity',
  'identity.invalid': 'identity',
  'access.forbidden': 'forbidden',
  'medical.access_denied': 'forbidden',
  'patient.not_found': 'notFound',
  'validation.failed': 'badRequest'
};

function keyFromStatus(status: number): PatientErrorKey {
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

  return status >= 500 ? 'server' : 'unknown';
}

/**
 * Chooses the message to show for any failure. The API `code` wins; otherwise
 * the HTTP status decides. Server English text is never shown to the user.
 */
export function getPatientErrorKey(error: unknown): PatientErrorKey {
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

export type {ApiError};
