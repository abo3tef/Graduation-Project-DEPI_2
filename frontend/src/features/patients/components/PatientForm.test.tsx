import {fireEvent, screen, waitFor} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type {ComponentProps} from 'react';
import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {resetAuthHeadersProviders} from '@/lib/api';
import {
  actAs,
  DOCTORS,
  DOCTOR_ID,
  FakeApi,
  makePatient,
  OTHER_DOCTOR_ID,
  problem,
  renderWithProviders
} from '../test/test-utils';
import {PatientForm} from './PatientForm';

const push = vi.hoisted(() => vi.fn());
vi.mock('@/i18n/navigation', () => ({
  Link: ({href, children, ...rest}: ComponentProps<'a'> & {href: string}) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
  useRouter: () => ({push}),
  usePathname: () => '/patients/new'
}));

const notify = vi.hoisted(() => ({success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn()}));
vi.mock('@/lib/utils/toast', () => ({notify}));

const LIST = '/api/patients';
const DOCTOR_LOOKUP = '/api/appointments/lookups/doctors';

let api: FakeApi;

beforeEach(() => {
  api = new FakeApi();
  api.install();
  api.on('GET', DOCTOR_LOOKUP, {body: DOCTORS});
  api.on('POST', LIST, {status: 201, body: makePatient()});
});

afterEach(() => {
  resetAuthHeadersProviders();
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
  push.mockReset();
  notify.success.mockReset();
});

async function fillRequired(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText(/^Full name/), 'Sara Ali');
  fireEvent.change(screen.getByLabelText(/^Date of birth/), {target: {value: '1990-04-12'}});
  await user.selectOptions(screen.getByLabelText(/^Gender/), 'Female');
  await user.type(screen.getByLabelText(/^Phone/), '01001234567');
}

describe('PatientForm as a doctor', () => {
  beforeEach(() => actAs('Doctor'));

  it('shows the blood type and no doctor picker, and sends the blood type without a doctorId', async () => {
    const user = userEvent.setup();
    renderWithProviders(<PatientForm />);

    expect(await screen.findByLabelText(/^Blood type/)).toBeInTheDocument();
    expect(screen.queryByLabelText(/^Doctor/)).not.toBeInTheDocument();
    expect(screen.queryByText(/administrative details only/i)).not.toBeInTheDocument();

    await fillRequired(user);
    await user.selectOptions(screen.getByLabelText(/^Blood type/), 'OPositive');
    await user.click(screen.getByRole('button', {name: 'Save patient'}));

    await waitFor(() => expect(push).toHaveBeenCalledWith('/patients'));
    const body = api.callsTo('POST', LIST)[0]?.body as Record<string, unknown>;
    expect(body).toMatchObject({
      fullName: 'Sara Ali',
      dateOfBirth: '1990-04-12',
      gender: 'Female',
      phone: '01001234567',
      address: null,
      emergencyContact: null,
      bloodType: 'OPositive'
    });
    expect('doctorId' in body).toBe(false);
    expect(notify.success).toHaveBeenCalledWith('Patient added.');
    expect(api.callsTo('GET', DOCTOR_LOOKUP)).toHaveLength(0);
  });

  it('sends a null blood type when none is chosen', async () => {
    const user = userEvent.setup();
    renderWithProviders(<PatientForm />);
    await fillRequired(user);

    await user.click(screen.getByRole('button', {name: 'Save patient'}));

    await waitFor(() => expect(api.callsTo('POST', LIST)).toHaveLength(1));
    expect((api.callsTo('POST', LIST)[0]?.body as Record<string, unknown>).bloodType).toBeNull();
  });

  it('edits a patient: prefilled values, blood type included, PUT to the patient route', async () => {
    const patient = makePatient({fullName: 'Old Name', bloodType: 'BNegative', gender: 'Male'});
    api.on('PUT', `${LIST}/${patient.id}`, {body: patient});
    const user = userEvent.setup();
    renderWithProviders(<PatientForm patient={patient} />);

    expect(screen.getByLabelText(/^Full name/)).toHaveValue('Old Name');
    expect(screen.getByLabelText(/^Blood type/)).toHaveValue('BNegative');
    expect(screen.getByLabelText(/^Gender/)).toHaveValue('Male');

    await user.clear(screen.getByLabelText(/^Full name/));
    await user.type(screen.getByLabelText(/^Full name/), 'New Name');
    await user.click(screen.getByRole('button', {name: 'Save patient'}));

    await waitFor(() => expect(api.callsTo('PUT', `${LIST}/${patient.id}`)).toHaveLength(1));
    expect(api.callsTo('PUT', `${LIST}/${patient.id}`)[0]?.body).toMatchObject({
      fullName: 'New Name',
      bloodType: 'BNegative'
    });
    expect(notify.success).toHaveBeenCalledWith('Patient updated.');
  });
});

