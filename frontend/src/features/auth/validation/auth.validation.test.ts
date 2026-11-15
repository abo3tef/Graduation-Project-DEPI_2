import {describe, expect, it} from 'vitest';
import {
  FULL_NAME_MAX_LENGTH,
  hasErrors,
  isStrongPassword,
  isValidEmail,
  validateLoginForm,
  validateRegisterForm,
  validateResetForm
} from './auth.validation';

const VALID = {
  fullName: 'New Doctor',
  email: 'new.doctor@example.test',
  password: 'Medilog@2026',
  confirmPassword: 'Medilog@2026',
  phone: '',
  specialization: 'Cardiology',
  role: 'Doctor' as const
};

describe('isStrongPassword', () => {
  it.each([
    ['Medilog@2026', true],
    ['short@A1', true], // exactly 8 characters, the minimum length
    ['Ab1!abc', false], // 7 characters: below the minimum
    ['abcDEF12', false], // no symbol
    ['abcdefg!', false], // no upper case
    ['ABCDEFG!', false], // no lower case
    ['Abcdefgh!', false], // no digit
    [`Aa1!${'x'.repeat(124)}`, true], // exactly 128 characters
    [`Aa1!${'x'.repeat(125)}`, false] // 129 characters
  ])('%s → %s', (password, expected) => {
    expect(isStrongPassword(password)).toBe(expected);
  });

  it('treats an 8-character password with every class as strong', () => {
    expect(isStrongPassword('Abcdef1!')).toBe(true);
  });

  it('accepts non-ASCII letters and symbols the server also accepts', () => {
    expect(isStrongPassword('كلمة1!Aa')).toBe(true);
  });
});

describe('isValidEmail', () => {
  it.each([
    ['doctor@medilog.local', true],
    ['a.b+tag@example.co.uk', true],
    ['not-an-email', false],
    ['missing@domain', false],
    ['two words@example.test', false],
    ['@example.test', false]
  ])('%s → %s', (email, expected) => {
    expect(isValidEmail(email)).toBe(expected);
  });
});

describe('validateLoginForm', () => {
  it('accepts a well-formed sign-in', () => {
    expect(validateLoginForm({email: 'doctor@medilog.local', password: 'anything'})).toEqual({});
  });

  it('marks both empty fields', () => {
    expect(validateLoginForm({email: '', password: ''})).toEqual({
      email: 'required',
      password: 'required'
    });
  });

  it('marks an invalid email', () => {
    expect(validateLoginForm({email: 'doctor', password: 'x'})).toEqual({email: 'invalidEmail'});
  });

  it('does not apply the password policy at sign-in', () => {
    // An existing account may predate the policy; rejecting the shape would leak policy details.
    expect(validateLoginForm({email: 'doctor@medilog.local', password: 'weak'})).toEqual({});
  });

  it('rejects a password longer than the accepted maximum', () => {
    expect(validateLoginForm({email: 'doctor@medilog.local', password: 'a'.repeat(129)})).toEqual({
      password: 'tooLong'
    });
  });
});

describe('validateRegisterForm', () => {
  it('accepts a complete doctor registration', () => {
    expect(validateRegisterForm(VALID)).toEqual({});
  });

  it('accepts a secretary without a specialization', () => {
    expect(validateRegisterForm({...VALID, role: 'Secretary', specialization: ''})).toEqual({});
  });

  it('requires a role so neither role can be assumed', () => {
    expect(validateRegisterForm({...VALID, role: ''})).toEqual({role: 'required'});
  });

  it('requires every mandatory field', () => {
    const errors = validateRegisterForm({
      fullName: ' ',
      email: '',
      password: '',
      confirmPassword: '',
      phone: '',
      specialization: '',
      role: 'Doctor'
    });

    expect(errors).toEqual({
      fullName: 'required',
      email: 'required',
      password: 'required',
      confirmPassword: 'required'
    });
  });

  it('rejects a full name longer than the persisted column', () => {
    expect(validateRegisterForm({...VALID, fullName: 'a'.repeat(FULL_NAME_MAX_LENGTH + 1)})).toEqual({
      fullName: 'tooLong'
    });
  });

  it('rejects a password that does not satisfy the policy', () => {
    expect(validateRegisterForm({...VALID, password: 'password', confirmPassword: 'password'})).toEqual({
      password: 'passwordPolicy'
    });
  });

  it('reports a confirmation that does not match', () => {
    expect(validateRegisterForm({...VALID, confirmPassword: 'Medilog@2027'})).toEqual({
      confirmPassword: 'passwordMismatch'
    });
  });

  it('rejects a phone or specialization longer than the columns', () => {
    expect(validateRegisterForm({...VALID, phone: '1'.repeat(31)})).toEqual({phone: 'tooLong'});
    expect(validateRegisterForm({...VALID, specialization: 'a'.repeat(151)})).toEqual({
      specialization: 'tooLong'
    });
  });

  it('checks the confirmation before the policy so the policy is still reported alone', () => {
    expect(validateRegisterForm({...VALID, password: 'weak', confirmPassword: 'weak'})).toEqual({
      password: 'passwordPolicy'
    });
  });
});

describe('validateResetForm', () => {
  it('accepts a strong pair', () => {
    expect(validateResetForm({password: 'Medilog@2026', confirmPassword: 'Medilog@2026'})).toEqual({});
  });

  it('applies the same password policy as registration', () => {
    expect(validateResetForm({password: 'weak', confirmPassword: 'weak'})).toEqual({
      password: 'passwordPolicy'
    });
  });

  it('requires the confirmation', () => {
    expect(validateResetForm({password: 'Medilog@2026', confirmPassword: ''})).toEqual({
      confirmPassword: 'required'
    });
  });

  it('reports a mismatch', () => {
    expect(validateResetForm({password: 'Medilog@2026', confirmPassword: 'Medilog@2027'})).toEqual({
      confirmPassword: 'passwordMismatch'
    });
  });
});

describe('hasErrors', () => {
  it('recognizes an empty and a populated error map', () => {
    expect(hasErrors({})).toBe(false);
    expect(hasErrors({email: 'required'})).toBe(true);
  });
});
