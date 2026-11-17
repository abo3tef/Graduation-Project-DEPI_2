'use client';

import {useEffect, useState} from 'react';
import {useQuery} from '@tanstack/react-query';
import {isRetryableError} from '../services/appointment-errors';
import {listDoctorLookups} from '../services/appointments.api';
import {appointmentKeys, useActor} from './useAppointments';

const LOOKUP_STALE_MS = 5 * 60 * 1000;
export const SEARCH_DEBOUNCE_MS = 300;

export function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);

  return debounced;
}

/** The doctors the caller may book (themself, or a secretary's assigned doctors). */
export function useDoctorLookups() {
  const {actor, ready} = useActor();

  return useQuery({
    queryKey: appointmentKeys.doctors(actor),
    queryFn: ({signal}) => listDoctorLookups(signal),
    enabled: ready,
    staleTime: LOOKUP_STALE_MS,
    retry: (count, error) => isRetryableError(error) && count < 1
  });
}
