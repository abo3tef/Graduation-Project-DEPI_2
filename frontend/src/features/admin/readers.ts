import {createInvalidResponseError} from '@/lib/api';
import type {AdminDoctor, AdminRole, AdminStatus, AdminUser, SecretaryDoctors} from './types';

type JsonRecord = Record<string, unknown>;

const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function fail(): never {
  throw createInvalidResponseError(200);
}

function record(value: unknown): JsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
    ? (value as JsonRecord)
    : fail();
}

function text(value: unknown): string {
  if (typeof value !== 'string' || value.trim() === '') {
    fail();
  }

  return value;
}

function nullableText(value: unknown): string | null {
  if (value === null || typeof value === 'string') {
    return value;
  }

  return fail();
}

function boolean(value: unknown): boolean {
  if (typeof value === 'boolean') {
    return value;
  }

  return fail();
}

function id(value: unknown): string {
  const candidate = text(value);

  return GUID_PATTERN.test(candidate) ? candidate : fail();
}

function oneOf<T extends string>(value: unknown, allowed: readonly T[]): T {
  if (typeof value === 'string' && (allowed as readonly string[]).includes(value)) {
    return value as T;
  }

  return fail();
}

const ROLES = ['Admin', 'Doctor', 'Secretary'] as const;
const STATUSES = ['Active', 'Inactive'] as const;

export function readAdminUser(value: unknown): AdminUser {
  const source = record(value);

  return {
    id: id(source.id),
    role: oneOf<AdminRole>(source.role, ROLES),
    fullName: text(source.fullName),
    email: text(source.email),
    phone: nullableText(source.phone),
    specialization: nullableText(source.specialization),
    status: oneOf<AdminStatus>(source.status, STATUSES),
    emailVerified: boolean(source.emailVerified),
    createdAtUtc: text(source.createdAtUtc)
  };
}

export function readAdminDoctor(value: unknown): AdminDoctor {
  const source = record(value);

  return {
    id: id(source.id),
    fullName: text(source.fullName),
    email: text(source.email)
  };
}

export function readSecretaryDoctors(value: unknown): SecretaryDoctors {
  const source = record(value);

  if (!Array.isArray(source.doctors)) {
    fail();
  }

  return {
    secretaryId: id(source.secretaryId),
    doctors: (source.doctors as unknown[]).map(readAdminDoctor)
  };
}
