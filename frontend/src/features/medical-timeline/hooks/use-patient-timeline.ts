'use client';

import {useQuery} from '@tanstack/react-query';
import {useActingIdentity} from '@/features/auth';
import {isGuid} from '@/features/medical-records';
import {ApiError} from '@/lib/api';
import {getPatientTimeline} from '../api';
import {medicalTimelineKeys} from '../keys';
import type {PatientTimelineQueryParams} from '../types';
import {retryOnceExceptPermanent} from './retry-policy';

/**
 * Timeline page query. It stays disabled until the client knows the token (D-28) and
 * the patient id is a GUID, so a malformed id never reaches the API. Filters and paging
 * are part of the query key, so every combination is fetched and cached separately.
 */
export function usePatientTimeline(
  patientId: string,
  params: PatientTimelineQueryParams,
  token: string | null,
  ready: boolean,
  tokenRevision: number
) {
  const identity = useActingIdentity();

  return useQuery({
    queryKey: medicalTimelineKeys.timeline(
      patientId,
      params,
      {userId: identity.userId, role: identity.role},
      tokenRevision
    ),
    queryFn: ({signal}) => {
      if (!token) {
        // Defensive: `enabled` already guards this, but keeps the function total.
        throw new ApiError({kind: 'unauthorized', status: 401});
      }

      return getPatientTimeline(patientId, params, token, signal);
    },
    enabled: ready && token !== null && isGuid(patientId),
    retry: retryOnceExceptPermanent
  });
}
