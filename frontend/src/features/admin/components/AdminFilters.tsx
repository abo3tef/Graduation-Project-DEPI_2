'use client';

import {useTranslations} from 'next-intl';
import {
  ADMIN_SEARCH_MAX_LENGTH,
  type AdminRoleFilter,
  type AdminStatusFilter,
  type AdminUserFilters
} from '../types';

type AdminFiltersProps = {
  filters: AdminUserFilters;
  onChange: (patch: Partial<AdminUserFilters>) => void;
  onClear: () => void;
};

const INPUT_CLASS =
  'w-full rounded-xl border border-border bg-background px-3 py-2 text-sm outline-none transition-colors focus:border-brand focus:ring-2 focus:ring-brand/30';

export function AdminFilters({filters, onChange, onClear}: AdminFiltersProps) {
  const t = useTranslations('Admin.filters');
  const labels = useTranslations('Admin');
  const hasFilters =
    filters.search.trim() !== '' || filters.role !== 'all' || filters.status !== 'all';

  return (
    <section
      aria-label={t('label')}
      className="rounded-2xl border border-border bg-surface p-4 sm:p-5"
    >
      <div className="grid gap-4 md:grid-cols-[2fr_1fr_1fr_auto] md:items-end">
        <label className="flex flex-col gap-2 text-sm font-medium">
          {t('searchLabel')}
          <input
            type="search"
            value={filters.search}
            maxLength={ADMIN_SEARCH_MAX_LENGTH}
            onChange={(event) => onChange({search: event.target.value, pageNumber: 1})}
            placeholder={t('searchPlaceholder')}
            className={INPUT_CLASS}
          />
        </label>

        <label className="flex flex-col gap-2 text-sm font-medium">
          {t('roleLabel')}
          <select
            value={filters.role}
            onChange={(event) =>
              onChange({role: event.target.value as AdminRoleFilter, pageNumber: 1})
            }
            className={INPUT_CLASS}
          >
            <option value="all">{t('allRoles')}</option>
            <option value="Admin">{labels('roles.Admin')}</option>
            <option value="Doctor">{labels('roles.Doctor')}</option>
            <option value="Secretary">{labels('roles.Secretary')}</option>
          </select>
        </label>

        <label className="flex flex-col gap-2 text-sm font-medium">
          {t('statusLabel')}
          <select
            value={filters.status}
            onChange={(event) =>
              onChange({status: event.target.value as AdminStatusFilter, pageNumber: 1})
            }
            className={INPUT_CLASS}
          >
            <option value="all">{t('allStatuses')}</option>
            <option value="Active">{labels('statuses.Active')}</option>
            <option value="Inactive">{labels('statuses.Inactive')}</option>
          </select>
        </label>

        <button
          type="button"
          onClick={onClear}
          disabled={!hasFilters}
          className="inline-flex h-10 items-center justify-center rounded-xl border border-border px-4 text-sm font-semibold transition-colors hover:bg-surface-2 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {t('clear')}
        </button>
      </div>
    </section>
  );
}
