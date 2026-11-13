import type {Metadata} from 'next';
import {Suspense} from 'react';
import {getTranslations} from 'next-intl/server';
import {MedicalVisitScreen} from '@/features/medical-records';

type VisitPageProps = {
  params: Promise<{locale: string; visitId: string}>;
};

export async function generateMetadata({
  params
}: VisitPageProps): Promise<Metadata> {
  const {locale} = await params;
  const t = await getTranslations({locale, namespace: 'MedicalRecords.visit'});

  return {
    title: t('title')
  };
}

export default function VisitPage({params}: VisitPageProps) {
  // `params` is runtime data for a dynamic segment, so it is awaited inside a Suspense
  // boundary as required by `cacheComponents: true`.
  return (
    <Suspense
      fallback={
        <div
          aria-busy="true"
          className="h-48 animate-pulse rounded-2xl border border-border bg-surface"
        />
      }
    >
      <VisitPageContent params={params} />
    </Suspense>
  );
}

type VisitPageContentProps = {
  params: Promise<{visitId: string}>;
};

async function VisitPageContent({params}: VisitPageContentProps) {
  const {visitId} = await params;

  return <MedicalVisitScreen visitId={visitId} />;
}
