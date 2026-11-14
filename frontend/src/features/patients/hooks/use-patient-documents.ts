'use client';

import {keepPreviousData, useQuery} from '@tanstack/react-query';
import {listPatientDocuments} from '../api';
import {patientKeys} from '../keys';
import {isRetryableError} from '../services/patient-errors';
import {isGuid} from '../validation';
import {useActorGate} from './use-actor-gate';
import {DEFAULT_PAGE_SIZE} from '@/types/paging';

export function usePatientDocuments(patientId: string, pageNumber: number) {
  const {actor, ready} = useActorGate();
  return useQuery({
    queryKey: patientKeys.documents(actor, patientId, pageNumber),
    queryFn: ({signal}) => listPatientDocuments(patientId, pageNumber, DEFAULT_PAGE_SIZE, signal),
    enabled: ready && isGuid(patientId),
    placeholderData: keepPreviousData,
    staleTime: 0,
    retry: (count, error) => isRetryableError(error) && count < 1
  });
}
