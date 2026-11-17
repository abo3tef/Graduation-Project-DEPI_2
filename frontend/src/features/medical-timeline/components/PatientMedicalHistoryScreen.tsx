'use client';

import {useTranslations} from 'next-intl';
import {FiArrowLeft} from 'react-icons/fi';
import {useActingIdentity} from '@/features/auth';
import {isGuid, LoadingPanel} from '@/features/medical-records';
import {Link} from '@/i18n/navigation';
import {PatientTimelinePanel} from './PatientTimelinePanel';
import {PatientMedicalProfilePanel} from './PatientMedicalProfilePanel';
import {TimelineFailure} from './TimelineFailure';

type PatientMedicalHistoryScreenProps = {
  patientId: string;
};

/**
 * Patient medical profile + timeline screen (T5). The signed-in session supplies the
 * identity (F6-C2); the `medical-records` layout already rejects non-doctors and
 * anonymous visitors, so the token no longer has a screen-level control (D-42).
 */
export function PatientMedicalHistoryScreen({
  patientId
}: PatientMedicalHistoryScreenProps) {
  const t = useTranslations('MedicalTimeline.profile');
  const commonT = useTranslations('MedicalRecords.common');
  const identity = useActingIdentity();

  if (identity.status === 'loading') {
    return <LoadingPanel label={t('loading')} />;
  }

  if (identity.status === 'none') {
    return <TimelineFailure kind="unauthorized" />;
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

      <PatientMedicalProfilePanel patientId={patientId} />
      <PatientTimelinePanel patientId={patientId} />
    </main>
  );
}
