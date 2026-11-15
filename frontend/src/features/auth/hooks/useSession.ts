'use client';

import {useSyncExternalStore} from 'react';
import {
  getServerSessionState,
  getSessionState,
  subscribeSession,
  type AuthSession,
  type SessionUser,
  type SessionUserRole
} from '../session/session-store';

export type SessionView = {
  readonly status: 'loading' | 'anonymous' | 'authenticated';
  readonly session: AuthSession | null;
  readonly user: SessionUser | null;
  readonly role: SessionUserRole | null;
  readonly isDoctor: boolean;
  readonly isSecretary: boolean;
  readonly isAdmin: boolean;
};

const LOADING_VIEW: SessionView = {
  status: 'loading',
  session: null,
  user: null,
  role: null,
  isDoctor: false,
  isSecretary: false,
  isAdmin: false
};

const ANONYMOUS_VIEW: SessionView = {
  status: 'anonymous',
  session: null,
  user: null,
  role: null,
  isDoctor: false,
  isSecretary: false,
  isAdmin: false
};

// One stable view per stored session, so consumers can use it in dependency arrays.
const views = new WeakMap<AuthSession, SessionView>();

function viewFor(session: AuthSession): SessionView {
  const cached = views.get(session);

  if (cached) {
    return cached;
  }

  const view: SessionView = {
    status: 'authenticated',
    session,
    user: session.user,
    role: session.user.role,
    isDoctor: session.user.role === 'Doctor',
    isSecretary: session.user.role === 'Secretary',
    isAdmin: session.user.role === 'Admin'
  };

  views.set(session, view);

  return view;
}

/**
 * Whatever the UI needs to know about the signed-in user. `loading` is the server render and the
 * first client render; screens must not call the API until it resolves (AGENTS.md §5: the server
 * is still the enforcement point).
 */
export function useSession(): SessionView {
  const state = useSyncExternalStore(
    subscribeSession,
    getSessionState,
    getServerSessionState
  );

  if (state.status === 'authenticated' && state.session) {
    return viewFor(state.session);
  }

  return state.status === 'anonymous' ? ANONYMOUS_VIEW : LOADING_VIEW;
}
