'use client';

import {useQuery} from '@tanstack/react-query';
import {ApiError} from '@/lib/api';
import {getMedicalVisit} from '../api';
import {medicalRecordKeys} from '../keys';
import {isGuid} from '../validation';

const NON_RETRYABLE_KINDS: readonly string[] = [
  'unauthorized',
  'forbidden',
  'not-found',
  'validation',
  'conflict'
];

/**
 * Visit detail query. It stays disabled until the route id is a valid GUID,
 * so a malformed URL never reaches the API.
 */
export function useMedicalVisit(visitId: string) {
  return useQuery({
    queryKey: medicalRecordKeys.visit(visitId),
    queryFn: ({signal}) => getMedicalVisit(visitId, signal),
    enabled: isGuid(visitId),
    retry: (failureCount, error) => {
      if (error instanceof ApiError && NON_RETRYABLE_KINDS.includes(error.kind)) {
        return false;
      }

      return failureCount < 1;
    }
  });
}
