'use client';

import {useMutation, useQueryClient} from '@tanstack/react-query';
import {useTranslations} from 'next-intl';
import {notify} from '@/lib/utils/toast';
import {createMedicalVisit, updateMedicalVisit} from '../api';
import {medicalRecordKeys} from '../keys';
import type {CreateMedicalVisitRequest, UpdateMedicalVisitRequest} from '../types';
import {ApiError} from '@/lib/api';
import {readMedicalToken} from '../use-medical-credentials';
import {readCredentialScope} from '../credential-scope';

const pendingScopes = new WeakMap<object, number>();

function captureScope(variables: object): number {
  if (!readMedicalToken()) {
    throw new ApiError({kind: 'unauthorized', status: 401});
  }
  const scope = readCredentialScope();
  pendingScopes.set(variables, scope);
  return scope;
}

function isCurrentScope(scope: number | undefined): boolean {
  return Boolean(readMedicalToken()) && scope !== undefined && scope === readCredentialScope();
}

function requireCapturedScope(variables: object): void {
  if (!isCurrentScope(pendingScopes.get(variables))) {
    throw new ApiError({kind: 'unauthorized', status: 401});
  }
}

type CreateVariables = {
  payload: CreateMedicalVisitRequest;
};

type UpdateVariables = {
  payload: UpdateMedicalVisitRequest;
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
    mutationFn: (variables: CreateVariables) => {
      requireCapturedScope(variables);
      return createMedicalVisit(variables.payload);
    },
    onSuccess: (visit, _variables, scope) => {
      if (scope === undefined || !isCurrentScope(scope)) return;
      queryClient.setQueryData(medicalRecordKeys.visitDetail(visit.id, scope), visit);

      notify.success(t('created'));
    },
    onError: (_error, _variables, scope) => {
      if (scope === undefined || !isCurrentScope(scope)) return;
      notify.error(t('saveFailed'));
    }
  });
}

export function useUpdateVisitMutation(visitId: string) {
  const queryClient = useQueryClient();
  const t = useTranslations('MedicalRecords.notifications');

  return useMutation({
    onMutate: captureScope,
    mutationFn: (variables: UpdateVariables) => {
      requireCapturedScope(variables);
      return updateMedicalVisit(visitId, variables.payload);
    },
    onSuccess: (visit, _variables, scope) => {
      if (scope === undefined || !isCurrentScope(scope)) return;
      queryClient.setQueryData(medicalRecordKeys.visitDetail(visit.id, scope), visit);

      notify.success(t('updated'));
    },
    onError: (_error, _variables, scope) => {
      if (!isCurrentScope(scope)) return;
      notify.error(t('saveFailed'));
    }
  });
}
