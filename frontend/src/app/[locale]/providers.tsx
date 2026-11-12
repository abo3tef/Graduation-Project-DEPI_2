'use client';

import {ToastContainer} from 'react-toastify';
import {type ReactNode} from 'react';
import 'react-toastify/dist/ReactToastify.css';
import {QueryProvider} from '@/lib/query-client/query-provider';
import {registerDevAuthHeaders} from '@/features/auth';
import type {Locale} from '@/i18n/routing';

// TODO (Q-07): temporary development identity for the API client. Registered at
// module load so it is in place before any child effect issues a request, and it
// is a no-op unless NEXT_PUBLIC_DEV_IDENTITY=true in a non-production build.
// Replace with the real authentication provider when it is delivered.
registerDevAuthHeaders();

type ProvidersProps = {
  children: ReactNode;
  locale: Locale;
};

export function Providers({children, locale}: ProvidersProps) {
  const isRtl = locale === 'ar';

  return (
    <QueryProvider>
      {children}
      <ToastContainer
        position={isRtl ? 'top-left' : 'top-right'}
        rtl={isRtl}
        theme="light"
        autoClose={4000}
        closeOnClick
        pauseOnHover
      />
    </QueryProvider>
  );
}
