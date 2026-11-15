import {ApiError, createInvalidResponseError} from '@/lib/api';
import type {
  PatientAllergyResponse,
  PatientChronicConditionResponse,
  PatientMedicalProfileResponse,
  PatientTimelineEventResponse,
  PatientTimelineEventType
} from './types';

type JsonObject = Record<string, unknown>;

const TIMELINE_EVENT_TYPES: readonly PatientTimelineEventType[] = [
  'Visit',
  'Diagnosis',
  'Medication',
  'MedicalNote'
];

function isObject(value: unknown): value is JsonObject {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function isString(value: unknown): value is string {
  return typeof value === 'string';
}

function isNullableString(value: unknown): value is string | null {
  return value === null || typeof value === 'string';
}

function isInteger(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value);
}

function isPatientTimelineEventType(value: unknown): value is PatientTimelineEventType {
  return isString(value) && (TIMELINE_EVENT_TYPES as readonly string[]).includes(value);
}

function invalidResponse(): ApiError {
  return createInvalidResponseError(0);
}

/**
 * The project has no schema-validation library (Q-06/D-29), so responses are narrowed by
 * hand at the API boundary. A shape mismatch fails loudly instead of producing a broken UI.
 * Field sets and nullability mirror `PatientTimelineEventResponse` and
 * `PatientMedicalProfileResponse` (backend `MedicalRecordResponses.cs`) exactly.
 */
export function expectPatientTimelineEvent(value: unknown): PatientTimelineEventResponse {
  if (!isObject(value)) {
    throw invalidResponse();
  }

  const {id, type, occurredAt, title, summary, relatedVisitId} = value;

  if (
    !isString(id) ||
    !isPatientTimelineEventType(type) ||
    !isString(occurredAt) ||
    !isString(title) ||
    !isNullableString(summary) ||
    !isNullableString(relatedVisitId)
  ) {
    throw invalidResponse();
  }

  return {id, type, occurredAt, title, summary, relatedVisitId};
}

export function expectPatientAllergy(value: unknown): PatientAllergyResponse {
  if (!isObject(value)) {
    throw invalidResponse();
  }

  const {id, name, severity, description, notes} = value;

  if (
    !isString(id) ||
    !isString(name) ||
    !isNullableString(severity) ||
    !isNullableString(description) ||
    !isNullableString(notes)
  ) {
    throw invalidResponse();
  }

  return {id, name, severity, description, notes};
}

export function expectPatientChronicCondition(value: unknown): PatientChronicConditionResponse {
  if (!isObject(value)) {
    throw invalidResponse();
  }

  const {id, name, diagnosisDate, status, notes} = value;

  if (
    !isString(id) ||
    !isString(name) ||
    !isNullableString(diagnosisDate) ||
    !isNullableString(status) ||
    !isNullableString(notes)
  ) {
    throw invalidResponse();
  }

  return {id, name, diagnosisDate, status, notes};
}

export function expectPatientMedicalProfile(value: unknown): PatientMedicalProfileResponse {
  if (!isObject(value)) {
    throw invalidResponse();
  }

  const {
    patientId,
    fullName,
    dateOfBirth,
    age,
    gender,
    phone,
    address,
    bloodType,
    emergencyContact,
    allergies,
    chronicConditions
  } = value;

  if (
    !isString(patientId) ||
    !isString(fullName) ||
    !isString(dateOfBirth) ||
    !isInteger(age) ||
    !isString(gender) ||
    !isNullableString(phone) ||
    !isNullableString(address) ||
    !isNullableString(bloodType) ||
    !isNullableString(emergencyContact) ||
    !Array.isArray(allergies) ||
    !Array.isArray(chronicConditions)
  ) {
    throw invalidResponse();
  }

  return {
    patientId,
    fullName,
    dateOfBirth,
    age,
    gender,
    phone,
    address,
    bloodType,
    emergencyContact,
    allergies: allergies.map(expectPatientAllergy),
    chronicConditions: chronicConditions.map(expectPatientChronicCondition)
  };
}
