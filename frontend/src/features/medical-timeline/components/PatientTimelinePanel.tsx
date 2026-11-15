'use client';

import {useTranslations} from 'next-intl';
import {
  DevTokenPanel,
  LoadingPanel,
  StatePanel,
  isGuid,
  toQueryFailureKind,
  useDevToken
} from '@/features/medical-records';
import {usePatientTimeline} from '../hooks/use-patient-timeline';
import {useTimelineFilters} from '../hooks/use-timeline-filters';
import {TimelineEventList} from './TimelineEventList';
import {TimelineFailure} from './TimelineFailure';
import {TimelineFilters} from './TimelineFilters';
import {TimelinePagination} from './TimelinePagination';

type PatientTimelinePanelProps = {
  patientId: string;
};

/**
 * The patient medical timeline (T3): token gate, filters, event list and
 * pagination over `usePatientTimeline`. The route/screen that provides the
 * patient id and page chrome is T5, so this panel stays chrome-free.
 */
export function PatientTimelinePanel({patientId}: PatientTimelinePanelProps) {
  const t = useTranslations('MedicalTimeline');
  const {token, tokenRevision, isReady} = useDevToken();
  const filters = useTimelineFilters();
  const query = usePatientTimeline(
    patientId,
    filters.params,
    token,
    isReady,
    tokenRevision
  );

  if (!isReady) {
    return <LoadingPanel label={t('states.loading')} />;
  }

  if (!token) {
    return (
      <div className="flex flex-col gap-4">
        <StatePanel title={t('states.tokenTitle')} description={t('states.tokenDescription')} />
        <DevTokenPanel />
      </div>
    );
  }

  if (!isGuid(patientId)) {
    return <TimelineFailure kind="notFound" />;
  }

  if (query.isPending) {
    return <LoadingPanel label={t('states.loading')} />;
  }

  if (query.isError) {
    return (
      <TimelineFailure
        kind={toQueryFailureKind(query.error)}
        onRetry={() => {
          void query.refetch();
        }}
      />
    );
  }

  if (!query.data) {
    return <LoadingPanel label={t('states.loading')} />;
  }

  const {items, page, totalPages} = query.data;
  const isRefreshing = query.isFetching;

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-sm font-bold">{t('title')}</h2>
        {isRefreshing ? (
          <span role="status" className="text-xs text-muted">
            {t('states.refreshing')}
          </span>
        ) : null}
      </div>

      <TimelineFilters controller={filters} />

      {items.length === 0 ? (
        <StatePanel
          title={
            filters.hasActiveFilters ? t('list.emptyFilteredTitle') : t('list.emptyTitle')
          }
          description={
            filters.hasActiveFilters
              ? t('list.emptyFilteredDescription')
              : t('list.emptyDescription')
          }
        />
      ) : (
        <>
          <TimelineEventList events={items} />
          <TimelinePagination
            pageNumber={page}
            totalPages={totalPages}
            onChange={filters.setPageNumber}
            disabled={isRefreshing}
          />
        </>
      )}
    </div>
  );
}
