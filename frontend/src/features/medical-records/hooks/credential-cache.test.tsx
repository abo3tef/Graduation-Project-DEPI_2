import {setMedicalTestSession, clearMedicalTestSession} from '../test-session';
import {act, renderHook, waitFor} from '@testing-library/react';
import {QueryClient, QueryClientProvider} from '@tanstack/react-query';
import type {ReactNode} from 'react';
import {beforeEach, describe, expect, it, vi} from 'vitest';
import {getMedicalVisit, listVitalSigns, createMedicalVisit, updateMedicalVisit, createVitalSigns} from '../api';
import {readMedicalToken, useMedicalCredentials} from '../use-medical-credentials';
import {medicalRecordKeys} from '../keys';
import {useMedicalVisit} from './use-medical-visit';
import {useVitalSigns} from './use-vital-signs';
import {useCreateVisitMutation, useUpdateVisitMutation} from './use-visit-mutations';
import {useCreateVitalSignsMutation} from './use-vital-signs-mutations';
import type {MedicalVisitResponse, SaveVitalSignsRequest} from '../types';

vi.mock('../api', () => ({
  getMedicalVisit: vi.fn(), listVitalSigns: vi.fn(),
  createMedicalVisit: vi.fn(), updateMedicalVisit: vi.fn(), createVitalSigns: vi.fn(), updateVitalSigns: vi.fn()
}));
vi.mock('next-intl', () => ({useTranslations: () => (key: string) => key}));
vi.mock('@/lib/utils/toast', () => ({notify: {success: vi.fn(), error: vi.fn()}}));

const visitId = '11111111-1111-4111-8111-111111111111';
const A = 'test-credential-A';
const B = 'test-credential-B';
const measurement: SaveVitalSignsRequest = {
  bloodPressure: null, heartRate: 72, temperature: null, respiratoryRate: null,
  weight: null, height: null, spO2: null
};
function visit(owner: string): MedicalVisitResponse {
  return {id: visitId, patientId: owner, doctorId: owner, appointmentId: null,
    visitDate: '2026-11-15T10:00:00Z', chiefComplaint: owner, symptoms: null,
    clinicalNotes: null, diagnoses: [], medications: [], vitalSigns: []};
}
function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => {resolve = done;});
  return {promise, resolve};
}
function setup() {
  const client = new QueryClient({defaultOptions: {queries: {retry: false}, mutations: {retry: false}}});
  function wrapper({children}: {children: ReactNode}) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  }
  return {client, wrapper};
}

beforeEach(() => {vi.clearAllMocks();});

