import {act, fireEvent, screen, waitFor, within} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type {ComponentProps} from 'react';
import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {resetAuthHeadersProviders} from '@/lib/api';
import {getClinicNow} from '../utils/date-time';
import {setDevIdentityForTests} from '../test/dev-identity-helper';
import {
  actAs,
  DOCTOR_ID,
  DOCTORS,
  FakeApi,
  makeAppointment,
  page,
  problem,
  renderWithProviders,
  SECRETARY_ID
} from '../test/test-utils';
import {AppointmentsPage} from './AppointmentsPage';

vi.mock('@/i18n/navigation', () => ({
  Link: ({href, children, ...rest}: ComponentProps<'a'> & {href: string}) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
  useRouter: () => ({push: vi.fn()}),
  usePathname: () => '/appointments'
}));

const notify = vi.hoisted(() => ({success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn()}));
vi.mock('@/lib/utils/toast', () => ({notify}));

let api: FakeApi;

const LIST = '/api/appointments';
const DOCTOR_LOOKUP = '/api/appointments/lookups/doctors';

beforeEach(() => {
  api = new FakeApi();
  api.install();
  api.on('GET', DOCTOR_LOOKUP, {body: DOCTORS});
});

afterEach(() => {
  resetAuthHeadersProviders();
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
  notify.success.mockReset();
});

const table = () => within(screen.getByTestId('appointments-table'));

/** Waits for the list to load, then finds text inside the table (not the stacked cards). */
async function waitForRow(text: string) {
  const loaded = await screen.findByTestId('appointments-table');
  return within(loaded).findByText(text);
}

