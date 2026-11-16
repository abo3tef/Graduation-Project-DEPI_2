import type {Metadata} from 'next';
import {getTranslations} from 'next-intl/server';
import {RegisterForm} from '@/features/auth';

type RegisterPageProps = {
  params: Promise<{locale: string}>;
};

export async function generateMetadata({params}: RegisterPageProps): Promise<Metadata> {
  const {locale} = await params;
  const t = await getTranslations({locale, namespace: 'Auth.meta'});

  return {title: t('registerTitle'), robots: {index: false, follow: false}};
}

export default function RegisterPage() {
  return <RegisterForm />;
}
