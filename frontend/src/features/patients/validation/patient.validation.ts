import {PATIENT_LIMITS, isBloodType, isGender, type BloodType, type Gender} from '../types/patient.types';

export type PatientFormValues = {
  doctorId: string;
  fullName: string;
  dateOfBirth: string;
  gender: '' | Gender;
  phone: string;
  address: string;
  emergencyContact: string;
  bloodType: '' | BloodType;
};

export type PatientField = keyof PatientFormValues;

export type PatientValidationKey =
  | 'required'
  | 'fullNameTooLong'
  | 'dateOfBirthInvalid'
  | 'dateOfBirthFuture'
  | 'dateOfBirthTooOld'
  | 'phoneInvalid'
  | 'phoneTooLong'
  | 'addressTooLong'
  | 'emergencyContactTooLong'
  | 'invalid';

export type PatientFormErrors = Partial<Record<PatientField, PatientValidationKey>>;

export const PATIENT_FIELD_ORDER: readonly PatientField[] = [
  'doctorId',
  'fullName',
  'dateOfBirth',
  'gender',
  'phone',
  'address',
  'emergencyContact',
  'bloodType'
];

export const EMPTY_PATIENT_VALUES: PatientFormValues = {
  doctorId: '',
  fullName: '',
  dateOfBirth: '',
  gender: '',
  phone: '',
  address: '',
  emergencyContact: '',
  bloodType: ''
};

/** Optional "+", then digits, spaces, dashes or parentheses; 7-20 characters and 7+ digits. */
const PHONE_PATTERN = /^\+?[0-9 ()-]{7,20}$/;
const EARLIEST_DATE_OF_BIRTH = '1900-01-01';

export function isValidPhone(phone: string): boolean {
  const trimmed = phone.trim();

  return PHONE_PATTERN.test(trimmed) && (trimmed.match(/\d/g)?.length ?? 0) >= 7;
}

function isIsoDate(value: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return false;
  }

  const date = new Date(`${value}T00:00:00Z`);

  return !Number.isNaN(date.getTime()) && date.toISOString().startsWith(value);
}

/**
 * UX-only validation that mirrors the API rules so most mistakes are caught before sending. The
 * backend remains the enforcement point; its field errors are shown on the same fields.
 * `today` is a yyyy-MM-dd string so tests do not depend on the clock.
 */
export function validatePatientForm(
  values: PatientFormValues,
  options: {requireDoctor: boolean; today: string}
): PatientFormErrors {
  const errors: PatientFormErrors = {};

  if (options.requireDoctor && values.doctorId === '') {
    errors.doctorId = 'required';
  }

  const name = values.fullName.trim();

  if (name === '') {
    errors.fullName = 'required';
  } else if (name.length > PATIENT_LIMITS.fullName) {
    errors.fullName = 'fullNameTooLong';
  }

  if (values.dateOfBirth === '') {
    errors.dateOfBirth = 'required';
  } else if (!isIsoDate(values.dateOfBirth)) {
    errors.dateOfBirth = 'dateOfBirthInvalid';
  } else if (values.dateOfBirth > options.today) {
    errors.dateOfBirth = 'dateOfBirthFuture';
  } else if (values.dateOfBirth < EARLIEST_DATE_OF_BIRTH) {
    errors.dateOfBirth = 'dateOfBirthTooOld';
  }

  if (!isGender(values.gender)) {
    errors.gender = 'required';
  }

  const phone = values.phone.trim();

  if (phone.length > PATIENT_LIMITS.phone) {
    errors.phone = 'phoneTooLong';
  } else if (phone !== '' && !isValidPhone(phone)) {
    errors.phone = 'phoneInvalid';
  }

  if (values.address.trim().length > PATIENT_LIMITS.address) {
    errors.address = 'addressTooLong';
  }

  if (values.emergencyContact.trim().length > PATIENT_LIMITS.emergencyContact) {
    errors.emergencyContact = 'emergencyContactTooLong';
  }

  if (values.bloodType !== '' && !isBloodType(values.bloodType)) {
    errors.bloodType = 'invalid';
  }

  return errors;
}

export function blankToNull(value: string): string | null {
  const trimmed = value.trim();

  return trimmed === '' ? null : trimmed;
}

export function todayIso(now: Date = new Date()): string {
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');

  return `${now.getFullYear()}-${month}-${day}`;
}
