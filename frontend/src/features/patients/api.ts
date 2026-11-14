import {apiRequest} from '@/lib/api';
import type {PagedResult} from '@/types/paging';
import {readMedicalProfile, readPatientLookups, readVisitPage} from './services/patient-readers';
import {PATIENT_SEARCH_LIMIT} from './validation';
import type {
  PatientLookup,
  PatientMedicalProfileResponse,
  PatientVisitResponse
} from './types';

/**
 * Thin wrappers over the shared client. Every response is narrowed by a reader
 * before it reaches a screen. All endpoints are doctor-only reads; the identity
 * headers are attached by the shared auth-headers providers (D-3).
 */
const PATIENTS_BASE = '/api/patients';
const LOOKUP_PATH = '/api/appointments/lookups/patients';

/**
 * The acting doctor's own patients by name or phone (F1-C4 contract). The
 * backend ignores any `doctorId` that is not the caller's own id, so passing
 * the acting doctor's id is a scope selector, never an authorization input.
 */
export async function searchOwnPatients(
  doctorId: string,
  search: string,
  signal?: AbortSignal
): Promise<readonly PatientLookup[]> {
  const data = await apiRequest<unknown>(LOOKUP_PATH, {
    query: {
      doctorId,
      search: search.trim() === '' ? undefined : search.trim(),
      limit: PATIENT_SEARCH_LIMIT
    },
    signal
  });

  return readPatientLookups(data);
}

/** The patient's medical profile with allergies and chronic conditions (§3.1). */
export async function getMedicalProfile(
  patientId: string,
  signal?: AbortSignal
): Promise<PatientMedicalProfileResponse> {
  const data = await apiRequest<unknown>(
    `${PATIENTS_BASE}/${encodeURIComponent(patientId)}/medical-profile`,
    {signal}
  );

  return readMedicalProfile(data);
}

/**
 * One page of the patient's visits with their children, newest first (§3.2).
 * Paging follows the shared Q-04 contract (1..50, default 10).
 */
export async function listPatientVisits(
  patientId: string,
  pageNumber: number,
  pageSize: number,
  signal?: AbortSignal
): Promise<PagedResult<PatientVisitResponse>> {
  const data = await apiRequest<unknown>(
    `${PATIENTS_BASE}/${encodeURIComponent(patientId)}/medical-visits`,
    {query: {pageNumber, pageSize}, signal}
  );

  return readVisitPage(data);
}
