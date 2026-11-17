'use client';

import {FiChevronLeft, FiChevronRight} from 'react-icons/fi';
import {useTranslations} from 'next-intl';

type TimelinePaginationProps = {
  pageNumber: number;
  totalPages: number;
  onChange: (pageNumber: number) => void;
  disabled?: boolean;
};

/**
 * Previous/next with the current position, driven only by the backend
 * `PagedResult` metadata. Hidden while there is nothing to page through.
 */
export function TimelinePagination({
  pageNumber,
  totalPages,
  onChange,
  disabled = false
}: TimelinePaginationProps) {
  const t = useTranslations('MedicalTimeline.pagination');

  if (totalPages <= 1) {
    return null;
  }

  return (
    <nav aria-label={t('label')} className="flex flex-wrap items-center justify-between gap-3">
      <p className="m-0 text-xs text-muted" aria-live="polite">
        {t('position', {page: pageNumber, total: totalPages})}
      </p>
      <div className="flex gap-2">
        <button
          type="button"
          disabled={disabled || pageNumber <= 1}
          onClick={() => onChange(pageNumber - 1)}
          className="inline-flex items-center gap-1.5 rounded-xl border border-border px-3 py-1.5 text-xs font-semibold text-foreground transition-colors hover:border-brand hover:text-brand disabled:cursor-not-allowed disabled:opacity-50"
        >
          <FiChevronLeft aria-hidden className="size-3.5 rtl:hidden" />
          <FiChevronRight aria-hidden className="size-3.5 ltr:hidden" />
          {t('previous')}
        </button>
        <button
          type="button"
          disabled={disabled || pageNumber >= totalPages}
          onClick={() => onChange(pageNumber + 1)}
          className="inline-flex items-center gap-1.5 rounded-xl border border-border px-3 py-1.5 text-xs font-semibold text-foreground transition-colors hover:border-brand hover:text-brand disabled:cursor-not-allowed disabled:opacity-50"
        >
          {t('next')}
          <FiChevronRight aria-hidden className="size-3.5 rtl:hidden" />
          <FiChevronLeft aria-hidden className="size-3.5 ltr:hidden" />
        </button>
      </div>
    </nav>
  );
}
