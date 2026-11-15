import {QueryClient, QueryClientProvider} from '@tanstack/react-query';
import {render, type RenderResult} from '@testing-library/react';
import {NextIntlClientProvider} from 'next-intl';
import type {ReactElement} from 'react';
import arMessages from '@/messages/ar.json';
import enMessages from '@/messages/en.json';
import {resetSessionForTests, setSession} from '../session/session-store';
import type {SessionUserRole} from '../session/session-store';

export type TestLocale = 'en' | 'ar';

const MESSAGES = {en: enMessages, ar: arMessages} as const;

export const TEST_IDS: Record<SessionUserRole, string> = {
  Doctor: '11111111-1111-4111-8111-111111111111',
  Secretary: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  Admin: '99999999-9999-4999-8999-999999999999'
};

/** Renders with the real translations (a missing key fails the test) and a fresh query cache. */
export function renderWithIntl(ui: ReactElement, locale: TestLocale = 'en'): RenderResult {
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

/** Seeds an authenticated session with the predictable token `token-<Role>`. */
export function signInAs(role: SessionUserRole): void {
  setSession({
    accessToken: `token-${role}`,
    expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    user: {
      id: TEST_IDS[role],
      fullName: role === 'Doctor' ? 'Dr. Dev One' : role === 'Secretary' ? 'Sara Dev' : 'Admin Dev',
      email: `${role.toLowerCase()}@medilog.test`,
      role,
      doctorId: role === 'Doctor' ? TEST_IDS[role] : null
    }
  });
}

export function resetSession(): void {
  resetSessionForTests();
}
