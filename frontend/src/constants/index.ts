export const APP_NAME = 'Medilog';

export const DEFAULT_LOCALE = 'ar';

export const SUPPORTED_LOCALES = ['ar', 'en'] as const;

/**
 * Time zone of the clinic. It decides what "today" and "in the past" mean in
 * appointment forms and must match the backend setting `Clinic:TimeZone`.
 * (Literal `process.env` access lets Next.js inline the value.)
 */
export const CLINIC_TIME_ZONE =
  process.env.NEXT_PUBLIC_CLINIC_TIME_ZONE || 'Africa/Cairo';
