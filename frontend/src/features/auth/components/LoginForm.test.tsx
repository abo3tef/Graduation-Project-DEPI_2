import {screen, waitFor} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type {ComponentProps} from 'react';
import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {getSessionState} from '../session/session-store';
import {renderWithIntl, resetSession, signInAs} from '../test/auth-test-utils';
import {LoginForm} from './LoginForm';

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
    accessToken: 'signed-in-token',
    expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    user: {
      id: DOCTOR,
      fullName: 'Dr. Dev One',
      email: 'doctor@medilog.test',
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

/** A spy that fails the test if the form sends anything. */
function installSpyFetch(): void {
  vi.stubGlobal('fetch', vi.fn());
}

const email = () => screen.getByLabelText('Email');
const password = () => screen.getByLabelText('Password');
const submit = () => screen.getByRole('button', {name: /^(Sign in|Please wait…)$/});

describe('LoginForm', () => {
  beforeEach(() => {
    vi.stubEnv('NEXT_PUBLIC_API_BASE_URL', 'http://api.test');
    resetSession();
    router.replace.mockReset();
  });

  afterEach(() => {
    resetSession();
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

  it('reports both empty fields and sends nothing', async () => {
    const user = userEvent.setup();
    installSpyFetch();
    renderWithIntl(<LoginForm />);

    await user.click(submit());

    expect(await screen.findAllByText('This field is required.')).toHaveLength(2);
    expect(fetch).not.toHaveBeenCalled();
  });

  it('rejects an invalid email address before sending', async () => {
    const user = userEvent.setup();
    installSpyFetch();
    renderWithIntl(<LoginForm />);

    await user.type(email(), 'not-an-email');
    await user.type(password(), 'Medilog@2026');
    await user.click(submit());

    expect(await screen.findByText('Enter a valid email address.')).toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();
  });

  it('signs in, stores the session and redirects a doctor to the dashboard', async () => {
    const user = userEvent.setup();
    reply(signInReply());
    renderWithIntl(<LoginForm />);

    await user.type(email(), ' doctor@medilog.test ');
    await user.type(password(), 'Medilog@2026');
    await user.click(submit());

    await waitFor(() => expect(router.replace).toHaveBeenCalledWith('/dashboard'));

    const state = getSessionState();
    expect(state.status).toBe('authenticated');
    expect(state.session?.user.role).toBe('Doctor');

    const [url, init] = (fetch as unknown as {mock: {calls: [string, RequestInit][]}}).mock.calls[0];
    expect(url).toBe('http://api.test/api/auth/login');
    expect(JSON.parse(String(init.body))).toEqual({
      email: 'doctor@medilog.test',
      password: 'Medilog@2026'
    });
  });

  it('honours a next target the role may open', async () => {
    const user = userEvent.setup();
    reply(signInReply());
    renderWithIntl(<LoginForm next="/appointments/new" />);

    await user.type(email(), 'doctor@medilog.test');
    await user.type(password(), 'Medilog@2026');
    await user.click(submit());

    await waitFor(() => expect(router.replace).toHaveBeenCalledWith('/appointments/new'));
  });

  it('ignores an off-site next target', async () => {
    const user = userEvent.setup();
    reply(signInReply());
    renderWithIntl(<LoginForm next="//evil.example/phish" />);

    await user.type(email(), 'doctor@medilog.test');
    await user.type(password(), 'Medilog@2026');
    await user.click(submit());

    await waitFor(() => expect(router.replace).toHaveBeenCalledWith('/dashboard'));
  });

  it('explains rejected credentials in the current language and never shows server text', async () => {
    const user = userEvent.setup();
    reply(
      {
        title: 'Unauthorized',
        status: 401,
        detail: 'The credentials are wrong, English server text',
        code: 'auth.invalid_credentials'
      },
      401
    );
    renderWithIntl(<LoginForm />);

    await user.type(email(), 'doctor@medilog.test');
    await user.type(password(), 'wrong-password');
    await user.click(submit());

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The email or password is incorrect.'
    );
    expect(screen.queryByText(/English server text/)).not.toBeInTheDocument();
    expect(router.replace).not.toHaveBeenCalled();
    expect(getSessionState().status).toBe('anonymous');
  });

  it('explains an unverified account from the server code', async () => {
    const user = userEvent.setup();
    reply({status: 403, code: 'auth.email_unverified', detail: 'English server text'}, 403);
    renderWithIntl(<LoginForm />, 'ar');

    await user.type(screen.getByLabelText('البريد الإلكتروني'), 'doctor@medilog.test');
    await user.type(screen.getByLabelText('كلمة المرور'), 'Medilog@2026');
    await user.click(screen.getByRole('button', {name: /^(تسجيل الدخول|جارٍ التنفيذ…)$/}));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'أكّد بريدك الإلكتروني قبل تسجيل الدخول.'
    );
  });

  it('shows the expiry notice only when the watcher said so', async () => {
    renderWithIntl(<LoginForm expired />);

    expect(screen.getByText('Your session expired. Sign in again to continue.')).toBeInTheDocument();
  });

  it('sends an already signed-in visitor straight to their landing page', async () => {
    signInAs('Secretary');
    renderWithIntl(<LoginForm />);

    await waitFor(() => expect(router.replace).toHaveBeenCalledWith('/appointments'));
    expect(screen.queryByLabelText('Email')).not.toBeInTheDocument();
  });
});
