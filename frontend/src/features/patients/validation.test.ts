import {describe, expect, it} from 'vitest';
import {
  isGuid,
  PATIENT_SEARCH_DEBOUNCE_MS,
  PATIENT_SEARCH_LIMIT,
  PATIENT_SEARCH_MIN_LENGTH
} from './validation';

describe('isGuid', () => {
  it('accepts a well-formed GUID', () => {
    expect(isGuid('b0000001-0000-4000-8000-000000000001')).toBe(true);
  });

  it('accepts any case', () => {
    expect(isGuid('B0000001-0000-4000-8000-000000000001')).toBe(true);
  });

  it('rejects malformed ids before any request is made', () => {
    expect(isGuid('')).toBe(false);
    expect(isGuid('not-a-guid')).toBe(false);
    expect(isGuid('b00000010000-4000-8000-000000000001')).toBe(false);
    expect(isGuid('../../etc')).toBe(false);
  });
});

describe('patient search constants', () => {
  // The backend stays authoritative; these only decide when a request is worth
  // sending and must mirror the F1-C4 lookup rules.
  it('matches the agreed F1 lookup limits', () => {
    expect(PATIENT_SEARCH_MIN_LENGTH).toBe(2);
    expect(PATIENT_SEARCH_LIMIT).toBe(10);
    expect(PATIENT_SEARCH_DEBOUNCE_MS).toBeGreaterThan(0);
  });
});
