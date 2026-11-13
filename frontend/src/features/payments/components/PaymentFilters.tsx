'use client';

import {useTranslations} from 'next-intl';
import {useId} from 'react';
import type {ChangeEvent} from 'react';
import {FiChevronDown, FiSearch, FiX} from 'react-icons/fi';
import type {PaymentFiltersController} from '../hooks/usePaymentFilters';
import {
  PAYMENT_METHODS,
  PAYMENT_STATUSES,
  isPaymentMethod,
  isPaymentStatus
} from '../types/payment.types';

const SEARCH_MAX_LENGTH = 100;

// 44px high controls: comfortable touch targets on mobile.
const FIELD_CLASSES =
  'h-11 w-full rounded-xl border border-border bg-background text-sm text-foreground transition-colors placeholder:text-muted hover:border-brand/50 focus-visible:border-brand focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand';

const LABEL_CLASSES = 'mb-1.5 block text-xs font-semibold text-muted';

type PaymentFiltersProps = {
  controller: PaymentFiltersController;
  className?: string;
};

export function PaymentFilters({
  controller,
  className = ''
}: PaymentFiltersProps) {
  const t = useTranslations('Payments');
  const id = useId();
  const {
    search,
    status,
    method,
    hasActiveFilters,
    setSearch,
    setStatus,
    setMethod,
    resetFilters
  } = controller;

  const searchId = `${id}-search`;
  const statusId = `${id}-status`;
  const methodId = `${id}-method`;

  function handleStatusChange(event: ChangeEvent<HTMLSelectElement>) {
    const value = event.target.value;

    if (value === 'all') {
      setStatus('all');
    } else if (isPaymentStatus(value)) {
      setStatus(value);
    }
  }

  function handleMethodChange(event: ChangeEvent<HTMLSelectElement>) {
    const value = event.target.value;

    if (value === 'all') {
      setMethod('all');
    } else if (isPaymentMethod(value)) {
      setMethod(value);
    }
  }

  // One extra column appears only while the "clear" button is visible.
  const desktopColumns = hasActiveFilters
    ? 'lg:grid-cols-[minmax(0,1fr)_11rem_11rem_auto]'
    : 'lg:grid-cols-[minmax(0,1fr)_11rem_11rem]';

  return (
    <div
      role="search"
      aria-label={t('filters.label')}
      className={`grid gap-3 rounded-2xl border border-border bg-surface p-3 sm:grid-cols-2 sm:p-4 ${desktopColumns} ${className}`.trim()}
    >
      <div className="sm:col-span-2 lg:col-span-1">
        <label htmlFor={searchId} className="sr-only">
          {t('filters.searchLabel')}
        </label>
        <div className="relative">
          <FiSearch
            aria-hidden="true"
            className="pointer-events-none absolute start-3.5 top-1/2 size-4 -translate-y-1/2 text-muted"
          />
          <input
            id={searchId}
            type="search"
            value={search}
            maxLength={SEARCH_MAX_LENGTH}
            autoComplete="off"
            enterKeyHint="search"
            placeholder={t('filters.searchPlaceholder')}
            onChange={(event) => setSearch(event.target.value)}
            className={`${FIELD_CLASSES} ps-10 pe-3`}
          />
        </div>
      </div>

      <div>
        <label htmlFor={statusId} className={LABEL_CLASSES}>
          {t('filters.statusLabel')}
        </label>
        <div className="relative">
          <select
            id={statusId}
            value={status}
            onChange={handleStatusChange}
            className={`${FIELD_CLASSES} appearance-none ps-3 pe-9`}
          >
            <option value="all">{t('filters.all')}</option>
            {PAYMENT_STATUSES.map((item) => (
              <option key={item} value={item}>
                {t(`status.${item}`)}
              </option>
            ))}
          </select>
          <FiChevronDown
            aria-hidden="true"
            className="pointer-events-none absolute end-3 top-1/2 size-4 -translate-y-1/2 text-muted"
          />
        </div>
      </div>

      <div>
        <label htmlFor={methodId} className={LABEL_CLASSES}>
          {t('filters.methodLabel')}
        </label>
        <div className="relative">
          <select
            id={methodId}
            value={method}
            onChange={handleMethodChange}
            className={`${FIELD_CLASSES} appearance-none ps-3 pe-9`}
          >
            <option value="all">{t('filters.all')}</option>
            {PAYMENT_METHODS.map((item) => (
              <option key={item} value={item}>
                {t(`method.${item}`)}
              </option>
            ))}
          </select>
          <FiChevronDown
            aria-hidden="true"
            className="pointer-events-none absolute end-3 top-1/2 size-4 -translate-y-1/2 text-muted"
          />
        </div>
      </div>

      {hasActiveFilters ? (
        <div className="flex items-end sm:col-span-2 lg:col-span-1">
          <button
            type="button"
            onClick={resetFilters}
            className="inline-flex h-11 w-full items-center justify-center gap-2 rounded-xl border border-border px-4 text-sm font-semibold text-foreground transition-colors hover:bg-surface-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand"
          >
            <FiX aria-hidden="true" className="size-4" />
            {t('actions.clearFilters')}
          </button>
        </div>
      ) : null}
    </div>
  );
}