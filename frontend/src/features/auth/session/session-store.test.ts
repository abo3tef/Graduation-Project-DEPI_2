import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {
  SESSION_STORAGE_KEY,
  clearSession,
  clearSignedOutReason,
  getServerSessionState,
  getSessionState,
  isSessionRole,
  resetSessionForTests,
  setSession,
  subscribeSession,
  type AuthSession
} from './session-store';

const HOUR_MS = 60 * 60 * 1000;

function makeSession(overrides: Partial<AuthSession> = {}): AuthSession {
  return {
    accessToken: 'token-value',
    expiresAtUtc: new Date(Date.now() + HOUR_MS).toISOString(),
    user: {
      id: '11111111-1111-4111-8111-111111111111',
      fullName: 'Dr. Dev One',
      email: 'doctor@medilog.test',
      role: 'Doctor',
      doctorId: '11111111-1111-4111-8111-111111111111'
    },
    ...overrides
  };
}

function readStored(): unknown {
  const raw = window.localStorage.getItem(SESSION_STORAGE_KEY);
  return raw === null ? null : JSON.parse(raw);
}

describe('session store', () => {
  beforeEach(() => {
    resetSessionForTests();
    window.localStorage.clear();
  });

  afterEach(() => {
    resetSessionForTests();
    window.localStorage.clear();
  });

  it('starts anonymous without a stored session', () => {
    expect(getSessionState()).toEqual({
      status: 'anonymous',
      session: null,
      signedOutReason: null
    });
  });

  it('reports the server snapshot as loading so SSR never renders a session', () => {
    setSession(makeSession());

    expect(getServerSessionState().status).toBe('loading');
    expect(getServerSessionState().session).toBeNull();
  });

  it('stores the session and exposes it after it is written', () => {
    const session = makeSession();

    setSession(session);

    const state = getSessionState();
    expect(state.status).toBe('authenticated');
    expect(state.session?.user.role).toBe('Doctor');
    expect(state.signedOutReason).toBeNull();
  });

  it('persists only the token, the expiry and the user summary', () => {
    setSession(makeSession());

    expect(readStored()).toEqual({
      accessToken: 'token-value',
      expiresAtUtc: expect.any(String),
      user: {
        id: '11111111-1111-4111-8111-111111111111',
        fullName: 'Dr. Dev One',
        email: 'doctor@medilog.test',
        role: 'Doctor',
        doctorId: '11111111-1111-4111-8111-111111111111'
      }
    });
  });

  it('restores a stored, still valid session on the next page load', () => {
    const session = makeSession();
    window.localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session));

    const state = getSessionState();

    expect(state.status).toBe('authenticated');
    expect(state.session?.accessToken).toBe('token-value');
  });

  it('drops an expired stored session instead of trusting it', () => {
    window.localStorage.setItem(
      SESSION_STORAGE_KEY,
      JSON.stringify(makeSession({expiresAtUtc: new Date(Date.now() - HOUR_MS).toISOString()}))
    );

    const state = getSessionState();

    expect(state).toEqual({status: 'anonymous', session: null, signedOutReason: 'expired'});
    expect(window.localStorage.getItem(SESSION_STORAGE_KEY)).toBeNull();
  });

  it('treats a token that expires within the clock-skew window as expired', () => {
    window.localStorage.setItem(
      SESSION_STORAGE_KEY,
      JSON.stringify(makeSession({expiresAtUtc: new Date(Date.now() + 1_000).toISOString()}))
    );

    expect(getSessionState().status).toBe('anonymous');
    expect(window.localStorage.getItem(SESSION_STORAGE_KEY)).toBeNull();
  });

  it.each([
    ['not JSON', 'not-json'],
    ['a JSON scalar', '42'],
    ['a record without a token', JSON.stringify({expiresAtUtc: new Date(Date.now() + HOUR_MS).toISOString(), user: makeSession().user})],
    ['a record without a user', JSON.stringify({accessToken: 't', expiresAtUtc: new Date(Date.now() + HOUR_MS).toISOString()})],
    [
      'an unknown role',
      JSON.stringify(makeSession({user: {...makeSession().user, role: 'Owner' as never}}))
    ],
    ['an unparsable expiry', JSON.stringify(makeSession({expiresAtUtc: 'tomorrow'}))]
  ])('removes a stored record with %s', (_label, raw) => {
    window.localStorage.setItem(SESSION_STORAGE_KEY, raw);

    const state = getSessionState();

    expect(state.status).toBe('anonymous');
    expect(window.localStorage.getItem(SESSION_STORAGE_KEY)).toBeNull();
  });

  it('clears the stored record and notifies subscribers on sign-out', () => {
    setSession(makeSession());
    const listener = vi.fn();
    const unsubscribe = subscribeSession(listener);

    clearSession();

    expect(getSessionState().status).toBe('anonymous');
    expect(getSessionState().signedOutReason).toBeNull();
    expect(window.localStorage.getItem(SESSION_STORAGE_KEY)).toBeNull();
    expect(listener).toHaveBeenCalledTimes(1);

    unsubscribe();
  });

  it('notifies subscribers when a session is stored', () => {
    const listener = vi.fn();
    const unsubscribe = subscribeSession(listener);

    setSession(makeSession());

    expect(listener).toHaveBeenCalledTimes(1);
    unsubscribe();
  });

  it('keeps the expiry reason until the notice has been shown', () => {
    setSession(makeSession());

    clearSession('expired');

    expect(getSessionState().signedOutReason).toBe('expired');

    clearSignedOutReason();

    expect(getSessionState().signedOutReason).toBeNull();
  });

  it('does not notify about a sign-out that changes nothing', () => {
    const listener = vi.fn();
    const unsubscribe = subscribeSession(listener);

    clearSession();

    expect(listener).not.toHaveBeenCalled();
    unsubscribe();
  });

  it('accepts only the three known roles', () => {
    expect(isSessionRole('Doctor')).toBe(true);
    expect(isSessionRole('Secretary')).toBe(true);
    expect(isSessionRole('Admin')).toBe(true);
    expect(isSessionRole('Patient')).toBe(false);
    expect(isSessionRole(undefined)).toBe(false);
  });
});
