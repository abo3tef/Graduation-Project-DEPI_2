# Appointments API contract (F1)

> Status: implemented in F1-C3. Source of truth for the frontend (F1-C4). Any change to a route,
> body, status code or JSON rule here must be coordinated with the frontend (AGENTS.md §4.4).
> The machine-readable form is served by the app in Development at `/openapi/v1.json`.

Base path: `/api/appointments`

## 1. Temporary identity (read this first)

Authentication is **not implemented** (STATE.md Q-07, deferred by the Tech Lead). Until the real
authentication lands, the acting user is read from two request headers:

| Header | Value |
|---|---|
| `X-User-Id` | The user's GUID in hyphenated form (`xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`), not the empty GUID |
| `X-User-Role` | Exactly `Doctor` or `Secretary` (case-sensitive; numbers or other names are rejected) |

Local test identities (synthetic doctors, a secretary with assignments, patients and appointments) are created by
`backend/docs/dev/seed-dev-data.sql`.

Rules:
- The headers are honored **only when `Auth:AllowHeaderIdentity` is `true`** (set in
  `appsettings.Development.json`; `false` in `appsettings.json`). With the flag off every request is `403`.
- Missing or invalid headers, an unknown user, or an inactive account → `403`.
- Anyone who can reach the API can forge these headers. **This is for development and testing only** and must
  never be exposed beyond a developer machine. `HeaderCurrentUser`, the flag and this section are removed when
  authentication is implemented; the controller then gets `[Authorize(Roles = "Doctor,Secretary")]`
  (`// TODO (Q-07)` in `AppointmentsController`).
- The creator of an appointment is always the identified user; `createdBy`, `status` and `id` in a request body are ignored.

CORS: the `Frontend` policy (default origin `http://localhost:3000`) allows any header, so the two headers above can be sent from the browser.

## 2. Who can do what

| Action | Doctor | Secretary |
|---|---|---|
| Book / reschedule / cancel / confirm / mark no-show | Own appointments | Appointments of assigned doctors (`doctor_secretaries`) |
| Mark **Completed** | Own appointments | **Not allowed** (403) |
| Get / list | Own appointments | Assigned doctors' appointments |

Anything outside the caller's scope behaves exactly like a missing record (`404`). No endpoint exposes medical data.

## 3. JSON rules

- Property names: camelCase. Enums: **strings** (`Scheduled`, `Confirmed`, `Completed`, `Cancelled`, `NoShow`); numbers are rejected with `400`.
- Dates: `yyyy-MM-dd`. Times: `HH:mm:ss` (clinic local time, `Clinic:TimeZone` = `Africa/Cairo`). Times are stored to the whole second.
- Timestamps (`createdAtUtc`): ISO-8601 UTC.
- Appointments cannot be booked or rescheduled into the past, and a wall-clock time skipped by a daylight-saving change is rejected.

### AppointmentResponse
```json
{
  "id": "guid",
  "patientId": "guid",
  "patientName": "string",
  "patientPhone": "string | null",
  "doctorId": "guid",
  "doctorName": "string",
  "appointmentDate": "2030-01-02",
  "startTime": "10:00:00",
  "endTime": "10:30:00",
  "reason": "string | null",
  "status": "Scheduled | Confirmed | Completed | Cancelled | NoShow",
  "notes": "string | null",
  "cancellationReason": "string | null",
  "createdBy": "guid",
  "createdAtUtc": "2030-01-01T06:00:00+00:00"
}
```

### Paged list
```json
{ "items": [ /* AppointmentResponse */ ], "pageNumber": 1, "pageSize": 20, "totalCount": 0, "totalPages": 0 }
```
> Temporary contract (STATE.md Q-04): the names and limits may be aligned with the shared pagination contract later.

## 4. Endpoints

| Verb and route | Body | Success | Errors |
|---|---|---|---|
| `POST /api/appointments` | `{ patientId, doctorId, appointmentDate, startTime, endTime, reason? }` (`endTime` required, later than `startTime`; `reason` ≤ 1000) | **201** + `Location: /api/appointments/{id}` + `AppointmentResponse` | 400, 403, 404, 409 |
| `GET /api/appointments/{id}` | – | 200 `AppointmentResponse` | 403, 404 |
| `GET /api/appointments` | query: `doctorId?`, `patientId?`, `dateFrom?`, `dateTo?`, `status?`, `pageNumber=1`, `pageSize=20` (1–100) | 200 paged list, ordered by date, start time, id | 400, 403 |
| `PUT /api/appointments/{id}/schedule` | `{ appointmentDate, startTime, endTime }` | 200 `AppointmentResponse` (status unchanged; patient and doctor never change) | 400, 403, 404, 409 |
| `POST /api/appointments/{id}/cancel` | `{ cancellationReason }` (required, non-blank, ≤ 500) | 200 `AppointmentResponse` | 400, 403, 404 |
| `PUT /api/appointments/{id}/status` | `{ status }` = `Confirmed`, `Completed` or `NoShow` | 200 `AppointmentResponse` | 400, 403, 404 |

### Lookups (read-only, for the booking and filter screens)

