import {apiBaseUrl} from './env';
import {
  isProblemDetails,
  toFieldErrors,
  type FieldErrors,
  type ProblemDetails
} from './problem-details';

export type ApiErrorKind =
  | 'unauthorized'
  | 'forbidden'
  | 'not-found'
  | 'conflict'
  | 'validation'
  | 'network'
  | 'server';

type RequestMethod = 'GET' | 'POST' | 'PUT' | 'DELETE';

type RequestOptions = {
  method?: RequestMethod;
  body?: unknown;
  token?: string | null;
  signal?: AbortSignal;
};

/**
 * Normalised failure of an API call (D-27). UI code switches on `kind` and never
 * renders `message`, `problem.title` or any server text directly.
 */
export class ApiError extends Error {
  readonly status: number;
  readonly kind: ApiErrorKind;
  readonly problem: ProblemDetails | null;
  readonly fieldErrors: FieldErrors;

  constructor(status: number, kind: ApiErrorKind, problem: ProblemDetails | null) {
    super(
      kind === 'network'
        ? 'The API could not be reached.'
        : (problem?.title ?? `Request failed with status ${status}.`)
    );

    this.name = 'ApiError';
    this.status = status;
    this.kind = kind;
    this.problem = problem;
    this.fieldErrors = toFieldErrors(problem);
  }
}

async function send(path: string, options: RequestOptions): Promise<Response> {
  const {method = 'GET', body, token, signal} = options;
  const headers = new Headers({Accept: 'application/json'});

  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  if (body !== undefined) {
    headers.set('Content-Type', 'application/json');
  }

  let response: Response;

  try {
    response = await fetch(`${apiBaseUrl}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
      // Doctor-scoped data must never be served from an HTTP cache.
      cache: 'no-store',
      credentials: 'omit'
    });
  } catch (error) {
    if (signal?.aborted) {
      // A cancelled query is a normal React Query lifecycle event, not an API failure.
      throw error;
    }

    throw new ApiError(0, 'network', null);
  }

  if (!response.ok) {
    throw new ApiError(response.status, toErrorKind(response.status), await readProblem(response));
  }

  return response;
}

/** Deserialises a JSON response. The caller narrows the result with a feature guard. */
export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const response = await send(path, options);
  const text = await response.text();

  if (text.trim().length === 0) {
    throw new ApiError(response.status, 'server', null);
  }

  let parsed: unknown;

  try {
    parsed = JSON.parse(text);
  } catch {
    throw new ApiError(response.status, 'server', null);
  }

  return parsed as T;
}

/** For endpoints that answer `204 No Content` (medication delete). */
export async function requestVoid(
  path: string,
  options: RequestOptions = {}
): Promise<void> {
  await send(path, options);
}

async function readProblem(response: Response): Promise<ProblemDetails | null> {
  let text: string;

  try {
    text = await response.text();
  } catch {
    return null;
  }

  if (text.trim().length === 0) {
    return null;
  }

  try {
    const parsed: unknown = JSON.parse(text);
    return isProblemDetails(parsed) ? parsed : null;
  } catch {
    return null;
  }
}

function toErrorKind(status: number): ApiErrorKind {
  switch (status) {
    case 400:
    case 422:
      return 'validation';
    case 401:
      return 'unauthorized';
    case 403:
      return 'forbidden';
    case 404:
      return 'not-found';
    case 409:
      return 'conflict';
    default:
      return 'server';
  }
}
