export const GENDERS = ['Male', 'Female'] as const;
export type Gender = (typeof GENDERS)[number];

export const BLOOD_TYPES = [
  'APositive',
  'ANegative',
  'BPositive',
  'BNegative',
  'ABPositive',
  'ABNegative',
  'OPositive',
  'ONegative'
] as const;
export type BloodType = (typeof BLOOD_TYPES)[number];

export function isGender(value: unknown): value is Gender {
  return typeof value === 'string' && (GENDERS as readonly string[]).includes(value);
}

export function isBloodType(value: unknown): value is BloodType {
  return typeof value === 'string' && (BLOOD_TYPES as readonly string[]).includes(value);
}

/** Field limits mirror `backend/docs/api/PATIENTS_API.md`. */
export const PATIENT_LIMITS = {
  fullName: 200,
  phone: 30,
  address: 500,
  emergencyContact: 100
} as const;

export const PATIENT_SEARCH_MIN_LENGTH = 2;
export const PATIENT_SEARCH_MAX_LENGTH = 100;
export const PATIENT_LOOKUP_LIMIT = 10;

/** A patient as the API returns it. `bloodType` is never present for a secretary (Q-18). */
export type Patient = {
  readonly id: string;
  readonly doctorId: string | null;
  readonly fullName: string;
  readonly dateOfBirth: string;
  readonly gender: Gender;
  readonly phone: string | null;
  readonly address: string | null;
  readonly emergencyContact: string | null;
  readonly bloodType: BloodType | null;
  readonly createdAt: string;
};

/** The minimal shape the patient picker works with (id, name, phone only). */
export type PatientOption = {
  readonly id: string;
  readonly fullName: string;
  readonly phone: string | null;
};

/** A doctor a secretary may create patients for. */
export type DoctorOption = {
  readonly id: string;
  readonly fullName: string;
};

export type PatientFilters = {
  readonly search: string;
  readonly gender: 'all' | Gender;
  readonly pageNumber: number;
  readonly pageSize: number;
};

export type SavePatientInput = {
  /** Secretary only: the assigned doctor who will own the patient. */
  readonly doctorId?: string;
  readonly fullName: string;
  readonly dateOfBirth: string;
  readonly gender: Gender;
  readonly phone: string | null;
  readonly address: string | null;
  readonly emergencyContact: string | null;
  /** Doctor only. Never sent by a secretary. */
  readonly bloodType?: BloodType | null;
};
