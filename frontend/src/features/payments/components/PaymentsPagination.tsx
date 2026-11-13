'use client';

import {useTranslations} from 'next-intl';
import {FiChevronLeft, FiChevronRight} from 'react-icons/fi';

type PageItem = number | 'start-ellipsis' | 'end-ellipsis';

const MAX_VISIBLE_PAGES = 7;

const BUTTON_BASE_CLASSES =
  'inline-flex items-center justify-center gap-1.5 rounded-xl border text-sm font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand disabled:cursor-not-allowed disabled:opacity-40';

const IDLE_BUTTON_CLASSES = `${BUTTON_BASE_CLASSES} border-border text-foreground enabled:hover:bg-surface-2`;

const CURRENT_PAGE_CLASSES = `${BUTTON_BASE_CLASSES} border-brand/40 bg-brand-soft text-brand [:root[data-theme=light]_&]:text-brand-strong`;

/** 1 … 4 5 6 … 20 : always keep the first, the last and the neighbours of the current page. */
function getPageItems(page: number, totalPages: number): PageItem[] {
  if (totalPages <= MAX_VISIBLE_PAGES) {
    return Array.from({length: totalPages}, (_, index) => index + 1);
  }

  const items: PageItem[] = [1];
  const start = Math.max(2, page - 1);
  const end = Math.min(totalPages - 1, page + 1);

  if (start > 2) {
    items.push('start-ellipsis');
  }

  for (let number = start; number <= end; number += 1) {
    items.push(number);
  }

  if (end < totalPages - 1) {
    items.push('end-ellipsis');
  }

  items.push(totalPages);

  return items;
}

type PaymentsPaginationProps = {
  page: number;
  totalPages: number;
  totalCount: number;
  onPageChange: (page: number) => void;
};

export function PaymentsPagination({
  page,
  totalPages,
  totalCount,
  onPageChange
}: PaymentsPaginationProps) {
  const t = useTranslations('Payments');

  if (totalCount === 0) {
    return null;
  }

  const goTo = (target: number) => {
    const next = Math.min(Math.max(target, 1), totalPages);

    if (next !== page) {
      onPageChange(next);
    }
  };

  return (
    <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
      {/* Announced by screen readers whenever a filter changes the total. */}
      <p aria-live="polite" className="text-sm text-muted">
        {t('results.count', {count: totalCount})}
      </p>

      {totalPages > 1 ? (
        <nav
          aria-label={t('pagination.label')}
          className="flex w-full items-center justify-between gap-1.5 sm:w-auto sm:justify-end"
        >
          <button
            type="button"
            onClick={() => goTo(page - 1)}
            disabled={page <= 1}
            className={`${IDLE_BUTTON_CLASSES} h-11 px-3 sm:h-10`}
          >
            <FiChevronLeft aria-hidden="true" className="size-4 rtl:rotate-180" />
            {t('pagination.previous')}
          </button>

          {/* Small screens: a short "Page x of y" instead of numbered buttons. */}
          <span className="text-sm text-muted sm:hidden">
            {t('pagination.pageOf', {page, totalPages})}
          </span>

          <ul className="hidden items-center gap-1.5 sm:flex">
            {getPageItems(page, totalPages).map((item) =>
              typeof item === 'number' ? (
                <li key={item}>
                  <button
                    type="button"
                    onClick={() => goTo(item)}
                    aria-label={t('pagination.pageOf', {
                      page: item,
                      totalPages
                    })}
                    aria-current={item === page ? 'page' : undefined}
                    className={`${item === page ? CURRENT_PAGE_CLASSES : IDLE_BUTTON_CLASSES} h-10 min-w-10 px-3 tabular-nums`}
                  >
                    {item}
                  </button>
                </li>
              ) : (
                <li key={item} aria-hidden="true" className="px-1 text-muted">
                  …
                </li>
              )
            )}
          </ul>

          <button
            type="button"
            onClick={() => goTo(page + 1)}
            disabled={page >= totalPages}
            className={`${IDLE_BUTTON_CLASSES} h-11 px-3 sm:h-10`}
          >
            {t('pagination.next')}
            <FiChevronRight aria-hidden="true" className="size-4 rtl:rotate-180" />
          </button>
        </nav>
      ) : null}
    </div>
  );
}
