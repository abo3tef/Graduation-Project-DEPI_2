import {screen} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type {ComponentProps} from 'react';
import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {resetAuthHeadersProviders} from '@/lib/api';
import {PatientFinder} from './PatientFinder';
import {
  actAs,
  DOCTOR_ID,
  FakeApi,
  PATIENT_ID,
  PATIENT_LOOKUPS,
  problem,
  renderWithProviders
} from '../test/test-utils';

vi.mock('@/i18n/navigation', () => ({
  Link: ({href, children, ...rest}: ComponentProps<'a'> & {href: string}) => (
    <a href={href} {...rest}>
      {children}
    </a>
  )
}));

let api: FakeApi;

const LOOKUP = '/api/appointments/lookups/patients';

beforeEach(() => {
  api = new FakeApi();
  api.install();
});

afterEach(() => {
  resetAuthHeadersProviders();
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
});

describe('PatientFinder', () => {
  it('shows the identity gate and never sends a request without an acting user', async () => {
    actAs(null);
    renderWithProviders(<PatientFinder />);

    expect(await screen.findByText('No acting user selected')).toBeInTheDocument();
    expect(api.calls).toHaveLength(0);
  });

  it('shows the doctors-only gate and never sends a request as a secretary', () => {
    actAs('Secretary');
    renderWithProviders(<PatientFinder />);

    expect(screen.getByText('Doctors only')).toBeInTheDocument();
    expect(api.calls).toHaveLength(0);
  });

  it('does not search until the minimum length is typed', async () => {
    actAs('Doctor');
    api.on('GET', LOOKUP, {body: PATIENT_LOOKUPS});
    renderWithProviders(<PatientFinder />);

    await userEvent.type(screen.getByLabelText('Search by name or phone'), 'M');

    // The debounced wait is longer than this assertion, so no request yet.
    expect(api.calls).toHaveLength(0);
    expect(await screen.findByText('Type at least 2 characters.')).toBeInTheDocument();
  });

  it("searches the acting doctor's own patients with the F1 lookup contract", async () => {
    actAs('Doctor');
    api.on('GET', LOOKUP, {body: PATIENT_LOOKUPS});
    renderWithProviders(<PatientFinder />);

    await userEvent.type(screen.getByLabelText('Search by name or phone'), 'Mona');

    const link = await screen.findByRole('link', {name: /Mona Said/});
    expect(link).toHaveAttribute('href', `/patients/${PATIENT_ID}`);

    const calls = api.callsTo('GET', LOOKUP);
    expect(calls.length).toBeGreaterThan(0);
    const last = calls[calls.length - 1];
    expect(last.query.get('doctorId')).toBe(DOCTOR_ID);
    expect(last.query.get('search')).toBe('Mona');
    expect(last.query.get('limit')).toBe('10');
    // The identity travels in headers, never in the URL.
    expect(last.headers['X-User-Id']).toBe(DOCTOR_ID);
    expect(last.headers['X-User-Role']).toBe('Doctor');
  });

  it('reports when no patient matches', async () => {
    actAs('Doctor');
    api.on('GET', LOOKUP, {body: []});
    renderWithProviders(<PatientFinder />);

    await userEvent.type(screen.getByLabelText('Search by name or phone'), 'Zzz');

    expect(await screen.findByText('No patients matched your search.')).toBeInTheDocument();
  });

  it('maps an API failure to a friendly key without leaking server text', async () => {
    actAs('Doctor');
    api.on('GET', LOOKUP, problem(403, 'access.forbidden'));
    renderWithProviders(<PatientFinder />);

    await userEvent.type(screen.getByLabelText('Search by name or phone'), 'Mona');

    expect(await screen.findByText('You are not allowed to view this data.')).toBeInTheDocument();
    expect(screen.queryByText(/English server text/)).not.toBeInTheDocument();
  });

  it('renders Arabic labels in the Arabic locale', async () => {
    actAs('Doctor');
    api.on('GET', LOOKUP, {body: PATIENT_LOOKUPS});
    renderWithProviders(<PatientFinder />, 'ar');

    expect(screen.getByRole('heading', {name: 'البحث عن مريض'})).toBeInTheDocument();
    await userEvent.type(screen.getByLabelText('البحث بالاسم أو رقم الهاتف'), 'Mona');

    expect(await screen.findByRole('link', {name: /Mona Said/})).toBeInTheDocument();
    expect(screen.getByRole('list', {name: 'النتائج المطابقة'})).toBeInTheDocument();
  });
});
