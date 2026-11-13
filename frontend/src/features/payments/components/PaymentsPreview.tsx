'use client';

import {useTranslations} from 'next-intl';
import {ThemeToggle} from '@/components/layout/ThemeToggle';
import {LocaleSwitcher} from '@/components/shared/LocaleSwitcher';
import {usePaymentFilters} from '../hooks/usePaymentFilters';
import {
  usePaymentErrorMessage,
  usePayments,
  useUpdatePaymentStatus
} from '../hooks/usePayments';
import {PAYMENT_STATUSES} from '../types/payment.types';
import type {Payment} from '../types/payment.types';
import {PaymentFilters} from './PaymentFilters';
import {PaymentsPagination} from './PaymentsPagination';
import {PaymentsTable} from './PaymentsTable';

/**
 * TEMPORARY preview page content.
 * It only wires the finished pieces together so they can be seen in the browser.
 * It is replaced by `PaymentsView` (file 15), which adds the dialogs.
 */
export function PaymentsPreview() {
  const t = useTranslations('Payments');
  const controller = usePaymentFilters();
  const query = usePayments(controller.filters);
  const updateStatus = useUpdatePaymentStatus();
  const getErrorMessage = usePaymentErrorMessage();

  const payments = query.data?.items ?? [];
  const isEmpty = !query.isPending && !query.isError && payments.length === 0;

  // TEMPORARY: cycles the status so the hooks and the toast can be tested.
  function handleChangeStatus(payment: Payment) {
    const index = PAYMENT_STATUSES.indexOf(payment.status);
    const next =
      PAYMENT_STATUSES[(index + 1) % PAYMENT_STATUSES.length] ?? payment.status;

    updateStatus.mutate({id: payment.id, status: next});
  }

  return (
    <main className="mx-auto flex min-h-screen w-full max-w-6xl flex-col gap-5 px-4 py-6 sm:px-6 lg:px-8">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <h1 className="text-2xl font-bold text-foreground sm:text-3xl">
            {t('title')}
          </h1>
          <p className="mt-1 text-sm text-muted">{t('subtitle')}</p>
        </div>

        <div className="flex items-center gap-2">
          <LocaleSwitcher />
          <ThemeToggle />
        </div>
      </header>

      <p
        role="note"
        className="rounded-xl border border-brand/30 bg-brand-soft px-4 py-3 text-sm font-semibold text-brand [:root[data-theme=light]_&]:text-brand-strong"
      >
        {t('prototypeNotice')}
      </p>

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
          ) : null}
        </div>
      ) : (
        <>
          <PaymentsTable
            payments={payments}
            isLoading={query.isPending}
            isFetching={query.isFetching && !query.isPending}
            pageSize={controller.filters.pageSize}
            onChangeStatus={handleChangeStatus}
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
    </main>
  );
}
