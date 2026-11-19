import type {Metadata} from 'next';
import {Suspense} from 'react';
import {getTranslations} from 'next-intl/server';
import {PatientMedicalHistoryScreen} from '@/features/medical-timeline';

type PatientMedicalHistoryPageProps = {
  params: Promise<{locale: string; patientId: string}>;
};

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('MedicalTimeline.profile');

  return {title: t('pageTitle')};
}

export default function PatientMedicalHistoryPage({
  params
}: PatientMedicalHistoryPageProps) {
  return (
    <Suspense
      fallback={
        <div
          aria-busy="true"
          className="h-48 animate-pulse rounded-2xl border border-border bg-surface"
        />
      }
    >
      <PatientMedicalHistoryPageContent params={params} />
    </Suspense>
  );
}

type PatientMedicalHistoryPageContentProps = {
  params: Promise<{patientId: string}>;
};

async function PatientMedicalHistoryPageContent({
  params
}: PatientMedicalHistoryPageContentProps) {
  const {patientId} = await params;

  return <PatientMedicalHistoryScreen patientId={patientId} />;
}
