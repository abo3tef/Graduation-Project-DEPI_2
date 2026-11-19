import {RequireRole} from '@/features/auth';

/**
 * Payments are a Doctor/Secretary workflow. The API authorises every request; this guard only
 * keeps the screen out of the wrong role's navigation.
 */
export default function PaymentsLayout({children}: {children: React.ReactNode}) {
  return (
    <main className="mx-auto w-[min(1180px,calc(100%-40px))] py-8">
      <RequireRole roles={['Doctor', 'Secretary']}>{children}</RequireRole>
    </main>
  );
}
