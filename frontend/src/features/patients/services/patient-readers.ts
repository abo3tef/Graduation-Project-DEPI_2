import {createInvalidResponseError, readPagedResult} from '@/lib/api';
import type {PagedResult} from '@/types/paging';
import {isBloodType, isGender, type DoctorOption, type Patient, type PatientOption} from '../types/patient.types';

/** Boundary where untrusted API JSON becomes typed data (the project has no schema library). */
function fail(): never {
  throw createInvalidResponseError(200);
}

function record(value: unknown): Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : fail();
}

function text(value: unknown): string {
  return typeof value === 'string' ? value : fail();
}

function nullableText(value: unknown): string | null {
  return value === null || value === undefined ? null : text(value);
}

export function readPatient(value: unknown): Patient {
  const source = record(value);
  const bloodType = source.bloodType;

  if (!isGender(source.gender) || (bloodType != null && !isBloodType(bloodType))) {
    fail();
  }

  return {
    id: text(source.id),
    doctorId: nullableText(source.doctorId),
    fullName: text(source.fullName),
    dateOfBirth: text(source.dateOfBirth),
    gender: source.gender as Patient['gender'],
    phone: nullableText(source.phone),
    address: nullableText(source.address),
    emergencyContact: nullableText(source.emergencyContact),
    // Absent for a secretary: the API omits the property entirely.
    bloodType: (bloodType ?? null) as Patient['bloodType'],
    createdAt: text(source.createdAt)
  };
}

export function readPatientPage(value: unknown): PagedResult<Patient> {
  return readPagedResult(value, readPatient);
}

export function readPatientOptions(value: unknown): readonly PatientOption[] {
  if (!Array.isArray(value)) {
    fail();
  }

  return (value as unknown[]).map((item) => {
    const source = record(item);

    return {id: text(source.id), fullName: text(source.fullName), phone: nullableText(source.phone)};
  });
}

export function readDoctorOptions(value: unknown): readonly DoctorOption[] {
  if (!Array.isArray(value)) {
    fail();
  }

  return (value as unknown[]).map((item) => {
    const source = record(item);

    return {id: text(source.id), fullName: text(source.fullName)};
  });
}
