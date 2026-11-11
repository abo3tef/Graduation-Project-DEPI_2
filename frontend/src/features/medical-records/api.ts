import {request, requestVoid} from '@/lib/api';
import {expectDiagnosis, expectMedicalVisit, expectMedication} from './guards';
import type {
  CreateDiagnosisRequest,
  CreateMedicalVisitRequest,
  CreateMedicationRequest,
  DiagnosisResponse,
  MedicalVisitResponse,
  MedicationResponse,
  UpdateDiagnosisRequest,
  UpdateMedicalVisitRequest,
  UpdateMedicationRequest
} from './types';

const BASE_PATH = '/api/medical-visits';

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
