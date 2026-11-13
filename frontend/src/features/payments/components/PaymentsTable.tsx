'use client';

import {useLocale, useTranslations} from 'next-intl';
import {FiEdit2} from 'react-icons/fi';
import type {Payment} from '../types/payment.types';
import {formatCurrency, formatDate, formatDateTime} from '../utils/formatters';
import {PaymentStatusBadge} from './PaymentStatusBadge';

const COLUMNS = [
  'patient',
  'appointment',
  'amount',
  'method',
  'status',
  'paidAt'
] as const;

const MAX_SKELETON_ROWS = 10;

const ACTION_BUTTON_CLASSES =
  'inline-flex items-center justify-center gap-2 rounded-xl border border-border font-semibold text-foreground transition-colors hover:bg-surface-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand';

type PaymentsTableProps = {
  payments: readonly Payment[];
  /** First load: show skeleton placeholders instead of rows. */
  isLoading?: boolean;
  /** Background refresh (page change, filter change): keep rows, dim them. */
  isFetching?: boolean;
  pageSize: number;
  onChangeStatus: (payment: Payment) => void;
};

export function PaymentsTable({
  payments,
  isLoading = false,
  isFetching = false,
  pageSize,
  onChangeStatus
}: PaymentsTableProps) {
  const t = useTranslations('Payments');
  const locale = useLocale();

  const skeletonRows = Array.from(
    {length: Math.min(Math.max(pageSize, 1), MAX_SKELETON_ROWS)},
    (_, index) => index
  );

  if (isLoading) {
    return (
      <div aria-busy="true">
        <span role="status" className="sr-only">
          {t('states.loading')}
        </span>

        <div className="hidden overflow-x-auto rounded-2xl border border-border bg-surface lg:block">
          <table className="w-full min-w-[56rem] text-sm">
            <TableHead />
            <tbody>
              {skeletonRows.map((row) => (
                <tr key={row} className="border-t border-border">
                  {COLUMNS.map((column) => (
                    <td key={column} className="px-4 py-4">
                      <SkeletonBar className="h-4 w-24" />
                    </td>
                  ))}
                  <td className="px-4 py-4">
                    <SkeletonBar className="h-9 w-28" />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <ul className="grid gap-3 sm:grid-cols-2 lg:hidden">
          {skeletonRows.slice(0, 4).map((row) => (
            <li
              key={row}
              className="rounded-2xl border border-border bg-surface p-4"
            >
              <SkeletonBar className="h-4 w-32" />
              <SkeletonBar className="mt-3 h-6 w-24" />
              <SkeletonBar className="mt-4 h-4 w-full" />
              <SkeletonBar className="mt-4 h-11 w-full" />
            </li>
          ))}
        </ul>
      </div>
    );
  }

  return (
    <div
      aria-busy={isFetching}
      className={`transition-opacity ${isFetching ? 'opacity-60' : ''}`.trim()}
    >
      {/* Desktop: a real table. */}
      <div className="hidden overflow-x-auto rounded-2xl border border-border bg-surface lg:block">
        <table className="w-full min-w-[56rem] text-sm">
          <caption className="sr-only">{t('table.caption')}</caption>
          <TableHead />
          <tbody>
            {payments.map((payment) => (
              <tr
                key={payment.id}
                className="border-t border-border transition-colors hover:bg-surface-2/50"
              >
                <td className="px-4 py-3">
                  <div dir="auto" className="font-semibold text-foreground">
                    {payment.patientName}
                  </div>
                  {payment.notes ? (
                    <div
                      dir="auto"
                      className="mt-0.5 line-clamp-1 max-w-[16rem] text-xs text-muted"
                    >
                      {payment.notes}
                    </div>
                  ) : null}
                </td>
                <td className="whitespace-nowrap px-4 py-3 text-foreground">
                  {formatDate(payment.appointmentDate, locale)}
                </td>
                <td className="whitespace-nowrap px-4 py-3 font-semibold tabular-nums text-foreground">
                  {formatCurrency(payment.amount, locale)}
                </td>
                <td className="whitespace-nowrap px-4 py-3 text-foreground">
                  {t(`method.${payment.method}`)}
                </td>
                <td className="px-4 py-3">
                  <PaymentStatusBadge status={payment.status} />
                </td>
                <td className="whitespace-nowrap px-4 py-3 text-muted">
                  {formatDateTime(payment.paidAt, locale)}
                </td>
                <td className="px-4 py-3 text-end">
                  <button
                    type="button"
                    onClick={() => onChangeStatus(payment)}
                    aria-label={`${t('actions.changeStatus')} — ${payment.patientName}`}
                    className={`${ACTION_BUTTON_CLASSES} h-9 px-3 text-xs`}
                  >
                    <FiEdit2 aria-hidden="true" className="size-3.5" />
                    {t('actions.changeStatus')}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Mobile and tablet: one card per payment. */}
      <ul className="grid gap-3 sm:grid-cols-2 lg:hidden">
        {payments.map((payment) => (
          <li
            key={payment.id}
            className="rounded-2xl border border-border bg-surface p-4"
          >
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <p dir="auto" className="truncate font-semibold text-foreground">
                  {payment.patientName}
                </p>
                <p className="mt-0.5 text-lg font-bold tabular-nums text-foreground">
                  {formatCurrency(payment.amount, locale)}
                </p>
              </div>
              <PaymentStatusBadge status={payment.status} className="shrink-0" />
            </div>

            <dl className="mt-3 grid grid-cols-2 gap-x-4 gap-y-2 text-sm">
              <div>
                <dt className="text-xs text-muted">{t('table.appointment')}</dt>
                <dd className="text-foreground">
                  {formatDate(payment.appointmentDate, locale)}
                </dd>
              </div>
              <div>
                <dt className="text-xs text-muted">{t('table.method')}</dt>
                <dd className="text-foreground">{t(`method.${payment.method}`)}</dd>
              </div>
              <div className="col-span-2">
                <dt className="text-xs text-muted">{t('table.paidAt')}</dt>
                <dd className="text-foreground">
                  {formatDateTime(payment.paidAt, locale)}
                </dd>
              </div>
            </dl>

            {payment.notes ? (
              <p dir="auto" className="mt-3 line-clamp-2 text-xs text-muted">
                {payment.notes}
              </p>
            ) : null}

            <button
              type="button"
              onClick={() => onChangeStatus(payment)}
              aria-label={`${t('actions.changeStatus')} — ${payment.patientName}`}
              className={`${ACTION_BUTTON_CLASSES} mt-4 h-11 w-full px-4 text-sm`}
            >
              <FiEdit2 aria-hidden="true" className="size-4" />
              {t('actions.changeStatus')}
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}

function TableHead() {
  const t = useTranslations('Payments.table');

  return (
    <thead>
      <tr className="bg-surface-2/60 text-xs font-semibold text-muted">
        {COLUMNS.map((column) => (
          <th
            key={column}
            scope="col"
            className="px-4 py-3 text-start font-semibold"
          >
            {t(column)}
          </th>
        ))}
        <th scope="col" className="px-4 py-3">
          <span className="sr-only">{t('actions')}</span>
        </th>
      </tr>
    </thead>
  );
}

function SkeletonBar({className}: {className: string}) {
  return (
    <span
      aria-hidden="true"
      className={`block animate-pulse rounded-md bg-surface-2 ${className}`}
    />
  );
}
