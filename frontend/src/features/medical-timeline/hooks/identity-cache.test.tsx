import {act, renderHook, waitFor} from '@testing-library/react';
import {QueryClient, QueryClientProvider} from '@tanstack/react-query';
import type {PropsWithChildren, ReactNode} from 'react';
import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {setDevIdentity} from '@/features/auth/dev-identity/dev-identity';
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

vi.mock('@/i18n/navigation', () => ({
  Link: ({
    href,
    children
  }: {
    href: string;
    children: ReactNode;
  }) => <a href={href}>{children}</a>
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
    vi.stubEnv('NEXT_PUBLIC_DEV_IDENTITY', 'true');
    vi.stubEnv('NEXT_PUBLIC_DEV_USER_ID', '');
    vi.stubEnv('NEXT_PUBLIC_DEV_USER_ROLE', '');
    setDevIdentity({userId: DOCTOR_A, role: 'Doctor'});
    queryClient = new QueryClient({
      defaultOptions: {queries: {retry: false}}
    });
  });

  afterEach(() => {
    queryClient.clear();
    setDevIdentity(null);
    vi.unstubAllEnvs();
    vi.clearAllMocks();
  });

  it('hides prior profile and timeline data on identity and credential changes', async () => {
    const identityBProfile = deferred<PatientMedicalProfileResponse>();
    const identityBTimeline = deferred<PagedResult<PatientTimelineEventResponse>>();
    const rotatedProfile = deferred<PatientMedicalProfileResponse>();
    const rotatedTimeline = deferred<PagedResult<PatientTimelineEventResponse>>();
    let profileCall = 0;
    let timelineCall = 0;

    vi.mocked(getMedicalProfile).mockImplementation(() => {
      profileCall += 1;
      return profileCall === 1
        ? Promise.resolve(PROFILE_A)
        : profileCall === 2
          ? identityBProfile.promise
          : rotatedProfile.promise;
    });
    vi.mocked(getPatientTimeline).mockImplementation(() => {
      timelineCall += 1;
      return timelineCall === 1
        ? Promise.resolve(PAGE_A)
        : timelineCall === 2
          ? identityBTimeline.promise
          : rotatedTimeline.promise;
    });

    const wrapper = ({children}: PropsWithChildren) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );
    const {result, rerender} = renderHook(
      ({token, tokenRevision}: {token: string; tokenRevision: number}) => ({
        profile: useMedicalProfile(PATIENT_ID, token, true, tokenRevision),
        timeline: usePatientTimeline(
          PATIENT_ID,
          {pageNumber: 1, pageSize: 10},
          token,
          true,
          tokenRevision
        )
      }),
      {
        initialProps: {token: 'credential-a', tokenRevision: 1},
        wrapper
      }
    );

    await waitFor(() => {
      expect(result.current.profile.data?.fullName).toBe(PROFILE_A.fullName);
      expect(result.current.timeline.data?.items[0]?.title).toBe(EVENT_A.title);
    });

    act(() => {
      setDevIdentity({userId: DOCTOR_B, role: 'Doctor'});
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

    rerender({token: 'credential-b', tokenRevision: 2});
    await waitFor(() => {
      expect(result.current.profile.data).toBeUndefined();
      expect(result.current.timeline.data).toBeUndefined();
    });

    await waitFor(() => {
      expect(getMedicalProfile).toHaveBeenCalledTimes(3);
      expect(getPatientTimeline).toHaveBeenCalledTimes(3);
    });

    const cacheKeys = queryClient
      .getQueryCache()
      .getAll()
      .map(({queryKey}) => JSON.stringify(queryKey));
    expect(cacheKeys.join('|')).not.toContain('credential-a');
    expect(cacheKeys.join('|')).not.toContain('credential-b');

    await act(async () => {
      rotatedProfile.resolve({...PROFILE_B, fullName: 'Profile after credential rotation'});
      rotatedTimeline.resolve({
        ...PAGE_B,
        items: [{...EVENT_B, title: 'Timeline after credential rotation'}]
      });
    });
    await waitFor(() => {
      expect(result.current.profile.data?.fullName).toBe(
        'Profile after credential rotation'
      );
      expect(result.current.timeline.data?.items[0]?.title).toBe(
        'Timeline after credential rotation'
      );
    });
  });
});
