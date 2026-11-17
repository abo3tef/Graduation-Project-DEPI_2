'use client';

import {useQuery} from '@tanstack/react-query';
import {useActingIdentity} from '@/features/auth';
import {isGuid} from '@/features/medical-records';
import {getPatientTimeline} from '../api';
import {medicalTimelineKeys} from '../keys';
import type {PatientTimelineQueryParams} from '../types';
import {retryOnceExceptPermanent} from './retry-policy';

/**
 * Timeline page query. It stays disabled until the signed-in doctor is known (F6-C2
 * session store) and the patient id is a GUID, so a malformed id never reaches the API.
 * Filters and paging are part of the query key, so every combination is fetched and
 * cached separately. The credential itself is attached by the shared API transport.
 */
export function usePatientTimeline(
  patientId: string,
  params: PatientTimelineQueryParams
) {
  const identity = useActingIdentity();

  return useQuery({
    queryKey: medicalTimelineKeys.timeline(patientId, params, {
      userId: identity.userId,
      role: identity.role
    }),
    queryFn: ({signal}) => getPatientTimeline(patientId, params, signal),
    enabled: identity.status === 'ready' && isGuid(patientId),
    retry: retryOnceExceptPermanent
  });
}
