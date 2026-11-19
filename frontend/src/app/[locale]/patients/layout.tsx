import {RequireRole} from '@/features/auth';

/**
 * Patient management (F8) is the doctor's and the secretary's area. The guard is UX only: the API
 * still authorises every request (AGENTS.md section 5). The shared top bar of the clinic screens
 * is reused through the appointments feature's public API.
 */
export default function PatientsLayout({children}: {children: React.ReactNode}) {
  return (
    <main className="mx-auto w-[min(1180px,calc(100%-40px))] py-8">
      <RequireRole roles={['Doctor', 'Secretary']}>{children}</RequireRole>
    </main>
  );
}
