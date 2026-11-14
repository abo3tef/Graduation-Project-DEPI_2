import {screen, waitFor, within} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type {ComponentProps} from 'react';
import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {resetAuthHeadersProviders} from '@/lib/api';
import {LabResultsSection} from './LabResultsSection';
import {
  actAs,
  FakeApi,
  makePatientLabResult,
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

const LABS_PATH = `/api/patients/${PATIENT_ID}/lab-results`;

const LINKED = makePatientLabResult();
const UNLINKED = makePatientLabResult({
  id: 'f0000005-0000-4000-8000-000000000005',
  visitId: null,
  testName: 'Fasting glucose',
  referenceRange: '70–99',
  notes: 'Sample collected in the clinic.'
});

function labPages(call: RecordedCall) {
  const pageNumber = Number(call.query.get('pageNumber') ?? '1');

  return pageNumber === 1
    ? {body: page([LINKED, UNLINKED], {totalCount: 11, totalPages: 2})}
    : {
        body: page(
          [makePatientLabResult({id: 'f0000006-0000-4000-8000-000000000006', testName: 'TSH', visitId: null})],
          {page: 2, totalCount: 11, totalPages: 2}
        )
      };
}

beforeEach(() => {
  api = new FakeApi();
  api.install();
});

afterEach(() => {
  resetAuthHeadersProviders();
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
});

describe('LabResultsSection', () => {
  it('sends no request and renders no data without an acting doctor', () => {
    // The gate itself lives on the status screen; standalone, the section
    // must simply never fetch and never show data.
    actAs(null);
    renderWithProviders(<LabResultsSection patientId={PATIENT_ID} />);

    expect(api.calls).toHaveLength(0);
    expect(screen.queryByTestId('patient-labs')).not.toBeInTheDocument();
  });

  it('sends no request as a secretary', () => {
    actAs('Secretary');
    renderWithProviders(<LabResultsSection patientId={PATIENT_ID} />);

    expect(api.calls).toHaveLength(0);
    expect(screen.queryByTestId('patient-labs')).not.toBeInTheDocument();
  });

  it('renders results in API order with value, unit and reference range', async () => {
    actAs('Doctor');
    api.on('GET', LABS_PATH, labPages);
    renderWithProviders(<LabResultsSection patientId={PATIENT_ID} />);

    const names = await screen.findAllByTestId('lab-test-name');
    expect(names).toHaveLength(2);
    expect(names[0]).toHaveTextContent('Hemoglobin');
    expect(names[1]).toHaveTextContent('Fasting glucose');

    const values = screen.getAllByTestId('lab-value');
    expect(values[0]).toHaveTextContent('13.5 g/dL');

    expect(screen.getByText(/Reference range: 70–99/)).toBeInTheDocument();

    const calls = api.callsTo('GET', LABS_PATH);
    expect(calls[0].query.get('pageNumber')).toBe('1');
    expect(calls[0].query.get('pageSize')).toBe('10');
  });

  it('links a visit-linked result and badges an unlinked one', async () => {
    actAs('Doctor');
    api.on('GET', LABS_PATH, labPages);
    renderWithProviders(<LabResultsSection patientId={PATIENT_ID} />);

    const names = await screen.findAllByTestId('lab-test-name');
    const linkedCard = names[0].closest('article');
    const unlinkedCard = names[1].closest('article');
    expect(linkedCard).not.toBeNull();
    expect(unlinkedCard).not.toBeNull();

    const linkedLink = within(linkedCard as HTMLElement).getByTestId('visit-link');
    expect(linkedLink).toHaveAttribute('href', `/medical-records/visits/${LINKED.visitId}`);
    expect(within(unlinkedCard as HTMLElement).queryByTestId('visit-link')).not.toBeInTheDocument();
    expect(within(unlinkedCard as HTMLElement).getByTestId('unlinked-badge')).toBeInTheDocument();
  });

  it('pages to the next page of lab results', async () => {
    actAs('Doctor');
    api.on('GET', LABS_PATH, labPages);
    renderWithProviders(<LabResultsSection patientId={PATIENT_ID} />);

    expect(await screen.findByText('Page 1 of 2')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', {name: 'Next'}));

    expect(await screen.findByText('TSH')).toBeInTheDocument();
    const calls = api.callsTo('GET', LABS_PATH);
    expect(calls[calls.length - 1].query.get('pageNumber')).toBe('2');
  });

  it('shows the empty state when the patient has no lab results', async () => {
    actAs('Doctor');
    api.on('GET', LABS_PATH, {body: page([], {totalPages: 0})});
    renderWithProviders(<LabResultsSection patientId={PATIENT_ID} />);

    expect(await screen.findByText('No lab results for this patient yet.')).toBeInTheDocument();
  });

  it('maps a 404 to the not-found key without leaking server text', async () => {
    actAs('Doctor');
    api.on('GET', LABS_PATH, problem(404, 'patient.not_found'));
    renderWithProviders(<LabResultsSection patientId={PATIENT_ID} />);

    expect(await screen.findByText('The requested record was not found.', {}, {timeout: 4000})).toBeInTheDocument();
    expect(screen.queryByText(/English server text/)).not.toBeInTheDocument();

    api.on('GET', LABS_PATH, labPages);
    await userEvent.click(screen.getByRole('button', {name: 'Try again'}));

    await waitFor(() => {
      expect(screen.getAllByTestId('lab-test-name').length).toBeGreaterThan(0);
    });
  }, 10000);

  it('renders Arabic labels in the Arabic locale', async () => {
    actAs('Doctor');
    api.on('GET', LABS_PATH, labPages);
    renderWithProviders(<LabResultsSection patientId={PATIENT_ID} />, 'ar');

    // The reference range is joined into one line with the test date, so match loosely.
    expect(await screen.findByText(/المعدل الطبيعي/)).toBeInTheDocument();
    expect(screen.getByText('غير مرتبطة بزيارة')).toBeInTheDocument();
  });
});