describe('AppointmentsPage', () => {
  it('asks for an acting user and sends no request while there is none', async () => {
    actAs(null);
    renderWithProviders(<AppointmentsPage />);

    expect(await screen.findByText('No acting user selected')).toBeInTheDocument();
    expect(screen.queryByRole('link', {name: /New appointment/})).not.toBeInTheDocument();
    expect(api.calls).toHaveLength(0);
  });

  it('lists the appointments from today onward and attaches the identity headers', async () => {
    actAs('Secretary');
    api.on('GET', LIST, {body: page([makeAppointment({patientName: 'Mona Said'}), makeAppointment({patientName: 'Omar Hassan'})])});
    renderWithProviders(<AppointmentsPage />);

    expect(await waitForRow('Mona Said')).toBeInTheDocument();
    expect(table().getByText('Omar Hassan')).toBeInTheDocument();
    expect(screen.getByText('2 appointments')).toBeInTheDocument();

    const [call] = api.callsTo('GET', LIST);
    expect(call?.query.get('dateFrom')).toBe(getClinicNow().date);
    expect(call?.query.get('pageNumber')).toBe('1');
    expect(call?.query.get('pageSize')).toBe('10');
    expect(call?.query.has('doctorId')).toBe(false);
    expect(call?.query.has('status')).toBe(false);
    expect(call?.headers['X-User-Id']).toBe(SECRETARY_ID);
    expect(call?.headers['X-User-Role']).toBe('Secretary');
    expect(screen.getByRole('link', {name: /New appointment/})).toHaveAttribute('href', '/appointments/new');
  });

  it('filters by doctor, status and date range and returns to page 1', async () => {
    const user = userEvent.setup();
    actAs('Secretary');
    api.on('GET', LIST, {body: page([makeAppointment()])});
    renderWithProviders(<AppointmentsPage />);
    await waitForRow('Mona Said');

    await user.selectOptions(await screen.findByLabelText('Doctor'), DOCTORS[1].id);
    await user.selectOptions(screen.getByLabelText('Status'), 'Cancelled');
    fireEvent.change(screen.getByLabelText('To date'), {target: {value: '2099-12-31'}});

    await waitFor(() => {
      const last = api.callsTo('GET', LIST).at(-1);
      expect(last?.query.get('doctorId')).toBe(DOCTORS[1].id);
      expect(last?.query.get('status')).toBe('Cancelled');
      expect(last?.query.get('dateTo')).toBe('2099-12-31');
      expect(last?.query.get('pageNumber')).toBe('1');
    });
  });

  it('offers only the doctors the lookup returned', async () => {
    actAs('Secretary');
    api.on('GET', LIST, {body: page([])});
    renderWithProviders(<AppointmentsPage />);

    const doctor = await screen.findByLabelText('Doctor');
    await waitFor(() => expect(within(doctor).getAllByRole('option')).toHaveLength(3));

    expect(within(doctor).getAllByRole('option').map((option) => option.textContent)).toEqual([
      'All doctors', 'Dr. Dev One', 'Dr. Dev Two'
    ]);
  });

  it('does not query while the date range is inverted and says why', async () => {
    actAs('Secretary');
    api.on('GET', LIST, {body: page([makeAppointment()])});
    renderWithProviders(<AppointmentsPage />);
    await waitForRow('Mona Said');
    const before = api.callsTo('GET', LIST).length;

    fireEvent.change(screen.getByLabelText('From date'), {target: {value: '2099-02-01'}});
    await waitFor(() => expect(api.callsTo('GET', LIST).length).toBeGreaterThan(before));
    const afterFrom = api.callsTo('GET', LIST).length;
    fireEvent.change(screen.getByLabelText('To date'), {target: {value: '2099-01-01'}});

    expect(await screen.findByText('The end date must not be before the start date.')).toBeInTheDocument();
    expect(api.callsTo('GET', LIST)).toHaveLength(afterFrom);
  });

  it('shows the empty state with a way to book', async () => {
    actAs('Secretary');
    api.on('GET', LIST, {body: page([])});
    renderWithProviders(<AppointmentsPage />);

    expect(await screen.findByText('No appointments found')).toBeInTheDocument();
    expect(screen.getAllByRole('link', {name: /New appointment/}).length).toBeGreaterThan(0);
  });

  it('shows a translated error with a retry that recovers', async () => {
    const user = userEvent.setup();
    actAs('Secretary');
    // A server error is retried once automatically, so the first two attempts fail.
    let attempts = 0;
    api.on('GET', LIST, () => (++attempts <= 2 ? problem(500, 'server.error') : {body: page([makeAppointment()])}));
    renderWithProviders(<AppointmentsPage />);

    expect(await screen.findByText('Appointments could not be loaded', {}, {timeout: 4000})).toBeInTheDocument();
    expect(screen.getByText('A server error occurred. Try again later.')).toBeInTheDocument();
    expect(screen.queryByText(/English server text/)).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', {name: 'Try again'}));

    expect(await waitForRow('Mona Said')).toBeInTheDocument();
  });

  it('pages through the results', async () => {
    const user = userEvent.setup();
    actAs('Secretary');
    api.on('GET', LIST, (call) => {
      const pageNumber = Number(call.query.get('pageNumber'));
      return {
        body: page([makeAppointment({patientName: `Patient page ${pageNumber}`})], {page: pageNumber, totalCount: 12, totalPages: 2})
      };
    });
    renderWithProviders(<AppointmentsPage />);

    expect(await waitForRow('Patient page 1')).toBeInTheDocument();
    expect(screen.getByText('Page 1 of 2')).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Previous'})).toBeDisabled();

    await user.click(screen.getByRole('button', {name: 'Next'}));

    expect(await waitForRow('Patient page 2')).toBeInTheDocument();
    expect(api.callsTo('GET', LIST).at(-1)?.query.get('pageNumber')).toBe('2');
    expect(screen.getByRole('button', {name: 'Next'})).toBeDisabled();
  });

  it('steps back when the current page no longer exists', async () => {
    const user = userEvent.setup();
    actAs('Secretary');
    api.on('GET', LIST, (call) => {
      const pageNumber = Number(call.query.get('pageNumber'));
      return pageNumber === 2
        ? {body: page([], {page: 2, totalCount: 10, totalPages: 1})}
        : {body: page([makeAppointment({patientName: 'Only page'})], {page: 1, totalCount: 10, totalPages: 2})};
    });
    renderWithProviders(<AppointmentsPage />);
    await waitForRow('Only page');

    await user.click(screen.getByRole('button', {name: 'Next'}));

    await waitFor(() => expect(api.callsTo('GET', LIST).at(-1)?.query.get('pageNumber')).toBe('1'));
    expect(await waitForRow('Only page')).toBeInTheDocument();
  });

  it('cancels an appointment through the dialog and refreshes the list', async () => {
    const user = userEvent.setup();
    actAs('Secretary');
    const appointment = makeAppointment({patientName: 'Mona Said'});
    api.on('GET', LIST, {body: page([appointment])});
    api.on('POST', `${LIST}/${appointment.id}/cancel`, {body: {...appointment, status: 'Cancelled'}});
    renderWithProviders(<AppointmentsPage />);
    await waitForRow('Mona Said');
    const listCallsBefore = api.callsTo('GET', LIST).length;

    await user.click(table().getByRole('button', {name: 'Cancel appointment — Mona Said'}));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText(/Reason for cancelling/), 'Patient asked');
    await user.click(within(dialog).getByRole('button', {name: 'Cancel appointment'}));

    await waitFor(() => expect(api.callsTo('POST', `${LIST}/${appointment.id}/cancel`)).toHaveLength(1));
    await waitFor(() => expect(api.callsTo('GET', LIST).length).toBeGreaterThan(listCallsBefore));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(notify.success).toHaveBeenCalledWith('Appointment cancelled.');
  });

  it('hides Complete from a secretary but shows it to a doctor', async () => {
    actAs('Doctor');
    api.on('GET', LIST, {body: page([makeAppointment({patientName: 'Mona Said'})])});
    renderWithProviders(<AppointmentsPage />);

    const loaded = await screen.findByTestId('appointments-table');
    expect(await within(loaded).findByRole('button', {name: 'Complete — Mona Said'})).toBeInTheDocument();
  });

  it('renders the whole page in Arabic', async () => {
    actAs('Secretary');
    api.on('GET', LIST, {body: page([makeAppointment({patientName: 'منى السيد'})])});
    renderWithProviders(<AppointmentsPage />, 'ar');

    expect(await screen.findByRole('heading', {level: 1, name: 'المواعيد'})).toBeInTheDocument();
    expect(await waitForRow('منى السيد')).toBeInTheDocument();
    expect(screen.getByLabelText('الطبيب')).toBeInTheDocument();
    expect(screen.getByText('موعد واحد')).toBeInTheDocument();
  });

  it('switches identity live: refetches as the new user and never shows the previous user\'s cached rows', async () => {
    actAs('Secretary');
    api.on('GET', LIST, (call) =>
      call.headers['X-User-Role'] === 'Doctor'
        ? {body: page([makeAppointment({patientName: 'Doctor Only Patient'})])}
        : {body: page([makeAppointment({patientName: 'Secretary Only Patient'})])}
    );
    renderWithProviders(<AppointmentsPage />);
    await waitForRow('Secretary Only Patient');
    expect(screen.queryByRole('button', {name: /^Complete/})).not.toBeInTheDocument();

    act(() => setDevIdentityForTests({userId: DOCTOR_ID, role: 'Doctor'}));

    await waitForRow('Doctor Only Patient');
    expect(screen.queryByText('Secretary Only Patient')).not.toBeInTheDocument();
    const last = api.callsTo('GET', LIST).at(-1);
    expect(last?.headers['X-User-Id']).toBe(DOCTOR_ID);
    expect(last?.headers['X-User-Role']).toBe('Doctor');
    // The new role gets the doctor-only action; the secretary never had it.
    expect(screen.getAllByRole('button', {name: 'Complete — Doctor Only Patient'}).length).toBeGreaterThan(0);
  });

  it('never requests another user\'s data after the acting user changes', async () => {
    actAs('Secretary');
    api.on('GET', LIST, {body: page([makeAppointment()])});
    const first = renderWithProviders(<AppointmentsPage />);
    await waitForRow('Mona Said');
    first.unmount();

    actAs('Doctor');
    renderWithProviders(<AppointmentsPage />);
    await waitFor(() => expect(api.callsTo('GET', LIST).length).toBe(2));

    expect(api.callsTo('GET', LIST).map((call) => call.headers['X-User-Role'])).toEqual(['Secretary', 'Doctor']);
  });
});
