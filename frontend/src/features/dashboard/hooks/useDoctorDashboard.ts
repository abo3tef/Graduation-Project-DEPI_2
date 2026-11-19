'use client';

import {useQuery} from '@tanstack/react-query';
import {useActingIdentity} from '@/features/auth';
import {ApiError} from '@/lib/api';
import {MAX_PAGE_SIZE} from '@/types/paging';
import {getClinicNow, listAppointments} from '@/features/appointments';
import {getDoctorDashboard} from '../services/dashboard.api';
import type {DoctorDashboardResponse} from '../types/dashboard.types';
import type {Appointment} from '@/features/appointments/types/appointment.types';

/**
 * Query key factory for the doctor dashboard, scoped by the acting doctor
 * so identity switches never leak cached data between doctors.
 */
export const dashboardKeys = {
  all: ['dashboard'] as const,
  doctor: (actor: string) => [...dashboardKeys.all, 'doctor', actor] as const,
  upcoming: (actor: string, from: string, to: string) =>
    [...dashboardKeys.all, 'upcoming', actor, from, to] as const
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

  const clinicToday = getClinicNow().date;
  const upcomingUntil = addDays(clinicToday, 7);
  const upcomingQuery = useQuery<readonly Appointment[], Error>({
    queryKey: dashboardKeys.upcoming(actor, clinicToday, upcomingUntil),
    queryFn: async ({signal}) => {
      const result = await listAppointments(
        {
          doctorId: identity.userId ?? '',
          status: 'all',
          dateFrom: clinicToday,
          dateTo: upcomingUntil,
          pageNumber: 1,
          pageSize: MAX_PAGE_SIZE
        },
        signal
      );

      return result.items.filter((appointment) => {
        if (appointment.status === 'Cancelled' || appointment.status === 'NoShow') {
          return false;
        }

        return (
          appointment.appointmentDate > clinicToday ||
          (appointment.appointmentDate === clinicToday && appointment.startTime >= getClinicNow().time)
        );
      });
    },
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
    isDoctor,
    upcomingAppointments: upcomingQuery.data ?? [],
    upcomingIsLoading: upcomingQuery.isPending,
    upcomingIsError: upcomingQuery.isError
  };
}

function addDays(isoDate: string, days: number): string {
  const date = new Date(`${isoDate}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

