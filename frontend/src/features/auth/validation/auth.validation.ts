/**
 * Client-side validation of the auth forms. These checks mirror the server rules for fast
 * feedback only (AGENTS.md §3.2): the backend remains the authority, and a request that skips the
 * form is still rejected there.
 */
export const FULL_NAME_MAX_LENGTH = 200;
export const EMAIL_MAX_LENGTH = 320;
export const PHONE_MAX_LENGTH = 30;
export const SPECIALIZATION_MAX_LENGTH = 150;
export const PASSWORD_MIN_LENGTH = 8;
export const PASSWORD_MAX_LENGTH = 128;

/** Keys of the `Auth.common` messages used as field errors. */
export type AuthValidationKey =
  | 'required'
  | 'invalidEmail'
  | 'tooLong'
  | 'passwordPolicy'
  | 'passwordMismatch'
  | 'invalid';

export const LOGIN_FIELDS = ['email', 'password'] as const;
export const REGISTER_FIELDS = [
  'fullName',
  'email',
  'password',
  'confirmPassword',
  'phone',
  'specialization',
  'role'
] as const;
export const RESET_FIELDS = ['password', 'confirmPassword'] as const;

export type LoginField = (typeof LOGIN_FIELDS)[number];
export type RegisterField = (typeof REGISTER_FIELDS)[number];
export type ResetField = (typeof RESET_FIELDS)[number];
export type AuthField = LoginField | RegisterField | ResetField;

export type AuthFieldErrors<TField extends string> = Partial<Record<TField, AuthValidationKey>>;

/** The same shape the server's `errors` map uses: one or more messages/codes per field. */
export type AuthServerFieldErrors = Readonly<Record<string, readonly string[]>>;

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/;

export function hasErrors(errors: Readonly<Record<string, unknown>>): boolean {
  return Object.keys(errors).length > 0;
}

export function isValidEmail(email: string): boolean {
  return EMAIL_PATTERN.test(email);
}

/**
 * Mirrors `PasswordPolicy.IsSatisfiedBy` (F6-C1): 8-128 characters with an upper-case letter, a
 * lower-case letter, a digit and a symbol (anything that is neither a letter, a digit nor a space).
 */
export function isStrongPassword(password: string): boolean {
  if (password.length < PASSWORD_MIN_LENGTH || password.length > PASSWORD_MAX_LENGTH) {
    return false;
  }

  let hasUpper = false;
  let hasLower = false;
  let hasDigit = false;
  let hasSymbol = false;

  for (const character of password) {
    if (character.toLowerCase() !== character) {
      hasUpper = true;
    } else if (character.toUpperCase() !== character) {
      hasLower = true;
    } else if (character >= '0' && character <= '9') {
      hasDigit = true;
    } else if (character.trim() !== '') {
      hasSymbol = true;
    }
  }

  return hasUpper && hasLower && hasDigit && hasSymbol;
}

function validateEmailField(email: string): AuthValidationKey | null {
  const value = email.trim();

  if (value === '') {
    return 'required';
  }

  if (value.length > EMAIL_MAX_LENGTH) {
    return 'tooLong';
  }

  return isValidEmail(value) ? null : 'invalidEmail';
}

function validatePasswordField(password: string): AuthValidationKey | null {
  if (password === '') {
    return 'required';
  }

  return isStrongPassword(password) ? null : 'passwordPolicy';
}

function validateOptionalLength(value: string, maxLength: number): AuthValidationKey | null {
  return value.trim().length > maxLength ? 'tooLong' : null;
}

export type LoginFormValues = {email: string; password: string};

export function validateLoginForm(values: LoginFormValues): AuthFieldErrors<LoginField> {
  const errors: AuthFieldErrors<LoginField> = {};
  const email = validateEmailField(values.email);

  if (email) {
    errors.email = email;
  }

  // Sign-in checks presence only: an existing account may predate the password policy.
  if (values.password === '') {
    errors.password = 'required';
  } else if (values.password.length > PASSWORD_MAX_LENGTH) {
    errors.password = 'tooLong';
  }

  return errors;
}

export type RegisterFormValues = {
  fullName: string;
  email: string;
  password: string;
  confirmPassword: string;
  phone: string;
  specialization: string;
  role: '' | 'Doctor' | 'Secretary';
};

export function validateRegisterForm(
  values: RegisterFormValues
): AuthFieldErrors<RegisterField> {
  const errors: AuthFieldErrors<RegisterField> = {};
  const fullName = values.fullName.trim();

  if (fullName === '') {
    errors.fullName = 'required';
  } else if (fullName.length > FULL_NAME_MAX_LENGTH) {
    errors.fullName = 'tooLong';
  }

  const email = validateEmailField(values.email);

  if (email) {
    errors.email = email;
  }

  const password = validatePasswordField(values.password);

  if (password) {
    errors.password = password;
  }

  if (values.confirmPassword === '') {
    errors.confirmPassword = 'required';
  } else if (values.confirmPassword !== values.password) {
    errors.confirmPassword = 'passwordMismatch';
  }

  const phone = validateOptionalLength(values.phone, PHONE_MAX_LENGTH);

  if (phone) {
    errors.phone = phone;
  }

  if (values.role === '') {
    errors.role = 'required';
  } else if (values.role === 'Secretary') {
    // The server rejects a specialization for a secretary, so it is not validated for one.
    return errors;
  }

  if (values.specialization.trim() === '') {
    errors.specialization = 'required';
    return errors;
  }

  const specialization = validateOptionalLength(
    values.specialization,
    SPECIALIZATION_MAX_LENGTH
  );

  if (specialization) {
    errors.specialization = specialization;
  }

  return errors;
}

export type ResetFormValues = {password: string; confirmPassword: string};

export function validateResetForm(values: ResetFormValues): AuthFieldErrors<ResetField> {
  const errors: AuthFieldErrors<ResetField> = {};
  const password = validatePasswordField(values.password);

  if (password) {
    errors.password = password;
  }

  if (values.confirmPassword === '') {
    errors.confirmPassword = 'required';
  } else if (values.confirmPassword !== values.password) {
    errors.confirmPassword = 'passwordMismatch';
  }

  return errors;
}
