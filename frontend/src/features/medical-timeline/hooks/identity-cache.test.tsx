import {act, renderHook, waitFor} from '@testing-library/react';
import {QueryClient, QueryClientProvider} from '@tanstack/react-query';
import type {PropsWithChildren, ReactNode} from 'react';
import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
// Test-only bridge to the auth feature's session module. The production code reaches the same
// module through `@/features/auth`; tests may use the concrete module for a deterministic store.
import {
  clearSession,
  resetSessionForTests,
  setSession
} from '../../auth/session/session-store';
import type {PagedResult} from '@/types/paging';
import {getMedicalProfile, getPatientTimeline} from '../api';
import type {
  PatientMedicalProfileResponse,
  PatientTimelineEventResponse
} from '../types';
import {useMedicalProfile} from './use-medical-profile';
import {usePatientTimeline} from './use-patient-timeline';

vi.mock('../api', () => ({
  getMedicalProfile: vi.fn(),
  getPatientTimeline: vi.fn()
}));

// The auth feature's public barrel reaches the localized navigation helpers; the query hooks do
// not use them, so the navigation module is stubbed to keep this suite free of the Next runtime.
vi.mock('@/i18n/navigation', () => ({
  Link: ({href, children}: {href: string; children: ReactNode}) => <a href={href}>{children}</a>,
  usePathname: () => '/',
  useRouter: () => ({push: vi.fn(), replace: vi.fn(), back: vi.fn()})
}));

const PATIENT_ID = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const DOCTOR_A = '11111111-1111-4111-8111-111111111111';
const DOCTOR_B = '22222222-2222-4222-8222-222222222222';
const PROFILE_A: PatientMedicalProfileResponse = {
  patientId: PATIENT_ID,
  fullName: 'Patient visible to doctor A',
  dateOfBirth: '1990-04-12',
  age: 36,
  gender: 'Female',
  phone: null,
  address: null,
  bloodType: null,
  emergencyContact: null,
  allergies: [],
  chronicConditions: []
};
const PROFILE_B: PatientMedicalProfileResponse = {
  ...PROFILE_A,
  fullName: 'Patient visible to doctor B'
};
const EVENT_A: PatientTimelineEventResponse = {
  id: 'event-a',
  type: 'Visit',
  occurredAt: '2026-11-15T10:00:00Z',
  title: 'Visit cached for doctor A',
  summary: null,
  relatedVisitId: null
};
const EVENT_B: PatientTimelineEventResponse = {
  ...EVENT_A,
  id: 'event-b',
  title: 'Visit fetched for doctor B'
};
const PAGE_A: PagedResult<PatientTimelineEventResponse> = {
  items: [EVENT_A],
  page: 1,
  pageSize: 10,
  totalCount: 1,
  totalPages: 1
};
const PAGE_B: PagedResult<PatientTimelineEventResponse> = {
  ...PAGE_A,
  items: [EVENT_B]
};

/** Signs a doctor in through the real session store (the credential owner, F6-C2). */
function signInAsDoctor(userId: string): void {
  setSession({
    accessToken: `token-${userId}`,
    expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    user: {
      id: userId,
      fullName: 'Dr. Test',
      email: 'doctor@medilog.test',
      role: 'Doctor',
      doctorId: userId
    }
  });
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((resolvePromise) => {
    resolve = resolvePromise;
  });

  return {promise, resolve};
}

describe('medical query identity cache scope', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    resetSessionForTests();
    signInAsDoctor(DOCTOR_A);
    queryClient = new QueryClient({
      defaultOptions: {queries: {retry: false}}
    });
  });

  afterEach(() => {
    queryClient.clear();
    resetSessionForTests();
    vi.clearAllMocks();
  });

  it('scopes profile and timeline data to the signed-in identity and stops while signed out', async () => {
    const identityBProfile = deferred<PatientMedicalProfileResponse>();
    const identityBTimeline = deferred<PagedResult<PatientTimelineEventResponse>>();
    let profileCall = 0;
    let timelineCall = 0;

    vi.mocked(getMedicalProfile).mockImplementation(() => {
      profileCall += 1;
      return profileCall === 1 ? Promise.resolve(PROFILE_A) : identityBProfile.promise;
    });
    vi.mocked(getPatientTimeline).mockImplementation(() => {
      timelineCall += 1;
      return timelineCall === 1 ? Promise.resolve(PAGE_A) : identityBTimeline.promise;
    });

    const wrapper = ({children}: PropsWithChildren) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );
    const {result} = renderHook(
      () => ({
        profile: useMedicalProfile(PATIENT_ID),
        timeline: usePatientTimeline(PATIENT_ID, {pageNumber: 1, pageSize: 10})
      }),
      {wrapper}
    );

    await waitFor(() => {
      expect(result.current.profile.data?.fullName).toBe(PROFILE_A.fullName);
      expect(result.current.timeline.data?.items[0]?.title).toBe(EVENT_A.title);
    });

    // Another doctor signs in: the previous doctor's data must disappear from the screen and
    // the queries must run again under the new identity scoped keys.
    act(() => {
      signInAsDoctor(DOCTOR_B);
    });
    await waitFor(() => {
      expect(result.current.profile.data).toBeUndefined();
      expect(result.current.timeline.data).toBeUndefined();
    });
    await waitFor(() => {
      expect(getMedicalProfile).toHaveBeenCalledTimes(2);
      expect(getPatientTimeline).toHaveBeenCalledTimes(2);
    });

    await act(async () => {
      identityBProfile.resolve(PROFILE_B);
      identityBTimeline.resolve(PAGE_B);
    });
    await waitFor(() => {
      expect(result.current.profile.data?.fullName).toBe(PROFILE_B.fullName);
      expect(result.current.timeline.data?.items[0]?.title).toBe(EVENT_B.title);
    });

    // Signing out disables both queries and surfaces no cached medical data.
    act(() => {
      clearSession();
    });
    await waitFor(() => {
      expect(result.current.profile.data).toBeUndefined();
      expect(result.current.timeline.data).toBeUndefined();
    });
    expect(getMedicalProfile).toHaveBeenCalledTimes(2);
    expect(getPatientTimeline).toHaveBeenCalledTimes(2);

    // Cache keys are scoped by actor identity and never contain the credential itself.
    const cacheKeys = queryClient
      .getQueryCache()
      .getAll()
      .map(({queryKey}) => JSON.stringify(queryKey))
      .join('|');
    expect(cacheKeys).toContain(DOCTOR_A);
    expect(cacheKeys).toContain(DOCTOR_B);
    expect(cacheKeys).not.toContain('token-');
  });
});
