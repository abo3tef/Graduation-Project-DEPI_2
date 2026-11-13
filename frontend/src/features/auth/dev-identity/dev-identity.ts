import {registerAuthHeadersProvider, type AuthHeaders} from '@/lib/api';

/**
 * TEMPORARY development identity (STATE.md Q-07). Real authentication is not
 * implemented yet, so the API identifies the caller from two request headers
 * that only a Development server accepts. Remove this module (and its use in
 * the app providers) when authentication is delivered.
 */
export const DEV_ROLES = ['Secretary', 'Doctor'] as const;
export type DevRole = (typeof DEV_ROLES)[number];

export type DevIdentity = {
  readonly userId: string;
  readonly role: DevRole;
};

export const DEV_IDENTITY_STORAGE_KEY = 'medilog-dev-identity';

const GUID_PATTERN =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function isGuid(value: string): boolean {
  return GUID_PATTERN.test(value);
}

export function isDevRole(value: unknown): value is DevRole {
  return (DEV_ROLES as readonly unknown[]).includes(value);
}

/**
 * The identity headers are active only in non-production builds and only when
 * `NEXT_PUBLIC_DEV_IDENTITY=true`. (The API additionally refuses them outside
 * Development.) Literal `process.env` reads let Next.js inline the values.
 */
export function isDevIdentityEnabled(): boolean {
  return (
    process.env.NODE_ENV !== 'production' &&
    process.env.NEXT_PUBLIC_DEV_IDENTITY === 'true'
  );
}

function parseIdentity(raw: string | null): DevIdentity | null {
  if (!raw) {
    return null;
  }

  try {
    const value: unknown = JSON.parse(raw);

    if (typeof value === 'object' && value !== null) {
      const {userId, role} = value as {userId?: unknown; role?: unknown};

      if (typeof userId === 'string' && isGuid(userId) && isDevRole(role)) {
        return {userId: userId.toLowerCase(), role};
      }
    }
  } catch {
    // Corrupt storage is treated as "no identity".
  }

  return null;
}

function readEnvDefault(): DevIdentity | null {
  const userId = process.env.NEXT_PUBLIC_DEV_USER_ID;
  const role = process.env.NEXT_PUBLIC_DEV_USER_ROLE;

  return userId && isGuid(userId) && isDevRole(role)
    ? {userId: userId.toLowerCase(), role}
    : null;
}

function readStorage(): string | null {
  try {
    return typeof window === 'undefined'
      ? null
      : window.localStorage.getItem(DEV_IDENTITY_STORAGE_KEY);
  } catch {
    return null;
  }
}

let cachedRaw: string | null | undefined;
let cachedIdentity: DevIdentity | null = null;

/**
 * Current identity: the stored one, else the env default, else `null`.
 * The returned object is referentially stable while nothing changes, so it is
 * safe to use as a `useSyncExternalStore` snapshot.
 */
export function getDevIdentity(): DevIdentity | null {
  const raw = readStorage();
  const key = raw ?? '';

  if (cachedRaw !== key) {
    cachedRaw = key;
    cachedIdentity = parseIdentity(raw) ?? readEnvDefault();
  }

  return cachedIdentity;
}

const listeners = new Set<() => void>();

function notify(): void {
  cachedRaw = undefined;
  listeners.forEach((listener) => listener());
}

/** Stores a new identity (or clears it with `null`) and notifies subscribers. */
export function setDevIdentity(identity: DevIdentity | null): void {
  try {
    if (identity === null) {
      window.localStorage.removeItem(DEV_IDENTITY_STORAGE_KEY);
    } else {
      window.localStorage.setItem(
        DEV_IDENTITY_STORAGE_KEY,
        JSON.stringify({userId: identity.userId.toLowerCase(), role: identity.role})
      );
    }
  } catch {
    // Storage may be unavailable (private mode); the identity then stays unset.
  }

  notify();
}

export function subscribeDevIdentity(listener: () => void): () => void {
  listeners.add(listener);

  const onStorage = (event: StorageEvent) => {
    if (event.key === null || event.key === DEV_IDENTITY_STORAGE_KEY) {
      notify();
    }
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

/** The headers the API expects from the temporary identity; empty when inactive. */
export function devAuthHeadersProvider(): AuthHeaders {
  if (!isDevIdentityEnabled()) {
    return {};
  }

  const identity = getDevIdentity();

  return identity
    ? {'X-User-Id': identity.userId, 'X-User-Role': identity.role}
    : {};
}

/**
 * Registers the dev identity with the API client. Call once at module load
 * (before any request) from the app providers; does nothing when disabled.
 */
export function registerDevAuthHeaders(): void {
  if (isDevIdentityEnabled()) {
    // Idempotent: `devAuthHeadersProvider` is a stable function reference, so the
    // shared provider registry keeps a single entry however often this is called.
    registerAuthHeadersProvider(devAuthHeadersProvider);
  }
}
