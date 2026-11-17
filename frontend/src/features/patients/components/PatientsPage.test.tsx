import {screen, waitFor, within} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type {ComponentProps} from 'react';
import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {resetAuthHeadersProviders} from '@/lib/api';
import {actAs, FakeApi, makePatient, pageOf, problem, renderWithProviders} from '../test/test-utils';
import {PatientsPage} from './PatientsPage';

vi.mock('@/i18n/navigation', () => ({
  Link: ({href, children, ...rest}: ComponentProps<'a'> & {href: string}) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
  useRouter: () => ({push: vi.fn()}),
  usePathname: () => '/patients'
}));

const notify = vi.hoisted(() => ({success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn()}));
vi.mock('@/lib/utils/toast', () => ({notify}));

const LIST = '/api/patients';

const MONA = makePatient({fullName: 'Mona Said', phone: '01012345601'});
const OMAR = makePatient({fullName: 'Omar Hassan', phone: null, gender: 'Male'});

let api: FakeApi;

beforeEach(() => {
  api = new FakeApi();
  api.install();
  api.on('GET', LIST, {body: pageOf([MONA, OMAR])});
  actAs('Doctor');
});

afterEach(() => {
  resetAuthHeadersProviders();
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
  notify.success.mockReset();
});

describe('PatientsPage', () => {
  it('lists patients with the shared envelope and shows names and phones with the right direction', async () => {
    renderWithProviders(<PatientsPage />);

    const mona = await screen.findByText('Mona Said');
    expect(mona).toHaveAttribute('dir', 'auto');
    expect(screen.getByText('01012345601')).toHaveAttribute('dir', 'ltr');
    expect(screen.getByText('2 patients')).toBeInTheDocument();
    expect(screen.getByText('—')).toBeInTheDocument();

    const call = api.callsTo('GET', LIST)[0];
    expect(call?.query.get('pageNumber')).toBe('1');
    expect(call?.query.get('pageSize')).toBe('10');
    expect(screen.getByRole('link', {name: 'Edit Mona Said'})).toHaveAttribute('href', `/patients/${MONA.id}/edit`);
    expect(screen.getByRole('link', {name: 'New patient'})).toHaveAttribute('href', '/patients/new');
  });

  it('searches with a debounce and goes back to the first page', async () => {
    const user = userEvent.setup();
    renderWithProviders(<PatientsPage />);
    await screen.findByText('Mona Said');

    await user.type(screen.getByLabelText('Search patients'), 'omar');

    await waitFor(() => expect(api.callsTo('GET', LIST).at(-1)?.query.get('search')).toBe('omar'));
    expect(api.callsTo('GET', LIST).every((call) => call.query.get('search') !== 'o')).toBe(true);
  });

  it('filters by gender', async () => {
    const user = userEvent.setup();
    renderWithProviders(<PatientsPage />);
    await screen.findByText('Mona Said');

    await user.selectOptions(screen.getByLabelText('Gender'), 'Male');

    await waitFor(() => expect(api.callsTo('GET', LIST).at(-1)?.query.get('gender')).toBe('Male'));
  });

  it('pages through the results using the server paging metadata', async () => {
    api.on('GET', LIST, (call) => {
      const pageNumber = Number(call.query.get('pageNumber'));

      return {body: pageOf([pageNumber === 1 ? MONA : OMAR], {page: pageNumber, totalCount: 12, totalPages: 2})};
    });
    const user = userEvent.setup();
    renderWithProviders(<PatientsPage />);

    expect(await screen.findByText('Page 1 of 2')).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Previous'})).toBeDisabled();

    await user.click(screen.getByRole('button', {name: 'Next'}));

    expect(await screen.findByText('Omar Hassan')).toBeInTheDocument();
    expect(screen.getByText('Page 2 of 2')).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Next'})).toBeDisabled();
  });

  it('explains an empty list and an empty search differently', async () => {
    api.on('GET', LIST, {body: pageOf([])});
    const user = userEvent.setup();
    renderWithProviders(<PatientsPage />);

    expect(await screen.findByText('No patients yet. Add the first one.')).toBeInTheDocument();

    await user.type(screen.getByLabelText('Search patients'), 'zz');

    expect(await screen.findByText('No patients match your search.')).toBeInTheDocument();
  });

  it('shows a translated error with a retry that refetches', async () => {
    api.on('GET', LIST, problem(500, 'server.error'));
    const user = userEvent.setup();
    renderWithProviders(<PatientsPage />);

    // A server failure is retried once (with the default back-off) before the error is shown.
    expect(
      await screen.findByText('Something went wrong on the server. Try again later.', {}, {timeout: 4000})
    ).toBeInTheDocument();
    expect(screen.queryByText(/English server text/)).not.toBeInTheDocument();

    api.on('GET', LIST, {body: pageOf([MONA])});
    await user.click(screen.getByRole('button', {name: 'Try again'}));

    expect(await screen.findByText('Mona Said')).toBeInTheDocument();
  }, 10000);

  it('asks for sign-in and sends no request without a session', async () => {
    actAs(null);
    renderWithProviders(<PatientsPage />);

    expect(await screen.findByText('Sign in required')).toBeInTheDocument();
    expect(api.calls).toHaveLength(0);
  });
});

