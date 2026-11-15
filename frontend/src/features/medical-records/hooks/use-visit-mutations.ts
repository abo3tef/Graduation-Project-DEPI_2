'use client';

import {useMutation, useQueryClient} from '@tanstack/react-query';
import {useTranslations} from 'next-intl';
import {notify} from '@/lib/utils/toast';
import {createMedicalVisit, updateMedicalVisit} from '../api';
import {medicalRecordKeys} from '../keys';
import type {CreateMedicalVisitRequest, UpdateMedicalVisitRequest} from '../types';
import {ApiError} from '@/lib/api';
import {readDevToken} from '../use-dev-token';
import {readCredentialScope} from '../credential-scope';

function captureScope({token}: {token: string}): number {
  if (!token.trim() || token !== readDevToken()) {
    throw new ApiError({kind: 'unauthorized', status: 401});
  }
  return readCredentialScope();
}

function isCurrentScope(token: string, scope: number | undefined): boolean {
  return token === readDevToken() && scope === readCredentialScope();
}

type CreateVariables = {
  payload: CreateMedicalVisitRequest;
  token: string;
};

type UpdateVariables = {
  payload: UpdateMedicalVisitRequest;
  token: string;
};

/**
 * The visit response already carries its diagnoses and medications (D-24), so a successful
 * create/update can seed the detail query with the returned body instead of refetching.
 */
export function useCreateVisitMutation() {
  const queryClient = useQueryClient();
  const t = useTranslations('MedicalRecords.notifications');

  return useMutation({
    onMutate: captureScope,
    mutationFn: ({payload, token}: CreateVariables) => createMedicalVisit(payload, token),
    onSuccess: (visit, variables, scope) => {
      if (scope === undefined || !isCurrentScope(variables.token, scope)) return;
      queryClient.setQueryData(medicalRecordKeys.visitDetail(visit.id, scope), visit);
      notify.success(t('created'));
    },
    onError: (_error, variables, scope) => {
      if (scope === undefined || !isCurrentScope(variables.token, scope)) return;
      notify.error(t('saveFailed'));
    }
  });
}

export function useUpdateVisitMutation(visitId: string) {
  const queryClient = useQueryClient();
  const t = useTranslations('MedicalRecords.notifications');

  return useMutation({
    onMutate: captureScope,
    mutationFn: ({payload, token}: UpdateVariables) =>
      updateMedicalVisit(visitId, payload, token),
    onSuccess: (visit, variables, scope) => {
      if (scope === undefined || !isCurrentScope(variables.token, scope)) return;
      queryClient.setQueryData(medicalRecordKeys.visitDetail(visit.id, scope), visit);
      notify.success(t('updated'));
    },
    onError: (_error, variables, scope) => {
      if (!isCurrentScope(variables.token, scope)) return;
      notify.error(t('saveFailed'));
    }
  });
}
