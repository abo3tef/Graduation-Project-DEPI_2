import {QueryClient, QueryClientProvider} from '@tanstack/react-query';
import {render, type RenderResult} from '@testing-library/react';
import {NextIntlClientProvider} from 'next-intl';
import type {ReactElement} from 'react';
import {vi} from 'vitest';
import {registerDevAuthHeaders, setDevIdentityForTests} from './dev-identity-helper';
import arMessages from '@/messages/ar.json';
import enMessages from '@/messages/en.json';
import type {Appointment} from '../types/appointment.types';

export type TestLocale = 'en' | 'ar';

const MESSAGES = {en: enMessages, ar: arMessages} as const;

export const SECRETARY_ID = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
export const DOCTOR_ID = '11111111-1111-4111-8111-111111111111';

/** Renders with the real translations (a missing key fails the test), React Query and a fresh cache. */
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

/** Acts as the given user: enables the dev identity and registers its headers with the API client. */
export function actAs(role: 'Secretary' | 'Doctor' | null, userId = role === 'Doctor' ? DOCTOR_ID : SECRETARY_ID) {
  vi.stubEnv('NEXT_PUBLIC_API_BASE_URL', 'http://api.test');
  vi.stubEnv('NEXT_PUBLIC_DEV_IDENTITY', 'true');
  vi.stubEnv('NEXT_PUBLIC_DEV_USER_ID', '');
  vi.stubEnv('NEXT_PUBLIC_DEV_USER_ROLE', '');
  setDevIdentityForTests(role === null ? null : {userId, role});
  registerDevAuthHeaders();
}

export type RecordedCall = {
  method: string;
  path: string;
  query: URLSearchParams;
  headers: Record<string, string>;
  body: unknown;
};

type Reply = {status?: number; body?: unknown; contentType?: string};
type Handler = (call: RecordedCall) => Reply | Promise<Reply>;

/**
 * A stand-in for the Medilog API behind `fetch`. Handlers are matched on
 * `METHOD /path`; anything unmatched answers 404 so a wrong request is visible.
 */
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
          headers: {...(init?.headers as Record<string, string> | undefined)},
          body: rawBody === undefined ? undefined : JSON.parse(rawBody)
        };
        this.calls.push(call);

        const handler = this.handlers.get(`${call.method} ${call.path}`);
        const reply = handler ? await handler(call) : {status: 404, body: {code: 'appointment.not_found'}};

        return new Response(reply.body === undefined ? null : JSON.stringify(reply.body), {
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

export function page(items: readonly Appointment[], overrides: Partial<{page: number; pageSize: number; totalCount: number; totalPages: number}> = {}) {
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

export function makeAppointment(overrides: Partial<Appointment> = {}): Appointment {
  counter += 1;

  return {
    id: `a0000000-0000-4000-8000-${String(counter).padStart(12, '0')}`,
    patientId: 'b0000001-0000-4000-8000-000000000001',
    patientName: 'Mona Said',
    patientPhone: '01012345601',
    doctorId: DOCTOR_ID,
    doctorName: 'Dr. Dev One',
    appointmentDate: '2099-01-02',
    startTime: '09:00:00',
    endTime: '09:30:00',
    reason: 'Routine check',
    status: 'Scheduled',
    notes: null,
    cancellationReason: null,
    createdBy: SECRETARY_ID,
    createdAtUtc: '2030-01-01T06:00:00+00:00',
    ...overrides
  };
}

export const DOCTORS = [
  {id: DOCTOR_ID, fullName: 'Dr. Dev One', specialization: 'Cardiology'},
  {id: '22222222-2222-4222-8222-222222222222', fullName: 'Dr. Dev Two', specialization: null}
] as const;

export const PATIENTS = [
  {id: 'b0000001-0000-4000-8000-000000000001', fullName: 'Mona Said', phone: '01012345601'},
  {id: 'b0000001-0000-4000-8000-000000000002', fullName: 'Mona Hassan', phone: '01012345602'}
] as const;
