import {PAYMENT_CURRENCY} from '../types/payment.types';

/**
 * F2 Payments — locale-aware formatting and amount parsing.
 
 */

const FALLBACK = '—';

/* -------------------------------------------------------------------------- */
/* Currency                                                                   */
/* -------------------------------------------------------------------------- */

const currencyFormatters = new Map<string, Intl.NumberFormat>();

export function formatCurrency(amount: number, locale: string): string {
  if (!Number.isFinite(amount)) {
    return FALLBACK;
  }

  let formatter = currencyFormatters.get(locale);

  if (!formatter) {
    formatter = new Intl.NumberFormat(locale, {
      style: 'currency',
      currency: PAYMENT_CURRENCY
    });
    currencyFormatters.set(locale, formatter);
  }

  return formatter.format(amount);
}

/* -------------------------------------------------------------------------- */
/* Dates and times                                                            */
/* -------------------------------------------------------------------------- */

type DateFormatKind = 'date' | 'dateTime' | 'time';

const DATE_FORMAT_OPTIONS: Record<DateFormatKind, Intl.DateTimeFormatOptions> = {
  date: {dateStyle: 'medium'},
  dateTime: {dateStyle: 'medium', timeStyle: 'short'},
  time: {timeStyle: 'short'}
};

const dateFormatters = new Map<string, Intl.DateTimeFormat>();

function getDateFormatter(locale: string, kind: DateFormatKind): Intl.DateTimeFormat {
  const key = `${locale}|${kind}`;
  let formatter = dateFormatters.get(key);

  if (!formatter) {
    formatter = new Intl.DateTimeFormat(locale, DATE_FORMAT_OPTIONS[kind]);
    dateFormatters.set(key, formatter);
  }

  return formatter;
}

const DATE_ONLY_PATTERN = /^(\d{4})-(\d{2})-(\d{2})$/;
const TIME_ONLY_PATTERN = /^(\d{2}):(\d{2})$/;


function parseDate(value: string): Date | null {
  const match = DATE_ONLY_PATTERN.exec(value);
  const date = match
    ? new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]))
    : new Date(value);

  return Number.isNaN(date.getTime()) ? null : date;
}


export function formatDate(value: string, locale: string): string {
  const date = parseDate(value);
  return date ? getDateFormatter(locale, 'date').format(date) : FALLBACK;
}


export function formatDateTime(value: string, locale: string): string {
  const date = parseDate(value);
  return date ? getDateFormatter(locale, 'dateTime').format(date) : FALLBACK;
}


export function formatTime(value: string, locale: string): string {
  const match = TIME_ONLY_PATTERN.exec(value);

  if (!match) {
    return FALLBACK;
  }

  const date = new Date(1970, 0, 1, Number(match[1]), Number(match[2]));

  return Number.isNaN(date.getTime())
    ? FALLBACK
    : getDateFormatter(locale, 'time').format(date);
}


const AMOUNT_PATTERN = /^\d+(\.\d{1,2})?$/;


export function parseAmountInput(raw: string): number | null {
  const normalized = raw
    .trim()
    .replace(/[٠-٩]/g, (digit) => String(digit.charCodeAt(0) - 0x0660))
    .replace(/[۰-۹]/g, (digit) => String(digit.charCodeAt(0) - 0x06f0))
    .replace(/[٫,]/g, '.');

  if (!AMOUNT_PATTERN.test(normalized)) {
    return null;
  }

  return Number(normalized);
}