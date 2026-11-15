/**
 * Wire types for the patient medical timeline and profile
 * (backend/docs/api/PATIENT_MEDICAL_API.md §3.1/§3.3, frozen by F10-C1).
 *
 * Nullable members are typed `string | null` because the API serialises explicit
 * nulls, not omitted properties. Dates are ISO strings: `occurredAt` is an
 * instant, `dateOfBirth`/`diagnosisDate` are plain `YYYY-MM-DD`.
 */

/** The four timeline event types delivered by F10-C1 (decision Q-20). */
export type PatientTimelineEventType = 'Visit' | 'Diagnosis' | 'Medication' | 'MedicalNote';

export type PatientTimelineEventResponse = {
  id: string;
  type: PatientTimelineEventType;
  occurredAt: string;
  title: string;
  summary: string | null;
  relatedVisitId: string | null;
};

/** Timeline filters and paging, mirroring the §3.3 query parameters. */
export type PatientTimelineQueryParams = {
  eventType?: PatientTimelineEventType;
  /** ISO-8601 instants; `from <= to` is enforced by the backend. */
  from?: string;
  to?: string;
  pageNumber: number;
  pageSize: number;
};

export type PatientAllergyResponse = {
  id: string;
  name: string;
  severity: string | null;
  description: string | null;
  notes: string | null;
};

export type PatientChronicConditionResponse = {
  id: string;
  name: string;
  diagnosisDate: string | null;
  status: string | null;
  notes: string | null;
};

export type PatientMedicalProfileResponse = {
  patientId: string;
  fullName: string;
  dateOfBirth: string;
  age: number;
  gender: string;
  phone: string | null;
  address: string | null;
  bloodType: string | null;
  emergencyContact: string | null;
  allergies: PatientAllergyResponse[];
  chronicConditions: PatientChronicConditionResponse[];
};
