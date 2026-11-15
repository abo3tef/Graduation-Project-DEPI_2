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

const BASE_PATH = '/api/medical-visits';

function requireVitalSignsToken(token: string): void {
  if (!token.trim()) {
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
    token, signal, query: {pageNumber, pageSize}
  });
  return readPagedResult(data, expectVitalSigns);
}

export async function createVitalSigns(
  visitId: string, payload: SaveVitalSignsRequest, token: string
): Promise<VitalSignsResponse> {
  requireVitalSignsToken(token);
  const data = await request<unknown>(`${BASE_PATH}/${encodeURIComponent(visitId)}/vital-signs`, {
    method: 'POST', body: vitalSignsBody(payload), token
  });
  return expectVitalSigns(data);
}

export async function updateVitalSigns(
  visitId: string, vitalSignsId: string, payload: SaveVitalSignsRequest, token: string
): Promise<VitalSignsResponse> {
  requireVitalSignsToken(token);
  const data = await request<unknown>(
    `${BASE_PATH}/${encodeURIComponent(visitId)}/vital-signs/${encodeURIComponent(vitalSignsId)}`,
    {method: 'PUT', body: vitalSignsBody(payload), token}
  );
  return expectVitalSigns(data);
}

export async function getMedicalVisit(
  visitId: string,
  token: string,
  signal?: AbortSignal
): Promise<MedicalVisitResponse> {
  const data = await request<unknown>(
    `${BASE_PATH}/${encodeURIComponent(visitId)}`,
    {token, signal}
  );

  return expectMedicalVisit(data);
}

export async function createMedicalVisit(
  payload: CreateMedicalVisitRequest,
  token: string
): Promise<MedicalVisitResponse> {
  const data = await request<unknown>(BASE_PATH, {
    method: 'POST',
    body: payload,
    token
  });

  return expectMedicalVisit(data);
}

export async function updateMedicalVisit(
  visitId: string,
  payload: UpdateMedicalVisitRequest,
  token: string
): Promise<MedicalVisitResponse> {
  const data = await request<unknown>(
    `${BASE_PATH}/${encodeURIComponent(visitId)}`,
    {method: 'PUT', body: payload, token}
  );

  return expectMedicalVisit(data);
}

export async function createDiagnosis(
  visitId: string,
  payload: CreateDiagnosisRequest,
  token: string
): Promise<DiagnosisResponse> {
  const data = await request<unknown>(
    `${BASE_PATH}/${encodeURIComponent(visitId)}/diagnoses`,
    {method: 'POST', body: payload, token}
  );

  return expectDiagnosis(data);
}

export async function updateDiagnosis(
  visitId: string,
  diagnosisId: string,
  payload: UpdateDiagnosisRequest,
  token: string
): Promise<DiagnosisResponse> {
  const data = await request<unknown>(
    `${BASE_PATH}/${encodeURIComponent(visitId)}/diagnoses/${encodeURIComponent(diagnosisId)}`,
    {method: 'PUT', body: payload, token}
  );

  return expectDiagnosis(data);
}

export async function createMedication(
  visitId: string,
  payload: CreateMedicationRequest,
  token: string
): Promise<MedicationResponse> {
  const data = await request<unknown>(
    `${BASE_PATH}/${encodeURIComponent(visitId)}/medications`,
    {method: 'POST', body: payload, token}
  );

  return expectMedication(data);
}

export async function updateMedication(
  visitId: string,
  medicationId: string,
  payload: UpdateMedicationRequest,
  token: string
): Promise<MedicationResponse> {
  const data = await request<unknown>(
    `${BASE_PATH}/${encodeURIComponent(visitId)}/medications/${encodeURIComponent(medicationId)}`,
    {method: 'PUT', body: payload, token}
  );

  return expectMedication(data);
}

export async function deleteMedication(
  visitId: string,
  medicationId: string,
  token: string
): Promise<void> {
  await requestVoid(
    `${BASE_PATH}/${encodeURIComponent(visitId)}/medications/${encodeURIComponent(medicationId)}`,
    {method: 'DELETE', token}
  );
}
