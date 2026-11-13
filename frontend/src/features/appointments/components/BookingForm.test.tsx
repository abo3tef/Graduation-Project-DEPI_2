import {fireEvent, screen, waitFor, within} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type {ComponentProps} from 'react';
import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {resetAuthHeadersProviders} from '@/lib/api';
import {
  actAs,
  DOCTORS,
  FakeApi,
  makeAppointment,
  page,
  PATIENTS,
  problem,
  renderWithProviders,
  SECRETARY_ID
} from '../test/test-utils';
import {BookingForm} from './BookingForm';

const push = vi.hoisted(() => vi.fn());
vi.mock('@/i18n/navigation', () => ({
  Link: ({href, children, ...rest}: ComponentProps<'a'> & {href: string}) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
  useRouter: () => ({push}),
  usePathname: () => '/appointments/new'
}));

const notify = vi.hoisted(() => ({success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn()}));
vi.mock('@/lib/utils/toast', () => ({notify}));

const LIST = '/api/appointments';
const DOCTOR_LOOKUP = '/api/appointments/lookups/doctors';
const PATIENT_LOOKUP = '/api/appointments/lookups/patients';
const DAY = '2099-01-02';

let api: FakeApi;

beforeEach(() => {
  api = new FakeApi();
  api.install();
  api.on('GET', DOCTOR_LOOKUP, {body: DOCTORS});
  api.on('GET', PATIENT_LOOKUP, (call) => {
    const text = (call.query.get('search') ?? '').toLowerCase();
    return {body: PATIENTS.filter((patient) => patient.fullName.toLowerCase().includes(text))};
  });
  api.on('GET', LIST, {body: page([])});
  actAs('Secretary');
});

afterEach(() => {
  resetAuthHeadersProviders();
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
  push.mockReset();
  notify.success.mockReset();
});

const doctorSelect = () => screen.getByRole('combobox', {name: /^Doctor/});
const patientInput = () => screen.getByRole('combobox', {name: /^Patient/});
const bookButton = () => screen.getByRole('button', {name: 'Book appointment'});

async function chooseDoctor(user: ReturnType<typeof userEvent.setup>, index = 0) {
  await waitFor(() => expect(within(doctorSelect()).getAllByRole('option').length).toBeGreaterThan(1));
  await user.selectOptions(doctorSelect(), DOCTORS[index].id);
}

async function choosePatient(user: ReturnType<typeof userEvent.setup>) {
  await user.type(patientInput(), 'mo');
  await screen.findByRole('option', {name: /Mona Said/});
  await user.keyboard('{ArrowDown}{Enter}');
  await waitFor(() => expect(patientInput()).toHaveValue('Mona Said'));
}

function fillSchedule(date = DAY, start = '09:00', end = '09:30') {
  fireEvent.change(screen.getByLabelText(/^Date/), {target: {value: date}});
  fireEvent.change(screen.getByLabelText(/Start time/), {target: {value: start}});
  fireEvent.change(screen.getByLabelText(/End time/), {target: {value: end}});
}

