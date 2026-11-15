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
  getPaymentErrorKey,
  isRetryablePaymentError
} from '../services/payment-errors';
import {
  createPayment,
  isPaymentPartiallyRecordedError,
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

function retryOnce(failureCount: number, error: unknown): boolean {
  return isRetryablePaymentError(error) && failureCount < 1;
}

/** Translated message for any payment failure (by API code, then status). */
export function usePaymentErrorMessage() {
  const t = useTranslations('Payments.errors');

  return useCallback(
    (error: unknown): string => t(getPaymentErrorKey(error)),
    [t]
  );
}

/**
 * Payments are server-scoped by the token, so the query is always sent and the
 * API answers `401`/`403` when the session or role is wrong.
 */
export function usePayments(filters: PaymentFilters) {
  return useQuery({
    queryKey: paymentKeys.list(filters),
    queryFn: ({signal}) => listPayments(filters, signal),
    placeholderData: keepPreviousData,
    retry: retryOnce
  });
}

export function usePayableAppointments(enabled: boolean) {
  return useQuery({
    queryKey: paymentKeys.payableAppointments(),
    queryFn: ({signal}) => listPayableAppointments(signal),
    enabled,
    staleTime: 0,
    retry: retryOnce
  });
}

export function useCreatePayment() {
  const queryClient = useQueryClient();
  const t = useTranslations('Payments.toast');
  const getErrorMessage = usePaymentErrorMessage();

  return useMutation({
    mutationFn: createPayment,
    onSuccess: async (_payment, variables) => {
      notify.success(t(variables.recordAsPaid ? 'createdPaid' : 'created'));
      await queryClient.invalidateQueries({queryKey: paymentKeys.all});
    },
    onError: async (error) => {
      if (isPaymentPartiallyRecordedError(error)) {
        notify.warning(t('partiallyRecorded'));
        await queryClient.invalidateQueries({queryKey: paymentKeys.all});
        return;
      }

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
      await queryClient.invalidateQueries({queryKey: paymentKeys.all});
    },
    onError: (error) => {
      notify.error(getErrorMessage(error));
    }
  });
}
