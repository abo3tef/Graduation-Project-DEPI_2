import type {Metadata} from 'next';
import {getTranslations} from 'next-intl/server';
import {ForgotPasswordForm} from '@/features/auth';

type ForgotPasswordPageProps = {
  params: Promise<{locale: string}>;
};

export async function generateMetadata({params}: ForgotPasswordPageProps): Promise<Metadata> {
  const {locale} = await params;
  const t = await getTranslations({locale, namespace: 'Auth.meta'});

  return {title: t('forgotPasswordTitle'), robots: {index: false, follow: false}};
}

export default function ForgotPasswordPage() {
  return <ForgotPasswordForm />;
}
