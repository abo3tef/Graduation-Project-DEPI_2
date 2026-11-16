'use client';

import {useMutation, useQueryClient} from '@tanstack/react-query';
import {useTranslations} from 'next-intl';
import {notify} from '@/lib/utils/toast';
import {createMedication, deleteMedication, updateMedication} from '../api';
import {medicalRecordKeys} from '../keys';
import type {CreateMedicationRequest, UpdateMedicationRequest} from '../types';

type CreateVariables = {
  payload: CreateMedicationRequest;
};

type UpdateVariables = {
  medicationId: string;
  payload: UpdateMedicationRequest;
};

type DeleteVariables = {
  medicationId: string;
};

/**
 * Medication mutations invalidate the parent visit query for the same reason as the diagnosis
 * hooks: the visit response already carries the ordered medication lines (D-24). The delete is a
 * permanent `DELETE` (D-23), so there is no optimistic removal and no rollback path.
 */
export function useCreateMedicationMutation(visitId: string) {
  const queryClient = useQueryClient();
  const t = useTranslations('MedicalRecords.notifications');

  return useMutation({
    mutationFn: ({payload}: CreateVariables) => createMedication(visitId, payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: medicalRecordKeys.visit(visitId)
      });
      notify.success(t('medicationCreated'));
    },
    onError: () => {
      notify.error(t('medicationSaveFailed'));
    }
  });
}

export function useUpdateMedicationMutation(visitId: string) {
  const queryClient = useQueryClient();
  const t = useTranslations('MedicalRecords.notifications');

  return useMutation({
    mutationFn: ({medicationId, payload}: UpdateVariables) =>
      updateMedication(visitId, medicationId, payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: medicalRecordKeys.visit(visitId)
      });
      notify.success(t('medicationUpdated'));
    },
    onError: () => {
      notify.error(t('medicationSaveFailed'));
    }
  });
}

export function useDeleteMedicationMutation(visitId: string) {
  const queryClient = useQueryClient();
  const t = useTranslations('MedicalRecords.notifications');

  return useMutation({
    mutationFn: ({medicationId}: DeleteVariables) =>
      deleteMedication(visitId, medicationId),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: medicalRecordKeys.visit(visitId)
      });
      notify.success(t('medicationDeleted'));
    },
    onError: () => {
      notify.error(t('medicationDeleteFailed'));
    }
  });
}
