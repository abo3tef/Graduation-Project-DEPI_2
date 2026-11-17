import {createTranslator} from 'next-intl';
import {describe, expect, it} from 'vitest';
import en from '@/messages/en.json';
import ar from '@/messages/ar.json';

function leaves(value: unknown, prefix = ''): Record<string, string> {
  if (typeof value === 'string') return {[prefix]: value};
  if (typeof value !== 'object' || value === null || Array.isArray(value)) throw new Error('Unexpected message shape');
  return Object.fromEntries(Object.entries(value).flatMap(([key, item]) =>
    Object.entries(leaves(item, prefix ? `${prefix}.${key}` : key))));
}

describe('vital signs locale audit', () => {
  it('has matching, nonempty ar/en keys and interpolation arguments', () => {
    const english = leaves(en.MedicalRecords.vitalSigns);
    const arabic = leaves(ar.MedicalRecords.vitalSigns);
    expect(Object.keys(arabic).sort()).toEqual(Object.keys(english).sort());
    const argumentsOf = (value: string) => [...value.matchAll(/\{(\w+)\s*[,}]/g)].map(match => match[1]).sort();
    for (const [key, text] of Object.entries(english)) {
      expect(text.trim(), key).not.toBe('');
      expect(arabic[key].trim(), key).not.toBe('');
      expect(argumentsOf(arabic[key]), key).toEqual(argumentsOf(text));
    }
  });

  it.each(['en', 'ar'] as const)('resolves all %s vitals messages without missing keys or ICU errors', locale => {
    const messages = locale === 'en' ? en : ar;
    const t = createTranslator({locale, messages: messages.MedicalRecords.vitalSigns,
      onError: error => {throw error;}});
    for (const key of Object.keys(leaves(messages.MedicalRecords.vitalSigns))) {
      const result = t(key as Parameters<typeof t>[0], {count: 2, number: 1, page: 1, total: 2, min: 20, max: 300});
      expect(result.trim()).not.toBe('');
      expect(result).not.toBe(key);
    }
    expect(t('orderingNote')).not.toMatch(/latest|most recent|الأحدث|أحدث قياس/i);
  });
});


it.each(['en', 'ar'] as const)('provides localized write actions and saved-preview labels in %s', locale => {
  const messages = (locale === 'en' ? en : ar).MedicalRecords.vitalSigns;
  const t = createTranslator({locale, messages, onError: error => {throw error;}});
  for (const key of ['addAction', 'editAction', 'savedPreviewTitle', 'savedPreviewDescription'] as const) {
    expect(t(key).trim()).not.toBe('');
  }
  expect(t('editLabel', {number: 3})).toContain('3');
  expect(messages.savedPreviewTitle).not.toMatch(/latest|most recent/i);
});

const writeLabelKeys = ['addAction', 'editAction', 'editLabel', 'savedPreviewTitle', 'savedPreviewDescription'] as const;
function assertArabicWriteLabel(value: string, english: string): void {
  expect(value.trim()).not.toBe('');
  expect(value).not.toMatch(/[?\uFFFD]/u);
  expect(value).toMatch(/[\u0621-\u064A]/u);
  const args = (text: string) => [...text.matchAll(/\{(\w+)\s*[,}]/g)].map(match => match[1]).sort();
  expect(args(value)).toEqual(args(english));
}

it.each(writeLabelKeys)('keeps %s as readable Arabic with the English ICU contract', key => {
  assertArabicWriteLabel(ar.MedicalRecords.vitalSigns[key], en.MedicalRecords.vitalSigns[key]);
});

it.each([
  '????? {number}', '\uFFFD {number}', '', '  ', 'Edit record {number}',
  '\u062A\u0639\u062F\u064A\u0644', '\u062A\u0639\u062F\u064A\u0644 {wrong}',
  '\u062A\u0639\u062F\u064A\u0644 ? {number}'
])('the Arabic label guard rejects corrupted text or ICU arguments: %j', value => {
  expect(() => assertArabicWriteLabel(value, 'Edit record {number}')).toThrow();
});
