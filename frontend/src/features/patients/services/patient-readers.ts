import {createInvalidResponseError, readPagedResult} from '@/lib/api';
import type {PagedResult} from '@/types/paging';
import type {
  AllergyResponse,
  ChronicConditionResponse,
  PatientLabResult,
  PatientLookup,
  PatientMedicalProfileResponse,
  PatientNote,
  PatientVisitResponse,
  VisitDiagnosis,
  VisitLabResult,
  VisitMedication,
  VisitNote,
  VisitVitalSign
} from '../types';

/**
 * The boundary where untrusted API JSON becomes typed data. The project has no
 * schema library (Q-06/D-29), so these small readers check exactly the fields
 * the screens use and reject anything else as an invalid response.
 */
function fail(): never {
  throw createInvalidResponseError(200);
}

function record(value: unknown): Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : fail();
}

function array(value: unknown): readonly unknown[] {
  return Array.isArray(value) ? value : fail();
}

function text(value: unknown): string {
  return typeof value === 'string' ? value : fail();
}

function nullableText(value: unknown): string | null {
  return value === null || value === undefined ? null : text(value);
}

function number(value: unknown): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fail();
}

function nullableNumber(value: unknown): number | null {
  return value === null || value === undefined ? null : number(value);
}

export function readPatientLookup(value: unknown): PatientLookup {
  const source = record(value);

  return {
    id: text(source.id),
    fullName: text(source.fullName),
    phone: nullableText(source.phone)
  };
}

export function readPatientLookups(value: unknown): readonly PatientLookup[] {
  return array(value).map(readPatientLookup);
}

export function readAllergy(value: unknown): AllergyResponse {
  const source = record(value);

  return {
    id: text(source.id),
    name: text(source.name),
    severity: text(source.severity),
    description: nullableText(source.description),
    notes: nullableText(source.notes)
  };
}

export function readChronicCondition(value: unknown): ChronicConditionResponse {
  const source = record(value);

  return {
    id: text(source.id),
    name: text(source.name),
    diagnosisDate: nullableText(source.diagnosisDate),
    status: text(source.status),
    notes: nullableText(source.notes)
  };
}

export function readMedicalProfile(value: unknown): PatientMedicalProfileResponse {
  const source = record(value);

  return {
    patientId: text(source.patientId),
    fullName: text(source.fullName),
    dateOfBirth: text(source.dateOfBirth),
    age: number(source.age),
    gender: text(source.gender),
    phone: nullableText(source.phone),
    address: nullableText(source.address),
    bloodType: nullableText(source.bloodType),
    emergencyContact: nullableText(source.emergencyContact),
    allergies: array(source.allergies).map(readAllergy),
    chronicConditions: array(source.chronicConditions).map(readChronicCondition)
  };
}

export function readVisitDiagnosis(value: unknown): VisitDiagnosis {
  const source = record(value);

  return {
    id: text(source.id),
    name: text(source.name),
    description: nullableText(source.description),
    diagnosisDate: text(source.diagnosisDate)
  };
}

export function readVisitMedication(value: unknown): VisitMedication {
  const source = record(value);

  return {
    id: text(source.id),
    name: text(source.name),
    dosage: text(source.dosage),
    frequency: text(source.frequency),
    duration: text(source.duration),
    instructions: nullableText(source.instructions)
  };
}

export function readVisitVitalSign(value: unknown): VisitVitalSign {
  const source = record(value);

  return {
    id: text(source.id),
    bloodPressure: text(source.bloodPressure),
    heartRate: nullableNumber(source.heartRate),
    temperature: nullableNumber(source.temperature),
    respiratoryRate: nullableNumber(source.respiratoryRate),
    weight: nullableNumber(source.weight),
    height: nullableNumber(source.height),
    spO2: nullableNumber(source.spO2)
  };
}

export function readVisitNote(value: unknown): VisitNote {
  const source = record(value);

  return {
    id: text(source.id),
    doctorId: text(source.doctorId),
    doctorName: text(source.doctorName),
    content: text(source.content),
    createdAtUtc: text(source.createdAtUtc),
    updatedAtUtc: nullableText(source.updatedAtUtc)
  };
}

export function readVisitLabResult(value: unknown): VisitLabResult {
  const source = record(value);

  return {
    id: text(source.id),
    testName: text(source.testName),
    testDate: text(source.testDate),
    value: number(source.value),
    unit: text(source.unit),
    referenceRange: nullableText(source.referenceRange),
    notes: nullableText(source.notes)
  };
}

export function readVisit(value: unknown): PatientVisitResponse {
  const source = record(value);

  return {
    visitId: text(source.visitId),
    visitDate: text(source.visitDate),
    doctorId: text(source.doctorId),
    doctorName: text(source.doctorName),
    chiefComplaint: nullableText(source.chiefComplaint),
    symptoms: nullableText(source.symptoms),
    clinicalNotes: nullableText(source.clinicalNotes),
    diagnoses: array(source.diagnoses).map(readVisitDiagnosis),
    medications: array(source.medications).map(readVisitMedication),
    vitalSigns: array(source.vitalSigns).map(readVisitVitalSign),
    notes: array(source.notes).map(readVisitNote),
    labResults: array(source.labResults).map(readVisitLabResult)
  };
}

export function readVisitPage(value: unknown): PagedResult<PatientVisitResponse> {
  return readPagedResult(value, readVisit);
}

export function readPatientNote(value: unknown): PatientNote {
  const source = record(value);

  return {
    id: text(source.id),
    visitId: nullableText(source.visitId),
    doctorId: text(source.doctorId),
    doctorName: text(source.doctorName),
    noteContent: text(source.noteContent),
    createdAtUtc: text(source.createdAtUtc),
    updatedAtUtc: nullableText(source.updatedAtUtc)
  };
}

export function readPatientLabResult(value: unknown): PatientLabResult {
  const source = record(value);

  return {
    id: text(source.id),
    visitId: nullableText(source.visitId),
    testName: text(source.testName),
    testDate: text(source.testDate),
    value: number(source.value),
    unit: text(source.unit),
    referenceRange: nullableText(source.referenceRange),
    notes: nullableText(source.notes)
  };
}

export function readNotePage(value: unknown): PagedResult<PatientNote> {
  return readPagedResult(value, readPatientNote);
}

export function readLabPage(value: unknown): PagedResult<PatientLabResult> {
  return readPagedResult(value, readPatientLabResult);
}