describe('BookingForm', () => {
  it('asks for an acting user and requests nothing while there is none', async () => {
    actAs(null);
    renderWithProviders(<BookingForm />);

    expect(await screen.findByText('No acting user selected')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Book appointment'})).not.toBeInTheDocument();
    expect(api.calls).toHaveLength(0);
  });

  it('keeps the patient search disabled until a doctor is chosen, then enables it', async () => {
    const user = userEvent.setup();
    renderWithProviders(<BookingForm />);

    expect(await screen.findByText('Select a doctor first.')).toBeInTheDocument();
    expect(patientInput()).toBeDisabled();

    await chooseDoctor(user);

    await waitFor(() => expect(patientInput()).toBeEnabled());
    expect(screen.getByText('Type at least 2 characters to search.')).toBeInTheDocument();
  });

  it('pre-selects the doctor when there is exactly one', async () => {
    api.on('GET', DOCTOR_LOOKUP, {body: [DOCTORS[0]]});
    renderWithProviders(<BookingForm />);

    await waitFor(() => expect(doctorSelect()).toHaveValue(DOCTORS[0].id));
    expect(patientInput()).toBeEnabled();
  });

  it('searches only the chosen doctor\'s patients and picks one with the keyboard', async () => {
    const user = userEvent.setup();
    renderWithProviders(<BookingForm />);
    await chooseDoctor(user);

    await user.type(await waitFor(() => (patientInput() as HTMLInputElement)), 'mo');
    const option = await screen.findByRole('option', {name: /Mona Said/});
    expect(option).toBeInTheDocument();
    expect(patientInput()).toHaveAttribute('aria-expanded', 'true');

    await user.keyboard('{ArrowDown}');
    expect(patientInput()).toHaveAttribute('aria-activedescendant', option.id);
    await user.keyboard('{Enter}');

    expect(patientInput()).toHaveValue('Mona Said');
    expect(patientInput()).toHaveAttribute('readonly');
    const search = api.callsTo('GET', PATIENT_LOOKUP).at(-1);
    expect(search?.query.get('doctorId')).toBe(DOCTORS[0].id);
    expect(search?.query.get('search')).toBe('mo');
    expect(search?.query.get('limit')).toBe('10');
    expect(api.callsTo('POST', LIST)).toHaveLength(0); // choosing with Enter must not submit the form
  });

  it('shows when nothing matches, and resets the patient when the doctor changes', async () => {
    const user = userEvent.setup();
    renderWithProviders(<BookingForm />);
    await chooseDoctor(user, 0);

    await user.type(patientInput(), 'zzz');
    expect(await screen.findByText('No matching patients for this doctor.')).toBeInTheDocument();

    await user.clear(patientInput());
    await choosePatient(user);
    await user.selectOptions(doctorSelect(), DOCTORS[1].id);

    expect(patientInput()).toHaveValue('');
    expect(patientInput()).not.toHaveAttribute('readonly');
  });

  it('shows every missing field at once and moves focus to the first one', async () => {
    const user = userEvent.setup();
    renderWithProviders(<BookingForm />);
    await waitFor(() => expect(within(doctorSelect()).getAllByRole('option').length).toBeGreaterThan(1));

    await user.click(bookButton());

    expect((await screen.findAllByText('This field is required.')).length).toBeGreaterThanOrEqual(5);
    expect(doctorSelect()).toHaveFocus();
    expect(doctorSelect()).toBeInvalid();
    expect(api.callsTo('POST', LIST)).toHaveLength(0);
  });

  it('has no default end time: only the quick buttons compute one, from the start time', async () => {
    const user = userEvent.setup();
    renderWithProviders(<BookingForm />);
    await screen.findByLabelText(/End time/);

    expect(screen.getByLabelText(/End time/)).toHaveValue('');
    expect(screen.getByRole('button', {name: '30 min'})).toBeDisabled();

    fireEvent.change(screen.getByLabelText(/Start time/), {target: {value: '09:00'}});
    await user.click(screen.getByRole('button', {name: '45 min'}));

    expect(screen.getByLabelText(/End time/)).toHaveValue('09:45');
  });

  it('rejects an end time that is not after the start and a past date, before sending', async () => {
    const user = userEvent.setup();
    renderWithProviders(<BookingForm />);
    await chooseDoctor(user);
    await choosePatient(user);

    fillSchedule(DAY, '10:00', '09:00');
    await user.click(bookButton());
    expect(await screen.findByText('The end time must be after the start time.')).toBeInTheDocument();

    fillSchedule('2001-01-01', '09:00', '09:30');
    await user.click(bookButton());
    expect(await screen.findByText('The time cannot be in the past.')).toBeInTheDocument();
    expect(api.callsTo('POST', LIST)).toHaveLength(0);
  });

  it('books with normalized times and a trimmed reason, notifies and returns to the list', async () => {
    const user = userEvent.setup();
    api.on('POST', LIST, (call) => ({status: 201, body: makeAppointment({...(call.body as object)})}));
    renderWithProviders(<BookingForm />);
    await chooseDoctor(user);
    await choosePatient(user);
    fillSchedule(DAY, '09:00', '09:30');
    await user.type(screen.getByLabelText(/Reason for the visit/), '  checkup  ');

    await user.click(bookButton());

    await waitFor(() => expect(push).toHaveBeenCalledWith('/appointments'));
    const [call] = api.callsTo('POST', LIST);
    expect(call?.body).toEqual({
      patientId: PATIENTS[0].id,
      doctorId: DOCTORS[0].id,
      appointmentDate: DAY,
      startTime: '09:00:00',
      endTime: '09:30:00',
      reason: 'checkup'
    });
    expect(call?.headers['X-User-Id']).toBe(SECRETARY_ID);
    expect(call?.headers['X-User-Role']).toBe('Secretary');
    expect(notify.success).toHaveBeenCalledWith('Appointment booked.');
  });

  it('sends a blank reason as null', async () => {
    const user = userEvent.setup();
    api.on('POST', LIST, {status: 201, body: makeAppointment()});
    renderWithProviders(<BookingForm />);
    await chooseDoctor(user);
    await choosePatient(user);
    fillSchedule();

    await user.click(bookButton());

    await waitFor(() => expect(api.callsTo('POST', LIST)).toHaveLength(1));
    expect((api.callsTo('POST', LIST)[0]?.body as {reason: unknown}).reason).toBeNull();
  });

  it('explains an overlap returned by the server without leaving the form', async () => {
    const user = userEvent.setup();
    api.on('POST', LIST, problem(409, 'appointment.overlap'));
    renderWithProviders(<BookingForm />);
    await chooseDoctor(user);
    await choosePatient(user);
    fillSchedule();

    await user.click(bookButton());

    expect(await screen.findByText('The doctor already has an appointment that overlaps the selected time.')).toBeInTheDocument();
    expect(screen.queryByText(/English server text/)).not.toBeInTheDocument();
    expect(push).not.toHaveBeenCalled();
    expect(bookButton()).toBeEnabled();
  });

  it('maps server field errors onto the fields and focuses the first', async () => {
    const user = userEvent.setup();
    api.on(
      'POST',
      LIST,
      problem(400, 'validation.failed', {
        errors: {endTime: ['required']},
        errorCodes: {endTime: ['NotNullValidator']}
      })
    );
    renderWithProviders(<BookingForm />);
    await chooseDoctor(user);
    await choosePatient(user);
    fillSchedule();

    await user.click(bookButton());

    await waitFor(() => expect(screen.getByLabelText(/End time/)).toHaveFocus());
    expect(screen.getByLabelText(/End time/)).toBeInvalid();
  });

  it.each([
    ['patient.not_found', 404, 'The patient does not exist or does not belong to this doctor.'],
    ['doctor.unavailable', 409, 'This doctor is not available for booking right now.'],
    ['appointment.in_the_past', 400, 'An appointment cannot be booked in the past.'],
    ['identity.invalid', 403, 'The current user could not be identified. Select the user and role.']
  ])('shows a translated message for %s', async (code, status, text) => {
    const user = userEvent.setup();
    api.on('POST', LIST, problem(status, code));
    renderWithProviders(<BookingForm />);
    await chooseDoctor(user);
    await choosePatient(user);
    fillSchedule();

    await user.click(bookButton());

    expect(await screen.findByText(text)).toBeInTheDocument();
  });

  describe('the doctor\'s day', () => {
    it('lists that day\'s appointments for the chosen doctor and date', async () => {
      const user = userEvent.setup();
      api.on('GET', LIST, {
        body: page([
          makeAppointment({patientName: 'Existing One', startTime: '09:00:00', endTime: '09:30:00'}),
          makeAppointment({patientName: 'Freed Slot', startTime: '11:00:00', endTime: '11:30:00', status: 'Cancelled'})
        ])
      });
      renderWithProviders(<BookingForm />);
      expect(await screen.findByText('Choose a doctor and a date to see the booked appointments.')).toBeInTheDocument();

      await chooseDoctor(user);
      fireEvent.change(screen.getByLabelText(/^Date/), {target: {value: DAY}});

      expect(await screen.findByText('Existing One')).toBeInTheDocument();
      expect(screen.getByText('Freed Slot')).toBeInTheDocument();
      expect(screen.getByText('Cancelled or no-show: this time is free.')).toBeInTheDocument();
      const day = api.callsTo('GET', LIST).at(-1);
      expect(day?.query.get('doctorId')).toBe(DOCTORS[0].id);
      expect(day?.query.get('dateFrom')).toBe(DAY);
      expect(day?.query.get('dateTo')).toBe(DAY);
      expect(day?.query.get('pageSize')).toBe('50');
    });

    it('warns about an overlap with the same rule as the server, and not for back-to-back or freed slots', async () => {
      const user = userEvent.setup();
      api.on('GET', LIST, {
        body: page([
          makeAppointment({startTime: '09:00:00', endTime: '09:30:00', status: 'Scheduled'}),
          makeAppointment({startTime: '11:00:00', endTime: '11:30:00', status: 'Cancelled'})
        ])
      });
      renderWithProviders(<BookingForm />);
      await chooseDoctor(user);
      fireEvent.change(screen.getByLabelText(/^Date/), {target: {value: DAY}});
      await screen.findAllByText(/9:00/);
      const warning = 'The selected time overlaps an existing appointment.';

      fillSchedule(DAY, '09:15', '09:45');
      expect(await screen.findByText(warning)).toBeInTheDocument();

      fillSchedule(DAY, '09:30', '10:00'); // adjacent: allowed
      await waitFor(() => expect(screen.queryByText(warning)).not.toBeInTheDocument());

      fillSchedule(DAY, '11:00', '11:30'); // cancelled slot: free
      await waitFor(() => expect(screen.queryByText(warning)).not.toBeInTheDocument());
    });

    it('shows an empty day and a translated error', async () => {
      const user = userEvent.setup();
      renderWithProviders(<BookingForm />);
      await chooseDoctor(user);
      fireEvent.change(screen.getByLabelText(/^Date/), {target: {value: DAY}});
      expect(await screen.findByText('No appointments on this day.')).toBeInTheDocument();

      api.on('GET', LIST, problem(403, 'identity.invalid'));
      fireEvent.change(screen.getByLabelText(/^Date/), {target: {value: '2099-01-03'}});
      expect(await screen.findByText('The current user could not be identified. Select the user and role.')).toBeInTheDocument();
    });
  });

  it('tells the secretary when she has no doctors to book for', async () => {
    api.on('GET', DOCTOR_LOOKUP, {body: []});
    renderWithProviders(<BookingForm />);

    expect(await screen.findByText('No doctors available for booking')).toBeInTheDocument();
    expect(bookButton()).toBeInTheDocument();
  });

  it('renders the form in Arabic', async () => {
    renderWithProviders(<BookingForm />, 'ar');

    expect(await screen.findByRole('heading', {level: 1, name: 'حجز موعد جديد'})).toBeInTheDocument();
    expect(screen.getByRole('combobox', {name: /^المريض/})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'حجز الموعد'})).toBeInTheDocument();
    expect(screen.getByLabelText(/^وقت الانتهاء/)).toBeInTheDocument();
  });
});
