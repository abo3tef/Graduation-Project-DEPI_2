import {screen, waitFor} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type {ComponentProps} from 'react';
import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {renderWithIntl, resetSession} from '../test/auth-test-utils';
import {VerifyEmailScreen} from './VerifyEmailScreen';

vi.mock('@/i18n/navigation', () => ({
  Link: ({href, children, ...rest}: ComponentProps<'a'> & {href: string}) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
  useRouter: () => ({replace: vi.fn(), push: vi.fn()}),
  usePathname: () => '/verify-email'
}));

type Call = [string, RequestInit];

function installFetch(replies: Array<{status: number; body?: unknown}>): void {
  let index = 0;
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => {
      const reply = replies[Math.min(index, replies.length - 1)] ?? {status: 204};
      index += 1;

      return new Response(reply.body === undefined ? null : JSON.stringify(reply.body), {
        status: reply.status,
        headers: {'content-type': 'application/problem+json'}
      });
    })
  );
}

function calls(): Call[] {
  return (fetch as unknown as {mock: {calls: Call[]}}).mock.calls;
}

describe('VerifyEmailScreen', () => {
  beforeEach(() => {
    vi.stubEnv('NEXT_PUBLIC_API_BASE_URL', 'http://api.test');
    resetSession();
  });

  afterEach(() => {
    resetSession();
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

  it('verifies the token from the link exactly once and reports success', async () => {
    installFetch([{status: 204}]);
    renderWithIntl(<VerifyEmailScreen token="verify-token" />);

    expect(await screen.findByRole('heading', {name: 'Email verified'})).toBeInTheDocument();
    expect(
      screen.getByText('Your email is verified. You can sign in now.')
    ).toBeInTheDocument();
    expect(calls()).toHaveLength(1);
    expect(calls()[0]?.[0]).toBe('http://api.test/api/auth/verify-email');
    expect(JSON.parse(String(calls()[0]?.[1].body))).toEqual({token: 'verify-token'});
  });

  it('explains a broken link and offers a new one', async () => {
    installFetch([{status: 204}]);
    renderWithIntl(<VerifyEmailScreen />);

    expect(
      screen.getByText('The link is incomplete. Open the newest link from your email.')
    ).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Send a new link'})).toBeInTheDocument();
    expect(calls()).toHaveLength(0);
  });

  it('explains a rejected token and keeps the resend form', async () => {
    installFetch([{status: 401, body: {code: 'auth.token_invalid', detail: 'English text'}}]);
    renderWithIntl(<VerifyEmailScreen token="used-token" />);

    expect(await screen.findByText('This link is invalid or has expired.')).toBeInTheDocument();
    expect(screen.queryByText(/English text/)).not.toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Send a new link'})).toBeInTheDocument();
  });

  it('resends a link and shows the same answer for any address', async () => {
    const user = userEvent.setup();
    installFetch([{status: 204}]);
    renderWithIntl(<VerifyEmailScreen />);

    await user.type(screen.getByLabelText('Email'), 'someone@example.test');
    await user.click(screen.getByRole('button', {name: 'Send a new link'}));

    expect(
      await screen.findByText(
        'If the address has a pending account, a new link is on its way.'
      )
    ).toBeInTheDocument();
    expect(calls()[0]?.[0]).toBe('http://api.test/api/auth/resend-verification');
    expect(JSON.parse(String(calls()[0]?.[1].body))).toEqual({email: 'someone@example.test'});
  });

  it('requires an address before resending', async () => {
    const user = userEvent.setup();
    installFetch([{status: 204}]);
    renderWithIntl(<VerifyEmailScreen />);

    await user.click(screen.getByRole('button', {name: 'Send a new link'}));

    expect(await screen.findByText('This field is required.')).toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();
  });

  it('renders in Arabic', async () => {
    installFetch([{status: 204}]);
    renderWithIntl(<VerifyEmailScreen token="verify-token" />, 'ar');

    await waitFor(() =>
      expect(screen.getByRole('heading', {name: 'تم تأكيد البريد'})).toBeInTheDocument()
    );
  });
});
