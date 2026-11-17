import {describe, expect, it} from 'vitest';
import {
  EMPTY_PATIENT_VALUES,
  blankToNull,
  isValidPhone,
  todayIso,
  validatePatientForm,
  type PatientFormValues
} from './patient.validation';

const TODAY = '2030-01-01';

const valid: PatientFormValues = {
  ...EMPTY_PATIENT_VALUES,
  fullName: 'Sara Ali',
  dateOfBirth: '1990-04-12',
  gender: 'Female',
  phone: '01001234567'
};

const check = (values: Partial<PatientFormValues>, requireDoctor = false) =>
  validatePatientForm({...valid, ...values}, {requireDoctor, today: TODAY});

describe('validatePatientForm', () => {
  it('accepts a complete valid form and an empty optional set', () => {
    expect(check({})).toEqual({});
    expect(check({phone: '', address: '', emergencyContact: '', bloodType: ''})).toEqual({});
  });

  it('requires name, date of birth and gender', () => {
    expect(validatePatientForm(EMPTY_PATIENT_VALUES, {requireDoctor: false, today: TODAY})).toEqual({
      fullName: 'required',
      dateOfBirth: 'required',
      gender: 'required'
    });
  });

  it('requires a doctor only when asked (a secretary creating)', () => {
    expect(check({}, true).doctorId).toBe('required');
    expect(check({doctorId: 'd1'}, true).doctorId).toBeUndefined();
    expect(check({}, false).doctorId).toBeUndefined();
  });

  it('limits the name length', () => {
    expect(check({fullName: 'x'.repeat(200)}).fullName).toBeUndefined();
    expect(check({fullName: 'x'.repeat(201)}).fullName).toBe('fullNameTooLong');
    expect(check({fullName: '   '}).fullName).toBe('required');
  });

  it('validates the date of birth range and shape', () => {
    expect(check({dateOfBirth: TODAY}).dateOfBirth).toBeUndefined();
    expect(check({dateOfBirth: '2030-01-02'}).dateOfBirth).toBe('dateOfBirthFuture');
    expect(check({dateOfBirth: '1899-12-31'}).dateOfBirth).toBe('dateOfBirthTooOld');
    expect(check({dateOfBirth: '2020-02-31'}).dateOfBirth).toBe('dateOfBirthInvalid');
    expect(check({dateOfBirth: 'yesterday'}).dateOfBirth).toBe('dateOfBirthInvalid');
  });

  it.each(['0100 123 4567', '+20 100 123 4567', '(02) 2345-6789'])('accepts the phone %s', (phone) => {
    expect(isValidPhone(phone)).toBe(true);
    expect(check({phone}).phone).toBeUndefined();
  });

  it.each(['abc', '12345', '+++1234567', '1'.repeat(21)])('rejects the phone %s', (phone) => {
    expect(check({phone}).phone).toBe('phoneInvalid');
  });

  it('limits phone, address and emergency contact lengths', () => {
    expect(check({phone: '1'.repeat(31)}).phone).toBe('phoneTooLong');
    expect(check({address: 'a'.repeat(501)}).address).toBe('addressTooLong');
    expect(check({emergencyContact: 'a'.repeat(101)}).emergencyContact).toBe('emergencyContactTooLong');
  });

  it('turns blank text into null and formats today as a local ISO date', () => {
    expect(blankToNull('  ')).toBeNull();
    expect(blankToNull(' x ')).toBe('x');
    expect(todayIso(new Date(2030, 0, 5))).toBe('2030-01-05');
  });
});
