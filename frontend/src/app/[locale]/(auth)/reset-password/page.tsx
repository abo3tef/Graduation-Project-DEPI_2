import type {Metadata} from 'next';
import {Suspense} from 'react';
import {getTranslations} from 'next-intl/server';
import {ResetPasswordForm} from '@/features/auth';

type ResetPasswordPageProps = {
  params: Promise<{locale: string}>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export async function generateMetadata({params}: ResetPasswordPageProps): Promise<Metadata> {
  const {locale} = await params;
  const t = await getTranslations({locale, namespace: 'Auth.meta'});

  return {title: t('resetPasswordTitle'), robots: {index: false, follow: false}};
}

async function ResetPasswordContent({
  searchParams
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const query = await searchParams;
  const token = typeof query.token === 'string' ? query.token : null;

  return <ResetPasswordForm token={token} />;
}

export default function ResetPasswordPage({searchParams}: ResetPasswordPageProps) {
  return (
    <Suspense fallback={null}>
      <ResetPasswordContent searchParams={searchParams} />
    </Suspense>
  );
}
