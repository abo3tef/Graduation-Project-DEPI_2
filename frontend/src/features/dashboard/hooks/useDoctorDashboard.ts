'use client';

import {useQuery} from '@tanstack/react-query';
import {useActingIdentity} from '@/features/auth';
import {ApiError} from '@/lib/api';
import {getDoctorDashboard} from '../services/dashboard.api';
import type {DoctorDashboardResponse} from '../types/dashboard.types';

/**
 * Query key factory for the doctor dashboard, scoped by the acting doctor
 * so identity switches never leak cached data between doctors.
 */
export const dashboardKeys = {
  all: ['dashboard'] as const,
  doctor: (actor: string) => [...dashboardKeys.all, 'doctor', actor] as const
};

const NON_RETRYABLE_KINDS: readonly string[] = [
  'unauthorized',
  'forbidden',
  'not-found'
];

/**
 * React Query hook for the Doctor Clinic Dashboard.
 * Remains disabled until the acting user is authenticated with the Doctor role.
 */
export function useDoctorDashboard() {
  const identity = useActingIdentity();
  const actor = identity.userId ?? 'anonymous';
  const isDoctor = identity.status === 'ready' && identity.role === 'Doctor';

  const query = useQuery<DoctorDashboardResponse, Error>({
    queryKey: dashboardKeys.doctor(actor),
    queryFn: ({signal}) => getDoctorDashboard(signal),
    enabled: isDoctor,
    staleTime: 30 * 1000,
    retry: (failureCount, error) => {
      if (error instanceof ApiError && NON_RETRYABLE_KINDS.includes(error.kind)) {
        return false;
      }
      return failureCount < 1;
    }
  });

  return {
    ...query,
    identity,
    isDoctor
  };
}

