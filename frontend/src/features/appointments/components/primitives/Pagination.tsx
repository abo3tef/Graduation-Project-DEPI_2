'use client';

import {FiChevronLeft, FiChevronRight} from 'react-icons/fi';
import {useTranslations} from 'next-intl';
import {Button} from './Button';

type PaginationProps = {
  pageNumber: number;
  totalPages: number;
  onChange: (pageNumber: number) => void;
  disabled?: boolean;
};

/** Previous/next with the current position; arrows mirror in RTL. */
export function Pagination({pageNumber, totalPages, onChange, disabled = false}: PaginationProps) {
  const t = useTranslations('Appointments.pagination');

  if (totalPages <= 1) {
    return null;
  }

  return (
    <nav aria-label={t('label')} className="flex flex-wrap items-center justify-between gap-3">
      <p className="m-0 text-xs text-muted" aria-live="polite">
        {t('position', {page: pageNumber, total: totalPages})}
      </p>
      <div className="flex gap-2">
        <Button
          size="sm"
          disabled={disabled || pageNumber <= 1}
          onClick={() => onChange(pageNumber - 1)}
        >
          <FiChevronLeft aria-hidden className="size-3.5 rtl:hidden" />
          <FiChevronRight aria-hidden className="size-3.5 ltr:hidden" />
          {t('previous')}
        </Button>
        <Button
          size="sm"
          disabled={disabled || pageNumber >= totalPages}
          onClick={() => onChange(pageNumber + 1)}
        >
          {t('next')}
          <FiChevronRight aria-hidden className="size-3.5 rtl:hidden" />
          <FiChevronLeft aria-hidden className="size-3.5 ltr:hidden" />
        </Button>
      </div>
    </nav>
  );
}
