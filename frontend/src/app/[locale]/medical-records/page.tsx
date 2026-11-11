import type {Metadata} from 'next';
import {getTranslations} from 'next-intl/server';
import {MedicalRecordsWorkspace} from '@/features/medical-records';

type MedicalRecordsPageProps = {
  params: Promise<{locale: string}>;
};

export async function generateMetadata({
  params
}: MedicalRecordsPageProps): Promise<Metadata> {
  const {locale} = await params;
  const t = await getTranslations({locale, namespace: 'MedicalRecords.meta'});

  return {
    title: t('title'),
    description: t('description')
  };
}

export default function MedicalRecordsPage() {
  return <MedicalRecordsWorkspace />;
}
