'use client';

import {FiActivity} from 'react-icons/fi';
import {useTranslations} from 'next-intl';
import {ThemeToggle} from '@/components/layout/ThemeToggle';
import {LocaleSwitcher} from '@/components/shared/LocaleSwitcher';
import {SessionBadge, useSession} from '@/features/auth';
import {Link, usePathname} from '@/i18n/navigation';

const NAV_ITEMS = [
  {href: '/dashboard', labelKey: 'dashboard', roles: ['Doctor']},
  {href: '/appointments', labelKey: 'appointments', roles: ['Doctor', 'Secretary']},
  {href: '/appointments/new', labelKey: 'newAppointment', roles: ['Doctor', 'Secretary']},
  {href: '/patients', labelKey: 'patients', roles: ['Doctor', 'Secretary']},
  {href: '/payments', labelKey: 'payments', roles: ['Doctor', 'Secretary']},
  {href: '/medical-records', labelKey: 'medicalRecords', roles: ['Doctor']},
  {href: '/admin', labelKey: 'admin', roles: ['Admin']}
] as const;

/**
 * Feature-local top bar for the appointment screens. The marketing header is not
 * reused (its links are page anchors); a global dashboard shell can replace this
 * later without touching the screens.
 */
export function AppointmentsAppBar() {
  const t = useTranslations('Appointments.nav');
  const navigation = useTranslations('Navigation');
  const pathname = usePathname();
  const session = useSession();
  const visibleItems = NAV_ITEMS.filter(
    (item) => session.role !== null && (item.roles as readonly string[]).includes(session.role)
  );

  return (
    <header className="sticky top-0 z-20 border-b border-border/60 bg-background/85 backdrop-blur-xl">
      <div className="mx-auto flex min-h-16 w-[min(1180px,calc(100%-40px))] flex-wrap items-center justify-between gap-x-6 gap-y-2 py-2">
        <div className="flex flex-wrap items-center gap-x-6 gap-y-2">
          <Link href="/" className="inline-flex items-center gap-2.5" aria-label={navigation('brand')}>
            <span className="grid size-9 place-items-center rounded-[10px] bg-brand text-[#06211f]">
              <FiActivity className="size-5" aria-hidden />
            </span>
            <span className="text-base font-black">{navigation('brand')}</span>
          </Link>

          <nav aria-label={t('label')} className="flex items-center gap-1">
            {visibleItems.map((item) => {
              const current =
                pathname === item.href ||
                (item.href !== '/appointments' && pathname.startsWith(`${item.href}/`));

              return (
                <Link
                  key={item.href}
                  href={item.href}
                  aria-current={current ? 'page' : undefined}
                  className={`rounded-full px-3 py-2 text-xs font-bold transition ${
                    current ? 'bg-brand-soft text-brand' : 'text-muted hover:text-foreground'
                  }`}
                >
                  {navigation(item.labelKey)}
                </Link>
              );
            })}
          </nav>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <SessionBadge />
          <ThemeToggle />
          <LocaleSwitcher />
        </div>
      </div>
    </header>
  );
}
