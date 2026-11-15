'use client';

import {useSyncExternalStore} from 'react';
import {
  getServerSessionState,
  getSessionState,
  subscribeSession,
  type SessionUserRole
} from '../session/session-store';

export type ActingRole = SessionUserRole;

export type ActingIdentity =
  /** Not known yet (server render / first client render). */
  | {status: 'loading'; userId: null; role: null}
  /** Nobody is signed in: the screens must not call the API. */
  | {status: 'none'; userId: null; role: null}
  | {status: 'ready'; userId: string; role: ActingRole};

const LOADING: ActingIdentity = {status: 'loading', userId: null, role: null};
const NONE: ActingIdentity = {status: 'none', userId: null, role: null};

const readyCache = new Map<string, ActingIdentity>();

// Stable object per identity so consumers can use it in dependency arrays.
function identityToState(userId: string, role: ActingRole): ActingIdentity {
  const key = `${role}:${userId}`;
  let state = readyCache.get(key);

  if (!state) {
    state = {status: 'ready', userId, role};
    readyCache.set(key, state);
  }

  return state;
}

/**
 * Who is acting, derived from the signed-in session. Kept as the single identity source of the
 * existing feature hooks (`useActor`, `useDoctorDashboard`) so those screens did not have to
 * change when real authentication replaced the development identity.
 */
export function useActingIdentity(): ActingIdentity {
  const state = useSyncExternalStore(
    subscribeSession,
    getSessionState,
    getServerSessionState
  );

  if (state.status === 'authenticated' && state.session) {
    return identityToState(state.session.user.id, state.session.user.role);
  }

  return state.status === 'anonymous' ? NONE : LOADING;
}
