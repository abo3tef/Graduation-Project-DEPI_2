import type {Metadata} from 'next';
import {getTranslations} from 'next-intl/server';
import {EditPatientScreen} from '@/features/patients';

type PageProps = {
  params: Promise<{locale: string; id: string}>;
};

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('Patients.meta');

  return {title: t('editTitle')};
}

export default async function EditPatientRoute({params}: PageProps) {
  const {id} = await params;

  return <EditPatientScreen patientId={id} />;
}
