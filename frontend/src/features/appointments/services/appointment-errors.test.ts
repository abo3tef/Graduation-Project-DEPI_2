import {describe, expect, it} from 'vitest';
import {ApiError, createInvalidResponseError, createNetworkError, parseProblem} from '@/lib/api';
import {getAppointmentErrorKey, isRetryableError, mapFieldErrors} from './appointment-errors';

describe('getAppointmentErrorKey', () => {
  it.each([
    ['identity.invalid', 403, 'identity'],
    ['appointment.complete_requires_doctor', 403, 'completeRequiresDoctor'],
    ['doctor.not_found', 404, 'doctorNotFound'],
    ['patient.not_found', 404, 'patientNotFound'],
    ['appointment.not_found', 404, 'appointmentNotFound'],
    ['appointment.overlap', 409, 'overlap'],
    ['doctor.unavailable', 409, 'doctorUnavailable'],
    ['schedule.busy', 409, 'scheduleBusy'],
    ['validation.failed', 400, 'validation'],
    ['appointment.in_the_past', 400, 'inPast'],
    ['appointment.invalid_interval', 400, 'invalidInterval'],
    ['appointment.end_time_required', 400, 'endTimeRequired'],
    ['appointment.not_open', 400, 'notOpen'],
    ['appointment.invalid_status_transition', 400, 'invalidTransition'],
    ['appointment.use_cancel_operation', 400, 'invalidTransition'],
    ['appointment.cancellation_reason_required', 400, 'cancellationReasonRequired'],
    ['appointment.cancellation_reason_too_long', 400, 'cancellationReasonTooLong'],
    ['domain.rule_violation', 400, 'badRequest'],
    ['server.error', 500, 'server']
  ])('code %s → %s', (code, status, expected) => {
    expect(getAppointmentErrorKey(parseProblem(status, {code}))).toBe(expected);
  });

  it('prefers the code over the status', () => {
    expect(getAppointmentErrorKey(parseProblem(409, {code: 'schedule.busy'}))).toBe('scheduleBusy');
  });

  it.each([
    [400, 'badRequest'],
    [403, 'forbidden'],
    [404, 'notFound'],
    [409, 'conflict'],
    [500, 'server'],
    [503, 'server'],
    [418, 'unknown']
  ])('falls back to the status when there is no known code (%d → %s)', (status, expected) => {
    expect(getAppointmentErrorKey(parseProblem(status, {code: 'something.new'}))).toBe(expected);
    expect(getAppointmentErrorKey(parseProblem(status, undefined))).toBe(expected);
  });

  it('recognizes network and invalid-response failures', () => {
    expect(getAppointmentErrorKey(createNetworkError())).toBe('network');
    expect(getAppointmentErrorKey(createInvalidResponseError(200))).toBe('invalidResponse');
  });

  it('treats anything that is not an ApiError as unknown', () => {
    expect(getAppointmentErrorKey(new Error('boom'))).toBe('unknown');
    expect(getAppointmentErrorKey('text')).toBe('unknown');
    expect(getAppointmentErrorKey(null)).toBe('unknown');
  });

  it('never exposes the English server text', () => {
    const error = new ApiError({kind: 'http', status: 409, code: 'appointment.overlap', detail: 'The doctor already has an appointment'});

    expect(getAppointmentErrorKey(error)).toBe('overlap');
  });
});

describe('isRetryableError', () => {
  it('retries network and server errors only', () => {
    expect(isRetryableError(createNetworkError())).toBe(true);
    expect(isRetryableError(parseProblem(500, null))).toBe(true);
    expect(isRetryableError(parseProblem(409, null))).toBe(false);
    expect(isRetryableError(parseProblem(403, null))).toBe(false);
    expect(isRetryableError(new Error('x'))).toBe(false);
  });
});

describe('mapFieldErrors', () => {
  const fields = ['patientId', 'doctorId', 'appointmentDate', 'startTime', 'endTime', 'reason'] as const;

  it('maps validator codes to validation keys', () => {
    const error = parseProblem(400, {
      code: 'validation.failed',
      errors: {endTime: ['x'], reason: ['x'], doctorId: ['x'], startTime: ['x']},
      errorCodes: {
        endTime: ['NotNullValidator'],
        reason: ['MaximumLengthValidator'],
        doctorId: ['NotEmptyValidator'],
        startTime: ['appointment.time_not_in_time_zone']
      }
    });

    expect(mapFieldErrors(error, fields)).toEqual({
      endTime: 'required',
      reason: 'reasonTooLong',
      doctorId: 'required',
      startTime: 'timeNotInTimeZone'
    });
  });

  it('uses the cancel-specific message for a too-long cancellation reason', () => {
    const error = parseProblem(400, {
      errors: {cancellationReason: ['x']},
      errorCodes: {cancellationReason: ['MaximumLengthValidator']}
    });

    expect(mapFieldErrors(error, ['cancellationReason'])).toEqual({cancellationReason: 'cancelReasonTooLong'});
  });

  it('falls back to "invalid" for unknown validator codes and for framework errors without codes', () => {
    const unknownCode = parseProblem(400, {errors: {endTime: ['x']}, errorCodes: {endTime: ['SomethingNewValidator']}});
    const framework = parseProblem(400, {errors: {'$.endTime': ['could not be converted']}});

    expect(mapFieldErrors(unknownCode, fields)).toEqual({endTime: 'invalid'});
    expect(mapFieldErrors(framework, fields)).toEqual({endTime: 'invalid'});
  });

  it('ignores fields that are not part of the form and fields without errors', () => {
    const error = parseProblem(400, {errors: {somethingElse: ['x']}, errorCodes: {somethingElse: ['NotNullValidator']}});

    expect(mapFieldErrors(error, fields)).toEqual({});
  });
});
