'use client';

import {useQuery} from '@tanstack/react-query';
import {ApiError} from '@/lib/api';
import {getMedicalVisit} from '../api';
import {medicalRecordKeys} from '../keys';
import {isGuid} from '../validation';
import {useMedicalCredentials} from '../use-medical-credentials';
import {readCredentialScope} from '../credential-scope';

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
  const identity = useMedicalCredentials();
  return useQuery({
    queryKey: medicalRecordKeys.visitDetail(visitId, identity.credentialScope),
    queryFn: ({signal}) => {
      if (!identity.isReady || !identity.token || identity.credentialScope !== readCredentialScope()) {
        throw new ApiError({kind: 'unauthorized', status: 401});
      }
      return getMedicalVisit(visitId, signal);
    },
    enabled: identity.isReady && Boolean(identity.token) && isGuid(visitId),
    placeholderData: undefined,
    gcTime: 0,
    retry: (failureCount, error) => {
      if (error instanceof ApiError && NON_RETRYABLE_KINDS.includes(error.kind)) {
        return false;
      }

      return failureCount < 1;
    }
  });
}
