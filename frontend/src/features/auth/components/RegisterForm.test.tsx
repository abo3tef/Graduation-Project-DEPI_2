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
  doctorId: '11111111-1111-4111-8111-111111111111',
  specialization: 'Cardiology'
};

function reply(body: unknown, status = 201): void {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: string | URL | Request) => {
      const url = typeof input === 'string' ? input : input instanceof URL ? input.toString() : input.url;
      if (url.endsWith('/api/specializations')) {
        return new Response(JSON.stringify([
          {key: 'cardiology', nameEn: 'Cardiology', nameAr: 'أمراض القلب'}
        ]), {
          status: 200,
          headers: {'content-type': 'application/json'}
        });
      }
      return new Response(JSON.stringify(body), {
        status,
        headers: {'content-type': status >= 400 ? 'application/problem+json' : 'application/json'}
      });
    })
  );
}

/** A spy that fails the test if the form sends anything. */
function installSpyFetch(): void {
  vi.stubGlobal('fetch', vi.fn(async (input: string | URL | Request) => {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.toString() : input.url;
    if (url.endsWith('/api/specializations')) {
      return new Response(JSON.stringify([
        {key: 'cardiology', nameEn: 'Cardiology', nameAr: 'أمراض القلب'}
      ]), {status: 200, headers: {'content-type': 'application/json'}});
    }
    return new Response(JSON.stringify({}), {status: 400, headers: {'content-type': 'application/problem+json'}});
  }));
}

const submit = () => screen.getByRole('button', {name: /^(Create account|Please wait…)$/});
function authRegisterCalls(): number {
  const mock = (fetch as unknown as {mock: {calls: [string | URL | Request, ...unknown[]][]}}).mock;
  return mock.calls.filter(([input]) => {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.toString() : input.url;
    return url.endsWith('/api/auth/register');
  }).length;
}

async function fillValidForm(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText('Full name'), 'New Doctor');
  await user.type(screen.getByLabelText('Email'), 'new.doctor@example.test');
  await user.type(screen.getByLabelText('Password'), 'Medilog@2026');
  await user.type(screen.getByLabelText('Confirm password'), 'Medilog@2026');
  const specialization = await screen.findByLabelText(/Specialization/);
  await screen.findByRole('option', {name: 'Cardiology'});
  await user.selectOptions(specialization, 'Cardiology');
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
    expect(screen.getByLabelText(/Specialization/)).toBeInTheDocument();
  });

  it('hides the specialization when a secretary is chosen', async () => {
    const user = userEvent.setup();
    renderWithIntl(<RegisterForm />);

    await user.click(screen.getByRole('radio', {name: 'Secretary'}));

    expect(screen.queryByLabelText(/Specialization/)).not.toBeInTheDocument();
  });

  it('reports every missing mandatory field and sends nothing', async () => {
    const user = userEvent.setup();
    installSpyFetch();
    renderWithIntl(<RegisterForm />);

    await user.click(submit());

    expect(await screen.findAllByText('This field is required.')).toHaveLength(5);
    expect(
      (fetch as unknown as {mock: {calls: [string | URL | Request, ...unknown[]][]}}).mock.calls
        .filter(([input]) => {
          const url = typeof input === 'string' ? input : input instanceof URL ? input.toString() : input.url;
          return url.endsWith('/api/auth/register');
        })
    ).toHaveLength(0);
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
    expect(authRegisterCalls()).toBe(0);
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
    expect(authRegisterCalls()).toBe(0);
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

    const [, init] = (fetch as unknown as {mock: {calls: [string, RequestInit][]}}).mock.calls
      .find(([url]) => url.endsWith('/api/auth/register'))!;
    expect(JSON.parse(String(init.body))).toEqual({
      fullName: 'New Doctor',
      email: 'new.doctor@example.test',
      password: 'Medilog@2026',
      phone: null,
      role: 'Doctor',
      specialization: 'Cardiology'
    });
  });

  it('offers a resend action on the check-email screen', async () => {
    const user = userEvent.setup();
    let requestCount = 0;
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: string | URL | Request) => {
        const url = typeof input === 'string' ? input : input instanceof URL ? input.toString() : input.url;
        if (url.endsWith('/api/specializations')) {
          return new Response(JSON.stringify([
            {key: 'cardiology', nameEn: 'Cardiology', nameAr: 'أمراض القلب'}
          ]), {status: 200, headers: {'content-type': 'application/json'}});
        }
        requestCount += 1;
        return new Response(requestCount === 1 ? JSON.stringify(ACCOUNT) : null, {
          status: requestCount === 1 ? 201 : 204,
          headers: {'content-type': 'application/json'}
        });
      })
    );
    renderWithIntl(<RegisterForm />);

    await fillValidForm(user);
    await user.click(submit());
    const heading = await screen.findByRole('heading', {name: 'Check your inbox'});
    expect(heading).toBeInTheDocument();

    await user.click(screen.getByRole('button', {name: 'Send a new link'}));

    expect(
      await screen.findByText('If the address has a pending account, a new link is on its way.')
    ).toBeInTheDocument();
    const resent = (fetch as unknown as {mock: {calls: [string, RequestInit][]}}).mock.calls
      .find(([url]) => url.endsWith('/api/auth/resend-verification'))!;
    expect(resent[0]).toBe('http://api.test/api/auth/resend-verification');
    expect(JSON.parse(String(resent[1].body))).toEqual({email: 'new.doctor@example.test'});
  });

  it('never sends a specialization for a secretary', async () => {
    const user = userEvent.setup();
    reply({...ACCOUNT, role: 'Secretary', doctorId: null});
    renderWithIntl(<RegisterForm />);

    await fillValidForm(user);
    await user.click(screen.getByRole('radio', {name: 'Secretary'}));
    await user.click(submit());

    await screen.findByRole('heading', {name: 'Check your inbox'});
    const [, init] = (fetch as unknown as {mock: {calls: [string, RequestInit][]}}).mock.calls
      .find(([url]) => url.endsWith('/api/auth/register'))!;
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
      vi.fn((input: string | URL | Request) => {
        const url = typeof input === 'string' ? input : input instanceof URL ? input.toString() : input.url;
        if (url.endsWith('/api/specializations')) {
          return Promise.resolve(new Response(JSON.stringify([
            {key: 'cardiology', nameEn: 'Cardiology', nameAr: 'أمراض القلب'}
          ]), {status: 200, headers: {'content-type': 'application/json'}}));
        }
        return new Promise<Response>(() => undefined);
      })
    );
    renderWithIntl(<RegisterForm />);

    await fillValidForm(user);
    await user.click(submit());

    const pending = await screen.findByRole('button', {name: 'Please wait…'});
    expect(pending).toBeDisabled();
  });
});
