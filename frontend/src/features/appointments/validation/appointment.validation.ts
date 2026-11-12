import {
  CANCELLATION_REASON_MAX_LENGTH,
  REASON_MAX_LENGTH,
  type BookAppointmentInput,
  type RescheduleAppointmentInput
} from '../types/appointment.types';
import {
  isBefore,
  isValidIsoDate,
  isValidTime,
  timeToMinutes,
  type ClinicNow
} from '../utils/date-time';

/**
 * Keys of the `Appointments.validation` messages. These checks mirror the server
 * rules for fast feedback only; the backend remains the authority.
 */
export type ValidationKey =
  | 'required'
  | 'invalidDate'
  | 'invalidTime'
  | 'endAfterStart'
  | 'inPast'
  | 'reasonTooLong'
  | 'cancelReasonTooLong'
  | 'invalid'
  | 'timeNotInTimeZone';

export type BookField =
  | 'doctorId'
  | 'patientId'
  | 'appointmentDate'
  | 'startTime'
  | 'endTime'
  | 'reason';

export type ScheduleField = 'appointmentDate' | 'startTime' | 'endTime';

export type FormErrors<TField extends string> = Partial<Record<TField, ValidationKey>>;

export function hasErrors(errors: Readonly<Record<string, unknown>>): boolean {
  return Object.keys(errors).length > 0;
}

function validateSchedule(
  input: {appointmentDate: string; startTime: string; endTime: string},
  now: ClinicNow
): FormErrors<ScheduleField> {
  const errors: FormErrors<ScheduleField> = {};
  const date = input.appointmentDate.trim();
  const start = input.startTime.trim();
  const end = input.endTime.trim();

  if (date === '') {
    errors.appointmentDate = 'required';
  } else if (!isValidIsoDate(date)) {
    errors.appointmentDate = 'invalidDate';
  }

  if (start === '') {
    errors.startTime = 'required';
  } else if (!isValidTime(start)) {
    errors.startTime = 'invalidTime';
  }

  if (end === '') {
    errors.endTime = 'required';
  } else if (!isValidTime(end)) {
    errors.endTime = 'invalidTime';
  }

  if (!errors.startTime && !errors.endTime && timeToMinutes(end) <= timeToMinutes(start)) {
    errors.endTime = 'endAfterStart';
  }

  if (!errors.appointmentDate && !errors.startTime && isBefore(date, start, now)) {
    if (date < now.date) {
      errors.appointmentDate = 'inPast';
    } else {
      errors.startTime = 'inPast';
    }
  }

  return errors;
}

export function validateBooking(
  input: BookAppointmentInput,
  now: ClinicNow
): FormErrors<BookField> {
  const errors: FormErrors<BookField> = validateSchedule(input, now);

  if (input.doctorId.trim() === '') {
    errors.doctorId = 'required';
  }

  if (input.patientId.trim() === '') {
    errors.patientId = 'required';
  }

  if (input.reason.trim().length > REASON_MAX_LENGTH) {
    errors.reason = 'reasonTooLong';
  }

  return errors;
}

export function validateReschedule(
  input: RescheduleAppointmentInput,
  now: ClinicNow
): FormErrors<ScheduleField> {
  return validateSchedule(input, now);
}

/** Returns the error for a cancellation reason, or `null` when it is acceptable. */
export function validateCancellationReason(reason: string): ValidationKey | null {
  const trimmed = reason.trim();

  if (trimmed === '') {
    return 'required';
  }

  return trimmed.length > CANCELLATION_REASON_MAX_LENGTH ? 'cancelReasonTooLong' : null;
}
