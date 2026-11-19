import {Suspense} from 'react';
import {AppointmentsAppBar} from '@/features/appointments';
import {RequireRole} from '@/features/auth';

/**
 * Payments are a Doctor/Secretary workflow. The API authorises every request; this guard only
 * keeps the screen out of the wrong role's navigation.
 */
export default function PaymentsLayout({children}: {children: React.ReactNode}) {
  return (
    <>
      <Suspense fallback={null}>
        <AppointmentsAppBar />
      </Suspense>
      <main className="mx-auto w-[min(1180px,calc(100%-40px))] py-8">
        <Suspense fallback={null}>
          <RequireRole roles={['Doctor', 'Secretary']}>{children}</RequireRole>
        </Suspense>
      </main>
    </>
  );
}
