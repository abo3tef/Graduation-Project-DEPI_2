import type {Metadata} from 'next';
import {getTranslations} from 'next-intl/server';
import {DoctorDashboardScreen} from '@/features/dashboard';

type PageProps = {
  params: Promise<{locale: string}>;
};

export async function generateMetadata({params}: PageProps): Promise<Metadata> {
  const {locale} = await params;
  const t = await getTranslations({locale, namespace: 'dashboard.meta'});

  return {title: t('title')};
}

export default function DoctorDashboardRoute() {
  return <DoctorDashboardScreen />;
}

