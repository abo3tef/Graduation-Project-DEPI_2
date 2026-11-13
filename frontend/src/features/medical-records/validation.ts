const GUID_PATTERN =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Route identifiers are GUIDs on the backend; reject malformed ids before calling the API. */
export function isGuid(value: string): boolean {
  return GUID_PATTERN.test(value.trim());
}

/** Client-side mirror of the documented visit limits (backend stays authoritative). */
export const VISIT_FIELD_LIMITS = {
  chiefComplaint: 2000,
  symptoms: 2000,
  clinicalNotes: 4000
} as const;

export type VisitFormValues = {
  patientId: string;
  appointmentId: string;
  visitDate: string;
  chiefComplaint: string;
  symptoms: string;
  clinicalNotes: string;
};

export type VisitField = keyof VisitFormValues;

export type VisitFormMode = 'create' | 'edit';

export type ValidationMessageKey =
  | 'patientIdRequired'
  | 'patientIdInvalid'
  | 'appointmentIdInvalid'
  | 'visitDateRequired'
  | 'visitDateInvalid'
  | 'visitDateFuture'
  | 'diagnosisNameRequired'
  | 'diagnosisDateRequired'
  | 'diagnosisDateInvalid'
  | 'diagnosisDateFuture'
  | 'medicationNameRequired'
  | 'dosageRequired'
  | 'frequencyRequired'
  | 'maxLength';

export type ValidationMessage = {
  key: ValidationMessageKey;
  values?: Record<string, number>;
};

export type VisitFormErrors = Partial<Record<VisitField, ValidationMessage>>;

/** Stable order used to focus the first invalid control. */
export const VISIT_FIELD_ORDER: readonly VisitField[] = [
  'patientId',
  'appointmentId',
  'visitDate',
  'chiefComplaint',
  'symptoms',
  'clinicalNotes'
];

export const VISIT_FIELD_IDS: Record<VisitField, string> = {
  patientId: 'visit-patient-id',
  appointmentId: 'visit-appointment-id',
  visitDate: 'visit-date',
  chiefComplaint: 'visit-chief-complaint',
  symptoms: 'visit-symptoms',
  clinicalNotes: 'visit-clinical-notes'
};

/**
 * UX-only validation. Sleeps on no rule that the server does not also enforce; it merely
 * surfaces the documented limits before a round-trip. Server `400` responses stay authoritative.
 */
export function validateVisitForm(
  values: VisitFormValues,
  mode: VisitFormMode
): VisitFormErrors {
  const errors: VisitFormErrors = {};

  if (mode === 'create') {
    const patientId = values.patientId.trim();

    if (patientId.length === 0) {
      errors.patientId = {key: 'patientIdRequired'};
    } else if (!isGuid(patientId)) {
      errors.patientId = {key: 'patientIdInvalid'};
    }

    const appointmentId = values.appointmentId.trim();

    if (appointmentId.length > 0 && !isGuid(appointmentId)) {
      errors.appointmentId = {key: 'appointmentIdInvalid'};
    }
  }

  const visitDate = values.visitDate.trim();

  if (visitDate.length === 0) {
    errors.visitDate = {key: 'visitDateRequired'};
  } else {
    const parsed = new Date(visitDate);

    if (Number.isNaN(parsed.getTime())) {
      errors.visitDate = {key: 'visitDateInvalid'};
    } else if (parsed.getTime() > Date.now()) {
      errors.visitDate = {key: 'visitDateFuture'};
    }
  }

  applyLimit(errors, 'chiefComplaint', values.chiefComplaint, VISIT_FIELD_LIMITS.chiefComplaint);
  applyLimit(errors, 'symptoms', values.symptoms, VISIT_FIELD_LIMITS.symptoms);
  applyLimit(errors, 'clinicalNotes', values.clinicalNotes, VISIT_FIELD_LIMITS.clinicalNotes);

  return errors;
}

function applyLimit<Field extends string>(
  errors: Partial<Record<Field, ValidationMessage>>,
  field: Field,
  value: string,
  max: number
): void {
  if (value.trim().length > max) {
    errors[field] = {key: 'maxLength', values: {max}};
  }
}

