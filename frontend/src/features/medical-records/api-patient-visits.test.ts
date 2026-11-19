import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {ApiError} from '@/lib/api';
import {listPatientMedicalVisits} from './api';

const PATIENT_ID = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const VISIT_ID = '11111111-1111-4111-8111-111111111111';

function jsonResponse(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: {'content-type': 'application/json'}
  });
}

/** The F10 patient-history item shape (`Responses.MedicalVisitResponse`). */
function historyVisit() {
  return {
    visitId: VISIT_ID,
    visitDate: '2026-11-13T10:00:00+00:00',
    doctorId: '22222222-2222-4222-8222-222222222222',
    doctorName: 'Doctor Under Test',
    chiefComplaint: 'Headache',
    symptoms: 'Since yesterday',
    clinicalNotes: null,
    diagnoses: [
      {
        id: '33333333-3333-4333-8333-333333333333',
        name: 'Migraine',
        description: null,
        diagnosisDate: '2026-11-13'
      }
    ],
    medications: [
      {
        id: '44444444-4444-4444-8444-444444444444',
        name: 'Amoxicillin',
        dosage: '500 mg',
        frequency: 'Every 8 hours',
        duration: '7 days',
        instructions: null
      }
    ],
    vitalSigns: [],
    notes: [],
    labResults: []
  };
}

describe('listPatientMedicalVisits', () => {
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    vi.stubEnv('NEXT_PUBLIC_API_BASE_URL', 'http://api.test/');
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    fetchMock.mockReset();
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

  it('requests the exact paged patient-history route and returns validated visits', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse(200, {
        items: [historyVisit()],
        page: 2,
        pageSize: 10,
        totalCount: 11,
        totalPages: 2
      })
    );

    const result = await listPatientMedicalVisits(PATIENT_ID, 2, 10);

    const [url] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe(
      `http://api.test/api/patients/${PATIENT_ID}/medical-visits?pageNumber=2&pageSize=10`
    );
    expect(result).toMatchObject({page: 2, pageSize: 10, totalCount: 11, totalPages: 2});
    expect(result.items).toHaveLength(1);
    expect(result.items[0]).toMatchObject({
      id: VISIT_ID,
      patientId: PATIENT_ID,
      visitDate: '2026-11-13T10:00:00+00:00',
      chiefComplaint: 'Headache'
    });
    expect(result.items[0]?.diagnoses[0]).toMatchObject({diagnosisName: 'Migraine'});
    expect(result.items[0]?.medications[0]).toMatchObject({medicationName: 'Amoxicillin'});
  });

  it('rejects a malformed item instead of rendering it in the browser', async () => {
    const malformed = {...historyVisit(), visitId: undefined};
    fetchMock.mockResolvedValue(
      jsonResponse(200, {
        items: [malformed],
        page: 1,
        pageSize: 10,
        totalCount: 1,
        totalPages: 1
      })
    );

    await expect(listPatientMedicalVisits(PATIENT_ID)).rejects.toBeInstanceOf(ApiError);
  });
});
