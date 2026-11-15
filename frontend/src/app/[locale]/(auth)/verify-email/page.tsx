import type {Metadata} from 'next';
import {Suspense} from 'react';
import {getTranslations} from 'next-intl/server';
import {VerifyEmailScreen} from '@/features/auth';

type VerifyEmailPageProps = {
  params: Promise<{locale: string}>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export async function generateMetadata({params}: VerifyEmailPageProps): Promise<Metadata> {
  const {locale} = await params;
  const t = await getTranslations({locale, namespace: 'Auth.meta'});

  return {title: t('verifyEmailTitle'), robots: {index: false, follow: false}};
}

async function VerifyEmailContent({
  searchParams
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const query = await searchParams;
  const token = typeof query.token === 'string' ? query.token : null;

  return <VerifyEmailScreen token={token} />;
}

export default function VerifyEmailPage({searchParams}: VerifyEmailPageProps) {
  return (
    <Suspense fallback={null}>
      <VerifyEmailContent searchParams={searchParams} />
    </Suspense>
  );
}
