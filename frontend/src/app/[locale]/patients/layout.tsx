import {Suspense} from 'react';
import {RequireRole} from '@/features/auth';
import {AppointmentsAppBar} from '@/features/appointments';

/**
 * Patient management (F8) is the doctor's and the secretary's area. The guard is UX only: the API
 * still authorises every request (AGENTS.md section 5). The shared top bar of the clinic screens
 * is reused through the appointments feature's public API.
 */
export default function PatientsLayout({children}: {children: React.ReactNode}) {
  return (
    <>
      {/* The bar reads the pathname and the page may read route params: both stream in. */}
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
