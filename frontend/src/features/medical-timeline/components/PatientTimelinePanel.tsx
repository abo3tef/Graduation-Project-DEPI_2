'use client';

import {useTranslations} from 'next-intl';
import {useActingIdentity} from '@/features/auth';
import {
  LoadingPanel,
  StatePanel,
  isGuid,
  toQueryFailureKind
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
 * The patient medical timeline (T3): identity gate, filters, event list and
 * pagination over `usePatientTimeline`. The acting identity comes from the signed-in
 * session (F6-C2); the credential is attached by the shared API transport, so this
 * panel never handles a token. The route/screen that provides the patient id and page
 * chrome is T5, so this panel stays chrome-free.
 */
export function PatientTimelinePanel({patientId}: PatientTimelinePanelProps) {
  const t = useTranslations('MedicalTimeline');
  const identity = useActingIdentity();
  const filters = useTimelineFilters();
  const query = usePatientTimeline(patientId, filters.params);

  if (identity.status === 'loading') {
    return <LoadingPanel label={t('states.loading')} />;
  }

  // The medical-records layout already requires the Doctor role; this is the
  // defensive state for an anonymous render (e.g. a cleared session).
  if (identity.status === 'none') {
    return <TimelineFailure kind="unauthorized" />;
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
