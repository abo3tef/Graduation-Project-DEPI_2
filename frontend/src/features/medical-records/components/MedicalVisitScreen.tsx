'use client';

import {useEffect, useRef} from 'react';
import {FiArrowLeft, FiRefreshCw} from 'react-icons/fi';
import {useTranslations} from 'next-intl';
import {Link} from '@/i18n/navigation';
import {toQueryFailureKind, type QueryFailureKind} from '../errors';
import {useMedicalVisit} from '../hooks/use-medical-visit';
import {isGuid} from '../validation';
import {useDevToken} from '../use-dev-token';
import {LoadingPanel, StatePanel} from './AsyncState';
import {DevTokenPanel} from './DevTokenPanel';
import {VisitDetails} from './VisitDetails';

type MedicalVisitScreenProps = {
  visitId: string;
};

/**
 * Visit detail screen. It owns the token gate, the malformed-id short circuit and the
 * `401/403/404/network/server` states, then hands the loaded visit to `VisitDetails`.
 */
export function MedicalVisitScreen({visitId}: MedicalVisitScreenProps) {
  const t = useTranslations('MedicalRecords.states');
  const {token, isReady} = useDevToken();
  const validId = isGuid(visitId);
  const query = useMedicalVisit(visitId, token, isReady);
  const {refetch} = query;
  const previousTokenRef = useRef(token);

  // A replaced token must refetch; the initial `null → token` transition is already covered by
  // `enabled`, so only a token-to-token change triggers an extra request.
  useEffect(() => {
    if (!isReady) {
      return;
    }

    const previous = previousTokenRef.current;

    if (previous === token) {
      return;
    }

    previousTokenRef.current = token;

    if (previous !== null && token !== null) {
      void refetch();
    }
  }, [isReady, token, refetch]);

  if (!isReady) {
    return <LoadingPanel label={t('loading')} />;
  }

  if (!token) {
    return (
      <div className="flex flex-col gap-4">
        <StatePanel
          title={t('tokenTitle')}
          description={t('tokenDescription')}
        >
          <BackToWorkspaceLink />
        </StatePanel>
        <DevTokenPanel />
      </div>
    );
  }

  if (!validId) {
    return <QueryFailure kind="notFound" />;
  }

  if (query.isPending || query.isLoading) {
    return <LoadingPanel label={t('loading')} />;
  }

  if (query.isError) {
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
      <VisitDetails visit={query.data} />
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
};

function QueryFailure({kind, onRetry}: QueryFailureProps) {
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
        <Link
          href="/medical-records"
          className="inline-flex items-center gap-2 rounded-xl border border-border px-4 py-2 text-sm font-semibold text-foreground transition-colors hover:border-brand hover:text-brand"
        >
          <FiArrowLeft aria-hidden className="size-4 rtl:rotate-180" />
          {commonT('backToWorkspace')}
        </Link>
      </StatePanel>

      {kind === 'unauthorized' ? <DevTokenPanel /> : null}
    </div>
  );
}
