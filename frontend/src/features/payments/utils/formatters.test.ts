import {describe, expect, it} from 'vitest';
import {
  formatCurrency,
  formatDate,
  formatDateTime,
  formatTime,
  parseAmountInput
} from './formatters';

describe('formatDateTime', () => {
  it('renders a dash for a payment that is not paid yet', () => {
    expect(formatDateTime(null, 'en')).toBe('—');
    expect(formatDateTime(undefined, 'en')).toBe('—');
    expect(formatDateTime('', 'en')).toBe('—');
  });

  it('formats an ISO timestamp', () => {
    expect(formatDateTime('2026-11-12T10:00:00+00:00', 'en')).not.toBe('—');
  });
});

describe('formatDate', () => {
  it('formats an ISO date and renders a dash when absent', () => {
    expect(formatDate('2026-11-12', 'en')).not.toBe('—');
    expect(formatDate(null, 'en')).toBe('—');
  });
});

describe('formatTime', () => {
  it('accepts both HH:mm and HH:mm:ss', () => {
    expect(formatTime('14:05', 'en')).toContain('2:05');
    expect(formatTime('14:05:00', 'en')).toContain('2:05');
  });

  it('renders a dash for an invalid time', () => {
    expect(formatTime('not-a-time', 'en')).toBe('—');
  });
});

describe('formatCurrency', () => {
  it('formats a finite amount and renders a dash otherwise', () => {
    expect(formatCurrency(500, 'en')).toContain('500');
    expect(formatCurrency(Number.NaN, 'en')).toBe('—');
  });
});

describe('parseAmountInput', () => {
  it('accepts up to two decimals and Arabic-Indic digits', () => {
    expect(parseAmountInput('250.50')).toBe(250.5);
    expect(parseAmountInput('٢٥٠٫٥٠')).toBe(250.5);
    expect(parseAmountInput('250,50')).toBe(250.5);
  });

  it('rejects more than two decimals and non-numeric text', () => {
    expect(parseAmountInput('1.234')).toBeNull();
    expect(parseAmountInput('abc')).toBeNull();
  });
});
