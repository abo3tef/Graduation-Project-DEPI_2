'use client';

import {useEffect, useRef, useSyncExternalStore} from 'react';
import {useRouter} from '@/i18n/navigation';
import {
  clearSignedOutReason,
  getServerSessionState,
  getSessionState,
  subscribeSession
} from '../session/session-store';

/**
 * Turns an expired session into a single, explained redirect to `/login?reason=expired`.
 *
 * The API client only reports the `401`; this watcher owns the navigation so the redirect has the
 * current locale and cannot fire repeatedly while several requests fail at the same time.
 */
export function SessionExpiryWatcher() {
  const state = useSyncExternalStore(
    subscribeSession,
    getSessionState,
    getServerSessionState
  );
  const router = useRouter();
  const handled = useRef(false);

  useEffect(() => {
    if (state.status === 'authenticated') {
      handled.current = false;
      return;
    }

    if (state.signedOutReason !== 'expired' || handled.current) {
      return;
    }

    handled.current = true;
    clearSignedOutReason();
    router.replace('/login?reason=expired');
  }, [state, router]);

  return null;
}
