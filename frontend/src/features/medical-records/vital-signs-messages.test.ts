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
