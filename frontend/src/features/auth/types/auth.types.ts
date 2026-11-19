import {createInvalidResponseError} from '@/lib/api';
import {isSessionRole, type SessionUser, type SessionUserRole} from '../session/session-store';

/**
 * Request and response shapes of `/api/auth` (F6-C1 contract, `backend/docs/api/AUTH_API.md`).
 * The types mirror the frozen DTOs; the readers below are the boundary where untrusted JSON
 * becomes typed data, because the project has no schema library.
 */
export type AuthUserRole = SessionUserRole;

export type AuthUser = SessionUser;

export type SignInResult = {
  readonly accessToken: string;
  readonly expiresAtUtc: string;
  readonly user: AuthUser;
};

/** The account summary `POST /api/auth/register` answers with; it contains no token. */
export type AccountSummary = {
  readonly id: string;
  readonly fullName: string;
  readonly email: string;
  readonly role: AuthUserRole;
  readonly doctorId: string | null;
  readonly specialization: string | null;
};

/** Registration accepts only these two roles (a closed allow-list; `Admin` is seeded, never registered). */
export const REGISTER_ROLES = ['Doctor', 'Secretary'] as const;

export type RegisterRole = (typeof REGISTER_ROLES)[number];

export type LoginInput = {
  email: string;
  password: string;
};

export type RegisterInput = {
  fullName: string;
  email: string;
  password: string;
  phone: string | null;
  role: RegisterRole;
  specialization: string | null;
};

export type VerifyEmailInput = {
  token: string;
  /** Copied from the emailed link for resend prefill only; the token alone authorizes verification. */
  email?: string;
};

export type ResetPasswordInput = {
  token: string;
  newPassword: string;
};

export function isRegisterRole(value: unknown): value is RegisterRole {
  return (REGISTER_ROLES as readonly unknown[]).includes(value);
}

function fail(): never {
  throw createInvalidResponseError(201);
}

function record(value: unknown): Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : fail();
}

function text(value: unknown): string {
  return typeof value === 'string' ? value : fail();
}

function optionalText(value: unknown): string | null {
  return value === null || value === undefined ? null : text(value);
}

function readUser(value: unknown): AuthUser {
  const source = record(value);
  const role = source.role;

  if (!isSessionRole(role)) {
    fail();
  }

  return {
    id: text(source.id),
    fullName: text(source.fullName),
    email: text(source.email),
    role,
    doctorId: optionalText(source.doctorId),
    specialization: optionalText(source.specialization)
  };
}

export function readSignInResult(value: unknown): SignInResult {
  const source = record(value);

  return {
    accessToken: text(source.accessToken),
    expiresAtUtc: text(source.expiresAtUtc),
    user: readUser(source.user)
  };
}

export function readAccountSummary(value: unknown): AccountSummary {
  const source = record(value);
  const role = source.role;

  if (!isSessionRole(role)) {
    fail();
  }

  return {
    id: text(source.id),
    fullName: text(source.fullName),
    email: text(source.email),
    role,
    doctorId: optionalText(source.doctorId),
    specialization: optionalText(source.specialization)
  };
}
