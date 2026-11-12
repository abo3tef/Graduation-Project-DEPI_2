import {CLINIC_TIME_ZONE} from '@/constants';

/**
 * Clinic-local date and time helpers.
 *
 * Appointment dates/times are plain wall-clock strings in the clinic time zone.
 * They are NEVER parsed with `new Date('yyyy-MM-dd')` (that is UTC midnight and
 * shifts the day in many zones); parts are read explicitly instead.
 */
const DATE_PATTERN = /^(\d{4})-(\d{2})-(\d{2})$/;
const TIME_PATTERN = /^([01]\d|2[0-3]):([0-5]\d)(?::([0-5]\d))?$/;
const DEFAULT_TIME_ZONE = 'Africa/Cairo';

export type ClinicNow = {
  /** yyyy-MM-dd */
  readonly date: string;
  /** HH:mm */
  readonly time: string;
};

export function isValidIsoDate(value: string): boolean {
  const match = DATE_PATTERN.exec(value);

  if (!match) {
    return false;
  }

  const [year, month, day] = [Number(match[1]), Number(match[2]), Number(match[3])];
  const date = new Date(Date.UTC(year, month - 1, day));

  return (
    date.getUTCFullYear() === year &&
    date.getUTCMonth() === month - 1 &&
    date.getUTCDate() === day
  );
}

export function isValidTime(value: string): boolean {
  return TIME_PATTERN.test(value);
}

/** `HH:mm` or `HH:mm:ss` → `HH:mm:ss` (what the API expects). */
export function normalizeTime(value: string): string {
  const match = TIME_PATTERN.exec(value);

  if (!match) {
    return value;
  }

  return `${match[1]}:${match[2]}:${match[3] ?? '00'}`;
}

/** `HH:mm:ss` → `HH:mm` (what inputs and the UI show). */
export function toHoursMinutes(value: string): string {
  return TIME_PATTERN.test(value) ? value.slice(0, 5) : value;
}

export function timeToMinutes(value: string): number {
  const match = TIME_PATTERN.exec(value);

  return match ? Number(match[1]) * 60 + Number(match[2]) : Number.NaN;
}

/** Adds minutes to a time; `null` when the result leaves the same day. */
export function addMinutesToTime(value: string, minutes: number): string | null {
  const total = timeToMinutes(value) + minutes;

  if (!Number.isFinite(total) || total < 0 || total >= 24 * 60) {
    return null;
  }

  const hours = Math.floor(total / 60);
  const rest = total % 60;

  return `${String(hours).padStart(2, '0')}:${String(rest).padStart(2, '0')}`;
}

function formatParts(now: Date, timeZone: string): ClinicNow {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23'
  }).formatToParts(now);

  const get = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((part) => part.type === type)?.value ?? '00';

  return {
    date: `${get('year')}-${get('month')}-${get('day')}`,
    time: `${get('hour')}:${get('minute')}`
  };
}

/** The current date and time in the clinic time zone. */
export function getClinicNow(
  now: Date = new Date(),
  timeZone: string = CLINIC_TIME_ZONE
): ClinicNow {
  try {
    return formatParts(now, timeZone);
  } catch {
    // An unknown time zone setting falls back to the clinic default, never to UTC.
    return formatParts(now, DEFAULT_TIME_ZONE);
  }
}

/** True when the given local date and start time are before `now`. */
export function isBefore(date: string, time: string, now: ClinicNow): boolean {
  if (date !== now.date) {
    return date < now.date;
  }

  return timeToMinutes(time) < timeToMinutes(now.time);
}

/** A `Date` at UTC midnight of the given day, for formatting only (use `timeZone: 'UTC'`). */
export function toUtcDate(isoDate: string, time = '00:00'): Date {
  const date = DATE_PATTERN.exec(isoDate);
  const clock = TIME_PATTERN.exec(time);

  if (!date || !clock) {
    return new Date(Number.NaN);
  }

  return new Date(
    Date.UTC(
      Number(date[1]),
      Number(date[2]) - 1,
      Number(date[3]),
      Number(clock[1]),
      Number(clock[2])
    )
  );
}

/**
 * The booking overlap rule, used only as a UI hint (the server is authoritative):
 * `newStart < existingEnd && newEnd > existingStart`. Back-to-back is not an overlap.
 */
export function intervalsOverlap(
  newStart: string,
  newEnd: string,
  existingStart: string,
  existingEnd: string
): boolean {
  return (
    timeToMinutes(newStart) < timeToMinutes(existingEnd) &&
    timeToMinutes(newEnd) > timeToMinutes(existingStart)
  );
}
