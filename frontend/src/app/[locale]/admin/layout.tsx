import {RequireRole} from '@/features/auth';

/**
 * Account administration is an Admin-only area. The backend authorises every Admin request;
 * this guard is the matching UX boundary.
 */
export default function AdminLayout({children}: {children: React.ReactNode}) {
  return (
    <RequireRole roles={['Admin']}>{children}</RequireRole>
  );
}
