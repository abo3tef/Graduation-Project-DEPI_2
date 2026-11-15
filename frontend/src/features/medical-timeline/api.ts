import {readPagedResult, request} from '@/lib/api';
import type {PagedResult} from '@/types/paging';
import {expectPatientMedicalProfile, expectPatientTimelineEvent} from './guards';
import type {
  PatientMedicalProfileResponse,
  PatientTimelineEventResponse,
  PatientTimelineQueryParams
} from './types';

/** Both doctor-facing read endpoints live on the patient resource (PATIENT_MEDICAL_API.md §2). */
const PATIENT_BASE_PATH = '/api/patients';

/**
 * One page of the patient's medical timeline (§3.3). Optional filters are skipped from
 * the query string by the shared client; `pageNumber`/`pageSize` are always sent.
 */
export async function getPatientTimeline(
  patientId: string,
  params: PatientTimelineQueryParams,
  token: string,
  signal?: AbortSignal
): Promise<PagedResult<PatientTimelineEventResponse>> {
  const data = await request<unknown>(
    `${PATIENT_BASE_PATH}/${encodeURIComponent(patientId)}/timeline`,
    {
      query: {
        eventType: params.eventType,
        from: params.from,
        to: params.to,
        pageNumber: params.pageNumber,
        pageSize: params.pageSize
      },
      token,
      signal
    }
  );

  return readPagedResult(data, expectPatientTimelineEvent);
}

/** The patient's medical profile with allergies and chronic conditions (§3.1). */
export async function getMedicalProfile(
  patientId: string,
  token: string,
  signal?: AbortSignal
): Promise<PatientMedicalProfileResponse> {
  const data = await request<unknown>(
    `${PATIENT_BASE_PATH}/${encodeURIComponent(patientId)}/medical-profile`,
    {token, signal}
  );

  return expectPatientMedicalProfile(data);
}
