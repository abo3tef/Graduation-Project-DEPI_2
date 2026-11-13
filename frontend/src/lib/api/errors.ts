export type FieldErrors = Readonly<Record<string, readonly string[]>>;

/**
 * Why a request failed. The value is derived from the HTTP status for error
 * responses, and uses dedicated values for failures that never produced a usable
 * response:
 *
 * - `network` — the request never reached the server.
 * - `invalid-response` — a success response had no usable JSON body.
 * - the rest map 1:1 to the HTTP status family.
 *
 * UI code switches on `kind` (or on `code` when a specific business rule matters)
 * and never renders `message`, `title` or `detail`, which are English server text.
 */
export type ApiErrorKind =
  | 'network'
  | 'invalid-response'
  | 'unauthorized'
  | 'forbidden'
  | 'not-found'
  | 'conflict'
  | 'validation'
  | 'server';

type ApiErrorInit = {
  kind: ApiErrorKind;
  status: number;
  message?: string;
  code?: string | null;
  title?: string | null;
  detail?: string | null;
  fieldErrors?: FieldErrors;
  fieldErrorCodes?: FieldErrors;
  traceId?: string | null;
};

/**
 * The single error type thrown by the API transport.
 *
 * - `status` is the HTTP status, or `0` when no response was received.
 * - `code` is the stable machine-readable code from the API (for example
 *   `appointment.overlap`); `null` for framework-generated errors.
 * - `fieldErrors` / `fieldErrorCodes` use camelCase field names.
 * - `detail` is English server text meant for logs; screens translate by `code`.
 */
export class ApiError extends Error {
  readonly kind: ApiErrorKind;
  readonly status: number;
  readonly code: string | null;
  readonly title: string | null;
  readonly detail: string | null;
  readonly fieldErrors: FieldErrors;
  readonly fieldErrorCodes: FieldErrors;
  readonly traceId: string | null;

  constructor(init: ApiErrorInit) {
    super(init.message ?? init.detail ?? init.title ?? `API error ${init.status}`);
    this.name = 'ApiError';
    this.kind = init.kind;
    this.status = init.status;
    this.code = init.code ?? null;
    this.title = init.title ?? null;
    this.detail = init.detail ?? null;
    this.fieldErrors = init.fieldErrors ?? {};
    this.fieldErrorCodes = init.fieldErrorCodes ?? {};
    this.traceId = init.traceId ?? null;
  }
}

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError;
}

export function createNetworkError(cause?: unknown): ApiError {
  const error = new ApiError({
    kind: 'network',
    status: 0,
    message: 'The server could not be reached.'
  });

  if (cause !== undefined) {
    (error as {cause?: unknown}).cause = cause;
  }

  return error;
}

export function createInvalidResponseError(status: number): ApiError {
  return new ApiError({
    kind: 'invalid-response',
    status,
    message: 'The server returned an unexpected response.'
  });
}

/** Maps an HTTP status to the failure kind the UI switches on. */
export function kindFromStatus(status: number): ApiErrorKind {
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

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function asString(value: unknown): string | null {
  return typeof value === 'string' ? value : null;
}

/**
 * Normalizes a field key: strips the JSON-path prefix the framework adds
 * (`$.status` → `status`) and lower-cases the first letter (`EndTime` → `endTime`).
 */
export function normalizeFieldKey(key: string): string {
  const withoutPath = key.startsWith('$.') ? key.slice(2) : key;
  return withoutPath.length === 0
    ? withoutPath
    : withoutPath.charAt(0).toLowerCase() + withoutPath.slice(1);
}

function readFieldMap(value: unknown): FieldErrors {
  if (!isRecord(value)) {
    return {};
  }

  const result: Record<string, readonly string[]> = {};

  for (const [key, entry] of Object.entries(value)) {
    if (Array.isArray(entry)) {
      const strings = entry.filter((item): item is string => typeof item === 'string');
      const normalized = normalizeFieldKey(key);
      result[normalized] = [...(result[normalized] ?? []), ...strings];
    }
  }

  return result;
}

/**
 * Builds an `ApiError` from an error response body. Understands both the
 * application's problem details (`code`, `errors`, `errorCodes`) and the
 * framework's standard 400 shape (JSON-path keys, no `code`). Never throws.
 */
export function parseProblem(status: number, body: unknown): ApiError {
  if (!isRecord(body)) {
    return new ApiError({kind: kindFromStatus(status), status});
  }

  return new ApiError({
    kind: kindFromStatus(status),
    status,
    code: asString(body.code),
    title: asString(body.title),
    detail: asString(body.detail),
    fieldErrors: readFieldMap(body.errors),
    fieldErrorCodes: readFieldMap(body.errorCodes),
    traceId: asString(body.traceId)
  });
}