/** ISO instant → the local wall-clock text an `<input type="datetime-local">` expects. */
export function toDateTimeLocalValue(value: string | null | undefined): string {
  if (!value) {
    return '';
  }

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return '';
  }

  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(
    date.getHours()
  )}:${pad(date.getMinutes())}`;
}

/** `<input type="datetime-local">` wall clock → the ISO instant the API expects. */
export function toVisitDateIso(value: string): string {
  const date = new Date(value);

  return Number.isNaN(date.getTime()) ? '' : date.toISOString();
}

/** Empty or whitespace-only optional text becomes `null`, matching the server rule. */
export function trimToNull(value: string): string | null {
  const trimmed = value.trim();

  return trimmed.length === 0 ? null : trimmed;
}

/**
 * Diagnosis limits. The backend is the source of truth; these mirror
 * `MedicalRecordText` so the common mistakes are caught before a round-trip.
 */
export const DIAGNOSIS_FIELD_LIMITS = {
  diagnosisName: 200,
  description: 4000
} as const;

/** Medication limits, mirrored from the medication request validators (D-26). */
export const MEDICATION_FIELD_LIMITS = {
  medicationName: 200,
  dosage: 100,
  frequency: 100,
  duration: 100,
  instructions: 4000
} as const;

export type DiagnosisFormValues = {
  diagnosisName: string;
  diagnosisDate: string;
  description: string;
};

export type DiagnosisField = keyof DiagnosisFormValues;

export type DiagnosisFormErrors = Partial<Record<DiagnosisField, ValidationMessage>>;

export const DIAGNOSIS_FIELD_ORDER: readonly DiagnosisField[] = [
  'diagnosisName',
  'diagnosisDate',
  'description'
];

export const DIAGNOSIS_FIELD_IDS: Record<DiagnosisField, string> = {
  diagnosisName: 'diagnosis-name',
  diagnosisDate: 'diagnosis-date',
  description: 'diagnosis-description'
};

export type MedicationFormValues = {
  medicationName: string;
  dosage: string;
  frequency: string;
  duration: string;
  instructions: string;
};

export type MedicationField = keyof MedicationFormValues;

export type MedicationFormErrors = Partial<Record<MedicationField, ValidationMessage>>;

export const MEDICATION_FIELD_ORDER: readonly MedicationField[] = [
  'medicationName',
  'dosage',
  'frequency',
  'duration',
  'instructions'
];

export const MEDICATION_FIELD_IDS: Record<MedicationField, string> = {
  medicationName: 'medication-name',
  dosage: 'medication-dosage',
  frequency: 'medication-frequency',
  duration: 'medication-duration',
  instructions: 'medication-instructions'
};

/**
 * UX-only diagnosis validation. The date rule mirrors the server validator exactly:
 * a diagnosis date up to *tomorrow* in UTC is accepted, which absorbs timezone skew.
 */
export function validateDiagnosisForm(
  values: DiagnosisFormValues
): DiagnosisFormErrors {
  const errors: DiagnosisFormErrors = {};
  const name = values.diagnosisName.trim();

  if (name.length === 0) {
    errors.diagnosisName = {key: 'diagnosisNameRequired'};
  } else if (name.length > DIAGNOSIS_FIELD_LIMITS.diagnosisName) {
    errors.diagnosisName = {
      key: 'maxLength',
      values: {max: DIAGNOSIS_FIELD_LIMITS.diagnosisName}
    };
  }

  const date = values.diagnosisDate.trim();

  if (date.length === 0) {
    errors.diagnosisDate = {key: 'diagnosisDateRequired'};
  } else if (!isDateOnly(date)) {
    errors.diagnosisDate = {key: 'diagnosisDateInvalid'};
  } else if (date > latestDiagnosisDateOnly()) {
    errors.diagnosisDate = {key: 'diagnosisDateFuture'};
  }

  applyLimit(
    errors,
    'description',
    values.description,
    DIAGNOSIS_FIELD_LIMITS.description
  );

  return errors;
}

/** UX-only medication validation, mirroring the create/update request validators. */
export function validateMedicationForm(
  values: MedicationFormValues
): MedicationFormErrors {
  const errors: MedicationFormErrors = {};

  requireText(errors, 'medicationName', values.medicationName, 'medicationNameRequired');
  applyLimit(
    errors,
    'medicationName',
    values.medicationName,
    MEDICATION_FIELD_LIMITS.medicationName
  );

  requireText(errors, 'dosage', values.dosage, 'dosageRequired');
  applyLimit(errors, 'dosage', values.dosage, MEDICATION_FIELD_LIMITS.dosage);

  requireText(errors, 'frequency', values.frequency, 'frequencyRequired');
  applyLimit(errors, 'frequency', values.frequency, MEDICATION_FIELD_LIMITS.frequency);

  applyLimit(errors, 'duration', values.duration, MEDICATION_FIELD_LIMITS.duration);
  applyLimit(
    errors,
    'instructions',
    values.instructions,
    MEDICATION_FIELD_LIMITS.instructions
  );

  return errors;
}

/** `DateOnly` response (`YYYY-MM-DD`) → the value an `<input type="date">` expects. */
export function toDateOnlyValue(value: string): string {
  const candidate = value.trim().slice(0, 10);

  return isDateOnly(candidate) ? candidate : '';
}

/**
 * Parses a `YYYY-MM-DD` date as UTC midnight so the rendered day never shifts with the
 * browser timezone (the plan's date-handling rule in §9).
 */
export function parseDateOnlyUtc(value: string): Date | null {
  if (!isDateOnly(value)) {
    return null;
  }

  const date = new Date(`${value}T00:00:00Z`);

  return Number.isNaN(date.getTime()) ? null : date;
}

function isDateOnly(value: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return false;
  }

  const date = new Date(`${value}T00:00:00Z`);

  return !Number.isNaN(date.getTime()) && date.toISOString().slice(0, 10) === value;
}

/** `DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)`, matching the server rule. */
export function latestDiagnosisDateOnly(): string {
  const limit = new Date();
  limit.setUTCDate(limit.getUTCDate() + 1);

  return limit.toISOString().slice(0, 10);
}

/** The doctor's local calendar date, used as the create-form default. */
export function todayDateOnly(): string {
  const now = new Date();

  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

function requireText<Field extends string>(
  errors: Partial<Record<Field, ValidationMessage>>,
  field: Field,
  value: string,
  key: ValidationMessageKey
): void {
  if (value.trim().length === 0) {
    errors[field] = {key};
  }
}

function pad(value: number): string {
  return String(value).padStart(2, '0');
}
