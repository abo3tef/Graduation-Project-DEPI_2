'use client';

import {keepPreviousData, useQuery} from '@tanstack/react-query';
import {listPatientNotes} from '../api';
import {patientKeys} from '../keys';
import {isRetryableError} from '../services/patient-errors';
import {isGuid} from '../validation';
import {useActorGate} from './use-actor-gate';
import {DEFAULT_PAGE_SIZE} from '@/types/paging';

/**
 * One page of the patient's clinical notes — visit-linked and unlinked
 * (§3.3, newest first). `keepPreviousData` keeps the previous page visible
 * while the next one loads. Nothing is requested before the doctor gate is
 * ready and the route id is a GUID.
 */
export function usePatientNotes(patientId: string, pageNumber: number) {
  const {actor, ready} = useActorGate();

  return useQuery({
    queryKey: patientKeys.notes(actor, patientId, pageNumber),
    queryFn: ({signal}) => listPatientNotes(patientId, pageNumber, DEFAULT_PAGE_SIZE, signal),
    enabled: ready && isGuid(patientId),
    placeholderData: keepPreviousData,
    staleTime: 0,
    retry: (count, error) => isRetryableError(error) && count < 1
  });
}
