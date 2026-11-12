'use client';

import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient
} from '@tanstack/react-query';
import {useTranslations} from 'next-intl';
import {useCallback} from 'react';
import {notify} from '@/lib/utils/toast';
import {
  createPayment,
  isPaymentServiceError,
  listPayableAppointments,
  listPayments,
  updatePaymentStatus
} from '../services/payments.service';
import type {PaymentFilters} from '../types/payment.types';


export const paymentKeys = {
  all: ['payments'] as const,
  lists: () => [...paymentKeys.all, 'list'] as const,
  list: (filters: PaymentFilters) => [...paymentKeys.lists(), filters] as const,
  payableAppointments: () =>
    [...paymentKeys.all, 'payable-appointments'] as const
};


function shouldRetry(failureCount: number, error: unknown): boolean {
  return (
    isPaymentServiceError(error) &&
    error.code === 'NETWORK' &&
    failureCount < 1
  );
}


export function usePaymentErrorMessage() {
  const t = useTranslations('Payments.errors');

  return useCallback(
    (error: unknown): string =>
      isPaymentServiceError(error) ? t(error.code) : t('unknown'),
    [t]
  );
}


export function usePayments(filters: PaymentFilters) {
  return useQuery({
    queryKey: paymentKeys.list(filters),
    queryFn: () => listPayments(filters),
    
    placeholderData: keepPreviousData,
    retry: shouldRetry
  });
}


export function usePayableAppointments(enabled: boolean) {
  return useQuery({
    queryKey: paymentKeys.payableAppointments(),
    queryFn: listPayableAppointments,
    enabled,
    staleTime: 0,
    retry: shouldRetry
  });
}


export function useCreatePayment() {
  const queryClient = useQueryClient();
  const t = useTranslations('Payments.toast');
  const getErrorMessage = usePaymentErrorMessage();

  return useMutation({
    mutationFn: createPayment,
    onSuccess: async () => {
      notify.success(t('created'));
      
      await queryClient.invalidateQueries({queryKey: paymentKeys.all});
    },
    onError: (error) => {
      notify.error(getErrorMessage(error));
    }
  });
}

export function useUpdatePaymentStatus() {
  const queryClient = useQueryClient();
  const t = useTranslations('Payments.toast');
  const getErrorMessage = usePaymentErrorMessage();

  return useMutation({
    mutationFn: updatePaymentStatus,
    onSuccess: async () => {
      notify.success(t('statusUpdated'));
      await queryClient.invalidateQueries({queryKey: paymentKeys.lists()});
    },
    onError: (error) => {
      notify.error(getErrorMessage(error));
    }
  });
}