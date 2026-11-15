'use client';

import {useQuery} from '@tanstack/react-query';
import {useActingIdentity} from '@/features/auth';
import {isGuid} from '@/features/medical-records';
import {ApiError} from '@/lib/api';
import {getMedicalProfile} from '../api';
import {medicalTimelineKeys} from '../keys';
import {retryOnceExceptPermanent} from './retry-policy';

/**
 * Medical-profile query. It stays disabled until the client knows the token (D-28) and
 * the patient id is a GUID, so a malformed id never reaches the API.
 */
export function useMedicalProfile(
  patientId: string,
  token: string | null,
  ready: boolean,
  tokenRevision: number
) {
  const identity = useActingIdentity();

  return useQuery({
    queryKey: medicalTimelineKeys.profile(
      patientId,
      {userId: identity.userId, role: identity.role},
      tokenRevision
    ),
    queryFn: ({signal}) => {
      if (!token) {
        // Defensive: `enabled` already guards this, but keeps the function total.
        throw new ApiError({kind: 'unauthorized', status: 401});
      }

      return getMedicalProfile(patientId, token, signal);
    },
    enabled: ready && token !== null && isGuid(patientId),
    retry: retryOnceExceptPermanent
  });
}
