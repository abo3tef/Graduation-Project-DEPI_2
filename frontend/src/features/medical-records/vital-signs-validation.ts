import type {SaveVitalSignsRequest, VitalSignsResponse} from './types';

export const VITAL_SIGNS_FIELDS = [
  'bloodPressure', 'heartRate', 'temperature', 'respiratoryRate', 'spO2', 'weight', 'height'
] as const;
export type VitalSignsField = typeof VITAL_SIGNS_FIELDS[number];
export type VitalSignsFormValues = Record<VitalSignsField, string>;
type ValidationMessage = {
  key: 'atLeastOne' | 'bloodPressureInvalid' | 'range' | 'integer';
  values?: {min: number; max: number};
};
export type VitalSignsFormErrors = Partial<Record<VitalSignsField | 'form', ValidationMessage>>;

// Temporary Q-19 values authorized by Anton; Pending Tech Lead Approval.
export const VITAL_SIGNS_LIMITS = {
  bloodPressure: {maxLength: 20, systolicMin: 50, systolicMax: 300, diastolicMin: 20, diastolicMax: 200},
  heartRate: {min: 20, max: 300, integer: true},
  temperature: {min: 30, max: 45, integer: false},
  respiratoryRate: {min: 4, max: 80, integer: true},
  spO2: {min: 50, max: 100, integer: true},
  weight: {min: 0.3, max: 500, integer: false},
  height: {min: 20, max: 260, integer: false}
} as const;

export function initialVitalSignsValues(value?: VitalSignsResponse): VitalSignsFormValues {
  return {
    bloodPressure: value?.bloodPressure ?? '', heartRate: value?.heartRate?.toString() ?? '',
    temperature: value?.temperature?.toString() ?? '', respiratoryRate: value?.respiratoryRate?.toString() ?? '',
    spO2: value?.spO2?.toString() ?? '', weight: value?.weight?.toString() ?? '', height: value?.height?.toString() ?? ''
  };
}

export function validateVitalSignsForm(values: VitalSignsFormValues): VitalSignsFormErrors {
  const errors: VitalSignsFormErrors = {};
  if (VITAL_SIGNS_FIELDS.every(field => values[field].trim() === '')) {
    errors.form = {key: 'atLeastOne'};
    return errors;
  }
  for (const field of VITAL_SIGNS_FIELDS) {
    const text = values[field].trim();
    if (!text) continue;
    if (field === 'bloodPressure') {
      const limits = VITAL_SIGNS_LIMITS.bloodPressure;
      const parts = /^([0-9]+)\/([0-9]+)$/.exec(text);
      const systolic = parts ? Number(parts[1]) : NaN;
      const diastolic = parts ? Number(parts[2]) : NaN;
      if (text.length > limits.maxLength || !parts ||
          systolic < limits.systolicMin || systolic > limits.systolicMax ||
          diastolic < limits.diastolicMin || diastolic > limits.diastolicMax || systolic <= diastolic) {
        errors[field] = {key: 'bloodPressureInvalid'};
      }
      continue;
    }
    const limits = VITAL_SIGNS_LIMITS[field];
    const number = Number(text);
    if (!/^[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)$/.test(text) || !Number.isFinite(number) ||
        number < limits.min || number > limits.max) {
      errors[field] = {key: 'range', values: {min: limits.min, max: limits.max}};
    } else if (limits.integer && !Number.isInteger(number)) {
      errors[field] = {key: 'integer'};
    }
  }
  return errors;
}

/** Call only after validation. Explicit nulls implement PUT replacement semantics. */
export function toVitalSignsRequest(values: VitalSignsFormValues): SaveVitalSignsRequest {
  const numberOrNull = (text: string) => text.trim() === '' ? null : Number(text.trim());
  return {
    bloodPressure: values.bloodPressure.trim() || null,
    heartRate: numberOrNull(values.heartRate), temperature: numberOrNull(values.temperature),
    respiratoryRate: numberOrNull(values.respiratoryRate), spO2: numberOrNull(values.spO2),
    weight: numberOrNull(values.weight), height: numberOrNull(values.height)
  };
}
