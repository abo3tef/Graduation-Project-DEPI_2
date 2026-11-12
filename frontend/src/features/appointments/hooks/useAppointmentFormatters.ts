'use client';

import {useFormatter} from 'next-intl';
import {useMemo} from 'react';
import {toHoursMinutes, toUtcDate} from '../utils/date-time';

/**
 * Locale-aware formatting of clinic-local dates and times. The values are plain
 * wall-clock strings, so they are formatted as UTC to avoid any time-zone shift.
 */
export function useAppointmentFormatters() {
  const format = useFormatter();

  return useMemo(
    () => ({
      date: (isoDate: string): string => {
        const date = toUtcDate(isoDate);

        return Number.isNaN(date.getTime())
          ? isoDate
          : format.dateTime(date, {dateStyle: 'medium', timeZone: 'UTC'});
      },
      time: (time: string): string => {
        const date = toUtcDate('2000-01-01', toHoursMinutes(time));

        return Number.isNaN(date.getTime())
          ? time
          : format.dateTime(date, {hour: 'numeric', minute: '2-digit', timeZone: 'UTC'});
      }
    }),
    [format]
  );
}
