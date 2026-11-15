'use client';

import {Suspense, useEffect, type ReactNode} from 'react';
import {FiLock, FiUserX} from 'react-icons/fi';
import {useTranslations} from 'next-intl';
import {Link, usePathname, useRouter} from '@/i18n/navigation';
import {useSession} from '../hooks/useSession';
import {landingPathFor} from '../routing/landing';
import type {SessionUserRole} from '../session/session-store';

type RequireRoleProps = {
  /** Roles that may open this area. */
  roles: readonly SessionUserRole[];
  children: ReactNode;
};

function RequireRolePlaceholder() {
  const t = useTranslations('Auth.guard');

  return (
    <GuardPanel
      icon={<FiLock aria-hidden className="size-6" />}
      title={t('signedOutTitle')}
      message={t('loading')}
      tone="neutral"
    />
  );
}

function RequireRoleContent({roles, children}: RequireRoleProps) {
  const session = useSession();
  const router = useRouter();
  const pathname = usePathname();
  const t = useTranslations('Auth.guard');
  const allowed = session.role !== null && roles.includes(session.role);

  useEffect(() => {
    if (session.status === 'anonymous') {
      router.replace(`/login?next=${encodeURIComponent(pathname)}`);
    }
  }, [session.status, pathname, router]);

  if (session.status === 'loading' || session.status === 'anonymous') {
    return (
      <GuardPanel
        icon={<FiLock aria-hidden className="size-6" />}
        title={t('signedOutTitle')}
        message={session.status === 'loading' ? t('loading') : t('signedOut')}
        tone="neutral"
      />
    );
  }

  if (!allowed) {
    return (
      <GuardPanel
        icon={<FiUserX aria-hidden className="size-6" />}
        title={t('forbiddenTitle')}
        message={t('forbidden')}
        tone="warning"
        action={
          <Link
            href={landingPathFor(session.role)}
            className="inline-flex min-h-9 items-center rounded-full bg-brand px-4 text-xs font-bold text-[#06211f] transition hover:opacity-90"
          >
            {t('goHome')}
          </Link>
        }
      />
    );
  }

  return <>{children}</>;
}

/**
 * UX-only protection for a signed-in area (AGENTS.md §5: the API still enforces access).
 *
 * - `loading`: renders a neutral placeholder, never the content.
 * - anonymous: redirects to `/login?next=<current path>` and keeps the placeholder until it lands.
 * - signed in with the wrong role: explains it and links to the role's own landing page.
 */
export function RequireRole(props: RequireRoleProps) {
  return (
    <Suspense fallback={<RequireRolePlaceholder />}>
      <RequireRoleContent {...props} />
    </Suspense>
  );
}

type GuardPanelProps = {
  icon: ReactNode;
  title: string;
  message: string;
  tone: 'neutral' | 'warning';
  action?: ReactNode;
};

function GuardPanel({icon, title, message, tone, action}: GuardPanelProps) {
  return (
    <section
      aria-live="polite"
      className="mx-auto flex w-[min(680px,calc(100%-32px))] flex-col items-center gap-3 py-16 text-center"
    >
      <span
        className={
          tone === 'warning'
            ? 'grid size-12 place-items-center rounded-2xl bg-danger/10 text-danger'
            : 'grid size-12 place-items-center rounded-2xl bg-brand-soft text-brand'
        }
      >
        {icon}
      </span>
      <h1 className="text-lg font-bold text-foreground">{title}</h1>
      <p className="text-sm text-muted">{message}</p>
      {action}
    </section>
  );
}
