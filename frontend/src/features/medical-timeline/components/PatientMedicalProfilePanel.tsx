'use client';

import {useTranslations} from 'next-intl';
import {LoadingPanel} from '@/features/medical-records';
import {toQueryFailureKind} from '@/features/medical-records';
import {useMedicalProfile} from '../hooks/use-medical-profile';
import {PatientMedicalProfileSummary} from './PatientMedicalProfileSummary';
import {TimelineFailure} from './TimelineFailure';

type PatientMedicalProfilePanelProps = {
  patientId: string;
  token: string;
  tokenRevision: number;
};

export function PatientMedicalProfilePanel({
  patientId,
  token,
  tokenRevision
}: PatientMedicalProfilePanelProps) {
  const t = useTranslations('MedicalTimeline.profile');
  const query = useMedicalProfile(patientId, token, true, tokenRevision);

  if (query.isPending || query.isLoading) {
    return <LoadingPanel label={t('loading')} />;
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
    return <LoadingPanel label={t('loading')} />;
  }

  return (
    <div className="flex flex-col gap-3">
      {query.isFetching ? (
        <span role="status" className="text-xs text-muted">
          {t('refreshing')}
        </span>
      ) : null}
      <PatientMedicalProfileSummary profile={query.data} />
    </div>
  );
}
