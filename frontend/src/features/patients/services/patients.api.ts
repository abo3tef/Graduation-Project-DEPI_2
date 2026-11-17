import {apiRequest, apiRequestVoid} from '@/lib/api';
import type {PagedResult} from '@/types/paging';
import {
  PATIENT_LOOKUP_LIMIT,
  type DoctorOption,
  type Patient,
  type PatientFilters,
  type PatientOption,
  type SavePatientInput
} from '../types/patient.types';
import {readDoctorOptions, readPatient, readPatientOptions, readPatientPage} from './patient-readers';

const BASE_PATH = '/api/patients';

export async function listPatients(
  filters: PatientFilters,
  signal?: AbortSignal
): Promise<PagedResult<Patient>> {
  const data = await apiRequest<unknown>(BASE_PATH, {
    query: {
      search: filters.search.trim(),
      gender: filters.gender === 'all' ? undefined : filters.gender,
      pageNumber: filters.pageNumber,
      pageSize: filters.pageSize
    },
    signal
  });

  return readPatientPage(data);
}

export async function getPatient(id: string, signal?: AbortSignal): Promise<Patient> {
  return readPatient(await apiRequest<unknown>(`${BASE_PATH}/${id}`, {signal}));
}

export async function createPatient(input: SavePatientInput): Promise<Patient> {
  return readPatient(await apiRequest<unknown>(BASE_PATH, {method: 'POST', body: toBody(input)}));
}

export async function updatePatient(id: string, input: SavePatientInput): Promise<Patient> {
  return readPatient(
    await apiRequest<unknown>(`${BASE_PATH}/${id}`, {method: 'PUT', body: toBody(input)})
  );
}

export async function deletePatient(id: string): Promise<void> {
  await apiRequestVoid(`${BASE_PATH}/${id}`, {method: 'DELETE'});
}

/** Small bounded search that powers the patient picker (id, name, phone only). */
export async function lookupPatients(
  search: string,
  doctorId: string | undefined,
  signal?: AbortSignal
): Promise<readonly PatientOption[]> {
  const data = await apiRequest<unknown>(`${BASE_PATH}/lookup`, {
    query: {search: search.trim(), doctorId, limit: PATIENT_LOOKUP_LIMIT},
    signal
  });

  return readPatientOptions(data);
}

/** Doctors the caller may create patients for (shared appointment doctor lookup, Q-F8-4). */
export async function listDoctorOptions(signal?: AbortSignal): Promise<readonly DoctorOption[]> {
  const data = await apiRequest<unknown>('/api/appointments/lookups/doctors', {signal});

  return readDoctorOptions(data);
}

/** Only the fields the API accepts; absent keys are left out so a secretary never sends `bloodType`. */
function toBody(input: SavePatientInput): Record<string, unknown> {
  const body: Record<string, unknown> = {
    fullName: input.fullName,
    dateOfBirth: input.dateOfBirth,
    gender: input.gender,
    phone: input.phone,
    address: input.address,
    emergencyContact: input.emergencyContact
  };

  if (input.doctorId !== undefined) {
    body.doctorId = input.doctorId;
  }

  if (input.bloodType !== undefined) {
    body.bloodType = input.bloodType;
  }

  return body;
}
