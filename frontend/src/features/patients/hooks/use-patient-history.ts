'use client';

import {keepPreviousData, useQuery} from '@tanstack/react-query';
import {listPatientVisits} from '../api';
import {patientKeys} from '../keys';
import {isRetryableError} from '../services/patient-errors';
import {isGuid} from '../validation';
import {useActorGate} from './use-actor-gate';
import {DEFAULT_PAGE_SIZE} from '@/types/paging';

/**
 * One page of the patient's visit history (§3.2). `keepPreviousData` keeps the
 * previous page on screen while the next one loads, so pagination never blanks.
 */
export function usePatientHistory(patientId: string, pageNumber: number) {
  const {actor, ready} = useActorGate();

  return useQuery({
    queryKey: patientKeys.history(actor, patientId, pageNumber),
    queryFn: ({signal}) =>
      listPatientVisits(patientId, pageNumber, DEFAULT_PAGE_SIZE, signal),
    enabled: ready && isGuid(patientId),
    placeholderData: keepPreviousData,
    staleTime: 0,
    retry: (count, error) => isRetryableError(error) && count < 1
  });
}
