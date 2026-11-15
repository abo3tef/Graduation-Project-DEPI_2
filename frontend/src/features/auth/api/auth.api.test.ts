import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {isApiError} from '@/lib/api';
import {
  forgotPassword,
  login,
  register,
  resendVerification,
  resetPassword,
  signInWithGoogle,
  verifyEmail
} from './auth.api';

type Recorded = {method: string; path: string; body: unknown};

const SIGN_IN = {
  accessToken: 'header.payload.signature',
  expiresAtUtc: '2026-11-16T12:00:00+00:00',
  user: {
    id: '0f8f6f0a-6d4c-4a54-9a5e-2f2b0c9d1a11',
    fullName: 'Demo Doctor',
    email: 'doctor@medilog.local',
    role: 'Doctor',
    doctorId: '0f8f6f0a-6d4c-4a54-9a5e-2f2b0c9d1a11'
  }
};

const ACCOUNT = {
  id: '0f8f6f0a-6d4c-4a54-9a5e-2f2b0c9d1a11',
  fullName: 'New Doctor',
  email: 'new.doctor@example.test',
  role: 'Doctor',
  doctorId: '0f8f6f0a-6d4c-4a54-9a5e-2f2b0c9d1a11'
};

let calls: Recorded[];

function install(reply: unknown, status = 200): void {
  calls = [];
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      calls.push({
        method: init?.method ?? 'GET',
        path: url.pathname,
        body: typeof init?.body === 'string' ? JSON.parse(init.body) : undefined
      });

      return new Response(status === 204 ? null : JSON.stringify(reply), {
        status,
        headers: {'content-type': 'application/json'}
      });
    })
  );
}

function problem(status: number, code: string): void {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () =>
      new Response(JSON.stringify({title: 'x', status, detail: 'English text', code}), {
        status,
        headers: {'content-type': 'application/problem+json'}
      })
    )
  );
}

describe('auth api', () => {
  beforeEach(() => {
    vi.stubEnv('NEXT_PUBLIC_API_BASE_URL', 'http://api.test');
  });

  afterEach(() => {
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

  it('signs in with the trimmed email and returns the typed session', async () => {
    install(SIGN_IN);

    const result = await login({email: '  doctor@medilog.local  ', password: 'Medilog@2026'});

    expect(calls).toEqual([
      {method: 'POST', path: '/api/auth/login', body: {email: 'doctor@medilog.local', password: 'Medilog@2026'}}
    ]);
    expect(result).toEqual(SIGN_IN);
    expect(result.user.role).toBe('Doctor');
  });

  it('rejects a sign-in response without a token as an invalid response', async () => {
    install({expiresAtUtc: SIGN_IN.expiresAtUtc, user: SIGN_IN.user});

    await expect(login({email: 'doctor@medilog.local', password: 'x'})).rejects.toSatisfy(
      (error: unknown) => isApiError(error) && error.kind === 'invalid-response'
    );
  });

  it('rejects a session for an unknown role', async () => {
    install({...SIGN_IN, user: {...SIGN_IN.user, role: 'Owner'}});

    await expect(login({email: 'doctor@medilog.local', password: 'x'})).rejects.toSatisfy(
      (error: unknown) => isApiError(error) && error.kind === 'invalid-response'
    );
  });

  it('registers a doctor with the trimmed values and the specialization', async () => {
    install(ACCOUNT, 201);

    const summary = await register({
      fullName: '  New Doctor ',
      email: ' new.doctor@example.test ',
      password: 'Medilog@2026',
      phone: '  01000000000 ',
      role: 'Doctor',
      specialization: ' Cardiology '
    });

    expect(calls[0]).toEqual({
      method: 'POST',
      path: '/api/auth/register',
      body: {
        fullName: 'New Doctor',
        email: 'new.doctor@example.test',
        password: 'Medilog@2026',
        phone: '01000000000',
        role: 'Doctor',
        specialization: 'Cardiology'
      }
    });
    expect(summary.email).toBe('new.doctor@example.test');
  });

  it('sends no phone or specialization when they are blank', async () => {
    install({...ACCOUNT, role: 'Secretary', doctorId: null}, 201);

    await register({
      fullName: 'New Secretary',
      email: 'secretary@example.test',
      password: 'Medilog@2026',
      phone: '   ',
      role: 'Secretary',
      specialization: 'Cardiology'
    });

    expect((calls[0]?.body as {phone: unknown; specialization: unknown}).phone).toBeNull();
    expect((calls[0]?.body as {phone: unknown; specialization: unknown}).specialization).toBeNull();
  });

  it('logs in with Google by sending only the ID token', async () => {
    install(SIGN_IN);

    await signInWithGoogle('firebase-id-token');

    expect(calls[0]).toEqual({
      method: 'POST',
      path: '/api/auth/google',
      body: {idToken: 'firebase-id-token'}
    });
  });

  it.each([
    ['verify-email', () => verifyEmail({token: ' abc '}), {token: 'abc'}],
    ['resend-verification', () => resendVerification(' a@b.test '), {email: 'a@b.test'}],
    ['forgot-password', () => forgotPassword(' a@b.test '), {email: 'a@b.test'}],
    [
      'reset-password',
      () => resetPassword({token: ' t ', newPassword: 'Medilog@2026'}),
      {token: 't', newPassword: 'Medilog@2026'}
    ]
  ] as const)('%s posts its body and accepts 204', async (path, call, expected) => {
    install(undefined, 204);

    await expect(call()).resolves.toBeUndefined();

    expect(calls[0]).toEqual({method: 'POST', path: `/api/auth/${path}`, body: expected});
  });

  it('throws the shared ApiError with the stable code so screens can translate it', async () => {
    problem(403, 'auth.email_unverified');

    await expect(login({email: 'doctor@medilog.local', password: 'x'})).rejects.toSatisfy(
      (error: unknown) =>
        isApiError(error) && error.status === 403 && error.code === 'auth.email_unverified'
    );
  });
});
