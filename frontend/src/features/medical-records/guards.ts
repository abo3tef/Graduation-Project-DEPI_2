import {ApiError, createInvalidResponseError} from '@/lib/api';
import type {
  DiagnosisResponse,
  MedicalVisitResponse,
  MedicationResponse
} from './types';

type JsonObject = Record<string, unknown>;

function isObject(value: unknown): value is JsonObject {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function isString(value: unknown): value is string {
  return typeof value === 'string';
}

function isNullableString(value: unknown): value is string | null {
  return value === null || typeof value === 'string';
}

function invalidResponse(): ApiError {
  return createInvalidResponseError(0);
}

/**
 * The project has no schema-validation library (Q-06/D-29), so responses are narrowed by
 * hand at the API boundary. A shape mismatch fails loudly instead of producing a broken UI.
 */
export function expectDiagnosis(value: unknown): DiagnosisResponse {
  if (!isObject(value)) {
    throw invalidResponse();
  }

  const {id, visitId, patientId, diagnosisName, description, diagnosisDate} = value;

  if (
    !isString(id) ||
    !isString(visitId) ||
    !isString(patientId) ||
    !isString(diagnosisName) ||
    !isNullableString(description) ||
    !isString(diagnosisDate)
  ) {
    throw invalidResponse();
  }

  return {id, visitId, patientId, diagnosisName, description, diagnosisDate};
}

export function expectMedication(value: unknown): MedicationResponse {
  if (!isObject(value)) {
    throw invalidResponse();
  }

  const {
    id,
    visitId,
    patientId,
    medicationName,
    dosage,
    frequency,
    duration,
    instructions
  } = value;

  if (
    !isString(id) ||
    !isString(visitId) ||
    !isString(patientId) ||
    !isString(medicationName) ||
    !isString(dosage) ||
    !isString(frequency) ||
    !isNullableString(duration) ||
    !isNullableString(instructions)
  ) {
    throw invalidResponse();
  }

  return {
    id,
    visitId,
    patientId,
    medicationName,
    dosage,
    frequency,
    duration,
    instructions
  };
}

export function expectMedicalVisit(value: unknown): MedicalVisitResponse {
  if (!isObject(value)) {
    throw invalidResponse();
  }

  const {
    id,
    patientId,
    doctorId,
    appointmentId,
    visitDate,
    chiefComplaint,
    symptoms,
    clinicalNotes,
    diagnoses,
    medications
  } = value;

  if (
    !isString(id) ||
    !isString(patientId) ||
    !isString(doctorId) ||
    !isNullableString(appointmentId) ||
    !isString(visitDate) ||
    !isNullableString(chiefComplaint) ||
    !isNullableString(symptoms) ||
    !isNullableString(clinicalNotes) ||
    !Array.isArray(diagnoses) ||
    !Array.isArray(medications)
  ) {
    throw invalidResponse();
  }

  return {
    id,
    patientId,
    doctorId,
    appointmentId,
    visitDate,
    chiefComplaint,
    symptoms,
    clinicalNotes,
    diagnoses: diagnoses.map(expectDiagnosis),
    medications: medications.map(expectMedication)
  };
}
