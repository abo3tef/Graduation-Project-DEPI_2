import {screen, waitFor, within} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type {ComponentProps} from 'react';
import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {resetAuthHeadersProviders} from '@/lib/api';
import {PatientMedicalStatusScreen} from './PatientMedicalStatusScreen';
import {
  actAs,
  DOCTOR_ID,
  FakeApi,
  makeProfile,
  makeVisit,
  page,
  PATIENT_ID,
  problem,
  renderWithProviders,
  type RecordedCall
} from '../test/test-utils';

vi.mock('@/i18n/navigation', () => ({
  Link: ({href, children, ...rest}: ComponentProps<'a'> & {href: string}) => (
    <a href={href} {...rest}>
      {children}
    </a>
  )
}));

let api: FakeApi;

const PROFILE_PATH = `/api/patients/${PATIENT_ID}/medical-profile`;
const HISTORY_PATH = `/api/patients/${PATIENT_ID}/medical-visits`;

const PROFILE = makeProfile();
const VISIT = makeVisit();

beforeEach(() => {
  api = new FakeApi();
  api.install();
});

afterEach(() => {
  resetAuthHeadersProviders();
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
});

function visitPages(call: RecordedCall) {
  const pageNumber = Number(call.query.get('pageNumber') ?? '1');

  return pageNumber === 1
    ? {body: page([VISIT], {totalCount: 11, totalPages: 2})}
    : {
        body: page(
          [{...VISIT, visitId: 'd0000002-0000-4000-8000-000000000002', chiefComplaint: 'Follow-up'}],
          {page: 2, totalCount: 11, totalPages: 2}
        )
      };
}

