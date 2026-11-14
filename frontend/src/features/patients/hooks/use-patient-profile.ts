'use client';

import {useQuery} from '@tanstack/react-query';
import {getMedicalProfile} from '../api';
import {patientKeys} from '../keys';
import {isRetryableError} from '../services/patient-errors';
import {isGuid} from '../validation';
import {useActorGate} from './use-actor-gate';

/**
 * The patient's medical profile (§3.1). It serves the profile tab *and* the
 * allergies/conditions tab, so both tabs share one request. Disabled until the
 * doctor gate is ready and the route id is a GUID — a malformed URL never
 * reaches the API.
 */
export function usePatientProfile(patientId: string) {
  const {actor, ready} = useActorGate();

  return useQuery({
    queryKey: patientKeys.profile(actor, patientId),
    queryFn: ({signal}) => getMedicalProfile(patientId, signal),
    enabled: ready && isGuid(patientId),
    staleTime: 30 * 1000,
    retry: (count, error) => isRetryableError(error) && count < 1
  });
}
