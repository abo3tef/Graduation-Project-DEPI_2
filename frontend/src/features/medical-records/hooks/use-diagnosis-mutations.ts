'use client';

import {useMutation, useQueryClient} from '@tanstack/react-query';
import {useTranslations} from 'next-intl';
import {notify} from '@/lib/utils/toast';
import {createDiagnosis, updateDiagnosis} from '../api';
import {medicalRecordKeys} from '../keys';
import type {CreateDiagnosisRequest, UpdateDiagnosisRequest} from '../types';

type CreateVariables = {
  payload: CreateDiagnosisRequest;
  token: string;
};

type UpdateVariables = {
  diagnosisId: string;
  payload: UpdateDiagnosisRequest;
  token: string;
};

/**
 * Diagnoses are children of the visit response (D-24), so both mutations invalidate the parent
 * query. The refetched `MedicalVisitResponse` is the single source of truth for the section list,
 * which preserves the server's `diagnosisDate, id` ordering without local bookkeeping.
 */
export function useCreateDiagnosisMutation(visitId: string) {
  const queryClient = useQueryClient();
  const t = useTranslations('MedicalRecords.notifications');

  return useMutation({
    mutationFn: ({payload, token}: CreateVariables) =>
      createDiagnosis(visitId, payload, token),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: medicalRecordKeys.visit(visitId)
      });
      notify.success(t('diagnosisCreated'));
    },
    onError: () => {
      notify.error(t('diagnosisSaveFailed'));
    }
  });
}

export function useUpdateDiagnosisMutation(visitId: string) {
  const queryClient = useQueryClient();
  const t = useTranslations('MedicalRecords.notifications');

  return useMutation({
    mutationFn: ({diagnosisId, payload, token}: UpdateVariables) =>
      updateDiagnosis(visitId, diagnosisId, payload, token),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: medicalRecordKeys.visit(visitId)
      });
      notify.success(t('diagnosisUpdated'));
    },
    onError: () => {
      notify.error(t('diagnosisSaveFailed'));
    }
  });
}
