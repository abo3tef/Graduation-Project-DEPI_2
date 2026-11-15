'use client';

import {useTranslations} from 'next-intl';
import {FiArrowLeft} from 'react-icons/fi';
import {
  DevTokenPanel,
  isGuid,
  LoadingPanel,
  StatePanel,
  useDevToken
} from '@/features/medical-records';
import {Link} from '@/i18n/navigation';
import {PatientTimelinePanel} from './PatientTimelinePanel';
import {PatientMedicalProfilePanel} from './PatientMedicalProfilePanel';
import {TimelineFailure} from './TimelineFailure';

type PatientMedicalHistoryScreenProps = {
  patientId: string;
};

export function PatientMedicalHistoryScreen({
  patientId
}: PatientMedicalHistoryScreenProps) {
  const t = useTranslations('MedicalTimeline.profile');
  const commonT = useTranslations('MedicalRecords.common');
  const {token, tokenRevision, isReady} = useDevToken();

  if (!isReady) {
    return <LoadingPanel label={t('loading')} />;
  }

  if (!token) {
    return (
      <div className="flex flex-col gap-4">
        <StatePanel
          title={t('tokenTitle')}
          description={t('tokenDescription')}
        />
        <DevTokenPanel />
      </div>
    );
  }

  if (!isGuid(patientId)) {
    return <TimelineFailure kind="notFound" />;
  }

  return (
    <main className="flex flex-col gap-6">
      <header className="flex flex-col gap-3">
        <Link
          href="/medical-records"
          className="inline-flex w-fit items-center gap-2 text-sm font-semibold text-muted transition-colors hover:text-foreground"
        >
          <FiArrowLeft aria-hidden className="size-4 rtl:rotate-180" />
          {commonT('backToWorkspace')}
        </Link>
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.2em] text-brand">
            {t('eyebrow')}
          </p>
          <h1 className="mt-2 text-3xl font-black sm:text-4xl">{t('pageTitle')}</h1>
          <p className="mt-2 max-w-2xl text-sm leading-relaxed text-muted">
            {t('pageDescription')}
          </p>
        </div>
      </header>

      <PatientMedicalProfilePanel
        patientId={patientId}
        token={token}
        tokenRevision={tokenRevision}
      />
      <PatientTimelinePanel patientId={patientId} />
    </main>
  );
}
