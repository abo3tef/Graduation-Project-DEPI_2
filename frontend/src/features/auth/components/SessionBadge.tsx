'use client';

import {FiLogOut, FiUser} from 'react-icons/fi';
import {useTranslations} from 'next-intl';
import {Link} from '@/i18n/navigation';
import {useSession} from '../hooks/useSession';
import {useSignOut} from '../hooks/useSignOut';

/**
 * Shows who is signed in, with a sign-out control. Replaces the temporary development identity
 * chip on every surface that used to render it (D-C2-3).
 */
export function SessionBadge() {
  const t = useTranslations('Auth.session');
  const session = useSession();
  const signOut = useSignOut();

  if (session.status === 'loading') {
    return (
      <span
        aria-hidden
        className="inline-block h-9 w-28 animate-pulse rounded-full bg-surface-2"
      />
    );
  }

  if (session.status === 'anonymous' || !session.user) {
    return (
      <Link
        href="/login"
        className="inline-flex min-h-9 items-center gap-2 rounded-full border border-brand/50 bg-brand-soft px-3.5 text-xs font-bold text-brand transition hover:border-brand"
      >
        <FiUser aria-hidden className="size-3.5" />
        {t('signIn')}
      </Link>
    );
  }

  const user = session.user;

  return (
    <span className="inline-flex min-h-9 items-center gap-2 rounded-full border border-border bg-surface px-2 ps-3 text-xs font-semibold text-foreground">
      <span className="flex min-w-0 flex-col leading-tight">
        <span className="max-w-32 truncate" title={user.fullName}>
          {user.fullName}
        </span>
        <span className="text-[10px] font-bold uppercase tracking-wide text-muted">
          {t(`role.${user.role}`)}
        </span>
      </span>
      <button
        type="button"
        onClick={signOut}
        title={t('signOut')}
        aria-label={t('signOut')}
        className="grid size-7 place-items-center rounded-full text-muted transition-colors hover:bg-danger/10 hover:text-danger"
      >
        <FiLogOut aria-hidden className="size-3.5" />
      </button>
    </span>
  );
}
