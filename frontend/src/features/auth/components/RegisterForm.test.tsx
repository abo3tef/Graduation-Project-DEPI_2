import {screen} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type {ComponentProps} from 'react';
import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {getSessionState} from '../session/session-store';
import {renderWithIntl, resetSession} from '../test/auth-test-utils';
import {RegisterForm} from './RegisterForm';

vi.mock('@/i18n/navigation', () => ({
  Link: ({href, children, ...rest}: ComponentProps<'a'> & {href: string}) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
  useRouter: () => ({replace: vi.fn(), push: vi.fn()}),
  usePathname: () => '/register'
}));

const ACCOUNT = {
  id: '11111111-1111-4111-8111-111111111111',
  fullName: 'New Doctor',
  email: 'new.doctor@example.test',
  role: 'Doctor',
  doctorId: '11111111-1111-4111-8111-111111111111'
};

function reply(body: unknown, status = 201): void {
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

const submit = () => screen.getByRole('button', {name: /^(Create account|Please wait…)$/});

async function fillValidForm(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText('Full name'), 'New Doctor');
  await user.type(screen.getByLabelText('Email'), 'new.doctor@example.test');
  await user.type(screen.getByLabelText('Password'), 'Medilog@2026');
  await user.type(screen.getByLabelText('Confirm password'), 'Medilog@2026');
}

describe('RegisterForm', () => {
  beforeEach(() => {
    vi.stubEnv('NEXT_PUBLIC_API_BASE_URL', 'http://api.test');
    resetSession();
  });

  afterEach(() => {
    resetSession();
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

  it('offers only Doctor and Secretary, defaulting to Doctor', () => {
    renderWithIntl(<RegisterForm />);

    const radios = screen.getAllByRole('radio');
    expect(radios.map((radio) => radio.getAttribute('value'))).toEqual(['Doctor', 'Secretary']);
    expect(screen.getByRole('radio', {name: 'Doctor'})).toBeChecked();
    expect(screen.getByLabelText('Specialization (optional)')).toBeInTheDocument();
  });

  it('hides the specialization when a secretary is chosen', async () => {
    const user = userEvent.setup();
    renderWithIntl(<RegisterForm />);

    await user.click(screen.getByRole('radio', {name: 'Secretary'}));

    expect(screen.queryByLabelText('Specialization (optional)')).not.toBeInTheDocument();
  });

  it('reports every missing mandatory field and sends nothing', async () => {
    const user = userEvent.setup();
    installSpyFetch();
    renderWithIntl(<RegisterForm />);

    await user.click(submit());

    expect(await screen.findAllByText('This field is required.')).toHaveLength(4);
    expect(fetch).not.toHaveBeenCalled();
  });

  it('applies the password policy before sending', async () => {
    const user = userEvent.setup();
    installSpyFetch();
    renderWithIntl(<RegisterForm />);

    await fillValidForm(user);
    await user.clear(screen.getByLabelText('Password'));
    await user.type(screen.getByLabelText('Password'), 'password');
    await user.clear(screen.getByLabelText('Confirm password'));
    await user.type(screen.getByLabelText('Confirm password'), 'password');
    await user.click(submit());

    expect(
      await screen.findByText(
        'Use 8 to 128 characters with an upper-case letter, a lower-case letter, a digit and a symbol.'
      )
    ).toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();
  });

  it('reports a confirmation that does not match', async () => {
    const user = userEvent.setup();
    installSpyFetch();
    renderWithIntl(<RegisterForm />);

    await fillValidForm(user);
    await user.clear(screen.getByLabelText('Confirm password'));
    await user.type(screen.getByLabelText('Confirm password'), 'Medilog@2027');
    await user.click(submit());

    expect(await screen.findByText('The passwords do not match.')).toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();
  });

  it('registers, shows the check-email state and does not sign the user in', async () => {
    const user = userEvent.setup();
    reply(ACCOUNT);
    renderWithIntl(<RegisterForm />);

    await fillValidForm(user);
    await user.click(submit());

    expect(await screen.findByRole('heading', {name: 'Check your inbox'})).toBeInTheDocument();
    expect(
      screen.getByText(
        'We sent a verification link to new.doctor@example.test. Open it to activate the account, then sign in.'
      )
    ).toBeInTheDocument();
    expect(getSessionState().status).toBe('anonymous');

    const [url, init] = (fetch as unknown as {mock: {calls: [string, RequestInit][]}}).mock.calls[0];
    expect(url).toBe('http://api.test/api/auth/register');
    expect(JSON.parse(String(init.body))).toEqual({
      fullName: 'New Doctor',
      email: 'new.doctor@example.test',
      password: 'Medilog@2026',
      phone: null,
      role: 'Doctor',
      specialization: null
    });
  });

  it('never sends a specialization for a secretary', async () => {
    const user = userEvent.setup();
    reply({...ACCOUNT, role: 'Secretary', doctorId: null});
    renderWithIntl(<RegisterForm />);

    await fillValidForm(user);
    await user.click(screen.getByRole('radio', {name: 'Secretary'}));
    await user.click(submit());

    await screen.findByRole('heading', {name: 'Check your inbox'});
    const [, init] = (fetch as unknown as {mock: {calls: [string, RequestInit][]}}).mock.calls[0];
    expect(JSON.parse(String(init.body))).toMatchObject({
      role: 'Secretary',
      specialization: null
    });
  });

  it('explains an existing account with the server code', async () => {
    const user = userEvent.setup();
    reply(
      {
        status: 409,
        code: 'auth.email_already_registered',
        detail: 'English server text that must never be shown'
      },
      409
    );
    renderWithIntl(<RegisterForm />);

    await fillValidForm(user);
    await user.click(submit());

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'An account already exists for this email.'
    );
    expect(screen.queryByText(/English server text/)).not.toBeInTheDocument();
  });

  it('maps a server-side field error onto the field', async () => {
    const user = userEvent.setup();
    reply(
      {
        status: 400,
        code: 'validation.failed',
        errors: {email: ['The email address is not valid.']},
        errorCodes: {email: ['EmailValidator']}
      },
      400
    );
    renderWithIntl(<RegisterForm />);

    await fillValidForm(user);
    await user.click(submit());

    expect(await screen.findByText('Enter a valid email address.')).toBeInTheDocument();
    expect(screen.getByLabelText('Email')).toBeInvalid();
  });

  it('renders in Arabic', () => {
    renderWithIntl(<RegisterForm />, 'ar');

    expect(screen.getByRole('heading', {name: 'أنشئ حسابك'})).toBeInTheDocument();
    expect(screen.getByLabelText('الاسم بالكامل')).toBeInTheDocument();
    expect(screen.getByRole('radio', {name: 'سكرتير'})).toBeInTheDocument();
  });

  it('disables the action while the request is pending', async () => {
    const user = userEvent.setup();
    vi.stubGlobal(
      'fetch',
      vi.fn(() => new Promise<Response>(() => undefined))
    );
    renderWithIntl(<RegisterForm />);

    await fillValidForm(user);
    await user.click(submit());

    const pending = await screen.findByRole('button', {name: 'Please wait…'});
    expect(pending).toBeDisabled();
  });
});
