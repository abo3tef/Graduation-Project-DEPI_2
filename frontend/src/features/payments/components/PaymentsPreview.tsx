'use client';

import {useState} from 'react';
import {useTranslations} from 'next-intl';
import {FiPlus} from 'react-icons/fi';
import {usePaymentFilters} from '../hooks/usePaymentFilters';
import {usePaymentErrorMessage, usePayments} from '../hooks/usePayments';
import type {Payment} from '../types/payment.types';
import {PaymentFilters} from './PaymentFilters';
import {PaymentFormDialog} from './PaymentFormDialog';
import {PaymentsPagination} from './PaymentsPagination';
import {PaymentStatusDialog} from './PaymentStatusDialog';
import {PaymentsTable} from './PaymentsTable';

/**
 * Payments screen: real payment list with filters, paging, a create dialog and
 * the allowed status transitions. Data is scoped by the API from the token.
 */
export function PaymentsPreview() {
  const t = useTranslations('Payments');
  const controller = usePaymentFilters();
  const query = usePayments(controller.filters);
  const getErrorMessage = usePaymentErrorMessage();
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [statusPayment, setStatusPayment] = useState<Payment | null>(null);

  const payments = query.data?.items ?? [];
  const isEmpty = !query.isPending && !query.isError && payments.length === 0;

  return (
    <main className="mx-auto flex min-h-screen w-full max-w-6xl flex-col gap-5 px-4 py-6 sm:px-6 lg:px-8">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <h1 className="text-2xl font-bold text-foreground sm:text-3xl">
            {t('title')}
          </h1>
          <p className="mt-1 text-sm text-muted">{t('subtitle')}</p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={() => setIsFormOpen(true)}
            className="inline-flex h-11 items-center justify-center gap-2 rounded-xl border border-transparent bg-brand px-4 text-sm font-bold text-[#06211f] transition-opacity hover:opacity-90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand"
          >
            <FiPlus aria-hidden="true" className="size-4" />
            {t('actions.record')}
          </button>
        </div>
      </header>

      <PaymentFilters controller={controller} />

      {query.isError && !query.data ? (
        <div
          role="alert"
          className="rounded-2xl border border-border bg-surface p-6 text-center"
        >
          <h2 className="text-lg font-bold text-foreground">
            {t('states.errorTitle')}
          </h2>
          <p className="mt-1 text-sm text-muted">
            {getErrorMessage(query.error)}
          </p>
          <button
            type="button"
            onClick={() => query.refetch()}
            className="mt-4 inline-flex h-11 items-center justify-center rounded-xl border border-border px-5 text-sm font-semibold text-foreground transition-colors hover:bg-surface-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand"
          >
            {t('actions.retry')}
          </button>
        </div>
      ) : isEmpty ? (
        <div className="rounded-2xl border border-border bg-surface p-6 text-center">
          <h2 className="text-lg font-bold text-foreground">
            {controller.hasActiveFilters
              ? t('states.noResultsTitle')
              : t('states.emptyTitle')}
          </h2>
          <p className="mt-1 text-sm text-muted">
            {controller.hasActiveFilters
              ? t('states.noResultsDescription')
              : t('states.emptyDescription')}
          </p>
          {controller.hasActiveFilters ? (
            <button
              type="button"
              onClick={controller.resetFilters}
              className="mt-4 inline-flex h-11 items-center justify-center rounded-xl border border-border px-5 text-sm font-semibold text-foreground transition-colors hover:bg-surface-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand"
            >
              {t('actions.clearFilters')}
            </button>
          ) : (
            <button
              type="button"
              onClick={() => setIsFormOpen(true)}
              className="mt-4 inline-flex h-11 items-center justify-center gap-2 rounded-xl border border-transparent bg-brand px-5 text-sm font-bold text-[#06211f] transition-opacity hover:opacity-90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand"
            >
              <FiPlus aria-hidden="true" className="size-4" />
              {t('actions.record')}
            </button>
          )}
        </div>
      ) : (
        <>
          <PaymentsTable
            payments={payments}
            isLoading={query.isPending}
            isFetching={query.isFetching && !query.isPending}
            pageSize={controller.filters.pageSize}
            onChangeStatus={setStatusPayment}
          />

          {query.data ? (
            <PaymentsPagination
              page={query.data.page}
              totalPages={query.data.totalPages}
              totalCount={query.data.totalCount}
              onPageChange={controller.setPage}
            />
          ) : null}
        </>
      )}

      <PaymentFormDialog open={isFormOpen} onClose={() => setIsFormOpen(false)} />
      <PaymentStatusDialog
        payment={statusPayment}
        onClose={() => setStatusPayment(null)}
      />
    </main>
  );
}
