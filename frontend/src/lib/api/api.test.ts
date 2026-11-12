import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {
  ApiError,
  apiRequest,
  buildQueryString,
  getApiBaseUrl,
  normalizeFieldKey,
  parseProblem,
  resetAuthHeadersProvider,
  setAuthHeadersProvider
} from './index';

function jsonResponse(status: number, body: unknown, contentType = 'application/json') {
  return new Response(JSON.stringify(body), {
    status,
    headers: {'content-type': contentType}
  });
}

describe('buildQueryString', () => {
  it('skips undefined, null and empty values and keeps the key order', () => {
    expect(
      buildQueryString({b: 2, a: 'x', skip: undefined, none: null, empty: '', flag: false})
    ).toBe('?b=2&a=x&flag=false');
  });

  it('returns an empty string when nothing remains', () => {
    expect(buildQueryString({a: undefined, b: ''})).toBe('');
  });

  it('encodes special characters', () => {
    expect(buildQueryString({search: 'منى & omar'})).toBe(
      `?search=${encodeURIComponent('منى & omar').replace(/%20/g, '+')}`
    );
  });
});

describe('normalizeFieldKey', () => {
  it.each([
    ['EndTime', 'endTime'],
    ['endTime', 'endTime'],
    ['$.status', 'status'],
    ['$', '$'],
    ['', '']
  ])('%s → %s', (input, expected) => {
    expect(normalizeFieldKey(input)).toBe(expected);
  });
});

describe('parseProblem', () => {
  it('reads the application problem shape', () => {
    const error = parseProblem(400, {
      code: 'validation.failed',
      title: 'One or more validation errors occurred.',
      errors: {endTime: ['required']},
      errorCodes: {endTime: ['NotNullValidator']},
      traceId: 'abc'
    });

    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(400);
    expect(error.code).toBe('validation.failed');
    expect(error.fieldErrors.endTime).toEqual(['required']);
    expect(error.fieldErrorCodes.endTime).toEqual(['NotNullValidator']);
    expect(error.traceId).toBe('abc');
  });

  it('understands the framework 400 shape with JSON-path keys and no code', () => {
    const error = parseProblem(400, {
      type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
      errors: {'$.status': ['could not be converted'], request: ['The request field is required.']}
    });

    expect(error.code).toBeNull();
    expect(error.fieldErrors.status).toEqual(['could not be converted']);
    expect(error.fieldErrors.request).toEqual(['The request field is required.']);
  });

  it('normalizes PascalCase keys and merges duplicates', () => {
    const error = parseProblem(400, {errors: {EndTime: ['a'], endTime: ['b']}});

    expect(error.fieldErrors.endTime).toEqual(['a', 'b']);
  });

  it.each([undefined, null, 'text', 42, ['x']])('never throws for body %j', (body) => {
    const error = parseProblem(500, body);

    expect(error.status).toBe(500);
    expect(error.code).toBeNull();
    expect(error.fieldErrors).toEqual({});
  });

  it('ignores non-string entries inside field errors', () => {
    const error = parseProblem(400, {errors: {name: ['ok', 3, null]}});

    expect(error.fieldErrors.name).toEqual(['ok']);
  });
});

describe('apiRequest', () => {
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    vi.stubEnv('NEXT_PUBLIC_API_BASE_URL', 'http://api.test/');
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    resetAuthHeadersProvider();
    fetchMock.mockReset();
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

  it('builds the URL from the base URL (without a trailing slash), path and query', async () => {
    fetchMock.mockResolvedValue(jsonResponse(200, {ok: true}));

    await apiRequest('/api/appointments', {query: {status: 'Scheduled', pageNumber: 2, skip: undefined}});

    expect(fetchMock.mock.calls[0]?.[0]).toBe(
      'http://api.test/api/appointments?status=Scheduled&pageNumber=2'
    );
  });

  it('asks the auth-headers provider on every call and sends JSON bodies', async () => {
    let id = 'one';
    setAuthHeadersProvider(() => ({'X-User-Id': id, 'X-User-Role': 'Secretary'}));
    fetchMock.mockImplementation(async () => jsonResponse(201, {id: 'x'}));

    await apiRequest('/api/appointments', {method: 'POST', body: {reason: 'r'}});
    id = 'two';
    await apiRequest('/api/appointments', {method: 'POST', body: {reason: 'r'}});

    const first = fetchMock.mock.calls[0]?.[1];
    const second = fetchMock.mock.calls[1]?.[1];
    expect((first?.headers as Record<string, string>)['X-User-Id']).toBe('one');
    expect((second?.headers as Record<string, string>)['X-User-Id']).toBe('two');
    expect((first?.headers as Record<string, string>)['Content-Type']).toBe('application/json');
    expect((first?.headers as Record<string, string>)['X-User-Role']).toBe('Secretary');
    expect(first?.method).toBe('POST');
    expect(first?.body).toBe(JSON.stringify({reason: 'r'}));
  });

  it('sends no identity headers and no content type by default', async () => {
    fetchMock.mockResolvedValue(jsonResponse(200, []));

    await apiRequest('/api/appointments/lookups/doctors');

    const headers = fetchMock.mock.calls[0]?.[1]?.headers as Record<string, string>;
    expect(headers).toEqual({Accept: 'application/json'});
  });

  it('returns parsed JSON on success and undefined on 204', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, {a: 1}));
    fetchMock.mockResolvedValueOnce(new Response(null, {status: 204}));

    await expect(apiRequest('/x')).resolves.toEqual({a: 1});
    await expect(apiRequest('/x')).resolves.toBeUndefined();
  });

  it('turns problem+json error responses into ApiError', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse(409, {code: 'appointment.overlap', title: 'Conflict'}, 'application/problem+json')
    );

    const error = await apiRequest('/x').catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(409);
    expect((error as ApiError).code).toBe('appointment.overlap');
    expect((error as ApiError).kind).toBe('http');
  });

  it('turns an error response without a JSON body into an ApiError with no code', async () => {
    fetchMock.mockResolvedValue(new Response('<html>bad gateway</html>', {status: 502, headers: {'content-type': 'text/html'}}));

    const error = (await apiRequest('/x').catch((e: unknown) => e)) as ApiError;

    expect(error.status).toBe(502);
    expect(error.code).toBeNull();
  });

  it('turns connection failures into an ApiError with status 0', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'));

    const error = (await apiRequest('/x').catch((e: unknown) => e)) as ApiError;

    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(0);
    expect(error.kind).toBe('network');
  });

  it('rethrows an aborted request unchanged', async () => {
    const abort = new DOMException('aborted', 'AbortError');
    fetchMock.mockRejectedValue(abort);

    await expect(apiRequest('/x')).rejects.toBe(abort);
  });

  it('rejects a successful response that is not JSON', async () => {
    fetchMock.mockResolvedValue(new Response('ok', {status: 200, headers: {'content-type': 'text/plain'}}));

    const error = (await apiRequest('/x').catch((e: unknown) => e)) as ApiError;

    expect(error.kind).toBe('invalid-response');
  });

  it('fails clearly when the base URL is missing', () => {
    vi.stubEnv('NEXT_PUBLIC_API_BASE_URL', '');

    expect(() => getApiBaseUrl()).toThrow(/NEXT_PUBLIC_API_BASE_URL/);
  });
});