describe('PatientForm as a secretary (Q-18: administrative fields only)', () => {
  beforeEach(() => actAs('Secretary'));

  it('hides the blood type, explains the restriction and requires a doctor', async () => {
    const user = userEvent.setup();
    renderWithProviders(<PatientForm />);

    expect(await screen.findByText(/administrative details only/i)).toBeInTheDocument();
    expect(screen.queryByLabelText(/Blood type/)).not.toBeInTheDocument();
    expect(screen.queryByText('Not recorded')).not.toBeInTheDocument();

    await fillRequired(user);
    await user.click(screen.getByRole('button', {name: 'Save patient'}));

    expect(await screen.findByText('This field is required.')).toBeInTheDocument();
    expect(api.callsTo('POST', LIST)).toHaveLength(0);
  });

  it('creates for the chosen assigned doctor and never sends a blood type', async () => {
    const user = userEvent.setup();
    renderWithProviders(<PatientForm />);
    await screen.findByRole('option', {name: 'Dr. Dev Two'});

    await user.selectOptions(screen.getByLabelText(/^Doctor/), OTHER_DOCTOR_ID);
    await fillRequired(user);
    await user.click(screen.getByRole('button', {name: 'Save patient'}));

    await waitFor(() => expect(push).toHaveBeenCalledWith('/patients'));
    const body = api.callsTo('POST', LIST)[0]?.body as Record<string, unknown>;
    expect(body.doctorId).toBe(OTHER_DOCTOR_ID);
    expect('bloodType' in body).toBe(false);
  });

  it('pre-selects the only assigned doctor', async () => {
    api.on('GET', DOCTOR_LOOKUP, {body: [DOCTORS[0]]});
    const user = userEvent.setup();
    renderWithProviders(<PatientForm />);

    await waitFor(() => expect(screen.getByLabelText(/^Doctor/)).toHaveValue(DOCTOR_ID));
    await fillRequired(user);
    await user.click(screen.getByRole('button', {name: 'Save patient'}));

    await waitFor(() => expect(api.callsTo('POST', LIST)).toHaveLength(1));
    expect((api.callsTo('POST', LIST)[0]?.body as Record<string, unknown>).doctorId).toBe(DOCTOR_ID);
  });

  it('edits without a doctor picker or blood type and sends neither', async () => {
    const patient = makePatient({bloodType: null});
    api.on('PUT', `${LIST}/${patient.id}`, {body: patient});
    const user = userEvent.setup();
    renderWithProviders(<PatientForm patient={patient} />);

    expect(screen.queryByLabelText(/^Doctor/)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/Blood type/)).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', {name: 'Save patient'}));

    await waitFor(() => expect(api.callsTo('PUT', `${LIST}/${patient.id}`)).toHaveLength(1));
    const body = api.callsTo('PUT', `${LIST}/${patient.id}`)[0]?.body as Record<string, unknown>;
    expect('bloodType' in body).toBe(false);
    expect('doctorId' in body).toBe(false);
    expect(api.callsTo('GET', DOCTOR_LOOKUP)).toHaveLength(0);
  });
});

describe('PatientForm validation and errors', () => {
  beforeEach(() => actAs('Doctor'));

  it('blocks an empty submit with field messages and focuses the first invalid field', async () => {
    const user = userEvent.setup();
    renderWithProviders(<PatientForm />);

    await user.click(screen.getByRole('button', {name: 'Save patient'}));

    expect(await screen.findByText('Review the fields marked with errors.')).toBeInTheDocument();
    expect(screen.getAllByText('This field is required.')).toHaveLength(3);
    expect(screen.getByLabelText(/^Full name/)).toHaveFocus();
    expect(screen.getByLabelText(/^Full name/)).toHaveAttribute('aria-invalid', 'true');
    expect(api.callsTo('POST', LIST)).toHaveLength(0);
  });

  it('rejects a future birth date and a malformed phone before sending', async () => {
    const user = userEvent.setup();
    renderWithProviders(<PatientForm />);
    await fillRequired(user);
    fireEvent.change(screen.getByLabelText(/^Date of birth/), {target: {value: '2999-01-01'}});
    await user.clear(screen.getByLabelText(/^Phone/));
    await user.type(screen.getByLabelText(/^Phone/), 'abc');

    await user.click(screen.getByRole('button', {name: 'Save patient'}));

    expect(await screen.findByText('The date of birth cannot be in the future.')).toBeInTheDocument();
    expect(screen.getByText('Enter a valid phone number (7 digits at least).')).toBeInTheDocument();
    expect(api.callsTo('POST', LIST)).toHaveLength(0);
  });

  it('maps server field errors back onto the fields without showing English server text', async () => {
    api.on(
      'POST',
      LIST,
      problem(400, 'validation.failed', {errors: {phone: ['English phone problem']}, errorCodes: {phone: ['x']}})
    );
    const user = userEvent.setup();
    renderWithProviders(<PatientForm />);
    await fillRequired(user);

    await user.click(screen.getByRole('button', {name: 'Save patient'}));

    expect(await screen.findByText('Some values are not valid. Review the form.')).toBeInTheDocument();
    expect(screen.getByLabelText(/^Phone/)).toHaveAttribute('aria-invalid', 'true');
    expect(screen.queryByText(/English/)).not.toBeInTheDocument();
    expect(push).not.toHaveBeenCalled();
  });

  it('shows the doctor-only message when the server refuses a medical field', async () => {
    api.on('POST', LIST, problem(403, 'patient.medical_field_forbidden'));
    const user = userEvent.setup();
    renderWithProviders(<PatientForm />);
    await fillRequired(user);

    await user.click(screen.getByRole('button', {name: 'Save patient'}));

    expect(await screen.findByText('Medical information can only be changed by a doctor.')).toBeInTheDocument();
  });

  it('renders Arabic labels, direction-neutral inputs for names and LTR for phone and date', async () => {
    renderWithProviders(<PatientForm />, 'ar');

    expect(await screen.findByLabelText(/^الاسم الكامل/)).toHaveAttribute('dir', 'auto');
    expect(screen.getByLabelText(/^الهاتف/)).toHaveAttribute('dir', 'ltr');
    expect(screen.getByLabelText(/^تاريخ الميلاد/)).toHaveAttribute('dir', 'ltr');
    expect(screen.getByLabelText(/^فصيلة الدم/)).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'حفظ المريض'})).toBeInTheDocument();
  });
});
