/**
 * Wire types for `/api/medical-visits` (backend/docs/api/MEDICAL_RECORDS_API.md).
 *
 * Nullable members are typed `string | null` because the API serialises explicit nulls,
 * not omitted properties. Dates are ISO strings; `diagnosisDate` is a plain `YYYY-MM-DD`.
 */

export type DiagnosisResponse = {
  id: string;
  visitId: string;
  patientId: string;
  diagnosisName: string;
  description: string | null;
  diagnosisDate: string;
};

export type MedicationResponse = {
  id: string;
  visitId: string;
  patientId: string;
  medicationName: string;
  dosage: string;
  frequency: string;
  duration: string | null;
  instructions: string | null;
};

export type SaveVitalSignsRequest = {
  bloodPressure: string | null;
  heartRate: number | null;
  temperature: number | null;
  respiratoryRate: number | null;
  weight: number | null;
  height: number | null;
  spO2: number | null;
};

export type VitalSignsResponse = SaveVitalSignsRequest & {id: string};

export type MedicalVisitResponse = {
  id: string;
  patientId: string;
  doctorId: string;
  appointmentId: string | null;
  visitDate: string;
  chiefComplaint: string | null;
  symptoms: string | null;
  clinicalNotes: string | null;
  diagnoses: DiagnosisResponse[];
  medications: MedicationResponse[];
  vitalSigns: VitalSignsResponse[];
};

export type CreateMedicalVisitRequest = {
  patientId: string;
  appointmentId: string | null;
  visitDate: string;
  chiefComplaint: string | null;
  symptoms: string | null;
  clinicalNotes: string | null;
};

export type UpdateMedicalVisitRequest = {
  visitDate: string;
  chiefComplaint: string | null;
  symptoms: string | null;
  clinicalNotes: string | null;
};

export type CreateDiagnosisRequest = {
  diagnosisName: string;
  description: string | null;
  diagnosisDate: string;
};

export type UpdateDiagnosisRequest = {
  diagnosisName: string;
  description: string | null;
  diagnosisDate: string;
};

export type CreateMedicationRequest = {
  medicationName: string;
  dosage: string;
  frequency: string;
  duration: string | null;
  instructions: string | null;
};

export type UpdateMedicationRequest = {
  medicationName: string;
  dosage: string;
  frequency: string;
  duration: string | null;
  instructions: string | null;
};
