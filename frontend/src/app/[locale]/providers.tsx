'use client';

import {ToastContainer} from 'react-toastify';
import {type ReactNode} from 'react';
import 'react-toastify/dist/ReactToastify.css';
import {QueryProvider} from '@/lib/query-client/query-provider';
import {registerSessionAuth, SessionExpiryWatcher} from '@/features/auth';
import type {Locale} from '@/i18n/routing';

// The session registers its bearer provider and 401 handler at module load, so it is in place
// before any child effect issues a request (F6-C2, Q-13). Idempotent.
registerSessionAuth();

type ProvidersProps = {
  children: ReactNode;
  locale: Locale;
};

export function Providers({children, locale}: ProvidersProps) {
  const isRtl = locale === 'ar';

  return (
    <QueryProvider>
      <SessionExpiryWatcher />
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
