import {apiRequest} from '@/lib/api';
import {
  PATIENT_SEARCH_LIMIT,
  type Appointment,
  type AppointmentFilters,
  type AppointmentStatus,
  type BookAppointmentInput,
  type DoctorLookup,
  type PagedResult,
  type PatientLookup,
  type RescheduleAppointmentInput
} from '../types/appointment.types';
import {normalizeTime} from '../utils/date-time';
import {
  readAppointment,
  readAppointmentPage,
  readDoctorLookups,
  readPatientLookups
} from './appointment-readers';

const BASE_PATH = '/api/appointments';

export async function listAppointments(
  filters: AppointmentFilters,
  signal?: AbortSignal
): Promise<PagedResult<Appointment>> {
  const data = await apiRequest<unknown>(BASE_PATH, {
    query: {
      doctorId: filters.doctorId,
      status: filters.status === 'all' ? undefined : filters.status,
      dateFrom: filters.dateFrom,
      dateTo: filters.dateTo,
      pageNumber: filters.pageNumber,
      pageSize: filters.pageSize
    },
    signal
  });

  return readAppointmentPage(data);
}

export async function bookAppointment(input: BookAppointmentInput): Promise<Appointment> {
  const data = await apiRequest<unknown>(BASE_PATH, {
    method: 'POST',
    body: {
      patientId: input.patientId,
      doctorId: input.doctorId,
      appointmentDate: input.appointmentDate,
      startTime: normalizeTime(input.startTime),
      endTime: normalizeTime(input.endTime),
      reason: input.reason.trim() === '' ? null : input.reason.trim()
    }
  });

  return readAppointment(data);
}

export async function rescheduleAppointment(
  id: string,
  input: RescheduleAppointmentInput
): Promise<Appointment> {
  const data = await apiRequest<unknown>(`${BASE_PATH}/${encodeURIComponent(id)}/schedule`, {
    method: 'PUT',
    body: {
      appointmentDate: input.appointmentDate,
      startTime: normalizeTime(input.startTime),
      endTime: normalizeTime(input.endTime)
    }
  });

  return readAppointment(data);
}

export async function cancelAppointment(
  id: string,
  cancellationReason: string
): Promise<Appointment> {
  const data = await apiRequest<unknown>(`${BASE_PATH}/${encodeURIComponent(id)}/cancel`, {
    method: 'POST',
    body: {cancellationReason: cancellationReason.trim()}
  });

  return readAppointment(data);
}

/** `Confirmed`, `Completed` or `NoShow`; cancelling uses `cancelAppointment`. */
export async function updateAppointmentStatus(
  id: string,
  status: Exclude<AppointmentStatus, 'Scheduled' | 'Cancelled'>
): Promise<Appointment> {
  const data = await apiRequest<unknown>(`${BASE_PATH}/${encodeURIComponent(id)}/status`, {
    method: 'PUT',
    body: {status}
  });

  return readAppointment(data);
}

export async function listDoctorLookups(signal?: AbortSignal): Promise<readonly DoctorLookup[]> {
  const data = await apiRequest<unknown>(`${BASE_PATH}/lookups/doctors`, {signal});

  return readDoctorLookups(data);
}

export async function searchPatientLookups(
  doctorId: string,
  search: string,
  signal?: AbortSignal
): Promise<readonly PatientLookup[]> {
  const data = await apiRequest<unknown>(`${BASE_PATH}/lookups/patients`, {
    query: {doctorId, search: search.trim(), limit: PATIENT_SEARCH_LIMIT},
    signal
  });

  return readPatientLookups(data);
}
