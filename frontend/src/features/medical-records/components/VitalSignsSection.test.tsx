import {setMedicalTestSession, clearMedicalTestSession} from '../test-session';
import {act, render, renderHook, screen, waitFor} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {QueryClient, QueryClientProvider} from '@tanstack/react-query';
import {NextIntlClientProvider} from 'next-intl';
import type {ReactNode} from 'react';
import {beforeEach, describe, expect, it, vi} from 'vitest';
import en from '@/messages/en.json';
import ar from '@/messages/ar.json';
import {ApiError} from '@/lib/api';
import {getMedicalVisit, listVitalSigns} from '../api';
import {useMedicalCredentials} from '../use-medical-credentials';
import type {VitalSignsResponse} from '../types';
import type {PagedResult} from '@/types/paging';
import {VitalSignsSection} from './VitalSignsSection';
import {MedicalVisitScreen} from './MedicalVisitScreen';

vi.mock('../api', () => ({getMedicalVisit: vi.fn(), listVitalSigns: vi.fn()}));
vi.mock('./VisitDetails', () => ({VisitDetails: () => <p>Test visit details</p>}));
vi.mock('./DiagnosisSection', () => ({DiagnosisSection: () => null}));
vi.mock('./MedicationSection', () => ({MedicationSection: () => null}));
vi.mock('@/i18n/navigation', () => ({Link: ({children}: {children: ReactNode}) => <span>{children}</span>}));

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
  return {wrapper, identity};
}
beforeEach(() => {vi.resetAllMocks();});

describe('read-only VitalSignsSection', () => {
  it('shows loading without exposing controls or prior records', () => {
    const {wrapper} = setup();
    vi.mocked(listVitalSigns).mockReturnValue(new Promise(() => {}));
    render(<VitalSignsSection visitId={visitId} />, {wrapper});
    expect(screen.getByRole('status')).toHaveTextContent('Loading vital signs');
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('shows an empty state', async () => {
    const {wrapper} = setup();
    vi.mocked(listVitalSigns).mockResolvedValue(page([]));
    render(<VitalSignsSection visitId={visitId} />, {wrapper});
    expect(await screen.findByText('No vital signs on this page.')).toBeInTheDocument();
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
  });

  it('displays measurements with correct units and no chronological or write controls', async () => {
    const {wrapper} = setup();
    vi.mocked(listVitalSigns).mockResolvedValue(page([record]));
    const {container} = render(<VitalSignsSection visitId={visitId} />, {wrapper});
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
    render(<VitalSignsSection visitId={visitId} />, {wrapper});
    await screen.findByText('Measurement record 1');
    expect(screen.getAllByText('Not provided')).toHaveLength(2);
  });

  it.each(['en', 'ar'] as const)('isolates value/unit pairs and safely displays seven null fields in %s', async locale => {
    const {wrapper} = setup(locale);
    vi.mocked(listVitalSigns).mockResolvedValue(page([record, {...record, id: 'empty-record', bloodPressure: null,
      heartRate: null, temperature: null, respiratoryRate: null, spO2: null, weight: null, height: null}]));
    const messages = (locale === 'ar' ? ar : en).MedicalRecords.vitalSigns;
    const {container} = render(<VitalSignsSection visitId={visitId} />, {wrapper});
    await waitFor(() => expect(container.querySelectorAll('bdi[dir="ltr"]')).toHaveLength(7));
    expect(screen.getAllByText(messages.notProvided)).toHaveLength(7);
    expect(container.querySelector('dd bdi')?.textContent).toContain(messages.bloodPressureUnit);
    expect(container.querySelector('form')).toBeNull();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('shows an error and retries a network failure', async () => {
    const {wrapper} = setup();
    vi.mocked(listVitalSigns).mockRejectedValue(new ApiError({kind: 'network', status: 0}));
    render(<VitalSignsSection visitId={visitId} />, {wrapper});
    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load vital signs');
    vi.mocked(listVitalSigns).mockResolvedValue(page([record]));
    await userEvent.click(screen.getByRole('button', {name: 'Retry'}));
    await screen.findByText('Measurement record 1');
  });

  it('does not retry forbidden access or show records', async () => {
    const {wrapper} = setup();
    vi.mocked(listVitalSigns).mockRejectedValue(new ApiError({kind: 'forbidden', status: 403}));
    render(<VitalSignsSection visitId={visitId} />, {wrapper});
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
    render(<VitalSignsSection visitId={visitId} />, {wrapper});
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
    const {container} = render(<VitalSignsSection visitId={visitId} />, {wrapper});
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
