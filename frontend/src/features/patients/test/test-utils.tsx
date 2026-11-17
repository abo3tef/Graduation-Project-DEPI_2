import {QueryClient, QueryClientProvider} from '@tanstack/react-query';
import {render, type RenderResult} from '@testing-library/react';
import {NextIntlClientProvider} from 'next-intl';
import type {ReactElement} from 'react';
import {vi} from 'vitest';
import arMessages from '@/messages/ar.json';
import enMessages from '@/messages/en.json';
import type {Patient} from '../types/patient.types';
// Test-only bridge to the auth feature's session module (production code uses `@/features/auth`).
import {registerSessionAuth} from '../../auth/session/session-auth';
import {resetSessionForTests, setSession} from '../../auth/session/session-store';

export type TestLocale = 'en' | 'ar';

const MESSAGES = {en: enMessages, ar: arMessages} as const;

export const SECRETARY_ID = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
export const DOCTOR_ID = '11111111-1111-4111-8111-111111111111';
export const OTHER_DOCTOR_ID = '22222222-2222-4222-8222-222222222222';

/** Renders with the real translations (a missing key fails the test) and a fresh query cache. */
export function renderWithProviders(ui: ReactElement, locale: TestLocale = 'en'): RenderResult {
  const queryClient = new QueryClient({
    defaultOptions: {queries: {retry: false, gcTime: 0}, mutations: {retry: false}}
  });

  return render(
    <NextIntlClientProvider
      locale={locale}
      messages={MESSAGES[locale]}
      onError={(error) => {
        throw error;
      }}
    >
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>
  );
}

/** Signs in as the given role (or signs out with `null`) and points the API client at a fake host. */
export function actAs(role: 'Secretary' | 'Doctor' | null) {
  vi.stubEnv('NEXT_PUBLIC_API_BASE_URL', 'http://api.test');
  registerSessionAuth();
  resetSessionForTests();

  if (role === null) {
    return;
  }

  const userId = role === 'Doctor' ? DOCTOR_ID : SECRETARY_ID;

  setSession({
    accessToken: `token-${role}`,
    expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    user: {
      id: userId,
      fullName: role === 'Doctor' ? 'Dr. Dev One' : 'Sara Dev',
      email: `${role.toLowerCase()}@medilog.test`,
      role,
      doctorId: role === 'Doctor' ? userId : null
    }
  });
}

export type RecordedCall = {
  method: string;
  path: string;
  query: URLSearchParams;
  body: unknown;
};

type Reply = {status?: number; body?: unknown; contentType?: string};
type Handler = (call: RecordedCall) => Reply | Promise<Reply>;

/** A stand-in for the Medilog API behind `fetch`; unmatched requests answer 404 so mistakes show. */
export class FakeApi {
  readonly calls: RecordedCall[] = [];
  private readonly handlers = new Map<string, Handler>();

  on(method: string, path: string, handler: Reply | Handler): this {
    this.handlers.set(`${method} ${path}`, typeof handler === 'function' ? handler : () => handler);
    return this;
  }

  install(): void {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
        const url = new URL(String(input));
        const rawBody = typeof init?.body === 'string' ? init.body : undefined;
        const call: RecordedCall = {
          method: init?.method ?? 'GET',
          path: url.pathname,
          query: url.searchParams,
          body: rawBody === undefined ? undefined : JSON.parse(rawBody)
        };
        this.calls.push(call);

        const handler = this.handlers.get(`${call.method} ${call.path}`);
        const reply = handler ? await handler(call) : {status: 404, body: {code: 'resource.not_found'}};

        return new Response(reply.status === 204 || reply.body === undefined ? null : JSON.stringify(reply.body), {
          status: reply.status ?? 200,
          headers: {'content-type': reply.contentType ?? 'application/json'}
        });
      })
    );
  }

  callsTo(method: string, path: string): RecordedCall[] {
    return this.calls.filter((call) => call.method === method && call.path === path);
  }
}

export function problem(status: number, code: string, extra: Record<string, unknown> = {}): Reply {
  return {
    status,
    contentType: 'application/problem+json',
    body: {title: 'x', status, detail: 'English server text that must never be shown', code, ...extra}
  };
}

export function pageOf(
  items: readonly Patient[],
  overrides: Partial<{page: number; pageSize: number; totalCount: number; totalPages: number}> = {}
) {
  return {
    items,
    page: 1,
    pageSize: 10,
    totalCount: items.length,
    totalPages: items.length === 0 ? 0 : 1,
    ...overrides
  };
}

let counter = 0;

export function makePatient(overrides: Partial<Patient> = {}): Patient {
  counter += 1;

  return {
    id: `b0000000-0000-4000-8000-${String(counter).padStart(12, '0')}`,
    doctorId: DOCTOR_ID,
    fullName: 'Mona Said',
    dateOfBirth: '1990-04-12',
    gender: 'Female',
    phone: '01012345601',
    address: '12 Nile Street',
    emergencyContact: 'Mother 01111111111',
    bloodType: null,
    createdAt: '2026-11-17T09:00:00+00:00',
    ...overrides
  };
}

export const PATIENT_OPTIONS = [
  {id: 'b0000001-0000-4000-8000-000000000001', fullName: 'Mona Said', phone: '01012345601'},
  {id: 'b0000001-0000-4000-8000-000000000002', fullName: 'Mona Hassan', phone: '01012345602'}
] as const;

export const DOCTORS = [
  {id: DOCTOR_ID, fullName: 'Dr. Dev One', specialization: null},
  {id: OTHER_DOCTOR_ID, fullName: 'Dr. Dev Two', specialization: 'Cardiology'}
] as const;
