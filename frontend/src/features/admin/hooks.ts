'use client';

import {keepPreviousData, useMutation, useQuery, useQueryClient} from '@tanstack/react-query';
import {useTranslations} from 'next-intl';
import {useCallback} from 'react';
import {useActingIdentity} from '@/features/auth';
import {notify} from '@/lib/utils/toast';
import {DEFAULT_PAGE_SIZE} from '@/types/paging';
import {
  changeAdminUserRole,
  getSecretaryDoctors,
  listAdminUsers,
  replaceSecretaryDoctors,
  setAdminUserStatus
} from './api';
import {getAdminErrorKey, isRetryableAdminError} from './errors';
import type {
  AdminUserFilters,
  ChangeAdminRoleVariables,
  ReplaceAdminDoctorsVariables,
  SetAdminStatusVariables
} from './types';

export const adminKeys = {
  all: (actor: string) => ['admin', actor] as const,
  users: (actor: string, filters: AdminUserFilters) =>
    [...adminKeys.all(actor), 'users', filters] as const,
  secretaryDoctors: (actor: string, secretaryId: string) =>
    [...adminKeys.all(actor), 'secretary-doctors', secretaryId] as const,
  doctorOptions: (actor: string, search: string) =>
    [...adminKeys.all(actor), 'doctor-options', search] as const
};

function retryOnce(failureCount: number, error: unknown): boolean {
  return isRetryableAdminError(error) && failureCount < 1;
}

/** Translated message for any admin failure, keyed by stable API error code. */
export function useAdminErrorMessage() {
  const t = useTranslations('Admin.notifications');

  return useCallback((error: unknown): string => t(getAdminErrorKey(error)), [t]);
}

function useAdminActor(): string {
  const identity = useActingIdentity();

  return identity.userId ?? 'anonymous';
}

/**
 * The query stays disabled until the signed-in identity is known. Query keys include the actor so
 * switching accounts can never reuse another administrator's cached data.
 */
export function useAdminUsers(filters: AdminUserFilters) {
  const identity = useActingIdentity();
  const actor = identity.userId ?? 'anonymous';

  return useQuery({
    queryKey: adminKeys.users(actor, filters),
    queryFn: ({signal}) => listAdminUsers(filters, signal),
    enabled: identity.status === 'ready',
    placeholderData: keepPreviousData,
    retry: retryOnce
  });
}

export function useSecretaryDoctors(secretaryId: string | null, enabled: boolean) {
  const actor = useAdminActor();

  return useQuery({
    queryKey: adminKeys.secretaryDoctors(actor, secretaryId ?? 'none'),
    queryFn: ({signal}) => getSecretaryDoctors(secretaryId as string, signal),
    enabled: enabled && secretaryId !== null,
    retry: retryOnce
  });
}

export function useAdminDoctorOptions(search: string, enabled: boolean) {
  const actor = useAdminActor();
  const term = search.trim();

  return useQuery({
    queryKey: adminKeys.doctorOptions(actor, term),
    queryFn: ({signal}) =>
      listAdminUsers(
        {
          search: term,
          role: 'Doctor',
          status: 'Active',
          pageNumber: 1,
          pageSize: DEFAULT_PAGE_SIZE
        },
        signal
      ),
    enabled,
    retry: retryOnce
  });
}

function useAdminMutation(successKey: string) {
  const queryClient = useQueryClient();
  const actor = useAdminActor();
  const t = useTranslations('Admin.notifications');
  const getErrorMessage = useAdminErrorMessage();

  return {
    queryClient,
    actor,
    onSuccess: async () => {
      notify.success(t(successKey));
      await queryClient.invalidateQueries({queryKey: adminKeys.all(actor)});
    },
    onError: (error: unknown) => {
      notify.error(getErrorMessage(error));
    }
  };
}

export function useSetAdminUserStatus() {
  const shared = useAdminMutation('statusUpdated');

  return useMutation({
    mutationFn: ({id, status}: SetAdminStatusVariables) => setAdminUserStatus(id, status),
    onSuccess: shared.onSuccess,
    onError: shared.onError
  });
}

export function useChangeAdminUserRole() {
  const shared = useAdminMutation('roleUpdated');

  return useMutation({
    mutationFn: ({id, role}: ChangeAdminRoleVariables) => changeAdminUserRole(id, role),
    onSuccess: shared.onSuccess,
    onError: shared.onError
  });
}

export function useReplaceSecretaryDoctors() {
  const shared = useAdminMutation('assignmentsUpdated');

  return useMutation({
    mutationFn: ({secretaryId, doctorIds}: ReplaceAdminDoctorsVariables) =>
      replaceSecretaryDoctors(secretaryId, doctorIds),
    onSuccess: shared.onSuccess,
    onError: shared.onError
  });
}
