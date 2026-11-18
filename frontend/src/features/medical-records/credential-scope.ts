import {getSessionState, subscribeSession} from '@/features/auth/session';

// Opaque tab-local generations. No token or token-derived query keys.
let scope = 0;
let previousSession = getSessionState().session;

export function readCredentialScope(): number {
  const session = getSessionState().session;
  if (session !== previousSession) {
    previousSession = session;
    scope += 1;
  }
  return scope;
}

// Observe logout/account changes even while medical screens are unmounted.
subscribeSession(() => {readCredentialScope();});
