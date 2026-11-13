'use client';

import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient
} from '@tanstack/react-query';
import {useTranslations} from 'next-intl';
import {useActingIdentity} from '@/features/auth';
import {notify} from '@/lib/utils/toast';
import {isRetryableError} from '../services/appointment-errors';
import {
  bookAppointment,
  cancelAppointment,
  listAppointments,
  rescheduleAppointment,
  updateAppointmentStatus
} from '../services/appointments.api';
import {MAX_PAGE_SIZE} from '@/types/paging';
import {
  type AppointmentFilters,
  type AppointmentStatus,
  type BookAppointmentInput,
  type RescheduleAppointmentInput
} from '../types/appointment.types';
import {isValidIsoDate} from '../utils/date-time';

/**
 * Query keys are scoped by the acting user so that switching identity (dev
 * identity today, real sign-in later) can never show another user's cached data.
 */
export const appointmentKeys = {
  all: (actor: string) => ['appointments', actor] as const,
  list: (actor: string, filters: AppointmentFilters) =>
    [...appointmentKeys.all(actor), 'list', filters] as const,
  day: (actor: string, doctorId: string, date: string) =>
    [...appointmentKeys.all(actor), 'day', doctorId, date] as const,
  doctors: (actor: string) => [...appointmentKeys.all(actor), 'lookups', 'doctors'] as const,
  patients: (actor: string, doctorId: string, search: string) =>
    [...appointmentKeys.all(actor), 'lookups', 'patients', doctorId, search] as const
};

function retryOnce(failureCount: number, error: unknown): boolean {
  return isRetryableError(error) && failureCount < 1;
}

/** Actor key and whether requests may be sent at all. */
export function useActor() {
  const identity = useActingIdentity();

  return {
    identity,
    actor: identity.userId ?? 'anonymous',
    ready: identity.status === 'ready'
  };
}

export function useAppointmentList(filters: AppointmentFilters, enabled = true) {
  const {actor, ready} = useActor();

  return useQuery({
    queryKey: appointmentKeys.list(actor, filters),
    queryFn: ({signal}) => listAppointments(filters, signal),
    enabled: ready && enabled,
    placeholderData: keepPreviousData,
    retry: retryOnce
  });
}

/** One doctor's appointments on one day, to help avoid conflicts while booking. */
export function useDaySchedule(doctorId: string, date: string) {
  const {actor, ready} = useActor();
  const enabled = ready && doctorId !== '' && isValidIsoDate(date);

  return useQuery({
    queryKey: appointmentKeys.day(actor, doctorId, date),
    queryFn: ({signal}) =>
      listAppointments(
        {
          doctorId,
          status: 'all',
          dateFrom: date,
          dateTo: date,
          pageNumber: 1,
          pageSize: MAX_PAGE_SIZE
        },
        signal
      ),
    enabled,
    staleTime: 0,
    retry: retryOnce
  });
}

function useRefreshAppointments() {
  const queryClient = useQueryClient();
  const {actor} = useActor();

  return () => queryClient.invalidateQueries({queryKey: appointmentKeys.all(actor)});
}

export function useBookAppointment() {
  const t = useTranslations('Appointments.toast');
  const refresh = useRefreshAppointments();

  return useMutation({
    mutationFn: (input: BookAppointmentInput) => bookAppointment(input),
    onSuccess: async () => {
      notify.success(t('booked'));
      await refresh();
    }
  });
}

export function useRescheduleAppointment() {
  const t = useTranslations('Appointments.toast');
  const refresh = useRefreshAppointments();

  return useMutation({
    mutationFn: (variables: {id: string; input: RescheduleAppointmentInput}) =>
      rescheduleAppointment(variables.id, variables.input),
    onSuccess: async () => {
      notify.success(t('rescheduled'));
      await refresh();
    }
  });
}

export function useCancelAppointment() {
  const t = useTranslations('Appointments.toast');
  const refresh = useRefreshAppointments();

  return useMutation({
    mutationFn: (variables: {id: string; reason: string}) =>
      cancelAppointment(variables.id, variables.reason),
    onSuccess: async () => {
      notify.success(t('cancelled'));
      await refresh();
    }
  });
}

export type ChangeableStatus = Exclude<AppointmentStatus, 'Scheduled' | 'Cancelled'>;

const STATUS_TOAST: Readonly<Record<ChangeableStatus, 'confirmed' | 'completed' | 'noShow'>> = {
  Confirmed: 'confirmed',
  Completed: 'completed',
  NoShow: 'noShow'
};

export function useUpdateAppointmentStatus() {
  const t = useTranslations('Appointments.toast');
  const refresh = useRefreshAppointments();

  return useMutation({
    mutationFn: (variables: {id: string; status: ChangeableStatus}) =>
      updateAppointmentStatus(variables.id, variables.status),
    onSuccess: async (_appointment, variables) => {
      notify.success(t(STATUS_TOAST[variables.status]));
      await refresh();
    }
  });
}
