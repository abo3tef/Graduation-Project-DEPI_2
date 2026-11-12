'use client';

import {useEffect, useState} from 'react';
import {keepPreviousData, useQuery} from '@tanstack/react-query';
import {isRetryableError} from '../services/appointment-errors';
import {listDoctorLookups, searchPatientLookups} from '../services/appointments.api';
import {PATIENT_SEARCH_MIN_LENGTH} from '../types/appointment.types';
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

/**
 * Patients of one doctor matching the text. Nothing is requested until a doctor
 * is chosen and at least `PATIENT_SEARCH_MIN_LENGTH` characters are typed.
 */
export function usePatientSearch(doctorId: string, text: string) {
  const {actor, ready} = useActor();
  const search = useDebouncedValue(text.trim(), SEARCH_DEBOUNCE_MS);
  const typedEnough = text.trim().length >= PATIENT_SEARCH_MIN_LENGTH;
  const enabled = ready && doctorId !== '' && search.length >= PATIENT_SEARCH_MIN_LENGTH;

  const query = useQuery({
    queryKey: appointmentKeys.patients(actor, doctorId, search),
    queryFn: ({signal}) => searchPatientLookups(doctorId, search, signal),
    enabled,
    placeholderData: keepPreviousData,
    staleTime: 30 * 1000,
    retry: (count, error) => isRetryableError(error) && count < 1
  });

  return {
    ...query,
    /** True while the typed text is long enough but the debounced request has not run yet. */
    isWaiting: typedEnough && doctorId !== '' && search !== text.trim(),
    typedEnough
  };
}
