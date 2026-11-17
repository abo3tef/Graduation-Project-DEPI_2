import type {Metadata} from 'next';
import {getTranslations} from 'next-intl/server';
import {NewPatientScreen} from '@/features/patients';

type PageProps = {
  params: Promise<{locale: string}>;
};

export async function generateMetadata({params}: PageProps): Promise<Metadata> {
  const {locale} = await params;
  const t = await getTranslations({locale, namespace: 'Patients.meta'});

  return {title: t('newTitle')};
}

export default function NewPatientRoute() {
  return <NewPatientScreen />;
}
