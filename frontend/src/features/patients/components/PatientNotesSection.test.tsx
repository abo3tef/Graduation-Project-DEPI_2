import {screen, waitFor, within} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type {ComponentProps} from 'react';
import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {resetAuthHeadersProviders} from '@/lib/api';
import {PatientNotesSection} from './PatientNotesSection';
import {
  actAs,
  DOCTOR_ID,
  FakeApi,
  makePatientNote,
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

const NOTES_PATH = `/api/patients/${PATIENT_ID}/notes`;

const LINKED = makePatientNote();
const UNLINKED = makePatientNote({
  id: 'f0000003-0000-4000-8000-000000000003',
  visitId: null,
  noteContent: 'Phone follow-up note.'
});

function notePages(call: RecordedCall) {
  const pageNumber = Number(call.query.get('pageNumber') ?? '1');

  return pageNumber === 1
    ? {body: page([LINKED, UNLINKED], {totalCount: 11, totalPages: 2})}
    : {
        body: page(
          [makePatientNote({id: 'f0000004-0000-4000-8000-000000000004', noteContent: 'Second page note.', visitId: null})],
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

describe('PatientNotesSection', () => {
  it('sends no request and renders no data without an acting doctor', () => {
    // The gate itself lives on the status screen; standalone, the section
    // must simply never fetch and never show data.
    actAs(null);
    renderWithProviders(<PatientNotesSection patientId={PATIENT_ID} />);

    expect(api.calls).toHaveLength(0);
    expect(screen.queryByTestId('patient-notes')).not.toBeInTheDocument();
  });

  it('sends no request as a secretary', () => {
    actAs('Secretary');
    renderWithProviders(<PatientNotesSection patientId={PATIENT_ID} />);

    expect(api.calls).toHaveLength(0);
    expect(screen.queryByTestId('patient-notes')).not.toBeInTheDocument();
  });

  it('renders notes in API order with doctor and date', async () => {
    actAs('Doctor');
    api.on('GET', NOTES_PATH, notePages);
    const {container} = renderWithProviders(<PatientNotesSection patientId={PATIENT_ID} />);

    const contents = await screen.findAllByTestId('note-content');
    expect(contents).toHaveLength(2);
    expect(contents[0]).toHaveTextContent(LINKED.noteContent);
    expect(contents[1]).toHaveTextContent('Phone follow-up note.');
    expect(container.textContent).toContain('Dr. Dev One');
    // Q-04 defaults are sent with the request.
    const calls = api.callsTo('GET', NOTES_PATH);
    expect(calls[0].query.get('pageNumber')).toBe('1');
    expect(calls[0].query.get('pageSize')).toBe('10');
    expect(calls[0].headers['X-User-Id']).toBe(DOCTOR_ID);
  });

  it('links a visit-linked note and badges an unlinked one', async () => {
    actAs('Doctor');
    api.on('GET', NOTES_PATH, notePages);
    renderWithProviders(<PatientNotesSection patientId={PATIENT_ID} />);

    const cards = await screen.findAllByTestId('note-content');
    const linkedCard = cards[0].closest('article');
    const unlinkedCard = cards[1].closest('article');
    expect(linkedCard).not.toBeNull();
    expect(unlinkedCard).not.toBeNull();

    const linkedLink = within(linkedCard as HTMLElement).getByTestId('visit-link');
    expect(linkedLink).toHaveAttribute('href', `/medical-records/visits/${LINKED.visitId}`);
    expect(within(unlinkedCard as HTMLElement).queryByTestId('visit-link')).not.toBeInTheDocument();
    expect(within(unlinkedCard as HTMLElement).getByTestId('unlinked-badge')).toHaveTextContent(
      'Not linked to a visit'
    );
  });

  it('pages to the next page of notes', async () => {
    actAs('Doctor');
    api.on('GET', NOTES_PATH, notePages);
    renderWithProviders(<PatientNotesSection patientId={PATIENT_ID} />);

    expect(await screen.findByText('Page 1 of 2')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', {name: 'Next'}));

    expect(await screen.findByText('Second page note.')).toBeInTheDocument();
    const calls = api.callsTo('GET', NOTES_PATH);
    expect(calls[calls.length - 1].query.get('pageNumber')).toBe('2');
  });

  it('shows the empty state when the patient has no notes', async () => {
    actAs('Doctor');
    api.on('GET', NOTES_PATH, {body: page([], {totalPages: 0})});
    renderWithProviders(<PatientNotesSection patientId={PATIENT_ID} />);

    expect(await screen.findByText('No clinical notes for this patient yet.')).toBeInTheDocument();
  });

  it('maps errors to friendly keys and supports retry', async () => {
    actAs('Doctor');
    api.on('GET', NOTES_PATH, problem(403, 'access.forbidden'));
    renderWithProviders(<PatientNotesSection patientId={PATIENT_ID} />);

    expect(
      await screen.findByText('You are not allowed to view this data.', {}, {timeout: 4000})
    ).toBeInTheDocument();
    expect(screen.queryByText(/English server text/)).not.toBeInTheDocument();

    api.on('GET', NOTES_PATH, notePages);
    await userEvent.click(screen.getByRole('button', {name: 'Try again'}));

    await waitFor(() => {
      expect(screen.getAllByTestId('note-content').length).toBeGreaterThan(0);
    });
  }, 10000);

  it('renders Arabic labels in the Arabic locale', async () => {
    actAs('Doctor');
    api.on('GET', NOTES_PATH, notePages);
    renderWithProviders(<PatientNotesSection patientId={PATIENT_ID} />, 'ar');

    expect(await screen.findByText('غير مرتبطة بزيارة')).toBeInTheDocument();
  });
});
