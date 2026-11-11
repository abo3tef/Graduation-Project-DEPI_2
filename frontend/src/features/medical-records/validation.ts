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

function applyLimit(
  errors: VisitFormErrors,
  field: VisitField,
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

function pad(value: number): string {
  return String(value).padStart(2, '0');
}
