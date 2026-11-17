'use client';

import {FiRefreshCw} from 'react-icons/fi';
import {useTranslations} from 'next-intl';
import {StatePanel, type QueryFailureKind} from '@/features/medical-records';

type TimelineFailureProps = {
  kind: QueryFailureKind;
  onRetry?: () => void;
};

/**
 * Failure states for a timeline/profile request. The kind mapping is shared
 * with the medical-records feature; the copy is patient-flavored (a timeline
 * `404` means the patient does not exist or belongs to another doctor).
 *
 * A rejected credential is handled by the auth feature: the API transport reports
 * the `401` to the session owner, which signs the user out and returns to `/login`
 * (F6-C2). No screen-level token input exists any more (D-42).
 */
export function TimelineFailure({kind, onRetry}: TimelineFailureProps) {
  const t = useTranslations('MedicalTimeline.states');

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
    </StatePanel>
  );
}
