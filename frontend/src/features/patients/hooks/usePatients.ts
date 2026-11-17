'use client';

import {useEffect, useState} from 'react';
import {keepPreviousData, useMutation, useQuery, useQueryClient} from '@tanstack/react-query';
import {useTranslations} from 'next-intl';
import {useActingIdentity} from '@/features/auth';
import {notify} from '@/lib/utils/toast';
import {isRetryableError} from '../services/patient-errors';
import {
  createPatient,
  deletePatient,
  getPatient,
  listDoctorOptions,
  listPatients,
  lookupPatients,
  updatePatient
} from '../services/patients.api';
import {
  PATIENT_SEARCH_MIN_LENGTH,
  type PatientFilters,
  type SavePatientInput
} from '../types/patient.types';

export const SEARCH_DEBOUNCE_MS = 300;
const LOOKUP_STALE_MS = 30 * 1000;

/**
 * Query keys are scoped by the acting user so that switching the signed-in user can never show
 * another user's cached patients.
 */
export const patientKeys = {
  all: (actor: string) => ['patients', actor] as const,
  list: (actor: string, filters: PatientFilters) => [...patientKeys.all(actor), 'list', filters] as const,
  detail: (actor: string, id: string) => [...patientKeys.all(actor), 'detail', id] as const,
  lookup: (actor: string, doctorId: string, search: string) =>
    [...patientKeys.all(actor), 'lookup', doctorId, search] as const,
  doctors: (actor: string) => [...patientKeys.all(actor), 'doctors'] as const
};

function retryOnce(failureCount: number, error: unknown): boolean {
  return isRetryableError(error) && failureCount < 1;
}

/** Who is acting and whether requests may be sent at all (Admins have no patient screens). */
export function usePatientActor() {
  const identity = useActingIdentity();

  return {
    identity,
    actor: identity.userId ?? 'anonymous',
    role: identity.role,
    ready: identity.status === 'ready' && identity.role !== 'Admin'
  };
}

export function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);

    return () => clearTimeout(timer);
  }, [value, delayMs]);

  return debounced;
}

export function usePatientList(filters: PatientFilters) {
  const {actor, ready} = usePatientActor();

  return useQuery({
    queryKey: patientKeys.list(actor, filters),
    queryFn: ({signal}) => listPatients(filters, signal),
    enabled: ready,
    placeholderData: keepPreviousData,
    retry: retryOnce
  });
}

export function usePatient(id: string | null) {
  const {actor, ready} = usePatientActor();

  return useQuery({
    queryKey: patientKeys.detail(actor, id ?? ''),
    queryFn: ({signal}) => getPatient(id ?? '', signal),
    enabled: ready && id !== null && id !== '',
    retry: retryOnce
  });
}

/** The doctors a secretary may create patients for (the existing appointment doctor lookup). */
export function useDoctorOptions(enabled: boolean) {
  const {actor, ready} = usePatientActor();

  return useQuery({
    queryKey: patientKeys.doctors(actor),
    queryFn: ({signal}) => listDoctorOptions(signal),
    enabled: ready && enabled,
    staleTime: 5 * 60 * 1000,
    retry: retryOnce
  });
}

/**
 * Picker search. Nothing is requested until at least PATIENT_SEARCH_MIN_LENGTH characters are
 * typed, and the request is debounced. An optional doctor narrows the (already scoped) result.
 */
export function usePatientLookup(text: string, doctorId = '') {
  const {actor, ready} = usePatientActor();
  const search = useDebouncedValue(text.trim(), SEARCH_DEBOUNCE_MS);
  const typedEnough = text.trim().length >= PATIENT_SEARCH_MIN_LENGTH;

  const query = useQuery({
    queryKey: patientKeys.lookup(actor, doctorId, search),
    queryFn: ({signal}) => lookupPatients(search, doctorId === '' ? undefined : doctorId, signal),
    enabled: ready && search.length >= PATIENT_SEARCH_MIN_LENGTH,
    placeholderData: keepPreviousData,
    staleTime: LOOKUP_STALE_MS,
    retry: retryOnce
  });

  return {
    ...query,
    /** True while the typed text is long enough but the debounced request has not run yet. */
    isWaiting: typedEnough && search !== text.trim(),
    typedEnough
  };
}

export function useCreatePatient() {
  const queryClient = useQueryClient();
  const {actor} = usePatientActor();
  const t = useTranslations('Patients.toasts');

  return useMutation({
    mutationFn: (input: SavePatientInput) => createPatient(input),
    onSuccess: async () => {
      await queryClient.invalidateQueries({queryKey: patientKeys.all(actor)});
      notify.success(t('created'));
    }
  });
}

export function useUpdatePatient(id: string) {
  const queryClient = useQueryClient();
  const {actor} = usePatientActor();
  const t = useTranslations('Patients.toasts');

  return useMutation({
    mutationFn: (input: SavePatientInput) => updatePatient(id, input),
    onSuccess: async () => {
      await queryClient.invalidateQueries({queryKey: patientKeys.all(actor)});
      notify.success(t('updated'));
    }
  });
}

export function useDeletePatient() {
  const queryClient = useQueryClient();
  const {actor} = usePatientActor();
  const t = useTranslations('Patients.toasts');

  return useMutation({
    mutationFn: (id: string) => deletePatient(id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({queryKey: patientKeys.all(actor)});
      notify.success(t('deleted'));
    }
  });
}
