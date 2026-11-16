import {setMedicalTestSession, clearMedicalTestSession} from '../test-session';
import {act, fireEvent, render, renderHook, screen, waitFor, within} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {QueryClient, QueryClientProvider} from '@tanstack/react-query';
import {NextIntlClientProvider} from 'next-intl';
import type {ReactNode} from 'react';
import {beforeEach, describe, expect, it, vi} from 'vitest';
import en from '@/messages/en.json';
import ar from '@/messages/ar.json';
import {notify} from '@/lib/utils/toast';
import {ApiError} from '@/lib/api';
import {createVitalSigns, updateVitalSigns, deleteDiagnosis, getMedicalVisit, listVitalSigns} from '../api';
import {medicalRecordKeys} from '../keys';
import {useMedicalCredentials} from '../use-medical-credentials';
import type {VitalSignsResponse} from '../types';
import type {PagedResult} from '@/types/paging';
import {VitalSignsSection} from './VitalSignsSection';
import {MedicalVisitScreen} from './MedicalVisitScreen';

vi.mock('../api', () => ({getMedicalVisit: vi.fn(), listVitalSigns: vi.fn(), createVitalSigns: vi.fn(), updateVitalSigns: vi.fn(), deleteDiagnosis: vi.fn(), createDiagnosis: vi.fn(), updateDiagnosis: vi.fn()}));
vi.mock('@/lib/utils/toast', () => ({notify: {success: vi.fn(), error: vi.fn()}}));
vi.mock('./VisitDetails', () => ({VisitDetails: () => <p>Test visit details</p>}));
vi.mock('./MedicationSection', () => ({MedicationSection: () => null}));
vi.mock('@/i18n/navigation', () => ({Link: ({children}: {children: ReactNode}) => <span>{children}</span>}));

const doctorId = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const visitId = '11111111-1111-4111-8111-111111111111';
const record: VitalSignsResponse = {id: 'record', bloodPressure: '120/80', heartRate: 72,
  temperature: 37, respiratoryRate: 16, spO2: 98, weight: 70, height: 175};
function page(items: VitalSignsResponse[], number = 1, totalPages = 1): PagedResult<VitalSignsResponse> {
  return {items, page: number, pageSize: 10, totalCount: totalPages > 1 ? 11 : items.length, totalPages};
}
function setup(locale: 'en' | 'ar' = 'en') {
  const client = new QueryClient({defaultOptions: {queries: {retryDelay: 0}}});
  function wrapper({children}: {children: ReactNode}) {
    return <QueryClientProvider client={client}><NextIntlClientProvider locale={locale}
      messages={locale === 'ar' ? ar : en} onError={error => {throw error;}}>{children}</NextIntlClientProvider></QueryClientProvider>;
  }
  const identity = renderHook(() => useMedicalCredentials());
  act(() => {clearMedicalTestSession(); setMedicalTestSession('section-test-token');});
  return {wrapper, identity, client};
}
beforeEach(() => {vi.resetAllMocks();});

