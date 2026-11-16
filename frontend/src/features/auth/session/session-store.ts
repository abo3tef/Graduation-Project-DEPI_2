/**
 * The signed-in session, kept in one place (F6-C2, decision Q-13).
 *
 * Exactly one `localStorage` record (`medilog-auth-session`) holds the access token and the
 * account summary. The module keeps an in-memory copy so reads during render are cheap and
 * SSR-safe, and so a blocked storage API still leaves a working session for the current tab.
 *
 * A stored record that is expired or cannot be parsed is removed on read and never trusted.
 * Nothing here logs or renders the token.
 */
export const SESSION_STORAGE_KEY = 'medilog-auth-session';

/** Treat a token that expires within this window as already expired, absorbing clock drift. */
const EXPIRY_SKEW_MS = 5_000;

export const SESSION_ROLES = ['Doctor', 'Secretary', 'Admin'] as const;

export type SessionUserRole = (typeof SESSION_ROLES)[number];

export type SessionUser = {
  readonly id: string;
  readonly fullName: string;
  readonly email: string;
  readonly role: SessionUserRole;
  /** Doctors only; equal to `id` (decision D-35). */
  readonly doctorId: string | null;
};

export type AuthSession = {
  readonly accessToken: string;
  readonly expiresAtUtc: string;
  readonly user: SessionUser;
};

/** Why the session ended, so the UI can explain an unexpected sign-out (never for a manual one). */
export type SignedOutReason = 'expired' | null;

export type SessionState = {
  readonly status: 'loading' | 'anonymous' | 'authenticated';
  readonly session: AuthSession | null;
  readonly signedOutReason: SignedOutReason;
};

const LOADING_STATE: SessionState = {
  status: 'loading',
  session: null,
  signedOutReason: null
};

/** Shared, referentially stable snapshot for "no session". */
export const ANONYMOUS_SESSION_STATE: SessionState = {
  status: 'anonymous',
  session: null,
  signedOutReason: null
};

const EXPIRED_SESSION_STATE: SessionState = {
  status: 'anonymous',
  session: null,
  signedOutReason: 'expired'
};

export function isSessionRole(value: unknown): value is SessionUserRole {
  return (SESSION_ROLES as readonly unknown[]).includes(value);
}

let initialized = false;
let state: SessionState = LOADING_STATE;

const listeners = new Set<() => void>();

function emit(): void {
  for (const listener of listeners) {
    listener();
  }
}

function readRaw(): string | null {
  if (typeof window === 'undefined') {
    return null;
  }

  try {
    return window.localStorage.getItem(SESSION_STORAGE_KEY);
  } catch {
    // Blocked storage behaves like "nothing stored": the in-memory copy still works.
    return null;
  }
}

function removeRaw(): void {
  if (typeof window === 'undefined') {
    return;
  }

  try {
    window.localStorage.removeItem(SESSION_STORAGE_KEY);
  } catch {
    // Ignore: the in-memory state is what the UI reads.
  }
}

