'use client';

import {ToastContainer} from 'react-toastify';
import {type ReactNode} from 'react';
import 'react-toastify/dist/ReactToastify.css';
import {QueryProvider} from '@/lib/query-client/query-provider';
import type {Locale} from '@/i18n/routing';

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