describe('PatientMedicalStatusScreen', () => {
  it('shows an invalid-id panel and never sends a request for a malformed id', () => {
    actAs('Doctor');
    renderWithProviders(<PatientMedicalStatusScreen patientId="not-a-guid" />);

    expect(screen.getByText('Invalid patient link')).toBeInTheDocument();
    expect(api.calls).toHaveLength(0);
  });

  it('shows the identity gate and never sends a request without an acting user', () => {
    actAs(null);
    renderWithProviders(<PatientMedicalStatusScreen patientId={PATIENT_ID} />);

    expect(screen.getByText('No acting user selected')).toBeInTheDocument();
    expect(api.calls).toHaveLength(0);
  });

  it('blocks a secretary with the doctors-only gate and sends no medical request', () => {
    actAs('Secretary');
    renderWithProviders(<PatientMedicalStatusScreen patientId={PATIENT_ID} />);

    expect(screen.getByText('Doctors only')).toBeInTheDocument();
    expect(api.calls).toHaveLength(0);
  });

  it('loads the profile for the acting doctor with blood type and emergency contact', async () => {
    actAs('Doctor');
    api.on('GET', PROFILE_PATH, {body: PROFILE});
    renderWithProviders(<PatientMedicalStatusScreen patientId={PATIENT_ID} />);

    expect(await screen.findByRole('heading', {name: 'Mona Said'})).toBeInTheDocument();
    expect(screen.getByText('O+')).toBeInTheDocument();
    expect(screen.getByText('Husband · 01098765432')).toBeInTheDocument();

    const calls = api.callsTo('GET', PROFILE_PATH);
    expect(calls).toHaveLength(1);
    expect(calls[0].headers['X-User-Id']).toBe(DOCTOR_ID);
    expect(calls[0].headers['X-User-Role']).toBe('Doctor');
  });

  it('shows the allergies and chronic conditions tab prominently', async () => {
    actAs('Doctor');
    api.on('GET', PROFILE_PATH, {body: PROFILE});
    renderWithProviders(<PatientMedicalStatusScreen patientId={PATIENT_ID} />);

    await screen.findByRole('heading', {name: 'Mona Said'});
    await userEvent.click(screen.getByRole('tab', {name: /Allergies & conditions/}));

    const allergies = await screen.findByTestId('allergy-list');
    expect(within(allergies).getByText('Penicillin')).toBeInTheDocument();
    expect(within(allergies).getByText('Severe')).toBeInTheDocument();
    expect(screen.getByTestId('condition-list')).toHaveTextContent('Hypertension');
  });

  it('renders a visit with all of its children and links to the F4 visit page', async () => {
    actAs('Doctor');
    api.on('GET', PROFILE_PATH, {body: PROFILE});
    api.on('GET', HISTORY_PATH, visitPages);
    renderWithProviders(<PatientMedicalStatusScreen patientId={PATIENT_ID} />);

    await screen.findByRole('heading', {name: 'Mona Said'});
    await userEvent.click(screen.getByRole('tab', {name: /Visit history/}));

    expect(await screen.findByTestId('visit-history')).toBeInTheDocument();
    expect(screen.getByText('Tension headache')).toBeInTheDocument();
    expect(screen.getByText('Paracetamol')).toBeInTheDocument();
    expect(screen.getByTestId('visit-vitals')).toHaveTextContent('120/80');
    expect(screen.getByTestId('visit-notes')).toHaveTextContent('Recheck in one week.');
    expect(screen.getByTestId('visit-labs')).toHaveTextContent('CBC');

    const link = screen.getByTestId('visit-link');
    expect(link).toHaveAttribute('href', `/medical-records/visits/${VISIT.visitId}`);
  });

  it('pages the visit history and requests the next page', async () => {
    actAs('Doctor');
    api.on('GET', PROFILE_PATH, {body: PROFILE});
    api.on('GET', HISTORY_PATH, visitPages);
    renderWithProviders(<PatientMedicalStatusScreen patientId={PATIENT_ID} />);

    await screen.findByRole('heading', {name: 'Mona Said'});
    await userEvent.click(screen.getByRole('tab', {name: /Visit history/}));

    expect(await screen.findByText('Page 1 of 2')).toBeInTheDocument();
    expect(api.callsTo('GET', HISTORY_PATH)[0].query.get('pageNumber')).toBe('1');

    await userEvent.click(screen.getByRole('button', {name: 'Next'}));

    await screen.findByText('Follow-up');
    const pageCalls = api.callsTo('GET', HISTORY_PATH);
    expect(pageCalls[pageCalls.length - 1].query.get('pageNumber')).toBe('2');
  });

  it('shows the empty state when the patient has no visits', async () => {
    actAs('Doctor');
    api.on('GET', PROFILE_PATH, {body: PROFILE});
    api.on('GET', HISTORY_PATH, {body: page([], {totalPages: 0})});
    renderWithProviders(<PatientMedicalStatusScreen patientId={PATIENT_ID} />);

    await screen.findByRole('heading', {name: 'Mona Said'});
    await userEvent.click(screen.getByRole('tab', {name: /Visit history/}));

    expect(await screen.findByText('No previous visits for this patient yet.')).toBeInTheDocument();
  });

  it('maps a 404 to the patient-not-available panel without leaking server text', async () => {
    actAs('Doctor');
    api.on('GET', PROFILE_PATH, problem(404, 'patient.not_found'));
    renderWithProviders(<PatientMedicalStatusScreen patientId={PATIENT_ID} />);

    expect(await screen.findByText('Patient not available')).toBeInTheDocument();
    expect(screen.queryByText(/English server text/)).not.toBeInTheDocument();
  });

  it('offers a retry after a server failure', async () => {
    actAs('Doctor');
    api.on('GET', PROFILE_PATH, problem(500, 'server.error'));
    renderWithProviders(<PatientMedicalStatusScreen patientId={PATIENT_ID} />);

    // The query retries once after ~1s, so allow for that before asserting.
    expect(
      await screen.findByText('The server had a problem. Try again later.', {}, {timeout: 4000})
    ).toBeInTheDocument();

    api.on('GET', PROFILE_PATH, {body: PROFILE});
    await userEvent.click(screen.getByRole('button', {name: 'Try again'}));

    await waitFor(() => {
      expect(screen.getByRole('heading', {name: 'Mona Said'})).toBeInTheDocument();
    });
  }, 10000);

  it('renders Arabic labels and the same request flow in the Arabic locale', async () => {
    actAs('Doctor');
    api.on('GET', PROFILE_PATH, {body: PROFILE});
    renderWithProviders(<PatientMedicalStatusScreen patientId={PATIENT_ID} />, 'ar');

    expect(await screen.findByText('فصيلة الدم')).toBeInTheDocument();
    expect(screen.getByText('O+')).toBeInTheDocument();
    expect(screen.getByRole('tab', {name: /الحساسية/})).toBeInTheDocument();
  });
});
