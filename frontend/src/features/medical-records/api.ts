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
