import {describe, expect, it} from 'vitest';
import {
  addMinutesToTime,
  getClinicNow,
  intervalsOverlap,
  isBefore,
  isValidIsoDate,
  isValidTime,
  normalizeTime,
  timeToMinutes,
  toHoursMinutes,
  toUtcDate
} from './date-time';

describe('isValidIsoDate', () => {
  it.each(['2030-01-02', '2024-02-29', '1999-12-31'])('accepts %s', (value) => {
    expect(isValidIsoDate(value)).toBe(true);
  });

  it.each(['', '2030-1-2', '2030-02-30', '2023-02-29', '2030-13-01', '2030-00-10', '01/02/2030', 'tomorrow'])(
    'rejects %j',
    (value) => {
      expect(isValidIsoDate(value)).toBe(false);
    }
  );
});

describe('time helpers', () => {
  it.each(['00:00', '09:05', '23:59', '10:30:45'])('accepts %s', (value) => {
    expect(isValidTime(value)).toBe(true);
  });

  it.each(['', '24:00', '9:05', '10:60', '10:30:60', 'noon'])('rejects %j', (value) => {
    expect(isValidTime(value)).toBe(false);
  });

  it('normalizes HH:mm and HH:mm:ss to HH:mm:ss', () => {
    expect(normalizeTime('09:05')).toBe('09:05:00');
    expect(normalizeTime('09:05:30')).toBe('09:05:30');
  });

  it('shortens HH:mm:ss for display and leaves other text alone', () => {
    expect(toHoursMinutes('09:05:30')).toBe('09:05');
    expect(toHoursMinutes('09:05')).toBe('09:05');
    expect(toHoursMinutes('later')).toBe('later');
  });

  it('converts to minutes', () => {
    expect(timeToMinutes('00:00')).toBe(0);
    expect(timeToMinutes('10:30:59')).toBe(630);
    expect(timeToMinutes('nope')).toBeNaN();
  });

  it.each([
    ['09:00', 30, '09:30'],
    ['09:45', 15, '10:00'],
    ['23:30', 29, '23:59'],
    ['23:30', 30, null],
    ['10:00', -30, '09:30'],
    ['00:10', -30, null],
    ['bad', 15, null]
  ])('addMinutesToTime(%s, %d) → %s', (time, minutes, expected) => {
    expect(addMinutesToTime(time, minutes)).toBe(expected);
  });
});

describe('getClinicNow (Africa/Cairo)', () => {
  it('uses the clinic wall clock, not UTC (winter, UTC+2)', () => {
    // 2030-01-01 22:30 UTC is already 2030-01-02 00:30 in Cairo (UTC+2).
    expect(getClinicNow(new Date('2030-01-01T22:30:00Z'), 'Africa/Cairo')).toEqual({
      date: '2030-01-02',
      time: '00:30'
    });
  });

  it('follows daylight saving time (summer, UTC+3)', () => {
    expect(getClinicNow(new Date('2030-07-01T21:30:00Z'), 'Africa/Cairo')).toEqual({
      date: '2030-07-02',
      time: '00:30'
    });
  });

  it('keeps the same day when the zones agree on the date', () => {
    expect(getClinicNow(new Date('2030-01-01T10:15:00Z'), 'Africa/Cairo')).toEqual({
      date: '2030-01-01',
      time: '12:15'
    });
  });

  it('falls back to the clinic default (never UTC) for an unknown time zone', () => {
    expect(getClinicNow(new Date('2030-01-01T22:30:00Z'), 'Nowhere/Land')).toEqual({
      date: '2030-01-02',
      time: '00:30'
    });
  });

  it('writes midnight as 00:xx, not 24:xx', () => {
    expect(getClinicNow(new Date('2030-01-01T22:00:00Z'), 'Africa/Cairo').time).toBe('00:00');
  });
});

describe('isBefore', () => {
  const now = {date: '2030-01-02', time: '10:00'};

  it.each([
    ['2030-01-01', '23:59', true],
    ['2030-01-02', '09:59', true],
    ['2030-01-02', '10:00', false], // exactly now is not in the past
    ['2030-01-02', '10:01', false],
    ['2030-01-03', '00:00', false]
  ])('%s %s before now → %s', (date, time, expected) => {
    expect(isBefore(date, time, now)).toBe(expected);
  });
});

describe('toUtcDate', () => {
  it('builds the same calendar day regardless of the machine time zone', () => {
    const date = toUtcDate('2030-01-02', '23:30');

    expect(date.getUTCFullYear()).toBe(2030);
    expect(date.getUTCMonth()).toBe(0);
    expect(date.getUTCDate()).toBe(2);
    expect(date.getUTCHours()).toBe(23);
    expect(date.getUTCMinutes()).toBe(30);
  });

  it('returns an invalid date for bad input instead of throwing', () => {
    expect(Number.isNaN(toUtcDate('2030-02-30x').getTime())).toBe(true);
    expect(Number.isNaN(toUtcDate('2030-01-02', '25:00').getTime())).toBe(true);
  });
});

describe('intervalsOverlap (UI hint; same rule as the server)', () => {
  // existing 10:00-10:30
  it.each([
    ['10:30', '11:00', false], // adjacent after
    ['09:30', '10:00', false], // adjacent before
    ['10:15', '10:45', true], // partial
    ['09:45', '10:15', true], // partial
    ['10:05', '10:20', true], // contained
    ['09:00', '11:00', true], // wraps
    ['10:00', '10:30', true] // identical
  ])('new %s-%s → overlap %s', (start, end, expected) => {
    expect(intervalsOverlap(start, end, '10:00', '10:30')).toBe(expected);
  });
});
