import {registerAuthHeadersProvider, registerUnauthorizedHandler} from '@/lib/api';
import {clearSession, getSessionState} from './session-store';

/**
 * Publishes the signed-in session to the shared API transport (F6-C2, decision Q-13).
 *
 * This is the ONE place where the credential is attached to requests. The provider is read on
 * every request, so signing in or out is picked up immediately without re-registering.
 */
export function sessionAuthHeadersProvider(): Record<string, string> {
  const state = getSessionState();

  return state.status === 'authenticated' && state.session !== null
    ? {Authorization: `Bearer ${state.session.accessToken}`}
    : {};
}

/**
 * A `401` means the stored token is no longer accepted. Only a request that actually carried a
 * session can expire one, so a failed login attempt never triggers a sign-out or a redirect.
 */
export function handleUnauthorized(): void {
  if (getSessionState().status === 'authenticated') {
    clearSession('expired');
  }
}

/**
 * Registers the session with the API transport. Idempotent: both contributors are stable function
 * references, so calling this more than once keeps a single entry in each registry.
 */
export function registerSessionAuth(): void {
  registerAuthHeadersProvider(sessionAuthHeadersProvider);
  registerUnauthorizedHandler(handleUnauthorized);
}
