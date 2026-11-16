import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {ApiError} from '@/lib/api';
import {deleteDiagnosis} from './api';

const VISIT_ID = '11111111-1111-1111-1111-111111111111';
const DIAGNOSIS_ID = '22222222-2222-2222-2222-222222222222';

function jsonResponse(status: number, body: unknown, contentType = 'application/json') {
  return new Response(JSON.stringify(body), {
    status,
    headers: {'content-type': contentType}
  });
}

describe('deleteDiagnosis', () => {
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

  it('sends DELETE to the diagnosis route and resolves to void on 204', async () => {
    fetchMock.mockResolvedValue(new Response(null, {status: 204}));

    await expect(deleteDiagnosis(VISIT_ID, DIAGNOSIS_ID)).resolves.toBeUndefined();

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe(
      `http://api.test/api/medical-visits/${VISIT_ID}/diagnoses/${DIAGNOSIS_ID}`
    );
    expect(init?.method).toBe('DELETE');
    expect(init?.body).toBeUndefined();
  });

  it('turns a 404 into an ApiError with the not-found kind', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse(
        404,
        {code: 'diagnosis.not_found', title: 'Not Found'},
        'application/problem+json'
      )
    );

    const error = await deleteDiagnosis(VISIT_ID, DIAGNOSIS_ID).catch(
      (caught: unknown) => caught
    );

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(404);
    expect((error as ApiError).kind).toBe('not-found');
    expect((error as ApiError).code).toBe('diagnosis.not_found');
  });
});
