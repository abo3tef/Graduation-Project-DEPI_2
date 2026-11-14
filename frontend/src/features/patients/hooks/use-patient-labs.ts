'use client';

import {keepPreviousData, useQuery} from '@tanstack/react-query';
import {listPatientLabResults} from '../api';
import {patientKeys} from '../keys';
import {isRetryableError} from '../services/patient-errors';
import {isGuid} from '../validation';
import {useActorGate} from './use-actor-gate';
import {DEFAULT_PAGE_SIZE} from '@/types/paging';

/**
 * One page of the patient's lab results — visit-linked and unlinked
 * (§3.4, newest test first). Same gating and paging behavior as the notes
 * and visit-history hooks.
 */
export function usePatientLabs(patientId: string, pageNumber: number) {
  const {actor, ready} = useActorGate();

  return useQuery({
    queryKey: patientKeys.labs(actor, patientId, pageNumber),
    queryFn: ({signal}) => listPatientLabResults(patientId, pageNumber, DEFAULT_PAGE_SIZE, signal),
    enabled: ready && isGuid(patientId),
    placeholderData: keepPreviousData,
    staleTime: 0,
    retry: (count, error) => isRetryableError(error) && count < 1
  });
}
