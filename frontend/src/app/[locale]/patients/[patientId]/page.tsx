import type {Metadata} from 'next';
import {Suspense} from 'react';
import {getTranslations} from 'next-intl/server';
import {PatientMedicalStatusScreen} from '@/features/patients';

type PatientPageProps = {
  params: Promise<{locale: string; patientId: string}>;
};

export async function generateMetadata({params}: PatientPageProps): Promise<Metadata> {
  const {locale} = await params;
  const t = await getTranslations({locale, namespace: 'Patients.meta'});

  return {
    title: t('title'),
    description: t('description')
  };
}

export default function PatientPage({params}: PatientPageProps) {
  // `params` is runtime data for a dynamic segment, so it is awaited inside a Suspense
  // boundary as required by `cacheComponents: true` (the F4 visit page pattern).
  return (
    <Suspense
      fallback={
        <div
          aria-busy="true"
          className="h-48 animate-pulse rounded-2xl border border-border bg-surface"
        />
      }
    >
      <PatientPageContent params={params} />
    </Suspense>
  );
}

type PatientPageContentProps = {
  params: Promise<{patientId: string}>;
};

async function PatientPageContent({params}: PatientPageContentProps) {
  const {patientId} = await params;

  return <PatientMedicalStatusScreen patientId={patientId} />;
}
