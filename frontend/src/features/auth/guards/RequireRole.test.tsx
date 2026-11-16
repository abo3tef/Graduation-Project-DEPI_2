import {screen, waitFor} from '@testing-library/react';
import type {ComponentProps} from 'react';
import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {renderWithIntl, resetSession, signInAs} from '../test/auth-test-utils';
import {RequireRole} from './RequireRole';

const router = vi.hoisted(() => ({replace: vi.fn(), push: vi.fn()}));

vi.mock('@/i18n/navigation', () => ({
  Link: ({href, children, ...rest}: ComponentProps<'a'> & {href: string}) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
  useRouter: () => router,
  usePathname: () => '/medical-records'
}));

describe('RequireRole', () => {
  beforeEach(() => {
    resetSession();
    router.replace.mockReset();
  });

  afterEach(() => {
    resetSession();
  });

  it('sends a signed-out visitor to the login screen with the current path', async () => {
    renderWithIntl(
      <RequireRole roles={['Doctor']}>
        <p>clinical content</p>
      </RequireRole>
    );

    await waitFor(() =>
      expect(router.replace).toHaveBeenCalledWith('/login?next=%2Fmedical-records')
    );
    expect(screen.getByRole('heading', {name: 'Sign in required'})).toBeInTheDocument();
    expect(screen.queryByText('clinical content')).not.toBeInTheDocument();
  });

  it('never renders the content for a role that is not allowed', () => {
    signInAs('Secretary');
    renderWithIntl(
      <RequireRole roles={['Doctor']}>
        <p>clinical content</p>
      </RequireRole>
    );

    expect(screen.getByRole('heading', {name: 'Not available for your account'})).toBeInTheDocument();
    expect(screen.queryByText('clinical content')).not.toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Go to your home page'})).toHaveAttribute(
      'href',
      '/appointments'
    );
    expect(router.replace).not.toHaveBeenCalled();
  });

  it('renders the content for an allowed role', () => {
    signInAs('Doctor');
    renderWithIntl(
      <RequireRole roles={['Doctor']}>
        <p>clinical content</p>
      </RequireRole>
    );

    expect(screen.getByText('clinical content')).toBeInTheDocument();
    expect(router.replace).not.toHaveBeenCalled();
  });

  it('lets both roles into the appointments area', () => {
    signInAs('Secretary');
    renderWithIntl(
      <RequireRole roles={['Doctor', 'Secretary']}>
        <p>appointments</p>
      </RequireRole>
    );

    expect(screen.getByText('appointments')).toBeInTheDocument();
  });
});
