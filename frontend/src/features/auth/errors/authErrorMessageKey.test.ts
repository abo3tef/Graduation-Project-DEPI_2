import {describe, expect, it} from 'vitest';
import {ApiError, type ApiErrorKind} from '@/lib/api';
import {authErrorMessageKey, authFieldErrors} from './authErrorMessageKey';
import {REGISTER_FIELDS} from '../validation/auth.validation';

function apiError(init: {status: number; code?: string; kind?: ApiErrorKind}): ApiError {
  return new ApiError({
    status: init.status,
    kind: init.kind ?? 'validation',
    code: init.code ?? null
  });
}

describe('authErrorMessageKey', () => {
  it.each([
    ['auth.invalid_credentials', 401, 'invalidCredentials'],
    ['auth.email_unverified', 403, 'emailUnverified'],
    ['auth.account_inactive', 403, 'accountInactive'],
    ['auth.email_already_registered', 409, 'emailAlreadyRegistered'],
    ['auth.token_invalid', 401, 'tokenInvalid'],
    ['auth.invalid_google_token', 401, 'invalidGoogleToken'],
    ['auth.google_not_configured', 503, 'googleNotConfigured'],
    ['auth.not_configured', 503, 'notConfigured'],
    ['validation.failed', 400, 'validation']
  ] as const)('maps %s to %s', (code, status, expected) => {
    expect(authErrorMessageKey(apiError({status, code}))).toBe(expected);
  });

  it('uses the stable code before the status, so a 401 token failure is not a credential error', () => {
    expect(authErrorMessageKey(apiError({status: 401, code: 'auth.token_invalid'}))).toBe(
      'tokenInvalid'
    );
  });

  it.each([
    [400, 'invalidCredentials-replacement', 'validation'],
    [401, 'fallback', 'invalidCredentials'],
    [403, 'fallback', 'forbidden'],
    [500, 'fallback', 'server'],
    [418, 'fallback', 'unknown']
  ] as const)('falls back by status %s', (status, _label, expected) => {
    expect(authErrorMessageKey(apiError({status}))).toBe(expected);
  });

  it('reports a transport failure as a network error', () => {
    expect(authErrorMessageKey(apiError({status: 0, kind: 'network'}))).toBe('network');
  });

  it('does not trust an unknown code', () => {
    expect(authErrorMessageKey(apiError({status: 400, code: 'auth.something_new'}))).toBe(
      'validation'
    );
  });

  it('falls back to unknown for a value that is not an ApiError', () => {
    expect(authErrorMessageKey(new Error('boom'))).toBe('unknown');
    expect(authErrorMessageKey(undefined)).toBe('unknown');
  });
});

describe('authFieldErrors', () => {
  it('maps FluentValidation rule codes to translated keys', () => {
    expect(
      authFieldErrors(
        {email: ['An email address is required.']},
        {email: ['NotEmptyValidator']},
        REGISTER_FIELDS
      )
    ).toEqual({email: 'required'});

    expect(
      authFieldErrors({}, {email: ['EmailValidator']}, REGISTER_FIELDS)
    ).toEqual({email: 'invalidEmail'});

    expect(
      authFieldErrors({}, {fullName: ['MaximumLengthValidator']}, REGISTER_FIELDS)
    ).toEqual({fullName: 'tooLong'});

    expect(
      authFieldErrors({}, {password: ['PredicateValidator']}, REGISTER_FIELDS)
    ).toEqual({password: 'passwordPolicy'});
  });

  it('renames the reset payload field to the form field', () => {
    expect(
      authFieldErrors({}, {newPassword: ['PredicateValidator']}, ['password', 'confirmPassword'])
    ).toEqual({password: 'passwordPolicy'});
  });

  it('drops a field the form does not have', () => {
    expect(authFieldErrors({}, {token: ['NotEmptyValidator']}, REGISTER_FIELDS)).toEqual({});
  });

  it('still marks a field the server answered with a message but no rule code', () => {
    expect(authFieldErrors({role: ['Only Doctor or Secretary accounts can be registered.']}, {}, REGISTER_FIELDS)).toEqual({
      role: 'invalid'
    });
  });

  it('falls back to the policy text for a password rule it does not know', () => {
    expect(authFieldErrors({}, {password: ['SomeNewValidator']}, REGISTER_FIELDS)).toEqual({
      password: 'passwordPolicy'
    });
  });

  it('ignores the server English text entirely', () => {
    const mapped = authFieldErrors(
      {email: ['The email address is not valid.']},
      {email: ['EmailValidator']},
      REGISTER_FIELDS
    );

    expect(JSON.stringify(mapped)).not.toContain('The email address');
  });
});
