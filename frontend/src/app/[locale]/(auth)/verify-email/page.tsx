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
  // The address in the link only prefills the resend form; it never proves account ownership.
  const email = typeof query.email === 'string' ? query.email : null;

  return <VerifyEmailScreen token={token} email={email} />;
}

export default function VerifyEmailPage({searchParams}: VerifyEmailPageProps) {
  return (
    <Suspense fallback={null}>
      <VerifyEmailContent searchParams={searchParams} />
    </Suspense>
  );
}
