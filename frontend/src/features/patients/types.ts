/**
 * Response types for the F3 patient medical read APIs
 * (`backend/docs/api/PATIENT_MEDICAL_API.md` §3.1–§3.4) and the F1-C4 patient
 * lookup. All fields are narrowed at the API boundary (`services/patient-readers`);
 * screens never receive untrusted JSON.
 */

export type AllergyResponse = {
  readonly id: string;
  readonly name: string;
  readonly severity: string;
  readonly description: string | null;
  readonly notes: string | null;
};

export type ChronicConditionResponse = {
  readonly id: string;
  readonly name: string;
  readonly diagnosisDate: string | null;
  readonly status: string;
  readonly notes: string | null;
};

export type PatientMedicalProfileResponse = {
  readonly patientId: string;
  readonly fullName: string;
  readonly dateOfBirth: string;
  readonly age: number;
  readonly gender: string;
  readonly phone: string | null;
  readonly address: string | null;
  readonly bloodType: string | null;
  readonly emergencyContact: string | null;
  readonly allergies: readonly AllergyResponse[];
  readonly chronicConditions: readonly ChronicConditionResponse[];
};

export type VisitDiagnosis = {
  readonly id: string;
  readonly name: string;
  readonly description: string | null;
  readonly diagnosisDate: string;
};

export type VisitMedication = {
  readonly id: string;
  readonly name: string;
  readonly dosage: string;
  readonly frequency: string;
  readonly duration: string;
  readonly instructions: string | null;
};

export type VisitVitalSign = {
  readonly id: string;
  readonly bloodPressure: string;
  readonly heartRate: number | null;
  readonly temperature: number | null;
  readonly respiratoryRate: number | null;
  readonly weight: number | null;
  readonly height: number | null;
  readonly spO2: number | null;
};

export type VisitNote = {
  readonly id: string;
  readonly doctorId: string;
  readonly doctorName: string;
  readonly content: string;
  readonly createdAtUtc: string;
  readonly updatedAtUtc: string | null;
};

export type VisitLabResult = {
  readonly id: string;
  readonly testName: string;
  readonly testDate: string;
  readonly value: number;
  readonly unit: string;
  readonly referenceRange: string | null;
  readonly notes: string | null;
};

export type PatientVisitResponse = {
  readonly visitId: string;
  readonly visitDate: string;
  readonly doctorId: string;
  readonly doctorName: string;
  readonly chiefComplaint: string | null;
  readonly symptoms: string | null;
  readonly clinicalNotes: string | null;
  readonly diagnoses: readonly VisitDiagnosis[];
  readonly medications: readonly VisitMedication[];
  readonly vitalSigns: readonly VisitVitalSign[];
  readonly notes: readonly VisitNote[];
  readonly labResults: readonly VisitLabResult[];
};

/** Minimal administrative patient data from the F1-C4 lookup (id, name, phone only). */
export type PatientLookup = {
  readonly id: string;
  readonly fullName: string;
  readonly phone: string | null;
};
