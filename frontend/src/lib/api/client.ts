import {getAuthHeaders} from './auth-headers';
import {
  createInvalidResponseError,
  createNetworkError,
  parseProblem
} from './errors';
import {buildQueryString, type QueryValue} from './query-string';

export type HttpMethod = 'GET' | 'POST' | 'PUT' | 'DELETE';

export type ApiRequestOptions = {
  method?: HttpMethod;
  query?: Readonly<Record<string, QueryValue>>;
  body?: unknown;
  signal?: AbortSignal;
};

/**
 * Base URL of the API. Read from `NEXT_PUBLIC_API_BASE_URL` (see `.env.example`)
 * with a literal property access so Next.js can inline it at build time.
 */
export function getApiBaseUrl(): string {
  const value = process.env.NEXT_PUBLIC_API_BASE_URL;

  if (!value || value.trim() === '') {
    throw new Error(
      'NEXT_PUBLIC_API_BASE_URL is not set. Copy frontend/.env.example to .env.local.'
    );
  }

  return value.trim().replace(/\/+$/, '');
}

function isJsonResponse(response: Response): boolean {
  const contentType = response.headers.get('content-type') ?? '';
  return contentType.includes('json');
}

async function readJson(response: Response): Promise<unknown> {
  try {
    return await response.json();
  } catch {
    return undefined;
  }
}

/**
 * The only function that talks to the API. It:
 * - builds the URL and query string,
 * - sends JSON,
 * - asks the auth-headers provider for the caller's identity on every call,
 * - turns error responses into `ApiError` (never leaking raw bodies to screens),
 * - turns connection failures into `ApiError` with status `0`.
 * Aborted requests are rethrown unchanged so React Query can handle them.
 */
export async function apiRequest<T>(
  path: string,
  options: ApiRequestOptions = {}
): Promise<T> {
  const {method = 'GET', query, body, signal} = options;
  const url = `${getApiBaseUrl()}${path}${query ? buildQueryString(query) : ''}`;

  const headers: Record<string, string> = {
    Accept: 'application/json',
    ...getAuthHeaders()
  };

  if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }

  let response: Response;

  try {
    response = await fetch(url, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
      cache: 'no-store'
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error;
    }

    throw createNetworkError(error);
  }

  if (!response.ok) {
    const problem = isJsonResponse(response) ? await readJson(response) : undefined;
    throw parseProblem(response.status, problem);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  if (!isJsonResponse(response)) {
    throw createInvalidResponseError(response.status);
  }

  const data = await readJson(response);

  if (data === undefined) {
    throw createInvalidResponseError(response.status);
  }

  return data as T;
}
