import type {Metadata} from 'next';
import {getTranslations} from 'next-intl/server';
import {Suspense} from 'react';
import {AdminDashboardScreen, AdminPageFallback} from '@/features/admin';

/**
 * This route is authenticated and its data is only known at request time. Opting out of the
 * prerendered "instant" shell avoids Next.js 16 treating runtime-only URL data in the client
 * feature components as a blocking-prerender warning.
 */
export const instant = false;

type PageProps = {
  params: Promise<{locale: string}>;
};

export async function generateMetadata({params}: PageProps): Promise<Metadata> {
  const {locale} = await params;
  const t = await getTranslations({locale, namespace: 'Admin.meta'});

  return {title: t('title')};
}

export default function AdminRoute() {
  return (
    <Suspense fallback={<AdminPageFallback />}>
      <AdminDashboardScreen />
    </Suspense>
  );
}
