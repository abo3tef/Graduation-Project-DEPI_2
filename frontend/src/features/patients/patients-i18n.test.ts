import {readdirSync, readFileSync} from 'node:fs';
import {join} from 'node:path';
import {describe, expect, it} from 'vitest';
import arMessages from '@/messages/ar.json';
import enMessages from '@/messages/en.json';
import type {PatientErrorKey} from './services/patient-errors';
import {BLOOD_TYPES, GENDERS} from './types/patient.types';
import type {PatientValidationKey} from './validation/patient.validation';

type Tree = {[key: string]: string | Tree};

function leafPaths(tree: Tree, prefix = ''): string[] {
  return Object.entries(tree).flatMap(([key, value]) =>
    typeof value === 'object' ? leafPaths(value, `${prefix}${key}.`) : [`${prefix}${key}`]
  );
}

function get(tree: Tree, path: string): string {
  return path.split('.').reduce<unknown>((node, key) => (node as Tree)[key], tree) as string;
}

const en = (enMessages as unknown as {Patients: Tree}).Patients;
const ar = (arMessages as unknown as {Patients: Tree}).Patients;

describe('Patients translations (ar / en parity)', () => {
  it('have the same keys in Arabic and English', () => {
    expect(leafPaths(ar).sort()).toEqual(leafPaths(en).sort());
  });

  it('have no empty text and keep ICU placeholders identical', () => {
    const placeholders = (text: string) => [...text.matchAll(/\{(\w+)\s*[,}]/g)].map((match) => match[1]).sort();

    for (const path of leafPaths(en)) {
      expect(get(en, path).trim(), `en ${path}`).not.toBe('');
      expect(get(ar, path).trim(), `ar ${path}`).not.toBe('');
      expect(placeholders(get(ar, path)), path).toEqual(placeholders(get(en, path)));
    }
  });

  // Record<…, true> makes the compiler fail when a key is added to the code but not listed here.
  const ERROR_KEYS: Record<PatientErrorKey, true> = {
    network: true, invalidResponse: true, identity: true, hasRelatedRecords: true, patientNotFound: true,
    doctorNotFound: true, medicalForbidden: true, validation: true, forbidden: true, notFound: true,
    conflict: true, badRequest: true, server: true, unknown: true
  };
  const VALIDATION_KEYS: Record<PatientValidationKey, true> = {
    required: true, fullNameTooLong: true, dateOfBirthInvalid: true, dateOfBirthFuture: true,
    dateOfBirthTooOld: true, phoneInvalid: true, phoneTooLong: true, addressTooLong: true,
    emergencyContactTooLong: true, invalid: true
  };

  it.each([['en', en], ['ar', ar]] as const)('%s defines every error, validation, gender and blood-type label', (_n, locale) => {
    expect(Object.keys(locale.errors as Tree).sort()).toEqual(Object.keys(ERROR_KEYS).sort());
    expect(Object.keys(locale.validation as Tree).sort()).toEqual(Object.keys(VALIDATION_KEYS).sort());
    expect(Object.keys(locale.gender as Tree).sort()).toEqual([...GENDERS].sort());
    expect(Object.keys(locale.bloodType as Tree).sort()).toEqual([...BLOOD_TYPES].sort());
  });

  it('translates the new navigation entries in both languages', () => {
    for (const messages of [enMessages, arMessages] as unknown as Array<Record<string, Record<string, Tree>>>) {
      expect(messages.Appointments?.nav).toHaveProperty('patients');
      expect(messages.MedicalRecords?.common).toHaveProperty('patients');
    }
  });
});

describe('RTL / LTR conventions of the patients components', () => {
  const dir = join(__dirname, 'components');
  const sources = readdirSync(dir)
    .filter((file) => file.endsWith('.tsx') && !file.endsWith('.test.tsx'))
    .map((file) => ({file, text: readFileSync(join(dir, file), 'utf8')}));

  // Logical utilities (ms-/me-/ps-/pe-/start-/end-/text-start) mirror automatically; physical ones do not.
  const PHYSICAL = /(?:^|[\s"'`:])(?:-?m[lr]-|p[lr]-|left-|right-|text-left|text-right|border-[lr]-|rounded-[lr]-)/;

  it.each(sources.map(({file, text}) => [file, text] as const))('%s uses logical (direction-safe) utilities only', (_file, text) => {
    const classNames = [...text.matchAll(/className=(?:"([^"]*)"|\{`([^`]*)`\})/g)].map((m) => m[1] ?? m[2] ?? '');

    for (const value of classNames) {
      expect(value, value).not.toMatch(PHYSICAL);
    }
  });

  it('marks free text as direction-neutral and phones/dates as left-to-right', () => {
    const form = sources.find(({file}) => file === 'PatientForm.tsx')?.text ?? '';
    const select = sources.find(({file}) => file === 'PatientSelect.tsx')?.text ?? '';

    expect(form).toContain('dir="auto"');
    expect(form).toContain('dir="ltr"');
    expect(select).toContain('dir="auto"');
    expect(select).toContain('dir="ltr"');
  });

  it('mirrors the pagination arrows with the rtl: / ltr: variants', () => {
    const page = sources.find(({file}) => file === 'PatientsPage.tsx')?.text ?? '';

    expect(page).toContain('rtl:hidden');
    expect(page).toContain('ltr:hidden');
  });
});
