import type {Metadata} from 'next';
import {Suspense} from 'react';
import {getTranslations} from 'next-intl/server';
import {LoginForm} from '@/features/auth';

type LoginPageProps = {
  params: Promise<{locale: string}>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export async function generateMetadata({params}: LoginPageProps): Promise<Metadata> {
  const {locale} = await params;
  const t = await getTranslations({locale, namespace: 'Auth.meta'});

  return {title: t('loginTitle'), robots: {index: false, follow: false}};
}

async function LoginContent({
  searchParams
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const query = await searchParams;
  const next = typeof query.next === 'string' ? query.next : null;
  const expired = query.reason === 'expired';

  return <LoginForm next={next} expired={expired} />;
}

export default function LoginPage({searchParams}: LoginPageProps) {
  return (
    <Suspense fallback={null}>
      <LoginContent searchParams={searchParams} />
    </Suspense>
  );
}
