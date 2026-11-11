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
 * Visit detail query. It stays disabled until the client knows the token (D-28) and the
 * route id is a GUID, so a malformed URL never reaches the API.
 */
export function useMedicalVisit(visitId: string, token: string | null, ready: boolean) {
  return useQuery({
    queryKey: medicalRecordKeys.visit(visitId),
    queryFn: ({signal}) => {
      if (!token) {
        // Defensive: `enabled` already guards this, but keeps the function total.
        throw new ApiError(401, 'unauthorized', null);
      }

      return getMedicalVisit(visitId, token, signal);
    },
    enabled: ready && token !== null && isGuid(visitId),
    retry: (failureCount, error) => {
      if (error instanceof ApiError && NON_RETRYABLE_KINDS.includes(error.kind)) {
        return false;
      }

      return failureCount < 1;
    }
  });
}
