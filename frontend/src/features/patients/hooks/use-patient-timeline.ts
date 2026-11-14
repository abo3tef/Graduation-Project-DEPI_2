'use client';

import {keepPreviousData, useQuery} from '@tanstack/react-query';
import {listPatientTimeline} from '../api';
import {patientKeys} from '../keys';
import {isRetryableError} from '../services/patient-errors';
import {isGuid} from '../validation';
import {useActorGate} from './use-actor-gate';
import {DEFAULT_PAGE_SIZE} from '@/types/paging';

export function usePatientTimeline(patientId: string, pageNumber: number) {
  const {actor, ready} = useActorGate();
  return useQuery({
    queryKey: patientKeys.timeline(actor, patientId, pageNumber),
    queryFn: ({signal}) => listPatientTimeline(patientId, pageNumber, DEFAULT_PAGE_SIZE, signal),
    enabled: ready && isGuid(patientId),
    placeholderData: keepPreviousData,
    staleTime: 0,
    retry: (count, error) => isRetryableError(error) && count < 1
  });
}
