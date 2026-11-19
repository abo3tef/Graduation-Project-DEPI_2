import {getAuthHeaders} from './auth-headers';
import {
  createInvalidResponseError,
  createNetworkError,
  parseProblem
} from './errors';
import {notifyUnauthorized} from './unauthorized-handler';
import {buildQueryString, type QueryValue} from './query-string';

export type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';

export type ApiRequestOptions = {
  method?: HttpMethod;
  query?: Readonly<Record<string, QueryValue>>;
  body?: unknown;
  signal?: AbortSignal;
};

/**
 * Base URL of the API. `NEXT_PUBLIC_API_URL` is the documented variable; the older
 * `NEXT_PUBLIC_API_BASE_URL` name remains as a fallback for existing local environments.
 * Both use literal property access so Next.js can inline them at build time.
 */
export function getApiBaseUrl(): string {
  const value = process.env.NEXT_PUBLIC_API_URL || process.env.NEXT_PUBLIC_API_BASE_URL;

  if (!value || value.trim() === '') {
    throw new Error(
      'NEXT_PUBLIC_API_URL (or NEXT_PUBLIC_API_BASE_URL) is not set. ' +
        'Copy frontend/.env.example to .env.local.'
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

function isAbortError(error: unknown, signal?: AbortSignal): boolean {
  return signal?.aborted === true || (error instanceof DOMException && error.name === 'AbortError');
}

/**
 * The only function that talks to the API. It:
 * - builds the URL and query string,
 * - sends JSON,
 * - asks the registered bearer-token provider for the current credential on every call,
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
      // Doctor-scoped data must never be served from an HTTP cache.
      cache: 'no-store',
      // Authentication is carried by headers, never ambient cookies.
      credentials: 'omit'
    });
  } catch (error) {
    if (isAbortError(error, signal)) {
      throw error;
    }

    throw createNetworkError(error);
  }

  if (!response.ok) {
    // The single sign-out hook: whoever owns the credential (the auth feature) decides what a
    // rejected token means. Anonymous callers such as a failed login are unaffected there.
    if (response.status === 401) {
      notifyUnauthorized();
    }

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

/** For endpoints that answer `204 No Content` (for example medication delete). */
export async function apiRequestVoid(
  path: string,
  options: ApiRequestOptions = {}
): Promise<void> {
  await apiRequest<unknown>(path, options);
}
