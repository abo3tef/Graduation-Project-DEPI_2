'use client';

import {useMutation, useQueryClient} from '@tanstack/react-query';
import {useTranslations} from 'next-intl';
import {notify} from '@/lib/utils/toast';
import {createMedicalVisit, updateMedicalVisit} from '../api';
import {medicalRecordKeys} from '../keys';
import type {CreateMedicalVisitRequest, UpdateMedicalVisitRequest} from '../types';

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
    mutationFn: ({payload, token}: CreateVariables) => createMedicalVisit(payload, token),
    onSuccess: (visit) => {
      queryClient.setQueryData(medicalRecordKeys.visit(visit.id), visit);
      notify.success(t('created'));
    },
    onError: () => {
      notify.error(t('saveFailed'));
    }
  });
}

export function useUpdateVisitMutation(visitId: string) {
  const queryClient = useQueryClient();
  const t = useTranslations('MedicalRecords.notifications');

  return useMutation({
    mutationFn: ({payload, token}: UpdateVariables) =>
      updateMedicalVisit(visitId, payload, token),
    onSuccess: (visit) => {
      queryClient.setQueryData(medicalRecordKeys.visit(visit.id), visit);
      notify.success(t('updated'));
    },
    onError: () => {
      notify.error(t('saveFailed'));
    }
  });
}
