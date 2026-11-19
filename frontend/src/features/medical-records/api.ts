import {ApiError, createInvalidResponseError, readPagedResult, request, requestVoid} from '@/lib/api';
import {DEFAULT_PAGE_SIZE, type PagedResult} from '@/types/paging';
import {expectDiagnosis, expectMedicalVisit, expectMedication, expectVitalSigns} from './guards';
import type {
  CreateDiagnosisRequest,
  CreateMedicalVisitRequest,
  CreateMedicationRequest,
  DiagnosisResponse,
  MedicalVisitResponse,
  MedicationResponse,
  UpdateDiagnosisRequest,
  UpdateMedicalVisitRequest,
  UpdateMedicationRequest,
  SaveVitalSignsRequest,
  VitalSignsResponse
} from './types';

import {readMedicalToken} from './use-medical-credentials';

const BASE_PATH = '/api/medical-visits';
const PATIENT_BASE_PATH = '/api/patients';

type JsonRecord = Record<string, unknown>;

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function asRecord(value: unknown): JsonRecord {
  if (!isRecord(value)) {
    throw createInvalidResponseError(200);
  }

  return value;
}

function requiredText(value: unknown): string {
  if (typeof value !== 'string') {
    throw createInvalidResponseError(200);
  }

  return value;
}

function optionalText(value: unknown): string | null {
  if (value === null || typeof value === 'string') {
    return value;
  }

  throw createInvalidResponseError(200);
}

function requiredArray(value: unknown): unknown[] {
  if (!Array.isArray(value)) {
    throw createInvalidResponseError(200);
  }

  return value;
}

function requireVitalSignsToken(token: string): void {
  if (!token.trim() || token !== readMedicalToken()) {
    throw new ApiError({kind: 'unauthorized', status: 401});
  }
}

// Explicitly pick all seven fields: extra runtime properties cannot become ownership input.
function vitalSignsBody(payload: SaveVitalSignsRequest): SaveVitalSignsRequest {
  const {bloodPressure, heartRate, temperature, respiratoryRate, weight, height, spO2} = payload;
  return {bloodPressure, heartRate, temperature, respiratoryRate, weight, height, spO2};
}

export async function listVitalSigns(
  visitId: string, token: string, pageNumber = 1, pageSize = DEFAULT_PAGE_SIZE,
  signal?: AbortSignal
): Promise<PagedResult<VitalSignsResponse>> {
  requireVitalSignsToken(token);
  const data = await request<unknown>(`${BASE_PATH}/${encodeURIComponent(visitId)}/vital-signs`, {
    signal, query: {pageNumber, pageSize}
  });
  return readPagedResult(data, expectVitalSigns);
}

export async function createVitalSigns(
  visitId: string, payload: SaveVitalSignsRequest, token: string
): Promise<VitalSignsResponse> {
  requireVitalSignsToken(token);
  const data = await request<unknown>(`${BASE_PATH}/${encodeURIComponent(visitId)}/vital-signs`, {
    method: 'POST', body: vitalSignsBody(payload)
  });
  return expectVitalSigns(data);
}

export async function updateVitalSigns(
  visitId: string, vitalSignsId: string, payload: SaveVitalSignsRequest, token: string
): Promise<VitalSignsResponse> {
  requireVitalSignsToken(token);
  const data = await request<unknown>(
    `${BASE_PATH}/${encodeURIComponent(visitId)}/vital-signs/${encodeURIComponent(vitalSignsId)}`,
    {method: 'PUT', body: vitalSignsBody(payload)}
  );
  return expectVitalSigns(data);
}

export async function getMedicalVisit(
  visitId: string,
  signal?: AbortSignal
): Promise<MedicalVisitResponse> {
  const data = await request<unknown>(
    `${BASE_PATH}/${encodeURIComponent(visitId)}`,
    {signal}
  );

  return expectMedicalVisit(data);
}

/**
 * The patient-history endpoint (`/api/patients/{id}/medical-visits`) returns the F10 visit
 * summary (`visitId`, `name` inside diagnoses/medications, plus notes and lab results), while
 * `expectMedicalVisit` validates the visit-centric contract of `/api/medical-visits`. Translate
 * the summary into that contract so both routes give the UI one validated, typed shape.
 * An item already using the visit-centric shape is passed through unchanged.
 */
