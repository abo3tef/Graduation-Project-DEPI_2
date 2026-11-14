'use client';

import {useEffect, useState} from 'react';
import {keepPreviousData, useQuery} from '@tanstack/react-query';
import {searchOwnPatients} from '../api';
import {patientKeys} from '../keys';
import {isRetryableError} from '../services/patient-errors';
import {
  PATIENT_SEARCH_DEBOUNCE_MS,
  PATIENT_SEARCH_MIN_LENGTH
} from '../validation';
import {useActorGate} from './use-actor-gate';

export function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);

  return debounced;
}

/**
 * The acting doctor's own patients matching the text. Nothing is requested
 * until the doctor gate is ready and at least `PATIENT_SEARCH_MIN_LENGTH`
 * characters are typed (the same rules as F1's patient search).
 */
export function usePatientSearch(doctorId: string, text: string) {
  const {actor, ready} = useActorGate();
  const debounced = useDebouncedValue(text.trim(), PATIENT_SEARCH_DEBOUNCE_MS);
  const typedEnough = text.trim().length >= PATIENT_SEARCH_MIN_LENGTH;
  const enabled = ready && doctorId !== '' && debounced.length >= PATIENT_SEARCH_MIN_LENGTH;

  const query = useQuery({
    queryKey: patientKeys.search(actor, debounced),
    queryFn: ({signal}) => searchOwnPatients(doctorId, debounced, signal),
    enabled,
    placeholderData: keepPreviousData,
    staleTime: 30 * 1000,
    retry: (count, error) => isRetryableError(error) && count < 1
  });

  return {
    ...query,
    /** True while the typed text is long enough but the debounced request has not run yet. */
    isWaiting: typedEnough && debounced !== text.trim(),
    typedEnough
  };
}
