'use client';

import {useQuery} from '@tanstack/react-query';
import {useActingIdentity} from '@/features/auth';
import {isGuid} from '@/features/medical-records';
import {getMedicalProfile} from '../api';
import {medicalTimelineKeys} from '../keys';
import {retryOnceExceptPermanent} from './retry-policy';

/**
 * Medical-profile query. It stays disabled until the signed-in doctor is known (F6-C2
 * session store) and the patient id is a GUID, so a malformed id never reaches the API.
 * The credential itself is attached by the shared API transport.
 */
export function useMedicalProfile(patientId: string) {
  const identity = useActingIdentity();

  return useQuery({
    queryKey: medicalTimelineKeys.profile(patientId, {
      userId: identity.userId,
      role: identity.role
    }),
    queryFn: ({signal}) => getMedicalProfile(patientId, signal),
    enabled: identity.status === 'ready' && isGuid(patientId),
    retry: retryOnceExceptPermanent
  });
}
