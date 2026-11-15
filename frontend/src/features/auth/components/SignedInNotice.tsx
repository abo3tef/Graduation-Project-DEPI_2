'use client';

import {FiArrowRight} from 'react-icons/fi';
import {useTranslations} from 'next-intl';
import {Link} from '@/i18n/navigation';
import {useSession} from '../hooks/useSession';
import {landingPathFor} from '../routing/landing';

/**
 * The home page is public, so a signed-in visitor is told where they are and gets one link to
 * their workspace. Admins have no screens until F11, so they see the scheduled notice (D-C2-1).
 */
export function SignedInNotice() {
  const t = useTranslations('Auth.home');
  const session = useSession();

  if (session.status !== 'authenticated' || !session.user || session.role === null) {
    return null;
  }

  return (
    <div className="mx-auto w-[min(1180px,calc(100%-40px))]">
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-brand/30 bg-brand-soft px-4 py-2 text-xs font-semibold text-foreground">
        <p role="status" className="m-0">
          {t('signedIn', {name: session.user.fullName})}
        </p>
        <Link
          href={landingPathFor(session.role)}
          className="inline-flex items-center gap-1 font-bold text-brand hover:underline"
        >
          {session.isAdmin ? t('backHome') : t('openWorkspace')}
          <FiArrowRight aria-hidden className="size-3 rtl:rotate-180" />
        </Link>
      </div>
      {session.isAdmin ? (
        <p className="mt-2 rounded-xl border border-border bg-surface-2 px-4 py-2 text-xs text-muted">
          {t('adminNotice')}
        </p>
      ) : null}
    </div>
  );
}
