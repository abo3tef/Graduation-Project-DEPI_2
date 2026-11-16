'use client';

import {useMutation, useQueryClient} from '@tanstack/react-query';
import {useTranslations} from 'next-intl';
import {notify} from '@/lib/utils/toast';
import {createDiagnosis, deleteDiagnosis, updateDiagnosis} from '../api';
import {medicalRecordKeys} from '../keys';
import type {CreateDiagnosisRequest, UpdateDiagnosisRequest} from '../types';

type CreateVariables = {
  payload: CreateDiagnosisRequest;
};

type UpdateVariables = {
  diagnosisId: string;
  payload: UpdateDiagnosisRequest;
};

type DeleteVariables = {
  diagnosisId: string;
};

/**
 * Diagnoses are children of the visit response (D-24), so every mutation invalidates the parent
 * query. The refetched `MedicalVisitResponse` is the single source of truth for the section list,
 * which preserves the server's `diagnosisDate, id` ordering without local bookkeeping. The delete
 * is a permanent `DELETE` (D-23), so there is no optimistic removal and no rollback path.
 */
export function useCreateDiagnosisMutation(visitId: string) {
  const queryClient = useQueryClient();
  const t = useTranslations('MedicalRecords.notifications');

  return useMutation({
    mutationFn: ({payload}: CreateVariables) => createDiagnosis(visitId, payload),
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
    mutationFn: ({diagnosisId, payload}: UpdateVariables) =>
      updateDiagnosis(visitId, diagnosisId, payload),
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

export function useDeleteDiagnosisMutation(visitId: string) {
  const queryClient = useQueryClient();
  const t = useTranslations('MedicalRecords.notifications');

  return useMutation({
    mutationFn: ({diagnosisId}: DeleteVariables) =>
      deleteDiagnosis(visitId, diagnosisId),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: medicalRecordKeys.visit(visitId)
      });
      notify.success(t('diagnosisDeleted'));
    },
    onError: () => {
      notify.error(t('diagnosisDeleteFailed'));
    }
  });
}