function asVisitResponse(value: unknown, patientId: string): unknown {
  if (!isRecord(value)) {
    return value; // expectMedicalVisit rejects it at the boundary.
  }

  if (typeof value.id === 'string') {
    return value;
  }

  const visitId = requiredText(value.visitId);

  return {
    id: visitId,
    patientId,
    doctorId: requiredText(value.doctorId),
    appointmentId: null,
    visitDate: requiredText(value.visitDate),
    chiefComplaint: optionalText(value.chiefComplaint),
    symptoms: optionalText(value.symptoms),
    clinicalNotes: optionalText(value.clinicalNotes),
    diagnoses: requiredArray(value.diagnoses).map((entry) => {
      const diagnosis = asRecord(entry);

      return {
        id: requiredText(diagnosis.id),
        visitId,
        patientId,
        diagnosisName: requiredText(diagnosis.name),
        description: optionalText(diagnosis.description),
        diagnosisDate: requiredText(diagnosis.diagnosisDate)
      };
    }),
    medications: requiredArray(value.medications).map((entry) => {
      const medication = asRecord(entry);

      return {
        id: requiredText(medication.id),
        visitId,
        patientId,
        medicationName: requiredText(medication.name),
        dosage: requiredText(medication.dosage),
        frequency: requiredText(medication.frequency),
        duration: optionalText(medication.duration),
        instructions: optionalText(medication.instructions)
      };
    }),
    vitalSigns: requiredArray(value.vitalSigns)
  };
}

/** One page of a patient's previous visits (newest first, ordering supplied by the backend). */
export async function listPatientMedicalVisits(
  patientId: string,
  pageNumber = 1,
  pageSize = DEFAULT_PAGE_SIZE,
  signal?: AbortSignal
): Promise<PagedResult<MedicalVisitResponse>> {
  const data = await request<unknown>(
    `${PATIENT_BASE_PATH}/${encodeURIComponent(patientId)}/medical-visits`,
    {query: {pageNumber, pageSize}, signal}
  );

  return readPagedResult(data, (item) => expectMedicalVisit(asVisitResponse(item, patientId)));
}

export async function createMedicalVisit(
  payload: CreateMedicalVisitRequest
): Promise<MedicalVisitResponse> {
  const data = await request<unknown>(BASE_PATH, {
    method: 'POST',
    body: payload
  });

  return expectMedicalVisit(data);
}

export async function updateMedicalVisit(
  visitId: string,
  payload: UpdateMedicalVisitRequest
): Promise<MedicalVisitResponse> {
  const data = await request<unknown>(
    `${BASE_PATH}/${encodeURIComponent(visitId)}`,
    {method: 'PUT', body: payload}
  );

  return expectMedicalVisit(data);
}

export async function createDiagnosis(
  visitId: string,
  payload: CreateDiagnosisRequest
): Promise<DiagnosisResponse> {
  const data = await request<unknown>(
    `${BASE_PATH}/${encodeURIComponent(visitId)}/diagnoses`,
    {method: 'POST', body: payload}
  );

  return expectDiagnosis(data);
}

export async function updateDiagnosis(
  visitId: string,
  diagnosisId: string,
  payload: UpdateDiagnosisRequest
): Promise<DiagnosisResponse> {
  const data = await request<unknown>(
    `${BASE_PATH}/${encodeURIComponent(visitId)}/diagnoses/${encodeURIComponent(diagnosisId)}`,
    {method: 'PUT', body: payload}
  );

  return expectDiagnosis(data);
}

export async function deleteDiagnosis(
  visitId: string,
  diagnosisId: string
): Promise<void> {
  await requestVoid(
    `${BASE_PATH}/${encodeURIComponent(visitId)}/diagnoses/${encodeURIComponent(diagnosisId)}`,
    {method: 'DELETE'}
  );
}

export async function createMedication(
  visitId: string,
  payload: CreateMedicationRequest
): Promise<MedicationResponse> {
  const data = await request<unknown>(
    `${BASE_PATH}/${encodeURIComponent(visitId)}/medications`,
    {method: 'POST', body: payload}
  );

  return expectMedication(data);
}

export async function updateMedication(
  visitId: string,
  medicationId: string,
  payload: UpdateMedicationRequest
): Promise<MedicationResponse> {
  const data = await request<unknown>(
    `${BASE_PATH}/${encodeURIComponent(visitId)}/medications/${encodeURIComponent(medicationId)}`,
    {method: 'PUT', body: payload}
  );

  return expectMedication(data);
}

export async function deleteMedication(
  visitId: string,
  medicationId: string
): Promise<void> {
  await requestVoid(
    `${BASE_PATH}/${encodeURIComponent(visitId)}/medications/${encodeURIComponent(medicationId)}`,
    {method: 'DELETE'}
  );
}
