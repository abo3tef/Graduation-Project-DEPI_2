'use client';

import {useTranslations} from 'next-intl';
import {useCallback} from 'react';
import {getAppointmentErrorKey} from '../services/appointment-errors';
import type {ValidationKey} from '../validation/appointment.validation';

/** Translated message for any failure (by API code, then status). */
export function useErrorMessage() {
  const t = useTranslations('Appointments.errors');

  return useCallback((error: unknown): string => t(getAppointmentErrorKey(error)), [t]);
}

/** Translated field validation message, or `null` when there is no error. */
export function useValidationMessage() {
  const t = useTranslations('Appointments.validation');

  return useCallback(
    (key: ValidationKey | null | undefined): string | null => (key ? t(key) : null),
    [t]
  );
}

/** Moves keyboard focus to the first invalid control (by element id) after a failed submit. */
export function focusFirstInvalid(ids: readonly string[]): void {
  const target = ids.map((id) => document.getElementById(id)).find((element) => element !== null);

  target?.focus();
}
