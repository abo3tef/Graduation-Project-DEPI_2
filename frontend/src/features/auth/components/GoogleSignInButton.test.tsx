import {screen, waitFor} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type {ComponentProps} from 'react';
import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import * as firebaseClient from '../google/firebase-client';
import {getSessionState} from '../session/session-store';
import {renderWithIntl, resetSession} from '../test/auth-test-utils';
import {GoogleSignInButton} from './GoogleSignInButton';

const router = vi.hoisted(() => ({replace: vi.fn(), push: vi.fn()}));

vi.mock('@/i18n/navigation', () => ({
  Link: ({href, children, ...rest}: ComponentProps<'a'> & {href: string}) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
  useRouter: () => router,
  usePathname: () => '/login'
}));

const DOCTOR = '11111111-1111-4111-8111-111111111111';

function signInReply() {
  return {
    accessToken: 'google-token',
    expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    user: {
      id: DOCTOR,
      fullName: 'Dr. Google User',
      email: 'doctor.google@medilog.test',
      role: 'Doctor',
      doctorId: DOCTOR
    }
  };
}

function reply(body: unknown, status = 200): void {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () =>
      new Response(JSON.stringify(body), {
        status,
        headers: {'content-type': status >= 400 ? 'application/problem+json' : 'application/json'}
      })
    )
  );
}

describe('GoogleSignInButton', () => {
  beforeEach(() => {
    vi.stubEnv('NEXT_PUBLIC_API_BASE_URL', 'http://api.test');
    resetSession();
    router.replace.mockReset();
    vi.restoreAllMocks();
  });

  afterEach(() => {
    resetSession();
    vi.restoreAllMocks();
  });

  it('renders and shows not-configured message when Firebase keys are missing', async () => {
    const user = userEvent.setup();
    const onError = vi.fn();
    vi.spyOn(firebaseClient, 'isFirebaseConfigured').mockReturnValue(false);

    renderWithIntl(<GoogleSignInButton onError={onError} />);

    const button = screen.getByRole('button', {name: /continue with google/i});
    expect(button).toBeInTheDocument();
    expect(button).toBeEnabled();

    await user.click(button);

    expect(onError).toHaveBeenCalledWith(
      expect.stringMatching(/Google sign-in is not configured/i)
    );
  });

  it('signs in with Google token and stores session when configured', async () => {
    const user = userEvent.setup();
    vi.spyOn(firebaseClient, 'isFirebaseConfigured').mockReturnValue(true);
    vi.spyOn(firebaseClient, 'requestGoogleIdToken').mockResolvedValue('fake-google-id-token');
    reply(signInReply());

    renderWithIntl(<GoogleSignInButton />);

    const button = screen.getByRole('button', {name: /continue with google/i});
    await user.click(button);

    await waitFor(() => expect(router.replace).toHaveBeenCalledWith('/dashboard'));
    expect(getSessionState().status).toBe('authenticated');
    expect(getSessionState().session?.accessToken).toBe('google-token');
  });

  it('handles cancellation gracefully', async () => {
    const user = userEvent.setup();
    const onError = vi.fn();
    vi.spyOn(firebaseClient, 'isFirebaseConfigured').mockReturnValue(true);
    const cancelError = {code: 'auth/popup-closed-by-user'};
    vi.spyOn(firebaseClient, 'requestGoogleIdToken').mockRejectedValue(cancelError);

    renderWithIntl(<GoogleSignInButton onError={onError} />);

    const button = screen.getByRole('button', {name: /continue with google/i});
    await user.click(button);

    await waitFor(() =>
      expect(onError).toHaveBeenCalledWith('Google sign-in was cancelled.')
    );
  });
});
