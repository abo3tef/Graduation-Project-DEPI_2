import {render, screen, waitFor} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {NextIntlClientProvider} from 'next-intl';
import type {ReactElement} from 'react';
import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import arMessages from '@/messages/ar.json';
import enMessages from '@/messages/en.json';
import {getDevIdentity, setDevIdentity} from '../dev-identity/dev-identity';
import {DevIdentityChip} from './DevIdentityChip';

const SECRETARY = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const DOCTOR = '11111111-1111-4111-8111-111111111111';

// Renders with the real translations; a missing message fails the test.
function renderWithProviders(ui: ReactElement, locale: 'en' | 'ar' = 'en') {
  return render(
    <NextIntlClientProvider
      locale={locale}
      messages={locale === 'ar' ? arMessages : enMessages}
      onError={(error) => {
        throw error;
      }}
    >
      {ui}
    </NextIntlClientProvider>
  );
}

beforeEach(() => {
  vi.stubEnv('NEXT_PUBLIC_DEV_IDENTITY', 'true');
  vi.stubEnv('NEXT_PUBLIC_DEV_USER_ID', '');
  vi.stubEnv('NEXT_PUBLIC_DEV_USER_ROLE', '');
  setDevIdentity(null);
});

afterEach(() => {
  vi.unstubAllEnvs();
  setDevIdentity(null);
});

describe('DevIdentityChip', () => {
  it('renders nothing when the dev identity is disabled', () => {
    vi.stubEnv('NEXT_PUBLIC_DEV_IDENTITY', 'false');
    const {container} = renderWithProviders(<DevIdentityChip />);

    expect(container).toBeEmptyDOMElement();
  });

  it('renders nothing in a production build', () => {
    vi.stubEnv('NODE_ENV', 'production');
    const {container} = renderWithProviders(<DevIdentityChip />);

    expect(container).toBeEmptyDOMElement();
  });

  it('invites the developer to set a user when none is known', () => {
    renderWithProviders(<DevIdentityChip />);

    expect(screen.getByRole('button', {name: 'Set user (dev)'})).toBeInTheDocument();
  });

  it('shows who the app is acting as', async () => {
    setDevIdentity({userId: SECRETARY, role: 'Secretary'});
    renderWithProviders(<DevIdentityChip />);

    expect(await screen.findByRole('button', {name: 'Acting as Secretary · aaaaaaaa'})).toBeInTheDocument();
  });

  it('rejects an invalid id, then saves a valid id and role and updates the label', async () => {
    const user = userEvent.setup();
    renderWithProviders(<DevIdentityChip />);

    await user.click(screen.getByRole('button', {name: 'Set user (dev)'}));
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveAccessibleName('Current user (development only)');

    await user.type(screen.getByLabelText('User id'), 'not-a-guid');
    await user.click(screen.getByRole('button', {name: 'Save'}));
    expect(await screen.findByRole('alert')).toHaveTextContent('The id must be a valid GUID with hyphens.');
    expect(getDevIdentity()).toBeNull();

    await user.clear(screen.getByLabelText('User id'));
    await user.type(screen.getByLabelText('User id'), DOCTOR);
    await user.selectOptions(screen.getByLabelText('Role'), 'Doctor');
    await user.click(screen.getByRole('button', {name: 'Save'}));

    await waitFor(() => expect(getDevIdentity()).toEqual({userId: DOCTOR, role: 'Doctor'}));
    expect(await screen.findByRole('button', {name: 'Acting as Doctor · 11111111'})).toBeInTheDocument();
  });

  it('clears the identity', async () => {
    const user = userEvent.setup();
    setDevIdentity({userId: SECRETARY, role: 'Secretary'});
    renderWithProviders(<DevIdentityChip />);

    await user.click(await screen.findByRole('button', {name: /Acting as/}));
    await user.click(screen.getByRole('button', {name: 'Clear'}));

    await waitFor(() => expect(getDevIdentity()).toBeNull());
    expect(await screen.findByRole('button', {name: 'Set user (dev)'})).toBeInTheDocument();
  });

  it('is available in Arabic', async () => {
    const user = userEvent.setup();
    renderWithProviders(<DevIdentityChip />, 'ar');

    await user.click(screen.getByRole('button', {name: 'تحديد المستخدم (تطوير)'}));

    expect(await screen.findByLabelText('معرّف المستخدم')).toBeInTheDocument();
    expect(screen.getByRole('option', {name: 'سكرتير'})).toBeInTheDocument();
  });
});
