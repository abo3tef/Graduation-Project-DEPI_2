import {isApiError} from '@/lib/api';

export type PatientErrorKey =
  | 'network'
  | 'invalidResponse'
  | 'identity'
  | 'hasRelatedRecords'
  | 'patientNotFound'
  | 'doctorNotFound'
  | 'medicalForbidden'
  | 'validation'
  | 'forbidden'
  | 'notFound'
  | 'conflict'
  | 'badRequest'
  | 'server'
  | 'unknown';

/** API code to message key. Codes are listed in backend/docs/api/PATIENTS_API.md. */
const CODE_TO_KEY: Readonly<Record<string, PatientErrorKey>> = {
  'identity.invalid': 'identity',
  'patient.has_related_records': 'hasRelatedRecords',
  'patient.not_found': 'patientNotFound',
  'doctor.not_found': 'doctorNotFound',
  'patient.medical_field_forbidden': 'medicalForbidden',
  'validation.failed': 'validation',
  'server.error': 'server'
};

function keyFromStatus(status: number): PatientErrorKey {
  if (status === 0) return 'network';
  if (status === 400) return 'badRequest';
  if (status === 403) return 'forbidden';
  if (status === 404) return 'notFound';
  if (status === 409) return 'conflict';

  return status >= 500 ? 'server' : 'unknown';
}

/** The API code wins, then the HTTP status. Server English text is never shown. */
export function getPatientErrorKey(error: unknown): PatientErrorKey {
  if (!isApiError(error)) {
    return 'unknown';
  }

  if (error.kind === 'network') return 'network';
  if (error.kind === 'invalid-response') return 'invalidResponse';

  const byCode = error.code === null ? undefined : CODE_TO_KEY[error.code];

  return byCode ?? keyFromStatus(error.status);
}

export function isRetryableError(error: unknown): boolean {
  return isApiError(error) && (error.status === 0 || error.status >= 500);
}

/** Names (camelCase) of the fields the server rejected, so the form can mark them. */
export function serverFieldNames(error: unknown): ReadonlySet<string> {
  return isApiError(error) ? new Set(Object.keys(error.fieldErrors)) : new Set();
}
