import {describe, expect, it} from 'vitest';
import {
  hasErrors,
  validateBooking,
  validateCancellationReason,
  validateReschedule
} from './appointment.validation';

const NOW = {date: '2030-01-02', time: '10:00'};

const VALID = {
  doctorId: 'doctor-1',
  patientId: 'patient-1',
  appointmentDate: '2030-01-03',
  startTime: '09:00',
  endTime: '09:30',
  reason: 'checkup'
};

describe('validateBooking', () => {
  it('accepts a complete future booking', () => {
    expect(validateBooking(VALID, NOW)).toEqual({});
  });

  it('requires doctor, patient, date, start and end', () => {
    const errors = validateBooking(
      {...VALID, doctorId: '', patientId: ' ', appointmentDate: '', startTime: '', endTime: ''},
      NOW
    );

    expect(errors).toEqual({
      doctorId: 'required',
      patientId: 'required',
      appointmentDate: 'required',
      startTime: 'required',
      endTime: 'required'
    });
  });

  it('has no default end time: an empty end is an error', () => {
    expect(validateBooking({...VALID, endTime: ''}, NOW).endTime).toBe('required');
  });

  it.each(['09:00', '08:59'])('rejects an end time (%s) that is not later than the start', (end) => {
    expect(validateBooking({...VALID, endTime: end}, NOW).endTime).toBe('endAfterStart');
  });

  it('accepts back-to-back minutes (end one minute after start)', () => {
    expect(validateBooking({...VALID, endTime: '09:01'}, NOW)).toEqual({});
  });

  it('rejects malformed dates and times', () => {
    const errors = validateBooking(
      {...VALID, appointmentDate: '2030-02-30', startTime: '25:00', endTime: 'late'},
      NOW
    );

    expect(errors.appointmentDate).toBe('invalidDate');
    expect(errors.startTime).toBe('invalidTime');
    expect(errors.endTime).toBe('invalidTime');
  });

  it('rejects a past date', () => {
    expect(validateBooking({...VALID, appointmentDate: '2030-01-01'}, NOW).appointmentDate).toBe('inPast');
  });

  it('rejects an earlier time today but accepts the current minute and later', () => {
    const today = {...VALID, appointmentDate: NOW.date};

    expect(validateBooking({...today, startTime: '09:59', endTime: '10:30'}, NOW).startTime).toBe('inPast');
    expect(validateBooking({...today, startTime: '10:00', endTime: '10:30'}, NOW)).toEqual({});
    expect(validateBooking({...today, startTime: '10:01', endTime: '10:30'}, NOW)).toEqual({});
  });

  it('limits the reason to 1000 characters after trimming', () => {
    expect(validateBooking({...VALID, reason: 'a'.repeat(1000)}, NOW)).toEqual({});
    expect(validateBooking({...VALID, reason: `  ${'a'.repeat(1000)}  `}, NOW)).toEqual({});
    expect(validateBooking({...VALID, reason: 'a'.repeat(1001)}, NOW).reason).toBe('reasonTooLong');
  });

  it('accepts seconds in time values', () => {
    expect(validateBooking({...VALID, startTime: '09:00:00', endTime: '09:30:00'}, NOW)).toEqual({});
  });
});

describe('validateReschedule', () => {
  it('uses the same schedule rules', () => {
    expect(validateReschedule({appointmentDate: '2030-01-03', startTime: '09:00', endTime: '09:30'}, NOW)).toEqual({});
    expect(validateReschedule({appointmentDate: '2030-01-01', startTime: '09:00', endTime: '09:30'}, NOW)).toEqual({
      appointmentDate: 'inPast'
    });
    expect(validateReschedule({appointmentDate: '2030-01-03', startTime: '09:00', endTime: '09:00'}, NOW)).toEqual({
      endTime: 'endAfterStart'
    });
  });
});

describe('validateCancellationReason', () => {
  it.each(['', '   ', '\n\t'])('requires a reason (%j)', (value) => {
    expect(validateCancellationReason(value)).toBe('required');
  });

  it('accepts 500 characters and rejects 501 (after trimming)', () => {
    expect(validateCancellationReason('a'.repeat(500))).toBeNull();
    expect(validateCancellationReason(`  ${'a'.repeat(500)}  `)).toBeNull();
    expect(validateCancellationReason('a'.repeat(501))).toBe('cancelReasonTooLong');
  });

  it('accepts a normal reason', () => {
    expect(validateCancellationReason('Patient asked to cancel')).toBeNull();
  });
});

describe('hasErrors', () => {
  it('detects any error', () => {
    expect(hasErrors({})).toBe(false);
    expect(hasErrors({endTime: 'required'})).toBe(true);
  });
});
