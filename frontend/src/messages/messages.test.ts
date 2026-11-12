import {describe, expect, it} from 'vitest';
import arMessages from './ar.json';
import enMessages from './en.json';
import type {AppointmentErrorKey} from '@/features/appointments/services/appointment-errors';
import {APPOINTMENT_STATUSES} from '@/features/appointments/types/appointment.types';
import type {ValidationKey} from '@/features/appointments/validation/appointment.validation';
import type {AppointmentAction} from '@/features/appointments/utils/status-actions';

type Tree = {[key: string]: string | string[] | Tree};

function leafPaths(tree: Tree, prefix = ''): string[] {
  return Object.entries(tree).flatMap(([key, value]) =>
    typeof value === 'object' && !Array.isArray(value)
      ? leafPaths(value, `${prefix}${key}.`)
      : [`${prefix}${key}`]
  );
}

function get(tree: Tree, path: string): unknown {
  return path.split('.').reduce<unknown>((node, key) => (node as Tree | undefined)?.[key], tree);
}

const en = enMessages as unknown as Tree;
const ar = arMessages as unknown as Tree;

describe('translations', () => {
  it('have the same namespaces in Arabic and English', () => {
    expect(Object.keys(ar).sort()).toEqual(Object.keys(en).sort());
  });

  it.each(Object.keys(enMessages))('namespace %s has identical keys in ar and en', (namespace) => {
    expect(leafPaths(ar[namespace] as Tree).sort()).toEqual(leafPaths(en[namespace] as Tree).sort());
  });

  it.each(['Appointments', 'DevIdentity'])('%s has no empty or untranslated strings', (namespace) => {
    for (const path of leafPaths(en[namespace] as Tree)) {
      const english = get(en[namespace] as Tree, path);
      const arabic = get(ar[namespace] as Tree, path);

      expect(typeof english === 'string' && english.trim() !== '', `en ${namespace}.${path}`).toBe(true);
      expect(typeof arabic === 'string' && arabic.trim() !== '', `ar ${namespace}.${path}`).toBe(true);
    }
  });

  it('keeps ICU placeholders identical between languages', () => {
    const placeholders = (text: string) =>
      // Real arguments only: `{name}` or `{name, plural, …}`; not the text of a plural branch.
      [...text.matchAll(/\{(\w+)\s*[,}]/g)].map((match) => match[1]).sort();

    for (const namespace of ['Appointments', 'DevIdentity']) {
      for (const path of leafPaths(en[namespace] as Tree)) {
        const english = get(en[namespace] as Tree, path) as string;
        const arabic = get(ar[namespace] as Tree, path) as string;

        expect(placeholders(arabic), `${namespace}.${path}`).toEqual(placeholders(english));
      }
    }
  });
});

describe('message coverage of the code', () => {
  // Record<…, true> makes the compiler fail if a key is added to the code without being listed here.
  const ERROR_KEYS: Record<AppointmentErrorKey, true> = {
    network: true, invalidResponse: true, identity: true, completeRequiresDoctor: true, doctorNotFound: true,
    patientNotFound: true, appointmentNotFound: true, overlap: true, doctorUnavailable: true, scheduleBusy: true,
    validation: true, timeNotInTimeZone: true, inPast: true, invalidInterval: true, endTimeRequired: true,
    notOpen: true, invalidTransition: true, cancellationReasonRequired: true, cancellationReasonTooLong: true,
    forbidden: true, notFound: true, conflict: true, badRequest: true, server: true, unknown: true
  };
  const VALIDATION_KEYS: Record<ValidationKey, true> = {
    required: true, invalidDate: true, invalidTime: true, endAfterStart: true, inPast: true, reasonTooLong: true,
    cancelReasonTooLong: true, invalid: true, timeNotInTimeZone: true
  };
  const ACTION_KEYS: Record<AppointmentAction, true> = {
    reschedule: true, cancel: true, confirm: true, noShow: true, complete: true
  };

  const appointments = (locale: Tree) => locale.Appointments as Tree;

  it.each([['en', en], ['ar', ar]] as const)('%s defines a message for every error key', (_name, locale) => {
    expect(Object.keys(appointments(locale).errors as Tree).sort()).toEqual(Object.keys(ERROR_KEYS).sort());
  });

  it.each([['en', en], ['ar', ar]] as const)('%s defines a message for every validation key', (_name, locale) => {
    expect(Object.keys(appointments(locale).validation as Tree).sort()).toEqual(Object.keys(VALIDATION_KEYS).sort());
  });

  it.each([['en', en], ['ar', ar]] as const)('%s defines a label for every status and action', (_name, locale) => {
    const messages = appointments(locale);

    expect(Object.keys(messages.status as Tree).sort()).toEqual([...APPOINTMENT_STATUSES].sort());
    for (const action of Object.keys(ACTION_KEYS)) {
      expect(messages.actions as Tree).toHaveProperty(action);
    }
  });
});
