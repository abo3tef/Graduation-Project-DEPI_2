import {fireEvent, screen, waitFor} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type {ComponentProps} from 'react';
import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {resetAuthHeadersProviders} from '@/lib/api';
import {
  actAs,
  FakeApi,
  makePatient,
  PATIENT_OPTIONS,
  renderWithProviders
} from '@/features/patients/test/test-utils';
import {MedicalRecordsWorkspace} from './MedicalRecordsWorkspace';
import {VisitForm} from './VisitForm';

const push = vi.hoisted(() => vi.fn());
vi.mock('@/i18n/navigation', () => ({
  Link: ({href, children, ...rest}: ComponentProps<'a'> & {href: string}) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
  useRouter: () => ({push}),
  usePathname: () => '/medical-records'
}));

const LOOKUP = '/api/patients/lookup';
const VISITS = '/api/medical-visits';

let api: FakeApi;

beforeEach(() => {
  api = new FakeApi();
  api.install();
  api.on('GET', LOOKUP, (call) => {
    const text = (call.query.get('search') ?? '').toLowerCase();

    return {body: PATIENT_OPTIONS.filter((patient) => patient.fullName.toLowerCase().includes(text))};
  });
  api.on('POST', VISITS, {status: 201, body: {}});
  actAs('Doctor');
});

afterEach(() => {
  resetAuthHeadersProviders();
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
  push.mockReset();
});

const picker = () => screen.getByRole('combobox', {name: /^Patient/});

describe('F4 visit form uses the patient picker instead of a GUID', () => {
  it('has no identifier text box: the patient is searched by name and chosen', async () => {
    const user = userEvent.setup();
    renderWithProviders(<VisitForm mode="create" onSuccess={vi.fn()} />);

    expect(screen.queryByPlaceholderText(/00000000/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Patient ID/)).not.toBeInTheDocument();

    await user.type(picker(), 'mo');
    await user.click(await screen.findByRole('option', {name: /Mona Hassan/}));
    await waitFor(() => expect(picker()).toHaveValue('Mona Hassan'));
    fireEvent.change(screen.getByLabelText(/Visit date/), {target: {value: '2020-01-02T09:30'}});
    await user.click(screen.getByRole('button', {name: 'Save visit'}));

    await waitFor(() => expect(api.callsTo('POST', VISITS)).toHaveLength(1));
    expect((api.callsTo('POST', VISITS)[0]?.body as Record<string, unknown>).patientId).toBe(PATIENT_OPTIONS[1].id);
  });

  it('refuses to submit without a chosen patient and says so', async () => {
    const user = userEvent.setup();
    renderWithProviders(<VisitForm mode="create" onSuccess={vi.fn()} />);

    await user.click(screen.getByRole('button', {name: 'Save visit'}));

    expect(await screen.findByText('Select a patient.')).toBeInTheDocument();
    expect(api.callsTo('POST', VISITS)).toHaveLength(0);
  });

  it('pre-selects the patient from a ?patientId= link by loading their name', async () => {
    const patient = makePatient({fullName: 'Layla Nour'});
    api.on('GET', `/api/patients/${patient.id}`, {body: patient});
    const user = userEvent.setup();
    renderWithProviders(<VisitForm mode="create" initialPatientId={patient.id} onSuccess={vi.fn()} />);

    await waitFor(() => expect(picker()).toHaveValue('Layla Nour'));
    fireEvent.change(screen.getByLabelText(/Visit date/), {target: {value: '2020-01-02T09:30'}});
    await user.click(screen.getByRole('button', {name: 'Save visit'}));

    await waitFor(() => expect(api.callsTo('POST', VISITS)).toHaveLength(1));
    expect((api.callsTo('POST', VISITS)[0]?.body as Record<string, unknown>).patientId).toBe(patient.id);
  });

  it('does not offer a picker when editing an existing visit (the patient cannot change)', () => {
    renderWithProviders(
      <VisitForm
        mode="edit"
        initialVisit={{
          id: 'v1',
          patientId: PATIENT_OPTIONS[0].id,
          appointmentId: null,
          doctorId: 'd1',
          visitDate: '2020-01-02T09:30:00+00:00',
          chiefComplaint: null,
          symptoms: null,
          clinicalNotes: null,
          diagnoses: [],
          medications: []
        } as never}
        onSuccess={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    expect(screen.queryByRole('combobox', {name: /^Patient/})).not.toBeInTheDocument();
  });
});

describe('F4 workspace uses the patient picker instead of a GUID', () => {
  it('opens the chosen patient record directly', async () => {
    const user = userEvent.setup();
    renderWithProviders(<MedicalRecordsWorkspace />);

    expect(screen.queryByRole('button', {name: 'Open patient profile'})).not.toBeInTheDocument();

    await user.type(picker(), 'mo');
    await user.click(await screen.findByRole('option', {name: /Mona Said/}));

    expect(push).toHaveBeenCalledWith(`/medical-records/patients/${PATIENT_OPTIONS[0].id}`);
  });
});