function writeRaw(session: AuthSession): void {
  if (typeof window === 'undefined') {
    return;
  }

  try {
    window.localStorage.setItem(
      SESSION_STORAGE_KEY,
      JSON.stringify({
        accessToken: session.accessToken,
        expiresAtUtc: session.expiresAtUtc,
        user: session.user
      })
    );
  } catch {
    // The tab keeps working from memory; a reload then requires signing in again.
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function parseUser(value: unknown): SessionUser | null {
  if (!isRecord(value)) {
    return null;
  }

  const {id, fullName, email, role, doctorId} = value;

  if (typeof id !== 'string' || id.trim() === '' || !isSessionRole(role)) {
    return null;
  }

  if (typeof fullName !== 'string' || typeof email !== 'string') {
    return null;
  }

  return {
    id,
    fullName,
    email,
    role,
    doctorId: typeof doctorId === 'string' && doctorId !== '' ? doctorId : null
  };
}

type ParseResult =
  | {kind: 'valid'; session: AuthSession}
  | {kind: 'invalid'}
  | {kind: 'expired'};

function parseSession(raw: string | null): ParseResult {
  if (!raw) {
    return {kind: 'invalid'};
  }

  let value: unknown;

  try {
    value = JSON.parse(raw);
  } catch {
    return {kind: 'invalid'};
  }

  if (!isRecord(value)) {
    return {kind: 'invalid'};
  }

  const accessToken =
    typeof value.accessToken === 'string' ? value.accessToken.trim() : '';
  const expiresAtUtc =
    typeof value.expiresAtUtc === 'string' ? value.expiresAtUtc : '';
  const user = parseUser(value.user);

  if (accessToken === '' || user === null) {
    return {kind: 'invalid'};
  }

  const expiresAt = Date.parse(expiresAtUtc);

  if (!Number.isFinite(expiresAt)) {
    return {kind: 'invalid'};
  }

  if (expiresAt - EXPIRY_SKEW_MS <= Date.now()) {
    return {kind: 'expired'};
  }

  return {kind: 'valid', session: {accessToken, expiresAtUtc, user}};
}

function initialize(): void {
  if (initialized) {
    return;
  }

  initialized = true;

  if (typeof window === 'undefined') {
    // Server render: no storage, the client snapshot takes over after hydration.
    return;
  }

  const raw = readRaw();
  const parsed = parseSession(raw);

  if (parsed.kind === 'valid') {
    state = {status: 'authenticated', session: parsed.session, signedOutReason: null};
    return;
  }

  if (raw !== null) {
    // Expired or corrupt: do not keep it around.
    removeRaw();
  }

  state = parsed.kind === 'expired' ? EXPIRED_SESSION_STATE : ANONYMOUS_SESSION_STATE;
}

/** Client snapshot: safe to call during render. */
export function getSessionState(): SessionState {
  initialize();
  return state;
}

/** Server snapshot: there is no storage on the server, and hydration decides the session. */
export function getServerSessionState(): SessionState {
  return LOADING_STATE;
}

export function subscribeSession(listener: () => void): () => void {
  listeners.add(listener);
  initialize();

  const onStorage = (event: StorageEvent) => {
    if (event.key !== null && event.key !== SESSION_STORAGE_KEY) {
      return;
    }

    // Another tab changed the session: re-read it instead of trusting the memory copy.
    initialized = false;
    initialize();
    emit();
  };

  if (typeof window !== 'undefined') {
    window.addEventListener('storage', onStorage);
  }

  return () => {
    listeners.delete(listener);

    if (typeof window !== 'undefined') {
      window.removeEventListener('storage', onStorage);
    }
  };
}

/** Stores the session from a successful sign-in. */
export function setSession(session: AuthSession): void {
  initialize();

  const normalized: AuthSession = {
    accessToken: session.accessToken,
    expiresAtUtc: session.expiresAtUtc,
    user: session.user
  };

  writeRaw(normalized);
  state = {status: 'authenticated', session: normalized, signedOutReason: null};
  emit();
}

/**
 * Ends the session. `reason: 'expired'` is used when the API rejected the token (401) so the
 * login screen can explain why the user was signed out; a manual sign-out passes no reason.
 */
export function clearSession(reason: SignedOutReason = null): void {
  initialize();

  const wasAnonymous = state.status === 'anonymous';

  removeRaw();

  state = reason === 'expired' ? EXPIRED_SESSION_STATE : ANONYMOUS_SESSION_STATE;

  if (!wasAnonymous || reason === 'expired') {
    emit();
  }
}

/** The expiry notice was shown; drop it so a reload does not repeat the message. */
export function clearSignedOutReason(): void {
  if (state.signedOutReason === null) {
    return;
  }

  state = ANONYMOUS_SESSION_STATE;
  emit();
}

/** Test-only: forget the in-memory state, the stored record and every subscriber. */
export function resetSessionForTests(): void {
  initialized = false;
  state = LOADING_STATE;
  removeRaw();
  listeners.clear();
}