describe('medical credential cache isolation', () => {
  it('never renders A visit or vitals under B, on clear, or when switching back to A', async () => {
    const {client, wrapper} = setup();
    const pendingB = deferred<MedicalVisitResponse>();
    vi.mocked(getMedicalVisit).mockImplementation(() => readMedicalToken() === B ? pendingB.promise : Promise.resolve(visit(A)));
    vi.mocked(listVitalSigns).mockImplementation(async (_id, token) => ({
      items: [{id: token, ...measurement}], page: 1, pageSize: 10, totalCount: 1, totalPages: 1
    }));
    const rendered: Array<{token: string | null; owner?: string; vital?: string}> = [];
    const {result, unmount} = renderHook(() => {
      const identity = useMedicalCredentials();
      const detail = useMedicalVisit(visitId);
      const vitals = useVitalSigns(visitId);
      rendered.push({token: identity.token, owner: detail.data?.doctorId, vital: vitals.data?.items[0]?.id});
      return {identity, detail, vitals};
    }, {wrapper});
    act(() => {clearMedicalTestSession(); setMedicalTestSession(A);});
    await waitFor(() => expect(result.current.detail.data?.doctorId).toBe(A));
    await waitFor(() => expect(result.current.vitals.data?.items[0]?.id).toBe(A));
    const scopeA = result.current.identity.credentialScope;
    act(() => {setMedicalTestSession(A);});
    expect(result.current.identity.credentialScope).toBe(scopeA);
    act(() => {setMedicalTestSession(B);});
    expect(result.current.detail.data).toBeUndefined();
    await waitFor(() => expect(result.current.vitals.data?.items[0]?.id).toBe(B));
    await act(async () => {pendingB.resolve(visit(B));});
    await waitFor(() => expect(result.current.detail.data?.doctorId).toBe(B));
    act(() => {clearMedicalTestSession();});
    expect(result.current.detail.data).toBeUndefined();
    expect(result.current.vitals.data).toBeUndefined();
    act(() => {setMedicalTestSession(A);});
    expect(result.current.detail.data?.doctorId).not.toBe(B);
    await waitFor(() => expect(result.current.detail.data?.doctorId).toBe(A));
    expect(rendered.some(row => row.token === B && (row.owner === A || row.vital === A))).toBe(false);
    expect(rendered.some(row => row.token === A && (row.owner === B || row.vital === B))).toBe(false);
    for (const query of client.getQueryCache().getAll()) {
      expect(JSON.stringify(query.queryKey)).not.toContain('test-credential');
    }
    unmount(); client.clear();
  });

  it.each(['create', 'update'] as const)('discards late %s visit responses after identity change', async (mode) => {
    const {client, wrapper} = setup();
    const pending = deferred<MedicalVisitResponse>();
    vi.mocked(createMedicalVisit).mockReturnValue(pending.promise);
    vi.mocked(updateMedicalVisit).mockReturnValue(pending.promise);
    const {result, unmount} = renderHook(() => ({
      identity: useMedicalCredentials(), create: useCreateVisitMutation(), update: useUpdateVisitMutation(visitId)
    }), {wrapper});
    act(() => {setMedicalTestSession(A);});
    const payload = {visitDate: '2026-11-15T10:00:00Z', chiefComplaint: null, symptoms: null, clinicalNotes: null};
    act(() => {
      if (mode === 'create') result.current.create.mutate({payload: {...payload, patientId: 'patient', appointmentId: null}});
      else result.current.update.mutate({payload});
    });
    await waitFor(() => expect(mode === 'create' ? createMedicalVisit : updateMedicalVisit).toHaveBeenCalledOnce());
    act(() => {setMedicalTestSession(B);});
    const bKey = medicalRecordKeys.visitDetail(visitId, result.current.identity.credentialScope);
    client.setQueryData(bKey, visit(B));
    await act(async () => {pending.resolve(visit(A));});
    await waitFor(() => expect(mode === 'create' ? result.current.create.isSuccess : result.current.update.isSuccess).toBe(true));
    expect(client.getQueryData(bKey)).toEqual(visit(B));
    expect(client.getQueryData(medicalRecordKeys.visit(visitId))).toBeUndefined();
    unmount(); client.clear();
  });

  it('discards a late response across logout and restoring the same token', async () => {
    const {client, wrapper} = setup();
    const pending = deferred<MedicalVisitResponse>();
    vi.mocked(updateMedicalVisit).mockReturnValue(pending.promise);
    const {result, unmount} = renderHook(() => ({identity: useMedicalCredentials(), mutation: useUpdateVisitMutation(visitId)}), {wrapper});
    act(() => {setMedicalTestSession(A);});
    const oldScope = result.current.identity.credentialScope;
    act(() => {result.current.mutation.mutate({payload: {
      visitDate: '2026-11-15T10:00:00Z', chiefComplaint: null, symptoms: null, clinicalNotes: null
    }});});
    await waitFor(() => expect(updateMedicalVisit).toHaveBeenCalledOnce());
    act(() => {clearMedicalTestSession(); setMedicalTestSession(A);});
    expect(result.current.identity.credentialScope).not.toBe(oldScope);
    const newKey = medicalRecordKeys.visitDetail(visitId, result.current.identity.credentialScope);
    await act(async () => {pending.resolve(visit(A));});
    await waitFor(() => expect(result.current.mutation.isSuccess).toBe(true));
    expect(client.getQueryData(newKey)).toBeUndefined();
    unmount(); client.clear();
  });

  it('current visit mutations still seed their scoped detail cache', async () => {
    const {client, wrapper} = setup();
    vi.mocked(updateMedicalVisit).mockResolvedValue(visit(A));
    const {result, unmount} = renderHook(() => ({identity: useMedicalCredentials(), mutation: useUpdateVisitMutation(visitId)}), {wrapper});
    act(() => {setMedicalTestSession(A);});
    await act(async () => {await result.current.mutation.mutateAsync({payload: {
      visitDate: '2026-11-15T10:00:00Z', chiefComplaint: null, symptoms: null, clinicalNotes: null
    }});});
    expect(client.getQueryData(medicalRecordKeys.visitDetail(visitId, result.current.identity.credentialScope))).toEqual(visit(A));
    unmount(); client.clear();
  });

  it('vitals mutations invalidate scoped detail and all paged lists using safe prefixes', async () => {
    const {client, wrapper} = setup();
    vi.mocked(createVitalSigns).mockResolvedValue({id: 'measurement', ...measurement});
    const {result, unmount} = renderHook(() => ({identity: useMedicalCredentials(), mutation: useCreateVitalSignsMutation(visitId)}), {wrapper});
    act(() => {setMedicalTestSession(A);});
    const scope = result.current.identity.credentialScope;
    const keys = [medicalRecordKeys.visitDetail(visitId, scope),
      medicalRecordKeys.vitalSignsPage(visitId, scope, 1, 10), medicalRecordKeys.vitalSignsPage(visitId, scope, 2, 10)];
    keys.forEach(key => client.setQueryData(key, {cached: true}));
    await act(async () => {await result.current.mutation.mutateAsync({token: A, payload: measurement});});
    keys.forEach(key => expect(client.getQueryState(key)?.isInvalidated).toBe(true));
    unmount(); client.clear();
  });
});
