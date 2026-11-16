import {isApiError} from '@/lib/api';
import type {AuthServerFieldErrors, AuthValidationKey} from '../validation/auth.validation';

/** Keys of the `Auth.errors` messages. */
export type AuthErrorKey =
  | 'invalidCredentials'
  | 'emailUnverified'
  | 'accountInactive'
  | 'emailAlreadyRegistered'
  | 'tokenInvalid'
  | 'invalidGoogleToken'
  | 'googleNotConfigured'
  | 'notConfigured'
  | 'validation'
  | 'forbidden'
  | 'network'
  | 'server'
  | 'unknown';

/** API `code` → message key. Codes are frozen in `backend/docs/api/AUTH_API.md` §6. */
const CODE_TO_KEY: Readonly<Record<string, AuthErrorKey>> = {
  'auth.invalid_credentials': 'invalidCredentials',
  'auth.email_unverified': 'emailUnverified',
  'auth.account_inactive': 'accountInactive',
  'auth.email_already_registered': 'emailAlreadyRegistered',
  'auth.token_invalid': 'tokenInvalid',
  'auth.invalid_google_token': 'invalidGoogleToken',
  'auth.google_not_configured': 'googleNotConfigured',
  'auth.not_configured': 'notConfigured',
  'validation.failed': 'validation'
};

function keyFromStatus(status: number): AuthErrorKey {
  if (status === 0) {
    return 'network';
  }

  if (status === 400) {
    return 'validation';
  }

  if (status === 401) {
    // The server always names the reason; this is only the fallback for a body without a code.
    return 'invalidCredentials';
  }

  if (status === 403) {
    return 'forbidden';
  }

  return status >= 500 ? 'server' : 'unknown';
}

/**
 * Picks the translated message key for a failed auth request. Only the stable `code` and the HTTP
 * status are used; the English `title`/`detail` is never rendered (AGENTS.md §4.4).
 */
export function authErrorMessageKey(error: unknown): AuthErrorKey {
  if (!isApiError(error)) {
    return 'unknown';
  }

  if (error.code && CODE_TO_KEY[error.code]) {
    return CODE_TO_KEY[error.code];
  }

  return keyFromStatus(error.status);
}

/** The field the server names in `errors` → the field name the form uses. */
const FIELD_ALIASES: Readonly<Record<string, string>> = {
  newPassword: 'password'
};

/** FluentValidation rule codes → the translated validation message. */
function validationKeyFromCode(code: string | undefined): AuthValidationKey | null {
  switch (code) {
    case 'NotNullValidator':
    case 'NotEmptyValidator':
      return 'required';
    case 'EmailValidator':
      return 'invalidEmail';
    case 'MaximumLengthValidator':
    case 'MinimumLengthValidator':
    case 'LengthValidator':
      return 'tooLong';
    case 'PredicateValidator':
    case 'RegexValidator':
    case 'AsyncPredicateValidator':
      return 'passwordPolicy';
    default:
      return null;
  }
}

/**
 * Combines the server's `errors` (messages) and `errorCodes` (rule names) into translated field
 * errors. Fields outside `allowed` are dropped so a server field never creates an unknown key,
 * and the server's English text is never shown. A field with an unrecognised rule falls back to a
 * generic message; passwords fall back to the policy text because that is the only rule they have.
 */
export function authFieldErrors<TField extends string>(
  fieldErrors: AuthServerFieldErrors,
  fieldErrorCodes: AuthServerFieldErrors,
  allowed: readonly TField[]
): Partial<Record<TField, AuthValidationKey>> {
  const result: Partial<Record<TField, AuthValidationKey>> = {};

  for (const [rawField, codes] of Object.entries(fieldErrorCodes)) {
    const field = FIELD_ALIASES[rawField] ?? rawField;

    if (!(allowed as readonly string[]).includes(field)) {
      continue;
    }

    const mapped = codes.map(validationKeyFromCode).find((key) => key !== null);

    if (mapped) {
      result[field as TField] = mapped;
      continue;
    }

    result[field as TField] =
      field === 'password' || field === 'confirmPassword' ? 'passwordPolicy' : 'invalid';
  }

  // A field the server only answered with a message (no rule code) still gets a generic mark.
  for (const rawField of Object.keys(fieldErrors)) {
    const field = FIELD_ALIASES[rawField] ?? rawField;

    if (
      (allowed as readonly string[]).includes(field) &&
      result[field as TField] === undefined
    ) {
      result[field as TField] = 'invalid';
    }
  }

  return result;
}
