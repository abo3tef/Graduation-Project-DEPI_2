'use client';

import {FiRotateCcw} from 'react-icons/fi';
import {useTranslations} from 'next-intl';
import type {TimelineFiltersController, TimelineTypeFilter} from '../hooks/use-timeline-filters';

type TimelineFiltersProps = {
  controller: TimelineFiltersController;
};

/**
 * Timeline filters, applied immediately (appointments convention): a change
 * updates the query key, returns to page 1 and refetches. The type options are
 * exactly the backend contract values; the reset button appears only when
 * something is active.
 */
export function TimelineFilters({controller}: TimelineFiltersProps) {
  const t = useTranslations('MedicalTimeline.filters');

  return (
    <section
      aria-labelledby="timeline-filters-title"
      className="rounded-2xl border border-border bg-surface p-4 sm:p-5"
    >
      <h2 id="timeline-filters-title" className="text-sm font-bold">
        {t('title')}
      </h2>

      <div className="mt-3 grid gap-3 sm:grid-cols-3">
        <div className="flex flex-col gap-1.5">
          <label className="text-xs font-semibold text-muted" htmlFor="timeline-type-filter">
            {t('type')}
          </label>
          <select
            id="timeline-type-filter"
            value={controller.state.eventType}
            onChange={(event) => controller.setEventType(event.target.value as TimelineTypeFilter)}
            className="w-full rounded-xl border border-border bg-background px-3 py-2 text-sm text-foreground outline-none transition-colors focus:border-brand focus:ring-2 focus:ring-brand/30"
          >
            <option value="all">{t('typeOptions.all')}</option>
            <option value="Visit">{t('typeOptions.visit')}</option>
            <option value="Diagnosis">{t('typeOptions.diagnosis')}</option>
            <option value="Medication">{t('typeOptions.medication')}</option>
            <option value="MedicalNote">{t('typeOptions.medicalNote')}</option>
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-xs font-semibold text-muted" htmlFor="timeline-from">
            {t('from')}
          </label>
          <input
            id="timeline-from"
            type="datetime-local"
            value={controller.state.fromInput}
            onChange={(event) => controller.setFromInput(event.target.value)}
            className="w-full rounded-xl border border-border bg-background px-3 py-2 text-sm text-foreground outline-none transition-colors focus:border-brand focus:ring-2 focus:ring-brand/30"
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-xs font-semibold text-muted" htmlFor="timeline-to">
            {t('to')}
          </label>
          <input
            id="timeline-to"
            type="datetime-local"
            value={controller.state.toInput}
            onChange={(event) => controller.setToInput(event.target.value)}
            className="w-full rounded-xl border border-border bg-background px-3 py-2 text-sm text-foreground outline-none transition-colors focus:border-brand focus:ring-2 focus:ring-brand/30"
          />
        </div>
      </div>

      {controller.invalidRange ? (
        <p role="alert" className="mt-3 text-xs font-semibold text-danger">
          {t('invalidRange')}
        </p>
      ) : null}

      {controller.hasActiveFilters ? (
        <button
          type="button"
          onClick={controller.reset}
          className="mt-3 inline-flex items-center gap-2 rounded-xl border border-border px-3 py-1.5 text-xs font-semibold text-muted transition-colors hover:border-brand hover:text-brand"
        >
          <FiRotateCcw aria-hidden className="size-3.5" />
          {t('reset')}
        </button>
      ) : null}
    </section>
  );
}