describe('PatientsPage delete', () => {
  it('offers delete to a doctor and removes the patient after confirmation', async () => {
    api.on('DELETE', `${LIST}/${MONA.id}`, {status: 204});
    const user = userEvent.setup();
    renderWithProviders(<PatientsPage />);

    await user.click(await screen.findByRole('button', {name: 'Delete Mona Said'}));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('Mona Said will be permanently deleted. This cannot be undone.')).toBeInTheDocument();

    await user.click(within(dialog).getByRole('button', {name: 'Delete patient'}));

    await waitFor(() => expect(api.callsTo('DELETE', `${LIST}/${MONA.id}`)).toHaveLength(1));
    await waitFor(() => expect(notify.success).toHaveBeenCalledWith('Patient deleted.'));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('keeps the dialog open and explains a 409 when the patient has related records', async () => {
    api.on('DELETE', `${LIST}/${MONA.id}`, problem(409, 'patient.has_related_records'));
    const user = userEvent.setup();
    renderWithProviders(<PatientsPage />);

    await user.click(await screen.findByRole('button', {name: 'Delete Mona Said'}));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', {name: 'Delete patient'}));

    expect(
      await within(dialog).findByText(
        'This patient has appointments, visits, payments or other records and cannot be deleted.'
      )
    ).toBeInTheDocument();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(notify.success).not.toHaveBeenCalled();
    expect(screen.getByText('Mona Said', {selector: 'td'})).toBeInTheDocument();
  });

  it('lets the user cancel without calling the API', async () => {
    const user = userEvent.setup();
    renderWithProviders(<PatientsPage />);

    await user.click(await screen.findByRole('button', {name: 'Delete Mona Said'}));
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', {name: 'Cancel'}));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(api.calls.some((call) => call.method === 'DELETE')).toBe(false);
  });

  it('hides delete from a secretary but keeps edit', async () => {
    actAs('Secretary');
    renderWithProviders(<PatientsPage />);

    await screen.findByText('Mona Said');

    expect(screen.queryByRole('button', {name: /^Delete/})).not.toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Edit Mona Said'})).toBeInTheDocument();
  });

  it('renders the list in Arabic', async () => {
    renderWithProviders(<PatientsPage />, 'ar');

    expect(await screen.findByRole('heading', {name: 'المرضى'})).toBeInTheDocument();
    expect(await screen.findByRole('button', {name: 'حذف Mona Said'})).toBeInTheDocument();
    expect(screen.getByText('مريضان')).toBeInTheDocument();
  });
});
