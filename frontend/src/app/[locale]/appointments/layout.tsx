import {RequireRole} from '@/features/auth';
import {AppointmentsAppBar} from '@/features/appointments';

/**
 * Appointment and booking management is the secretary's and the doctor's area (F1). The guard is
 * UX only: the API still authorises every request (AGENTS.md §5).
 */
export default function AppointmentsLayout({children}: {children: React.ReactNode}) {
  return (
    <>
      <AppointmentsAppBar />
      <main className="mx-auto w-[min(1180px,calc(100%-40px))] py-8">
        <RequireRole roles={['Doctor', 'Secretary']}>{children}</RequireRole>
      </main>
    </>
  );
}