describe('read-only VitalSignsSection', () => {
  it('shows loading without exposing controls or prior records', () => {
    const {wrapper} = setup();
    vi.mocked(listVitalSigns).mockReturnValue(new Promise(() => {}));
    render(<VitalSignsSection visitDoctorId={doctorId} visitId={visitId} />, {wrapper});
    expect(screen.getByRole('status')).toHaveTextContent('Loading vital signs');
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('shows an empty state', async () => {
    const {wrapper} = setup();
    vi.mocked(listVitalSigns).mockResolvedValue(page([]));
    render(<VitalSignsSection visitDoctorId={doctorId} visitId={visitId} />, {wrapper});
    expect(await screen.findByText('No vital signs on this page.')).toBeInTheDocument();
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
  });

  it('displays measurements with correct units and no chronological or write controls', async () => {
    const {wrapper} = setup();
    vi.mocked(listVitalSigns).mockResolvedValue(page([record]));
    const {container} = render(<VitalSignsSection visitDoctorId={doctorId} visitId={visitId} />, {wrapper});
    await screen.findByText('Measurement record 1');
    for (const text of ['120/80 mmHg', '72 bpm', '37 °C', '16 breaths/min', '98 %', '70 kg', '175 cm']) {
      expect(Array.from(container.querySelectorAll('dd')).some(node => node.textContent === text)).toBe(true);
    }
    expect(container.textContent).not.toMatch(/latest|most recent/i);
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(container.querySelector('form')).toBeNull();
  });

  it('renders explicit null placeholders', async () => {
    const {wrapper} = setup();
    vi.mocked(listVitalSigns).mockResolvedValue(page([{...record, bloodPressure: null, temperature: null}]));
    render(<VitalSignsSection visitDoctorId={doctorId} visitId={visitId} />, {wrapper});
    await screen.findByText('Measurement record 1');
    expect(screen.getAllByText('Not provided')).toHaveLength(2);
  });

  it.each(['en', 'ar'] as const)('isolates value/unit pairs and safely displays seven null fields in %s', async locale => {
    const {wrapper} = setup(locale);
    vi.mocked(listVitalSigns).mockResolvedValue(page([record, {...record, id: 'empty-record', bloodPressure: null,
      heartRate: null, temperature: null, respiratoryRate: null, spO2: null, weight: null, height: null}]));
    const messages = (locale === 'ar' ? ar : en).MedicalRecords.vitalSigns;
    const {container} = render(<VitalSignsSection visitDoctorId={doctorId} visitId={visitId} />, {wrapper});
    await waitFor(() => expect(container.querySelectorAll('bdi[dir="ltr"]')).toHaveLength(7));
    expect(screen.getAllByText(messages.notProvided)).toHaveLength(7);
    expect(container.querySelector('dd bdi')?.textContent).toContain(messages.bloodPressureUnit);
    expect(container.querySelector('form')).toBeNull();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('shows an error and retries a network failure', async () => {
    const {wrapper} = setup();
    vi.mocked(listVitalSigns).mockRejectedValue(new ApiError({kind: 'network', status: 0}));
    render(<VitalSignsSection visitDoctorId={doctorId} visitId={visitId} />, {wrapper});
    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load vital signs');
    vi.mocked(listVitalSigns).mockResolvedValue(page([record]));
    await userEvent.click(screen.getByRole('button', {name: 'Retry'}));
    await screen.findByText('Measurement record 1');
  });

  it('does not retry forbidden access or show records', async () => {
    const {wrapper} = setup();
    vi.mocked(listVitalSigns).mockRejectedValue(new ApiError({kind: 'forbidden', status: 403}));
    render(<VitalSignsSection visitDoctorId={doctorId} visitId={visitId} />, {wrapper});
    await screen.findByRole('alert');
    expect(listVitalSigns).toHaveBeenCalledOnce();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(screen.queryByText('120/80')).not.toBeInTheDocument();
  });

  it('paginates using the hook and hides the previous page while loading', async () => {
    const {wrapper} = setup();
    let resolvePage!: (value: PagedResult<VitalSignsResponse>) => void;
    vi.mocked(listVitalSigns).mockImplementation((_id, _token, number) => number === 2
      ? new Promise(resolve => {resolvePage = resolve;}) : Promise.resolve(page([record], 1, 2)));
    render(<VitalSignsSection visitDoctorId={doctorId} visitId={visitId} />, {wrapper});
    await screen.findByText('Page 1 of 2');
    expect(screen.getByRole('button', {name: 'Previous'})).toBeDisabled();
    await userEvent.click(screen.getByRole('button', {name: 'Next'}));
    await waitFor(() => expect(listVitalSigns).toHaveBeenCalledWith(visitId, 'section-test-token', 2, 10, expect.any(AbortSignal)));
    expect(screen.queryByText('120/80')).not.toBeInTheDocument();
    await act(async () => {resolvePage(page([{...record, id: 'second', heartRate: 80}], 2, 2));});
    await screen.findByText('Page 2 of 2');
    expect(screen.getByRole('button', {name: 'Next'})).toBeDisabled();
    await userEvent.click(screen.getByRole('button', {name: 'Previous'}));
    await screen.findByText('Page 1 of 2');
  });

  it('renders Arabic labels and removes sensitive section data when token is cleared', async () => {
    const {wrapper} = setup('ar');
    vi.mocked(listVitalSigns).mockResolvedValue(page([record]));
    const {container} = render(<VitalSignsSection visitDoctorId={doctorId} visitId={visitId} />, {wrapper});
    await screen.findByText('ضغط الدم');
    expect(container.textContent).toContain('ملم زئبق');
    act(() => {clearMedicalTestSession();});
    expect(container).toBeEmptyDOMElement();
  });

  it('mounts only after visit access succeeds and hides visit plus vitals on token removal', async () => {
    const {wrapper} = setup();
    vi.mocked(getMedicalVisit).mockResolvedValue({id: visitId, patientId: 'patient', doctorId: 'doctor',
      appointmentId: null, visitDate: '2026-11-15T10:00:00Z', chiefComplaint: null, symptoms: null,
      clinicalNotes: null, diagnoses: [], medications: [], vitalSigns: []});
    vi.mocked(listVitalSigns).mockResolvedValue(page([record]));
    render(<MedicalVisitScreen visitId={visitId} />, {wrapper});
    await screen.findByText('Test visit details');
    await screen.findByText('Measurement record 1');
    act(() => {clearMedicalTestSession();});
    expect(screen.queryByText('Test visit details')).not.toBeInTheDocument();
    expect(screen.queryByText('120/80')).not.toBeInTheDocument();
  });

  it('does not mount the section or fetch vitals when the visit is forbidden', async () => {
    const {wrapper} = setup();
    vi.mocked(getMedicalVisit).mockRejectedValue(new ApiError({kind: 'forbidden', status: 403}));
    render(<MedicalVisitScreen visitId={visitId} />, {wrapper});
    await screen.findByRole('alert');
    expect(listVitalSigns).not.toHaveBeenCalled();
  });
});

function setupOwner(locale: 'en' | 'ar' = 'en') {
  const result = setup(locale);
  act(() => {setMedicalTestSession('owning-doctor', {id: doctorId, doctorId});});
  return result;
}

describe('owning-doctor VitalSignsSection integration', () => {
  it.each(['en', 'ar'] as const)('mounts one editor, disables paging, cancels and restores focus in %s', async locale => {
    const {wrapper} = setupOwner(locale);
    const t = (locale === 'ar' ? ar : en).MedicalRecords;
    vi.mocked(listVitalSigns).mockResolvedValue(page([record], 1, 2));
    render(<VitalSignsSection visitId={visitId} visitDoctorId={doctorId} />, {wrapper});
    await userEvent.click(await screen.findByRole('button', {name: t.vitalSigns.addAction}));
    expect(screen.getByRole('heading', {name: t.vitalSigns.createTitle})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: t.vitalSigns.addAction})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: t.vitalSigns.editLabel.replace('{number}', '1')})).not.toBeInTheDocument();
    expect(screen.getByRole('button', {name: t.vitalSigns.next})).toBeDisabled();
    await userEvent.click(screen.getByRole('button', {name: t.common.cancel}));
    expect(screen.getByRole('button', {name: t.vitalSigns.addAction})).toHaveFocus();
    const editLabel = t.vitalSigns.editLabel.replace('{number}', '1');
    await userEvent.click(screen.getByRole('button', {name: editLabel}));
    expect(screen.getByLabelText(t.vitalSigns.fields.heartRate)).toHaveValue('72');
    await userEvent.click(screen.getByRole('button', {name: t.common.cancel}));
    expect(screen.getByRole('button', {name: editLabel})).toHaveFocus();
    expect(createVitalSigns).not.toHaveBeenCalled();
  });

  it.each([
    {role: 'Secretary' as const}, {role: 'Admin' as const}, {doctorId: null},
    {id: 'invalid'}, {id: visitId, doctorId: visitId}
  ])('keeps authorized reads without write controls for %j', async identity => {
    const {wrapper} = setupOwner();
    act(() => {setMedicalTestSession('restricted', {id: doctorId, doctorId, ...identity});});
    vi.mocked(listVitalSigns).mockResolvedValue(page([record]));
    render(<VitalSignsSection visitId={visitId} visitDoctorId={doctorId} />, {wrapper});
    await screen.findByText('Measurement record 1');
    expect(screen.queryByRole('button', {name: 'Add vital signs'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Edit measurement record 1'})).not.toBeInTheDocument();
  });

  it('does not render or request data after logout', () => {
    const {wrapper} = setupOwner();
    act(() => {clearMedicalTestSession();});
    const {container} = render(<VitalSignsSection visitId={visitId} visitDoctorId={doctorId} />, {wrapper});
    expect(container).toBeEmptyDOMElement();
    expect(listVitalSigns).not.toHaveBeenCalled();
  });

  it('creates through the existing mutation and previews a saved record absent from the current page', async () => {
    const {wrapper} = setupOwner();
    vi.mocked(listVitalSigns).mockResolvedValue(page([], 1, 2));
    vi.mocked(createVitalSigns).mockResolvedValue({...record, id: 'off-page', heartRate: 84});
    render(<VitalSignsSection visitId={visitId} visitDoctorId={doctorId} />, {wrapper});
    await userEvent.click(await screen.findByRole('button', {name: 'Add vital signs'}));
    await userEvent.type(screen.getByLabelText('Heart rate'), '84');
    await userEvent.click(screen.getByRole('button', {name: 'Save vital signs'}));
    const preview = await screen.findByRole('complementary', {name: 'Saved measurement'});
    expect(preview.textContent).toContain('84 bpm');
    expect(preview.textContent).toContain('not on the current page');
    expect(preview.textContent).not.toMatch(/latest/i);
    expect(screen.getByRole('button', {name: 'Add vital signs'})).toHaveFocus();
    expect(createVitalSigns).toHaveBeenCalledOnce();
    expect(vi.mocked(listVitalSigns).mock.calls.every(call => call[2] === 1)).toBe(true);
    vi.mocked(listVitalSigns).mockResolvedValue(page([{...record, id: 'off-page', heartRate: 84}], 2, 2));
    await userEvent.click(screen.getByRole('button', {name: 'Next'}));
    await screen.findByText('Measurement record 11');
    expect(screen.queryByRole('complementary')).not.toBeInTheDocument();
  });

  it('edits and refreshes the current page without adding a duplicate preview', async () => {
    const {wrapper} = setupOwner();
    let current = record;
    vi.mocked(listVitalSigns).mockImplementation(async () => page([current]));
    vi.mocked(updateVitalSigns).mockImplementation(async () => {
      current = {...record, heartRate: 85};
      return current;
    });
    render(<VitalSignsSection visitId={visitId} visitDoctorId={doctorId} />, {wrapper});
    await userEvent.click(await screen.findByRole('button', {name: 'Edit measurement record 1'}));
    await userEvent.clear(screen.getByLabelText('Heart rate'));
    await userEvent.type(screen.getByLabelText('Heart rate'), '85');
    await userEvent.click(screen.getByRole('button', {name: 'Update vital signs'}));
    await waitFor(() => expect(screen.queryByLabelText('Heart rate')).not.toBeInTheDocument());
    expect(screen.getByText('85', {exact: false})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Edit measurement record 1'})).toHaveFocus();
    expect(screen.queryByRole('complementary')).not.toBeInTheDocument();
    expect(updateVitalSigns).toHaveBeenCalledWith(visitId, record.id, expect.objectContaining({heartRate: 85}), 'owning-doctor');
  });

  it('retains failed-save values and does not overwrite drafts during a background refetch', async () => {
    const {wrapper, client} = setupOwner();
    vi.mocked(listVitalSigns).mockResolvedValue(page([record]));
    vi.mocked(updateVitalSigns).mockRejectedValue(new ApiError({kind: 'validation', status: 400, fieldErrors: {heartRate: ['Invalid']}}));
    render(<VitalSignsSection visitId={visitId} visitDoctorId={doctorId} />, {wrapper});
    await userEvent.click(await screen.findByRole('button', {name: 'Edit measurement record 1'}));
    await userEvent.clear(screen.getByLabelText('Heart rate'));
    await userEvent.type(screen.getByLabelText('Heart rate'), '86');
    vi.mocked(listVitalSigns).mockResolvedValue(page([{...record, heartRate: 95}]));
    await act(async () => {await client.invalidateQueries();});
    expect(screen.getByLabelText('Heart rate')).toHaveValue('86');
    await userEvent.click(screen.getByRole('button', {name: 'Update vital signs'}));
    await screen.findByText(en.MedicalRecords.vitalSigns.validation.serverRejected);
    expect(screen.getByLabelText('Heart rate')).toHaveValue('86');
    expect(screen.queryByRole('button', {name: 'Add vital signs'})).not.toBeInTheDocument();
  });

  it('resets an editor and preview on visit or ownership changes', async () => {
    const {wrapper} = setupOwner();
    vi.mocked(listVitalSigns).mockResolvedValue(page([record]));
    const {rerender} = render(<VitalSignsSection visitId={visitId} visitDoctorId={doctorId} />, {wrapper});
    await userEvent.click(await screen.findByRole('button', {name: 'Add vital signs'}));
    await userEvent.type(screen.getByLabelText('Heart rate'), '90');
    rerender(<VitalSignsSection visitId={doctorId} visitDoctorId={doctorId} />);
    await screen.findByRole('button', {name: 'Add vital signs'});
    expect(screen.queryByLabelText('Heart rate')).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', {name: 'Add vital signs'}));
    expect(screen.getByLabelText('Heart rate')).toHaveValue('');
    rerender(<VitalSignsSection visitId={doctorId} visitDoctorId={visitId} />);
    await screen.findByText('Measurement record 1');
    expect(screen.queryByLabelText('Heart rate')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Add vital signs'})).not.toBeInTheDocument();
  });

  it('ignores an old save after logout and re-login while a new editor is open', async () => {
    const {wrapper} = setupOwner();
    let finish!: (value: VitalSignsResponse) => void;
    vi.mocked(listVitalSigns).mockResolvedValue(page([record]));
    vi.mocked(createVitalSigns).mockReturnValue(new Promise(resolve => {finish = resolve;}));
    const {container} = render(<VitalSignsSection visitId={visitId} visitDoctorId={doctorId} />, {wrapper});
    await userEvent.click(await screen.findByRole('button', {name: 'Add vital signs'}));
    fireEvent.change(screen.getByLabelText('Heart rate'), {target: {value: '80'}});
    fireEvent.submit(container.querySelector('form')!);
    await waitFor(() => expect(createVitalSigns).toHaveBeenCalledOnce());
    act(() => {clearMedicalTestSession(); setMedicalTestSession('new-session', {id: doctorId, doctorId});});
    await userEvent.click(await screen.findByRole('button', {name: 'Add vital signs'}));
    await userEvent.type(screen.getByLabelText('Heart rate'), '88');
    await act(async () => {finish({...record, id: 'stale', heartRate: 80});});
    expect(screen.getByLabelText('Heart rate')).toHaveValue('88');
    expect(screen.queryByRole('complementary')).not.toBeInTheDocument();
  });

  it('preserves the F4-v2 diagnosis delete flow after a vitals save invalidates the visit', async () => {
    const {wrapper} = setupOwner();
    let diagnoses = [{id: 'diagnosis', visitId, patientId: 'patient', diagnosisName: 'Synthetic diagnosis',
      description: null, diagnosisDate: '2026-11-16'}];
    vi.mocked(getMedicalVisit).mockImplementation(async () => ({id: visitId, patientId: 'patient', doctorId,
      appointmentId: null, visitDate: '2026-11-16T10:00:00Z', chiefComplaint: null, symptoms: null,
      clinicalNotes: null, diagnoses, medications: [], vitalSigns: []}));
    vi.mocked(listVitalSigns).mockResolvedValue(page([]));
    vi.mocked(createVitalSigns).mockResolvedValue(record);
    vi.mocked(deleteDiagnosis).mockImplementation(async () => {diagnoses = [];});
    render(<MedicalVisitScreen visitId={visitId} />, {wrapper});
    await userEvent.click(await screen.findByRole('button', {name: 'Add vital signs'}));
    await userEvent.type(screen.getByLabelText('Heart rate'), '80');
    await userEvent.click(screen.getByRole('button', {name: 'Save vital signs'}));
    await screen.findByRole('button', {name: 'Add vital signs'});
    expect(vi.mocked(getMedicalVisit).mock.calls.length).toBeGreaterThan(1);
    await userEvent.click(screen.getByRole('button', {name: 'Delete: Synthetic diagnosis'}));
    const dialog = screen.getByRole('dialog');
    await userEvent.click(within(dialog).getByRole('button', {name: en.MedicalRecords.diagnosis.deleteConfirm}));
    await waitFor(() => expect(screen.queryByText('Synthetic diagnosis')).not.toBeInTheDocument());
    expect(deleteDiagnosis).toHaveBeenCalledWith(visitId, 'diagnosis');
    expect(screen.getByRole('button', {name: 'Add vital signs'})).toBeInTheDocument();
  });
});

it.each(['visit', 'session', 'ownership'] as const)('clears a saved-record preview when %s changes', async change => {
  const {wrapper} = setupOwner();
  vi.mocked(listVitalSigns).mockResolvedValue(page([]));
  vi.mocked(createVitalSigns).mockResolvedValue({...record, id: 'saved-off-page'});
  const {rerender} = render(<VitalSignsSection visitId={visitId} visitDoctorId={doctorId} />, {wrapper});
  await userEvent.click(await screen.findByRole('button', {name: 'Add vital signs'}));
  await userEvent.type(screen.getByLabelText('Heart rate'), '80');
  await userEvent.click(screen.getByRole('button', {name: 'Save vital signs'}));
  await screen.findByRole('complementary', {name: 'Saved measurement'});
  if (change === 'session') act(() => {setMedicalTestSession('replacement', {id: doctorId, doctorId});});
  else rerender(<VitalSignsSection visitId={change === 'visit' ? doctorId : visitId}
    visitDoctorId={change === 'ownership' ? visitId : doctorId} />);
  await screen.findByText('No vital signs on this page.');
  expect(screen.queryByRole('complementary')).not.toBeInTheDocument();
});

it('does not let a save from the previous visit affect a new visit editor', async () => {
  const {wrapper} = setupOwner();
  let finish!: (value: VitalSignsResponse) => void;
  vi.mocked(listVitalSigns).mockResolvedValue(page([record]));
  vi.mocked(createVitalSigns).mockReturnValue(new Promise(resolve => {finish = resolve;}));
  const {container, rerender} = render(<VitalSignsSection visitId={visitId} visitDoctorId={doctorId} />, {wrapper});
  await userEvent.click(await screen.findByRole('button', {name: 'Add vital signs'}));
  fireEvent.change(screen.getByLabelText('Heart rate'), {target: {value: '80'}});
  fireEvent.submit(container.querySelector('form')!);
  await waitFor(() => expect(createVitalSigns).toHaveBeenCalledOnce());
  rerender(<VitalSignsSection visitId={doctorId} visitDoctorId={doctorId} />);
  await userEvent.click(await screen.findByRole('button', {name: 'Add vital signs'}));
  await userEvent.type(screen.getByLabelText('Heart rate'), '89');
  await act(async () => {finish({...record, id: 'previous-visit'});});
  expect(screen.getByLabelText('Heart rate')).toHaveValue('89');
  expect(screen.queryByRole('complementary')).not.toBeInTheDocument();
  expect(notify.success).not.toHaveBeenCalled();
});

it('never renders shell commands in the Vital Signs section', async () => {
  const {wrapper} = setupOwner();
  vi.mocked(listVitalSigns).mockResolvedValue(page([record]));
  const {container} = render(<VitalSignsSection visitId={visitId} visitDoctorId={doctorId} />, {wrapper});
  await screen.findByRole('button', {name: 'Add vital signs'});
  expect(container.textContent).not.toContain('git branch --show-current');
});

function loadedVisit() {
  return {id: visitId, patientId: 'patient', doctorId, appointmentId: null,
    visitDate: '2026-11-16T10:00:00Z', chiefComplaint: null, symptoms: null,
    clinicalNotes: null, diagnoses: [], medications: [], vitalSigns: []};
}

describe('MedicalVisitScreen background read lifecycle', () => {
  it.each([
    ['create', 'network', 0], ['edit', 'network', 0],
    ['create', 'server', 500], ['edit', 'server', 503]
  ] as const)('preserves the %s draft after a background %s/%i failure and retry', async (mode, kind, status) => {
    const {wrapper, client} = setupOwner();
    vi.mocked(getMedicalVisit).mockResolvedValue(loadedVisit());
    vi.mocked(listVitalSigns).mockResolvedValue(page([record]));
    const {container} = render(<MedicalVisitScreen visitId={visitId} />, {wrapper});
    await userEvent.click(await screen.findByRole('button', {
      name: mode === 'create' ? 'Add vital signs' : 'Edit measurement record 1'
    }));
    const input = screen.getByLabelText('Heart rate');
    await userEvent.clear(input);
    await userEvent.type(input, '87');
    const form = container.querySelector('form');
    vi.mocked(getMedicalVisit).mockRejectedValue(new ApiError({kind, status}));
    await act(async () => {await client.invalidateQueries({queryKey: medicalRecordKeys.visit(visitId)});});
    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(en.MedicalRecords.states[kind === 'network' ? 'networkTitle' : 'serverTitle']);
    expect(screen.getByText('Test visit details')).toBeInTheDocument();
    expect(screen.getByLabelText('Heart rate')).toBe(input);
    expect(input).toHaveValue('87');
    expect(container.querySelector('form')).toBe(form);
    const retry = within(alert).getByRole('button', {name: en.MedicalRecords.states.retry});
    const previousRequests = vi.mocked(getMedicalVisit).mock.calls.length;
    vi.mocked(getMedicalVisit).mockResolvedValue(loadedVisit());
    await userEvent.click(retry);
    await waitFor(() => expect(screen.queryByRole('alert')).not.toBeInTheDocument());
    expect(vi.mocked(getMedicalVisit).mock.calls.length).toBeGreaterThan(previousRequests);
    expect(screen.getByLabelText('Heart rate')).toBe(input);
    expect(input).toHaveValue('87');
    expect(createVitalSigns).not.toHaveBeenCalled();
    expect(updateVitalSigns).not.toHaveBeenCalled();
  });

  it.each([
    ['unauthorized', 401], ['forbidden', 403], ['not-found', 404]
  ] as const)('hides cached visit and draft after background %s/%i', async (kind, status) => {
    const {wrapper, client} = setupOwner();
    vi.mocked(getMedicalVisit).mockResolvedValue(loadedVisit());
    vi.mocked(listVitalSigns).mockResolvedValue(page([record]));
    render(<MedicalVisitScreen visitId={visitId} />, {wrapper});
    await userEvent.click(await screen.findByRole('button', {name: 'Edit measurement record 1'}));
    await userEvent.type(screen.getByLabelText('Heart rate'), '9');
    vi.mocked(getMedicalVisit).mockRejectedValue(new ApiError({kind, status}));
    await act(async () => {await client.invalidateQueries({queryKey: medicalRecordKeys.visit(visitId)});});
    await screen.findByRole('alert');
    expect(screen.queryByText('Test visit details')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Heart rate')).not.toBeInTheDocument();
    expect(screen.queryByText('120/80')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Add vital signs'})).not.toBeInTheDocument();
  });

  it('retains the full error screen when the first read fails', async () => {
    const {wrapper} = setupOwner();
    vi.mocked(getMedicalVisit).mockRejectedValue(new ApiError({kind: 'network', status: 0}));
    render(<MedicalVisitScreen visitId={visitId} />, {wrapper});
    await screen.findByRole('alert');
    expect(screen.queryByText('Test visit details')).not.toBeInTheDocument();
    expect(listVitalSigns).not.toHaveBeenCalled();
    expect(screen.getByRole('button', {name: en.MedicalRecords.states.retry})).toBeInTheDocument();
  });

  it.each(['logout', 'account'] as const)('hides cached data on %s after a failed background read', async change => {
    const {wrapper, client} = setupOwner();
    vi.mocked(getMedicalVisit).mockResolvedValue(loadedVisit());
    vi.mocked(listVitalSigns).mockResolvedValue(page([record]));
    render(<MedicalVisitScreen visitId={visitId} />, {wrapper});
    await userEvent.click(await screen.findByRole('button', {name: 'Add vital signs'}));
    await userEvent.type(screen.getByLabelText('Heart rate'), '88');
    vi.mocked(getMedicalVisit).mockRejectedValue(new ApiError({kind: 'network', status: 0}));
    await act(async () => {await client.invalidateQueries({queryKey: medicalRecordKeys.visit(visitId)});});
    await screen.findByRole('alert');
    act(() => {
      if (change === 'logout') clearMedicalTestSession();
      else setMedicalTestSession('another-account', {id: visitId, doctorId: visitId});
    });
    expect(screen.queryByText('Test visit details')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Heart rate')).not.toBeInTheDocument();
    expect(screen.queryByText('120/80')).not.toBeInTheDocument();
  });
});

it.each(['Add vital signs', 'Edit measurement record 1'])('moves focus into the editor opened by %s', async action => {
  const {wrapper} = setupOwner();
  vi.mocked(listVitalSigns).mockResolvedValue(page([record]));
  render(<VitalSignsSection visitId={visitId} visitDoctorId={doctorId} />, {wrapper});
  await userEvent.click(await screen.findByRole('button', {name: action}));
  expect(screen.getByLabelText('Blood pressure')).toHaveFocus();
  await userEvent.click(screen.getByRole('button', {name: 'Cancel'}));
  expect(screen.getByRole('button', {name: action})).toHaveFocus();
});
