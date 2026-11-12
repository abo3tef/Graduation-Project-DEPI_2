'use client';

import {useSyncExternalStore} from 'react';
import {
  getDevIdentity,
  isDevIdentityEnabled,
  subscribeDevIdentity,
  type DevRole
} from '../dev-identity/dev-identity';

export type ActingRole = DevRole;

export type ActingIdentity =
  /** Not known yet (server render / first client render). */
  | {status: 'loading'; userId: null; role: null}
  /** No identity available: the screens must not call the API. */
  | {status: 'none'; userId: null; role: null}
  | {status: 'ready'; userId: string; role: ActingRole};

const LOADING: ActingIdentity = {status: 'loading', userId: null, role: null};
const NONE: ActingIdentity = {status: 'none', userId: null, role: null};

const subscribeNothing = () => () => undefined;

/**
 * Who is acting. Today this is the temporary development identity; when real
 * authentication arrives only this hook changes, not the screens.
 */
export function useActingIdentity(): ActingIdentity {
  const hydrated = useSyncExternalStore(
    subscribeNothing,
    () => true,
    () => false
  );
  const identity = useSyncExternalStore(
    subscribeDevIdentity,
    getDevIdentity,
    () => null
  );

  if (!hydrated) {
    return LOADING;
  }

  if (!isDevIdentityEnabled() || identity === null) {
    return NONE;
  }

  return identityToState(identity.userId, identity.role);
}

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
