import type {Metadata} from 'next';
import {getTranslations} from 'next-intl/server';
import {PatientFinder} from '@/features/patients';

type PatientsPageProps = {
  params: Promise<{locale: string}>;
};

export async function generateMetadata({params}: PatientsPageProps): Promise<Metadata> {
  const {locale} = await params;
  const t = await getTranslations({locale, namespace: 'Patients.meta'});

  return {
    title: t('title'),
    description: t('description')
  };
}

export default function PatientsPage() {
  return <PatientFinder />;
}
