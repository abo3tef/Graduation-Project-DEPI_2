import type {Metadata} from 'next';
import {Suspense} from 'react';
import {getTranslations} from 'next-intl/server';
import {NewVisitScreen} from '@/features/medical-records';

type NewVisitPageProps = {
  params: Promise<{locale: string}>;
};

export async function generateMetadata({
  params
}: NewVisitPageProps): Promise<Metadata> {
  const {locale} = await params;
  const t = await getTranslations({
    locale,
    namespace: 'MedicalRecords.visitForm'
  });

  return {
    title: t('createTitle'),
    description: t('createDescription')
  };
}

export default function NewVisitPage() {
  // `NewVisitScreen` reads `?patientId=` with `useSearchParams`, which needs a Suspense
  // boundary under `cacheComponents: true`.
  return (
    <Suspense
      fallback={
        <div
          aria-busy="true"
          className="h-48 animate-pulse rounded-2xl border border-border bg-surface"
        />
      }
    >
      <NewVisitScreen />
    </Suspense>
  );
}
