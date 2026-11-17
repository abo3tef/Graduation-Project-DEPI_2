import {ApiError, readPagedResult, request, requestVoid} from '@/lib/api';
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
