'use client';

import {FiArrowLeft, FiRefreshCw} from 'react-icons/fi';
import {useTranslations} from 'next-intl';
import {Link} from '@/i18n/navigation';
import {ApiError} from '@/lib/api';
import {toQueryFailureKind, type QueryFailureKind} from '../errors';
import {useMedicalVisit} from '../hooks/use-medical-visit';
import {isGuid} from '../validation';
import {LoadingPanel, StatePanel} from './AsyncState';
import {DiagnosisSection} from './DiagnosisSection';
import {MedicationSection} from './MedicationSection';
import {VisitDetails} from './VisitDetails';
import {VitalSignsSection} from './VitalSignsSection';

type MedicalVisitScreenProps = {
  visitId: string;
};

/**
 * Visit detail screen. It handles GUID validation, loading and
 * `401/403/404/network/server` failure states, then hands the loaded visit to `VisitDetails`.
 */
export function MedicalVisitScreen({visitId}: MedicalVisitScreenProps) {
  const t = useTranslations('MedicalRecords.states');
  const validId = isGuid(visitId);
  const query = useMedicalVisit(visitId);

  if (!validId) {
    return <QueryFailure kind="notFound" />;
  }

  if (query.isPending || query.isLoading) {
    return <LoadingPanel label={t('loading')} />;
  }

  const failureKind = query.isError ? toQueryFailureKind(query.error) : null;
  // Retain only this credential's loaded visit for transient background failures.
  // Authorization, not-found and initial-load failures still replace the protected screen.
  const keepLoadedVisit = Boolean(query.data) && query.error instanceof ApiError &&
    (query.error.kind === 'network' || (query.error.status >= 500 && query.error.status <= 599));

  if (query.isError && !keepLoadedVisit) {
    return (
      <QueryFailure
        kind={toQueryFailureKind(query.error)}
        onRetry={() => {
          void query.refetch();
        }}
      />
    );
  }

  if (!query.data) {
    return <LoadingPanel label={t('loading')} />;
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <BackToWorkspaceLink />
        {query.isFetching ? (
          <span role="status" className="text-xs text-muted">
            {t('refreshing')}
          </span>
        ) : null}
      </div>
      {query.isError && keepLoadedVisit && failureKind ? (
        <QueryFailure kind={failureKind} showBackLink={false} onRetry={() => {void query.refetch();}} />
      ) : null}
      <VisitDetails visit={query.data} />
      <VitalSignsSection visitId={query.data.id} visitDoctorId={query.data.doctorId} />
      <DiagnosisSection visitId={query.data.id} diagnoses={query.data.diagnoses} />
      <MedicationSection
        visitId={query.data.id}
        medications={query.data.medications}
      />
    </div>
  );
}

function BackToWorkspaceLink() {
  const commonT = useTranslations('MedicalRecords.common');

  return (
    <Link
      href="/medical-records"
      className="inline-flex w-fit items-center gap-2 text-sm font-semibold text-muted transition-colors hover:text-foreground"
    >
      <FiArrowLeft aria-hidden className="size-4 rtl:rotate-180" />
      {commonT('backToWorkspace')}
    </Link>
  );
}

type QueryFailureProps = {
  kind: QueryFailureKind;
  onRetry?: () => void;
  showBackLink?: boolean;
};

function QueryFailure({kind, onRetry, showBackLink = true}: QueryFailureProps) {
  const t = useTranslations('MedicalRecords.states');
  const commonT = useTranslations('MedicalRecords.common');

  const copy = {
    unauthorized: {
      title: t('unauthorizedTitle'),
      description: t('unauthorizedDescription')
    },
    forbidden: {
      title: t('forbiddenTitle'),
      description: t('forbiddenDescription')
    },
    notFound: {
      title: t('notFoundTitle'),
      description: t('notFoundDescription')
    },
    network: {
      title: t('networkTitle'),
      description: t('networkDescription')
    },
    server: {
      title: t('serverTitle'),
      description: t('serverDescription')
    }
  } satisfies Record<QueryFailureKind, {title: string; description: string}>;

  const {title, description} = copy[kind];
  const canRetry = kind === 'network' || kind === 'server';

  return (
    <div className="flex flex-col gap-4">
      <StatePanel tone="danger" title={title} description={description}>
        {canRetry && onRetry ? (
          <button
            type="button"
            onClick={onRetry}
            className="inline-flex items-center gap-2 rounded-xl border border-danger/50 px-4 py-2 text-sm font-semibold text-danger transition-colors hover:bg-danger/10"
          >
            <FiRefreshCw aria-hidden className="size-4" />
            {t('retry')}
          </button>
        ) : null}
        {showBackLink ? <Link
          href="/medical-records"
          className="inline-flex items-center gap-2 rounded-xl border border-border px-4 py-2 text-sm font-semibold text-foreground transition-colors hover:border-brand hover:text-brand"
        >
          <FiArrowLeft aria-hidden className="size-4 rtl:rotate-180" />
          {commonT('backToWorkspace')}
        </Link> : null}
      </StatePanel>
    </div>
  );
}