| Verb and route | Query | Success | Errors |
|---|---|---|---|
| `GET /api/appointments/lookups/doctors` | – | 200 `[{ id, fullName, specialization }]` — a doctor gets themself; a secretary gets the **assigned, active** doctors, ordered by name | 403 |
| `GET /api/appointments/lookups/patients` | `doctorId` (required), `search?` (name or phone contains, ≤ 100 chars), `limit?` (1–20, default 10) | 200 `[{ id, fullName, phone }]` — only the **patients of that doctor**, ordered by name; at most `limit` rows | 400, 403, 404 (doctor out of scope) |

The lookups return **only** the fields shown: no date of birth, address, blood type, contacts or any medical data. Text matching is case- and accent-insensitive, and `%` / `_` in the search text are matched literally.

Rules behind the status codes:
- **Overlap (409):** the same doctor cannot have two appointments whose intervals overlap on the same date
  (`newStart < existingEnd && newEnd > existingStart`). Back-to-back appointments are allowed. `Cancelled` and
  `NoShow` appointments free their slot. A reschedule ignores the appointment's own old slot.
- **Status changes:** `Scheduled → Confirmed / Completed / NoShow`, `Confirmed → Completed / NoShow`. `Completed`,
  `Cancelled` and `NoShow` are final. Cancelling is done only through `cancel`. Invalid transitions → 400.
- **Patient:** must belong to the booking doctor; otherwise 404. The doctor must be active; otherwise 409.
- A busy doctor schedule (lock timeout) → 409 `Please try again`.

## 5. Errors

All errors are RFC 7807 `application/problem+json` and carry `traceId`. Nothing internal (stack trace, SQL,
exception text) is ever returned.

| Status | When | Body |
|---|---|---|
| 400 | Validation, malformed JSON, unknown enum or date format, domain rule (past date, invalid transition, already cancelled, …) | `{ title, status, detail }` or, for field errors, `{ title, status, errors: { "endTime": ["…"] } }` (keys camelCase) |
| 403 | Missing/invalid identity, inactive user, secretary marking Completed | `{ title: "Forbidden", status, detail }` |
| 404 | Missing or out-of-scope appointment / doctor / patient | `{ title: "Not Found", status, detail }` |
| 409 | Overlap, inactive doctor, busy schedule | `{ title: "Conflict", status, detail }` |
| 500 | Unexpected error | `{ title: "Internal Server Error", status, detail: "An unexpected error occurred. Please try again later." , traceId }` |

### Machine-readable error codes (for localization)

Every problem response produced by the application carries a stable `code`; validation responses also carry `errorCodes`
(same camelCase field keys as `errors`, holding the validator codes such as `NotNullValidator`, `NotEmptyValidator`,
`MaximumLengthValidator`, `PredicateValidator`, `InclusiveBetweenValidator`, `EnumValidator`). Clients should translate by
`code`/`errorCodes` and never show the English `detail` text. Codes are never renamed; new ones may be added.

| `code` | Status | Meaning |
|---|---|---|
| `identity.invalid` | 403 | Missing/invalid identity, unknown or inactive user |
| `appointment.complete_requires_doctor` | 403 | Only a doctor can mark Completed |
| `doctor.not_found` | 404 | Doctor missing or outside the caller's scope |
| `patient.not_found` | 404 | Patient missing or not that doctor's patient |
| `appointment.not_found` | 404 | Appointment missing or outside the caller's scope |
| `appointment.overlap` | 409 | The doctor already has an overlapping appointment |
| `doctor.unavailable` | 409 | The doctor is inactive |
| `schedule.busy` | 409 | The doctor's schedule is locked by another request; retry |
| `validation.failed` | 400 | Field validation failed (see `errors` / `errorCodes`) |
| `appointment.time_not_in_time_zone` | 400 (field `startTime`/`endTime`) | The wall-clock time does not exist (daylight-saving gap) |
| `appointment.in_the_past` | 400 | Start is in the past (clinic time zone) |
| `appointment.invalid_interval` | 400 | End is not later than start |
| `appointment.end_time_required` | 400 | End time missing |
| `appointment.not_open` | 400 | The appointment is Completed/Cancelled/NoShow and cannot change |
| `appointment.invalid_status_transition` | 400 | Not an allowed status change |
| `appointment.use_cancel_operation` | 400 | Use `cancel` instead of the status route |
| `appointment.cancellation_reason_required` | 400 | Cancel reason missing |
| `appointment.cancellation_reason_too_long` | 400 | Cancel reason over 500 characters |
| `appointment.missing_reference` | 400 | Patient/doctor/creator missing |
| `domain.rule_violation` | 400 | Any other domain rule |
| `server.error` | 500 | Unexpected error (generic body) |

Responses without a `code` come from the framework (malformed JSON, wrong types, unmatched route); treat a 400 without a `code` as `request.invalid`.

Note: errors produced by the framework itself (malformed JSON, wrong types) use the framework's standard 400
`ValidationProblemDetails` shape (`type`, `title`, `status`, `errors`, `traceId`), with JSON paths such as `$.status` as keys.
