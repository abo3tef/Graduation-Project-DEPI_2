import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {getAuthHeaders, resetAuthHeadersProvider} from '@/lib/api';
import {
  DEV_IDENTITY_STORAGE_KEY,
  devAuthHeadersProvider,
  getDevIdentity,
  isDevIdentityEnabled,
  isGuid,
  registerDevAuthHeaders,
  setDevIdentity
} from './dev-identity';

const SECRETARY = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const DOCTOR = '11111111-1111-4111-8111-111111111111';

describe('dev identity', () => {
  beforeEach(() => {
    window.localStorage.clear();
    vi.stubEnv('NEXT_PUBLIC_DEV_IDENTITY', 'true');
    vi.stubEnv('NEXT_PUBLIC_DEV_USER_ID', '');
    vi.stubEnv('NEXT_PUBLIC_DEV_USER_ROLE', '');
    setDevIdentity(null);
  });

  afterEach(() => {
    resetAuthHeadersProvider();
    vi.unstubAllEnvs();
    window.localStorage.clear();
  });

  it('validates hyphenated GUIDs only', () => {
    expect(isGuid(SECRETARY)).toBe(true);
    expect(isGuid(SECRETARY.replaceAll('-', ''))).toBe(false);
    expect(isGuid('not-a-guid')).toBe(false);
    expect(isGuid('')).toBe(false);
  });

  it('is enabled only in non-production builds with the flag on', () => {
    expect(isDevIdentityEnabled()).toBe(true);

    vi.stubEnv('NEXT_PUBLIC_DEV_IDENTITY', 'false');
    expect(isDevIdentityEnabled()).toBe(false);

    vi.stubEnv('NEXT_PUBLIC_DEV_IDENTITY', 'true');
    vi.stubEnv('NODE_ENV', 'production');
    expect(isDevIdentityEnabled()).toBe(false);
  });

  it('sends the X-User-Id and X-User-Role headers once an identity is stored', () => {
    setDevIdentity({userId: SECRETARY.toUpperCase(), role: 'Secretary'});

    expect(devAuthHeadersProvider()).toEqual({'X-User-Id': SECRETARY, 'X-User-Role': 'Secretary'});
  });

  it('sends no headers when no identity is known', () => {
    expect(devAuthHeadersProvider()).toEqual({});
  });

  it('falls back to the env defaults and lets the stored identity win', () => {
    vi.stubEnv('NEXT_PUBLIC_DEV_USER_ID', DOCTOR);
    vi.stubEnv('NEXT_PUBLIC_DEV_USER_ROLE', 'Doctor');
    setDevIdentity(null);

    expect(getDevIdentity()).toEqual({userId: DOCTOR, role: 'Doctor'});

    setDevIdentity({userId: SECRETARY, role: 'Secretary'});
    expect(getDevIdentity()).toEqual({userId: SECRETARY, role: 'Secretary'});

    setDevIdentity(null);
    expect(getDevIdentity()).toEqual({userId: DOCTOR, role: 'Doctor'});
  });

  it('ignores invalid env defaults and corrupt storage', () => {
    vi.stubEnv('NEXT_PUBLIC_DEV_USER_ID', 'nope');
    vi.stubEnv('NEXT_PUBLIC_DEV_USER_ROLE', 'Admin');
    window.localStorage.setItem(DEV_IDENTITY_STORAGE_KEY, '{not json');
    setDevIdentity(null);
    window.localStorage.setItem(DEV_IDENTITY_STORAGE_KEY, '{not json');

    expect(getDevIdentity()).toBeNull();

    window.localStorage.setItem(DEV_IDENTITY_STORAGE_KEY, JSON.stringify({userId: SECRETARY, role: 'Admin'}));
    expect(getDevIdentity()).toBeNull();
  });

  it('returns a referentially stable snapshot while nothing changes', () => {
    setDevIdentity({userId: SECRETARY, role: 'Secretary'});

    expect(getDevIdentity()).toBe(getDevIdentity());
  });

  it('adds NO headers in a production build even if an identity is stored', () => {
    setDevIdentity({userId: SECRETARY, role: 'Secretary'});
    vi.stubEnv('NODE_ENV', 'production');

    expect(devAuthHeadersProvider()).toEqual({});
  });

  it('registers with the API client only when enabled', () => {
    setDevIdentity({userId: SECRETARY, role: 'Secretary'});

    vi.stubEnv('NEXT_PUBLIC_DEV_IDENTITY', 'false');
    registerDevAuthHeaders();
    expect(getAuthHeaders()).toEqual({});

    vi.stubEnv('NEXT_PUBLIC_DEV_IDENTITY', 'true');
    registerDevAuthHeaders();
    expect(getAuthHeaders()).toEqual({'X-User-Id': SECRETARY, 'X-User-Role': 'Secretary'});
  });
});
