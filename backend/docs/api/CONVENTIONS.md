# API conventions (shared)

> Status: Phase 1 (shared technical foundation). Source of truth for every feature's HTTP
> contract. A feature document describes its own routes, fields and business rules; this file
> describes the parts every feature must share. Changing anything here requires coordination with
> all affected frontend/backend contributors (AGENTS.md §4.4).
>
> Machine-readable form: the app serves OpenAPI in Development at `/openapi/v1.json`.

## 1. Transport and JSON

- Base path: `/api/<resource>`; every response is `application/json` unless it is an error, which
  is `application/problem+json` (section 5).
- Property names are **camelCase** in both requests and responses.
- Enums are **strings** (`"Scheduled"`, `"NoShow"`); numbers are rejected with `400`.
- Dates are `yyyy-MM-dd`; times are `HH:mm:ss` in the clinic time zone (`Clinic:TimeZone`);
  timestamps are ISO-8601 UTC.
- Unknown body properties are ignored; a malformed body, wrong value type or missing required
  member is a `400` with `code = request.invalid` (section 5.2).

## 2. Identity and authorization

Authentication is implemented per feature; see the feature document. Regardless of how the
principal is established:

- The acting user and role are read from the authenticated server-side identity. A
  client-supplied user id, doctor id, secretary id, role or ownership field is never trusted.
- Doctor-facing data is scoped to the authenticated doctor; secretary-facing data is scoped to the
  secretary's assigned doctors.
- An out-of-scope resource behaves like a missing one (`404`) unless the caller is not allowed to
  use the endpoint at all (`403`).

## 3. List, filtering and pagination

Every list endpoint uses this shape (STATE.md Q-04):

```http
GET /api/<resource>?<filters>&pageNumber=1&pageSize=10
```

### 3.1 Request

| Query parameter | Type | Rules |
|---|---|---|
| `pageNumber` | integer | `>= 1`; default `1` |
| `pageSize` | integer | `1..50`; default `10` |
| `<filter>` | feature-defined | optional camelCase name of the domain field |

- Filters are combined with `AND`, applied **at the data source** (never by loading an unbounded
  set and filtering it in the API or browser).
- An omitted or empty filter means "no filter". The UI sentinel `"all"` is never sent to the API.
- Date-range filters are inclusive.
- Invalid `pageNumber`, `pageSize` or filter values return `400` with
  `code = validation.failed` and camelCase `errors`.

Shared constants: `MinPageNumber = 1`, `DefaultPageNumber = 1`, `DefaultPageSize = 10`,
`MaxPageSize = 50`. Features must not define their own limits.

### 3.2 Response

```json
{
  "items": [ /* feature item */ ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 0,
  "totalPages": 0
}
```

- The response field is `page`; the request field is `pageNumber` (intentional, per Q-04).
- `totalPages = ceil(totalCount / pageSize)` and is `0` when there are no rows.
- A `pageNumber` beyond the last page returns an empty `items` array with the correct
  `totalCount`/`totalPages`.

### 3.3 Ordering

- Ordering is fixed per endpoint and **deterministic**: the primary sort field plus a stable
  tie-breaker (normally `id`) so that paging never duplicates or drops rows.
- Example (F1 appointments): `appointmentDate ASC, startTime ASC, id ASC`.

## 4. Success status codes

| Status | When |
|---|---|
| `200 OK` | Successful `GET`/`PUT` |
| `201 Created` | Successful `POST`; includes a `Location` header pointing at the new resource |
| `204 No Content` | Successful `DELETE` with no body to return |

## 5. Error envelope

Every error response — application exceptions, model-binding failures and framework-generated
responses (unmatched route, authentication challenge, method/media-type errors) — uses the same
RFC 7807 `application/problem+json` envelope:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "instance": "/api/appointments",
  "code": "validation.failed",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

| Member | Always present | Meaning |
|---|---|---|
| `type` | yes | URI identifying the problem class |
| `title` | yes | Short, client-readable English summary |
| `status` | yes | HTTP status code (mirrors the response status) |
| `instance` | yes | Request path |
| `code` | yes | Stable, machine-readable identifier (section 6) |
| `traceId` | yes | Correlation id for logs/support |
| `detail` | sometimes | Client-readable English explanation; never a stack trace, SQL or patient data |
| `errors` | validation only | Map of camelCase field name → array of messages |
| `errorCodes` | validation only | Map of camelCase field name → array of validator codes |

Clients switch on `status`, `code` and `errorCodes`; they must not display `title`/`detail` as
localized UI text.

### 5.1 Validation — `400`, `code = validation.failed`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "instance": "/api/medical-visits",
  "code": "validation.failed",
  "traceId": "00-…",
  "errors": {
    "patientId": ["A patient identifier is required."]
  },
  "errorCodes": {
    "patientId": ["NotEmptyValidator"]
  }
}
```

Field keys are always camelCase. `errorCodes` holds the FluentValidation validator code for each
message (for example `NotNullValidator`, `NotEmptyValidator`, `MaximumLengthValidator`,
`InclusiveBetweenValidator`, `EnumValidator`, `PredicateValidator`) so the client can localize
without matching English text. A domain-level field rule may contribute its own code (for example
`appointment.time_not_in_time_zone`); the top-level `code` stays `validation.failed`.

### 5.2 Model binding — `400`, `code = request.invalid`

Malformed JSON, a wrong value type, an unknown enum member or a missing body produces the same
envelope with `code = request.invalid` and camelCase `errors` (JSON-path prefixes such as
`$.status` are normalized to `status`). There is no `errorCodes` member because no validator ran.

### 5.3 Other failures

| Status | Default `code` | Default `title` |
|---|---|---|
| `401` | `auth.unauthorized` | `Unauthorized.` |
| `403` | `access.forbidden` (or a specific domain code) | `Forbidden.` |
| `404` | `resource.not_found` (or a specific domain code) | `Resource not found.` |
| `405` | `request.method_not_allowed` | `Method not allowed.` |
| `409` | `resource.conflict` (or a specific domain code) | `The request conflicts with the current state.` |
| `415` | `request.unsupported_media_type` | `Unsupported media type.` |
| `500` | `server.error` | `An unexpected error occurred.` |

A `500` body never contains the exception message, stack trace, SQL text or any patient data; the
exception is logged on the server with the same `traceId`.

## 6. Error code catalogue

Codes are stable identifiers and are never renamed; add new ones instead. Domain rule codes are
declared in `DomainErrorCodes`, application codes in `ErrorCodes`.

| `code` | Typical status | Meaning |
|---|---|---|
| `validation.failed` | 400 | Field validation failed; see `errors`/`errorCodes` |
| `request.invalid` | 400 | Model binding failed (malformed JSON, wrong type, missing body) |
| `auth.unauthorized` | 401 | No authenticated principal |
| `access.forbidden` | 403 | Authenticated but not allowed |
| `resource.not_found` | 404 | Missing or outside the caller's scope |
| `request.method_not_allowed` | 405 | Route does not support the HTTP method |
| `resource.conflict` | 409 | State conflict |
| `request.unsupported_media_type` | 415 | Request content type is not supported |
| `server.error` | 500 | Unexpected server failure |

Feature/domain codes are documented in each feature's contract (for example
`appointment.overlap`, `doctor.unavailable`, `identity.invalid`).

## 7. Out of scope

- Sorting query parameters (ordering is fixed per endpoint).
- Offset/cursor pagination other than the contract above.
- Response envelopes other than `PagedResult<T>` for lists and `ProblemDetails` for errors.
- New list endpoints. Features add them only when their Cycle explicitly includes them.
