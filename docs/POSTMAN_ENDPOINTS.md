# Medilog — Postman API reference (48 endpoints)

Evidence: `backend/src/Medilog.WebAPI/Controllers/*.cs`, request/response DTOs, validators and
domain enums under `backend/src/Medilog.Application` and `backend/src/Medilog.Domain`, plus
`backend/docs/api/CONVENTIONS.md` and the feature API docs. Where an existing document disagrees
with the source, **the source is used here** and the difference is listed at the end of this file.

---

## Quick start

1. Start the API from `backend/src/Medilog.WebAPI` (`dotnet run`, `http` launch profile). The local
   base URL is **`http://localhost:5208`**. OpenAPI in Development: `http://localhost:5208/openapi/v1.json`.
2. Sign in with one of the seeded accounts:

   | Role | Email |
   |---|---|
   | Admin | `admin@medilog.local` |
   | Doctor | `doctor@medilog.local` |
   | Secretary | `secretary@medilog.local` |

   **No password value is printed in this document.** The development password is configured
   through `Seed:Password` (dev user-secrets) and documented in
   `backend/docs/dev/SEED_ACCOUNTS.md` / `backend/docs/dev/AUTH_SETUP.md`. Open that file, copy the
   local value, and paste it into the `password` field of the login request below. Never commit it.
   Seeding only happens in `Development` with `Seed:Enabled = true`; the accounts are pre-verified.
3. Call `POST http://localhost:5208/api/auth/login` with `{"email": "doctor@medilog.local", "password": "<copy from the seed-account docs / user-secrets>"}`.
4. Copy the **`accessToken`** value from the `200` JSON response into the Postman variable
   `{{token}}` (a Tests script can do this for you):
   ```js
   pm.environment.set("token", pm.response.json().accessToken);
   ```
5. For every protected endpoint add the header `Authorization: Bearer {{token}}`. Roles come from
   the token claims only; identity headers (`X-User-*`) are not supported. There is no refresh
   token — sign in again when the token expires (default 24 h).
6. The seeded doctor starts with **no patients and no appointments** (see `SEED_ACCOUNTS.md`).
   Create a patient with `POST /api/patients` and an appointment with `POST /api/appointments`
   before testing visit, payment, timeline or lookup endpoints, or load the synthetic
   `backend/docs/dev/seed-dev-data.sql` and sign in again.

All example IDs below are synthetic GUIDs. Replace them with IDs from your own responses.

---

## Postman setup

### Variables (set at Environment or Collection level)

| Variable | Suggested value | How to obtain |
|---|---|---|
| `baseUrl` | `http://localhost:5208` | Fix from the `http` launch profile |
| `token` | *(empty at first)* | `accessToken` from `POST /api/auth/login`, or the Google sign-in response |
| `doctorId` | *(optional)* | `user.doctorId` from a Doctor login (`doctorId == user.id`) |
| `secretaryId` | *(optional)* | `user.id` from a Secretary login |
| `adminId` | *(optional)* | `user.id` from an Admin login |
| `patientId` | *(optional)* | `id` from `POST /api/patients` or `GET /api/patients/lookup` |
| `appointmentId` | *(optional)* | `id` from `POST /api/appointments` or `GET /api/payments/payable-appointments` |
| `visitId` | *(optional)* | `id` from `POST /api/medical-visits` |
| `paymentId` | *(optional)* | `id` from `POST /api/payments` |
| `diagnosisId`, `medicationId`, `vitalSignsId` | *(optional)* | `id` from the corresponding `POST` response / containing visit |

The headings below use the full literal URL so they can be pasted directly. To use the variable,
replace `http://localhost:5208` with `{{baseUrl}}`.

### Headers

| Call type | Headers |
|---|---|
| Anonymous (`/api/auth/*`) | `Content-Type: application/json` (for bodies), `Accept: application/json` |
| Protected | `Authorization: Bearer {{token}}`, plus `Content-Type: application/json` for bodies |
| Error responses | Content type is `application/problem+json`, not `application/json` |

---

## Shared conventions

### Paged lists

Every list endpoint uses the same request/response contract:

- Query: `pageNumber` (`int`, 1-based, default `1`) and `pageSize` (`int`, `1..50`, default `10`).
  Invalid values → `400 validation.failed`. A page past the end → `200` with empty `items`.
- Response envelope (note request `pageNumber` vs response **`page`**):
  ```json
  { "items": [], "page": 1, "pageSize": 10, "totalCount": 0, "totalPages": 0 }
  ```
  `totalPages = ceil(totalCount / pageSize)`, and `0` when there are no rows.

### Enums in JSON

The API registers `JsonStringEnumConverter(allowIntegerValues: false)`. **Always send strings,
never numbers**; a numeric value fails with `400 request.invalid`. Valid PascalCase labels from the
source:

| Enum / field | Valid values | Used in |
|---|---|---|
| `UserRole` (`role`) | `Doctor`, `Secretary`, `Admin` | Auth responses, admin list filter |
| `UserRole` — register `role` | `Doctor`, `Secretary` only | `POST /api/auth/register` |
| `UserRole` — admin `role` body | `Doctor`, `Secretary` only | `PUT /api/admin/users/{id}/role` |
| `AccountStatus` (`status`) | `Active`, `Inactive` | Admin status filter, status body, admin responses |
| `AppointmentStatus` (`status`) | `Scheduled`, `Confirmed`, `Completed`, `Cancelled`, `NoShow` | Appointment list filter, responses |
| `AppointmentStatus` — status body | `Confirmed`, `Completed`, `NoShow` (`Cancelled` is rejected by validation; use `cancel`) | `PUT /api/appointments/{id}/status` |
| `Gender` (`gender`) | `Male`, `Female` | Patient create/update/filter, medical profile |
| `BloodType` (`bloodType`) | `APositive`, `ANegative`, `BPositive`, `BNegative`, `ABPositive`, `ABNegative`, `OPositive`, `ONegative` | Patient create/update (doctor only), medical profile |
| `PaymentMethod` (`method`) | `Cash`, `Card` | Payment create/update/filter |
| `PaymentStatus` (`status`) | `Pending`, `Partial`, `Paid` | Payment status body, filter, responses |
| `eventType` (string filter, case-insensitive) | `Visit`, `Diagnosis`, `Medication`, `MedicalNote` | `GET /api/patients/{patientId}/timeline` |

Response-only string enums: `AllergySeverity` = `Mild`/`Moderate`/`Severe`;
`ChronicConditionStatus` = `Active`/`Controlled`/`Resolved`. Timeline `type` values use the same
four strings as `eventType`.

### Dates and times

- Date-only (`DateOnly`): `yyyy-MM-dd`, e.g. `"2030-01-02"`.
- Time-of-day (`TimeOnly`): `HH:mm:ss` in clinic time (`Africa/Cairo`), e.g. `"10:00:00"`.
- Timestamps (`DateTimeOffset`): ISO-8601 UTC, e.g. `"2026-11-10T09:30:00+00:00"`.

Appointment dates must not be in the past; use a future date such as `2030-01-02` in booking
examples. Visit/diagnosis dates must not be in the future relative to the server clock (the visit
validator allows a 5-minute tolerance).

### Errors (shared example — applies to all 48 endpoints)

All failures use RFC 7807 `application/problem+json` with `type`, `title`, `status`, `instance`,
`code`, `traceId`, plus camelCase `errors`/`errorCodes` on validation failures. Example `400`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "instance": "/api/appointments",
  "code": "validation.failed",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "errors": { "patientId": ["A patient identifier is required."] },
  "errorCodes": { "patientId": ["NotEmptyValidator"] }
}
```

Malformed JSON / wrong type / unknown enum → `400` with `code = request.invalid` and `errors` only.
Common status/code pairs: `401 auth.unauthorized`, `403 access.forbidden` (or a specific code such
as `identity.invalid`, `patient.medical_field_forbidden`, `appointment.complete_requires_doctor`),
`404 resource.not_found` (or `patient.not_found`, `appointment.not_found`, `payment.not_found`,
`admin.user_not_found`, …), `409 resource.conflict` (or `appointment.overlap`,
`payment.paid_final`, `patient.has_related_records`, …), `405 request.method_not_allowed`,
`415 request.unsupported_media_type`, `500 server.error`. Responses never contain stack traces,
SQL, secrets or patient data beyond the contract.

---

## 1. Auth — 7 endpoints (all Public / anonymous)

Controller: `AuthController` (`[ApiController]`, `[Route("api/auth")]`, `[AllowAnonymous]`).
No token is required or expected; identity is derived from credentials or the Firebase ID token.
Details: `backend/docs/api/AUTH_API.md`.

### POST http://localhost:5208/api/auth/login

- **Auth:** Public.
- **Success:** `200 OK` — `AuthResponse`. Copy `accessToken` into `{{token}}`.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `email` | body | `string` | yes | Valid email, ≤ 320 chars; trimmed/lower-cased server-side |
| `password` | body | `string` | yes | Non-empty, ≤ 128 chars. **Copy from the seed docs / user-secrets — no value here** |

```json
{
  "email": "doctor@medilog.local",
  "password": "<copy the local Seed:Password value from backend/docs/dev/SEED_ACCOUNTS.md>"
}
```

```json
{
  "accessToken": "<compact JWT>",
  "expiresAtUtc": "2026-11-20T12:00:00+00:00",
  "user": {
    "id": "0f8f6f0a-6d4c-4a54-9a5e-2f2b0c9d1a11",
    "fullName": "Demo Doctor",
    "email": "doctor@medilog.local",
    "role": "Doctor",
    "doctorId": "0f8f6f0a-6d4c-4a54-9a5e-2f2b0c9d1a11"
  }
}
```

- **Errors:** `400 validation.failed` / `request.invalid`; `401 auth.invalid_credentials` (unknown
  email or wrong password, identical answer); `403 auth.email_unverified` or
  `auth.account_inactive`; `503 auth.not_configured` (missing signing key).

### POST http://localhost:5208/api/auth/register

- **Auth:** Public.
- **Success:** `201 Created` — `AuthUserResponse`, **no `Location` header and no token**
  (decision D-38). The account is created unverified/inactive; verify the email before login.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `fullName` | body | `string` | yes | Non-empty, ≤ 200 |
| `email` | body | `string` | yes | Valid email, ≤ 320 |
| `password` | body | `string` | yes | 8–128 chars with upper-case, lower-case, digit and symbol. Placeholder only below |
| `phone` | body | `string?` | no | ≤ 30 |
| `role` | body | `UserRole` (string) | yes | `Doctor` or `Secretary` only; `Admin` → `400` |
| `specialization` | body | `string?` | no | ≤ 150; only accepted when `role` = `Doctor` |

```json
{
  "fullName": "New Doctor",
  "email": "new.doctor@example.test",
  "password": "<a new value satisfying the 8-128 / upper+lower+digit+symbol policy>",
  "phone": "01000000000",
  "role": "Doctor",
  "specialization": "Cardiology"
}
```

```json
{
  "id": "1f8f6f0a-6d4c-4a54-9a5e-2f2b0c9d1a11",
  "fullName": "New Doctor",
  "email": "new.doctor@example.test",
  "role": "Doctor",
  "doctorId": "1f8f6f0a-6d4c-4a54-9a5e-2f2b0c9d1a11"
}
```

- **Errors:** `400 validation.failed` (including `Admin` role, weak password, specialization with
  Secretary); `409 auth.email_already_registered`; `503 auth.not_configured`. A failed email send
  never leaks whether the address exists.

### POST http://localhost:5208/api/auth/verify-email

- **Auth:** Public. **Success:** `204 No Content` (empty body) — activates the account.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `token` | body | `string` | yes | Non-empty, ≤ 512; the token from the emailed link |
| `email` | body | `string` | no | Valid email, ≤ 320; copied from the link for resend prefill only |

```json
{
  "token": "<the one-time token from the verification email link>",
  "email": "new.doctor@example.test"
}
```

`email` never authorizes the change: the single-use token is the only credential. Sending only
`token` is still valid.

- **Errors:** `400 validation.failed` / `request.invalid`; `401 auth.token_invalid` (unknown, used,
  expired or wrong-purpose token).

### POST http://localhost:5208/api/auth/resend-verification

- **Auth:** Public. **Success:** `204 No Content` (empty body), whether or not the email exists.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `email` | body | `string` | yes | Valid email, ≤ 320 |

```json
{ "email": "new.doctor@example.test" }
```

- **Errors:** `400 validation.failed` / `request.invalid`; `503 auth.not_configured`.

### POST http://localhost:5208/api/auth/forgot-password

- **Auth:** Public. **Success:** `204 No Content` (empty body), whether or not the email exists.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `email` | body | `string` | yes | Valid email, ≤ 320 |

```json
{ "email": "doctor@medilog.local" }
```

- **Errors:** `400 validation.failed` / `request.invalid`; `503 auth.not_configured`.

### POST http://localhost:5208/api/auth/reset-password

- **Auth:** Public. **Success:** `204 No Content` (empty body). A reset does not verify the email.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `token` | body | `string` | yes | Non-empty, ≤ 512; single-use reset token from the email |
| `newPassword` | body | `string` | yes | 8–128 chars with upper-case, lower-case, digit and symbol |

```json
{
  "token": "<the one-time token from the reset email link>",
  "newPassword": "<a new value satisfying the 8-128 / upper+lower+digit+symbol policy>"
}
```

- **Errors:** `400 validation.failed` / `request.invalid`; `401 auth.token_invalid`.

### POST http://localhost:5208/api/auth/google

- **Auth:** Public. **Success:** `200 OK` — `AuthResponse` (same shape as `login`).
- The role is never taken from the client: only an existing, email-verified, active account with
  the same email can sign in (Q-12). No account is created here.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `idToken` | body | `string` | yes | Firebase ID token from the Google button, non-empty, ≤ 8192 |

```json
{ "idToken": "<Firebase ID token from the Google sign-in SDK>" }
```

- **Errors:** `400 validation.failed` / `request.invalid`; `401 auth.invalid_google_token`;
  `403 auth.email_unverified` / `auth.account_inactive`; `503 auth.google_not_configured`.

---

## 2. Admin — 5 endpoints (Bearer, role `Admin` only)

Controller: `AdminController` (`[Authorize(Roles = "Admin")]`). All endpoints re-check that the
acting Admin account still exists and is `Active`; an already-issued JWT is not revoked. Details:
`backend/docs/api/ADMIN_API.md`.

### GET http://localhost:5208/api/admin/users

- **Auth:** Bearer `Admin`. **Success:** `200 OK` — `PagedResult<AdminUserResponse>`.
- Filter/count/order in the data source; order is `email` (ordinal), then `id`. The list includes
  `Admin` accounts. No token, password hash or one-time token hash is ever returned.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `role` | query | `string?` | no | `Admin`, `Doctor` or `Secretary` (case-insensitive) |
| `status` | query | `string?` | no | `Active` or `Inactive` (case-insensitive) |
| `search` | query | `string?` | no | ≤ 100; GUID matches id, otherwise contains over name/email/phone |
| `pageNumber` | query | `int` | no | ≥ 1, default `1` |
| `pageSize` | query | `int` | no | `1..50`, default `10` |

No request body.

```json
{
  "items": [
    {
      "id": "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
      "role": "Doctor",
      "fullName": "Demo Doctor",
      "email": "doctor@medilog.local",
      "phone": "01000000000",
      "specialization": "General Medicine",
      "status": "Active",
      "emailVerified": true,
      "createdAtUtc": "2026-11-18T09:00:00+00:00"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 4,
  "totalPages": 1
}
```

- **Errors:** `400 validation.failed` (bad filter/paging); `401`; `403 access.forbidden` for a
  non-Admin or `identity.invalid` for an inactive acting Admin.

### PATCH http://localhost:5208/api/admin/users/{id}/status

- **Auth:** Bearer `Admin`. **Success:** `200 OK` — `AdminUserResponse`. Repeating the current
  status is a successful no-op. **Method is `PATCH`, not `PUT`** (the API catalog index is stale).

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `id` | route | `Guid` (`{id:guid}`) | yes | Target account id |
| `status` | body | `AccountStatus` (string) | yes | `Active` or `Inactive` |

```json
{ "status": "Inactive" }
```

Response: an `AdminUserResponse` object as shown in the previous endpoint.

- **Errors:** `400 validation.failed` / `request.invalid`; `401`; `403 access.forbidden`,
  `identity.invalid`, `admin.self_deactivation_forbidden`; `404 admin.user_not_found`;
  `409 admin.last_admin_protected`.

### PUT http://localhost:5208/api/admin/users/{id}/role

- **Auth:** Bearer `Admin`. **Success:** `200 OK` — `AdminUserResponse`.
- Q-22: Doctor↔Secretary only; an account-table move preserving id, credentials, status,
  verification and `createdAtUtc`. An Admin target or self-change is forbidden.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `id` | route | `Guid` (`{id:guid}`) | yes | Target account id |
| `role` | body | `UserRole` (string) | yes | `Doctor` or `Secretary`; `Admin` → `400` |

```json
{ "role": "Secretary" }
```

Response: an `AdminUserResponse` object (with the new `role` value).

- **Errors:** `400 validation.failed` / `request.invalid`; `401`; `403 access.forbidden`,
  `identity.invalid`, `admin.self_role_change_forbidden`, `admin.role_change_forbidden`;
  `404 admin.user_not_found`; `409 admin.role_change_blocked` (conflicting owned data).

### GET http://localhost:5208/api/admin/secretaries/{secretaryId}/doctors

- **Auth:** Bearer `Admin`. **Success:** `200 OK` — `SecretaryDoctorsResponse`; doctors ordered
  by `email`, then `id`.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `secretaryId` | route | `Guid` (`{secretaryId:guid}`) | yes | Secretary account id |

No query or body parameters.

```json
{
  "secretaryId": "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
  "doctors": [
    { "id": "1f2504e0-4f89-41d3-9a0c-0305e82c3301", "fullName": "Doctor A", "email": "a@medilog.test" }
  ]
}
```

- **Errors:** `400`; `401`; `403`; `404 admin.secretary_not_found`.

### PUT http://localhost:5208/api/admin/secretaries/{secretaryId}/doctors

- **Auth:** Bearer `Admin`. **Success:** `200 OK` — `SecretaryDoctorsResponse`.
- Full replacement of the assignment set; `[]` clears all assignments. Assignments scope the
  secretary's patient/appointment access (Q-16).

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `secretaryId` | route | `Guid` (`{secretaryId:guid}`) | yes | Secretary account id |
| `doctorIds` | body | `IReadOnlyList<Guid>` | yes | ≤ 100 ids, no duplicates, no `Guid.Empty` |

```json
{
  "doctorIds": ["1f2504e0-4f89-41d3-9a0c-0305e82c3301"]
}
```

Response: `SecretaryDoctorsResponse` as shown above.

- **Errors:** `400 validation.failed` (empty GUID, duplicate, > 100) / `request.invalid`; `401`;
  `403`; `404 admin.secretary_not_found` or `doctor.not_found`.

---

## 3. Appointments — 8 endpoints (Bearer, roles `Doctor`, `Secretary`)

Controller: `AppointmentsController` (`[Authorize(Roles = "Doctor,Secretary")]`). Doctors act on
their own appointments; secretaries on appointments of their assigned, active doctors. Anything
out of scope returns `404`. No medical data is exposed. Details: `backend/docs/api/appointments.md`.

### POST http://localhost:5208/api/appointments

- **Auth:** Bearer `Doctor` or `Secretary`. **Success:** `201 Created` + `Location` header +
  `AppointmentResponse`. The creator, `id` and `status` come from the server, not the body.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `patientId` | body | `Guid` | yes | Must belong to the booking doctor |
| `doctorId` | body | `Guid` | yes | Self for a doctor; assigned active doctor for a secretary |
| `appointmentDate` | body | `DateOnly?` | yes | `yyyy-MM-dd`, not in the past |
| `startTime` | body | `TimeOnly?` | yes | `HH:mm:ss` |
| `endTime` | body | `TimeOnly?` | yes | Must be later than `startTime` |
| `reason` | body | `string?` | no | ≤ 1000 |

```json
{
  "patientId": "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
  "doctorId": "0f8f6f0a-6d4c-4a54-9a5e-2f2b0c9d1a11",
  "appointmentDate": "2030-01-02",
  "startTime": "10:00:00",
  "endTime": "10:30:00",
  "reason": "Follow-up consultation"
}
```

```json
{
  "id": "5a6b7c8d-9e0f-4a1b-8c2d-3e4f5a6b7c8d",
  "patientId": "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
  "patientName": "Sara Ali",
  "patientPhone": "01001234567",
  "doctorId": "0f8f6f0a-6d4c-4a54-9a5e-2f2b0c9d1a11",
  "doctorName": "Demo Doctor",
  "appointmentDate": "2030-01-02",
  "startTime": "10:00:00",
  "endTime": "10:30:00",
  "reason": "Follow-up consultation",
  "status": "Scheduled",
  "notes": null,
  "cancellationReason": null,
  "createdBy": "0f8f6f0a-6d4c-4a54-9a5e-2f2b0c9d1a11",
  "createdAtUtc": "2026-11-19T08:00:00+00:00"
}
```

- **Errors:** `400 validation.failed` / `request.invalid` (missing values, bad interval, past date,
  DST gap, bad time zone); `403 identity.invalid`; `404 patient.not_found` / `doctor.not_found`;
  `409 appointment.overlap`, `doctor.unavailable`, `schedule.busy`.

### GET http://localhost:5208/api/appointments/{id}

- **Auth:** Bearer `Doctor` or `Secretary`. **Success:** `200 OK` — `AppointmentResponse`.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `id` | route | `Guid` (`{id:guid}`) | yes | Appointment id |

No query or body parameters. Response shape as above.

- **Errors:** `400`; `401`; `403 identity.invalid`; `404 appointment.not_found`.

### GET http://localhost:5208/api/appointments

- **Auth:** Bearer `Doctor` or `Secretary`. **Success:** `200 OK` —
  `PagedResult<AppointmentResponse>`, ordered by `appointmentDate`, `startTime`, `id`.
- A `doctorId` filter can only narrow the caller's scope, never widen it.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `doctorId` | query | `Guid?` | no | Must be a doctor the caller may act for |
| `patientId` | query | `Guid?` | no | Patient filter |
| `dateFrom` | query | `DateOnly?` | no | Inclusive; `yyyy-MM-dd` |
| `dateTo` | query | `DateOnly?` | no | Inclusive; must be ≥ `dateFrom` |
| `status` | query | `AppointmentStatus?` | no | `Scheduled`, `Confirmed`, `Completed`, `Cancelled`, `NoShow` |
| `pageNumber` | query | `int` | no | ≥ 1, default `1` |
| `pageSize` | query | `int` | no | `1..50`, default `10` |

```json
{
  "items": [
    {
      "id": "5a6b7c8d-9e0f-4a1b-8c2d-3e4f5a6b7c8d",
      "patientId": "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
      "patientName": "Sara Ali",
      "patientPhone": "01001234567",
      "doctorId": "0f8f6f0a-6d4c-4a54-9a5e-2f2b0c9d1a11",
      "doctorName": "Demo Doctor",
      "appointmentDate": "2030-01-02",
      "startTime": "10:00:00",
      "endTime": "10:30:00",
      "reason": "Follow-up consultation",
      "status": "Scheduled",
      "notes": null,
      "cancellationReason": null,
      "createdBy": "0f8f6f0a-6d4c-4a54-9a5e-2f2b0c9d1a11",
      "createdAtUtc": "2026-11-19T08:00:00+00:00"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1
}
```

- **Errors:** `400 validation.failed` (bad paging, `dateTo < dateFrom`, unknown `status`); `401`;
  `403 identity.invalid`.

### GET http://localhost:5208/api/appointments/lookups/doctors

- **Auth:** Bearer `Doctor` or `Secretary`. **Success:** `200 OK` — array of
  `DoctorLookupResponse`. A doctor gets themself; a secretary gets assigned **active** doctors
  ordered by name. No parameters, no body.

```json
[
  { "id": "0f8f6f0a-6d4c-4a54-9a5e-2f2b0c9d1a11", "fullName": "Demo Doctor", "specialization": "General Medicine" }
]
```

- **Errors:** `401`; `403 identity.invalid`. If nothing is in scope the result is `[]`.

### GET http://localhost:5208/api/appointments/lookups/patients

- **Auth:** Bearer `Doctor` or `Secretary`. **Success:** `200 OK` — `IReadOnlyList<PatientLookupResponse>`
  (only `id`, `fullName`, `phone`; at most `limit` rows, ordered by name).

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `doctorId` | query | `Guid` | yes | Doctor whose patients are searched; must be in scope |
| `search` | query | `string?` | no | Name or phone contains, ≤ 100 |
| `limit` | query | `int` | no | `1..20`, default `10` |

```json
[
  { "id": "3f2504e0-4f89-41d3-9a0c-0305e82c3301", "fullName": "Sara Ali", "phone": "01001234567" }
]
```

- **Errors:** `400 validation.failed` (`limit` out of range, missing/empty `doctorId`, long
  `search`); `401`; `403`; `404 doctor.not_found` when out of scope.

### PUT http://localhost:5208/api/appointments/{id}/schedule

- **Auth:** Bearer `Doctor` or `Secretary`. **Success:** `200 OK` — `AppointmentResponse`.
  Patient and doctor never change; the status is unchanged.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `id` | route | `Guid` (`{id:guid}`) | yes | Appointment id |
| `appointmentDate` | body | `DateOnly?` | yes | `yyyy-MM-dd`, not in the past |
| `startTime` | body | `TimeOnly?` | yes | `HH:mm:ss` |
| `endTime` | body | `TimeOnly?` | yes | Later than `startTime` |

```json
{
  "appointmentDate": "2030-01-02",
  "startTime": "11:00:00",
  "endTime": "11:30:00"
}
```

Response: `AppointmentResponse` as above (the appointment's own old slot is ignored for overlap).

- **Errors:** `400 validation.failed` / domain codes; `401`; `403`; `404 appointment.not_found`;
  `409 appointment.overlap`, `schedule.busy`.

### POST http://localhost:5208/api/appointments/{id}/cancel

- **Auth:** Bearer `Doctor` or `Secretary`. **Success:** `200 OK` — `AppointmentResponse`
  with `status = "Cancelled"`.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `id` | route | `Guid` (`{id:guid}`) | yes | Appointment id |
| `cancellationReason` | body | `string?` | yes | Non-blank after trim, ≤ 500 |

```json
{ "cancellationReason": "Patient requested a different day" }
```

- **Errors:** `400 validation.failed` / `appointment.cancellation_reason_required` /
  `appointment.cancellation_reason_too_long` / `appointment.not_open`; `401`; `403`;
  `404 appointment.not_found`.

### PUT http://localhost:5208/api/appointments/{id}/status

- **Auth:** Bearer `Doctor` or `Secretary`. A **secretary cannot set `Completed`**
  (`403 appointment.complete_requires_doctor`); `Confirmed` and `NoShow` are allowed for both.
  **Success:** `200 OK` — `AppointmentResponse`.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `id` | route | `Guid` (`{id:guid}`) | yes | Appointment id |
| `status` | body | `AppointmentStatus?` | yes | `Confirmed`, `Completed`, `NoShow`; `Cancelled` → `400` (use `cancel`) |

```json
{ "status": "Confirmed" }
```

- **Errors:** `400 validation.failed` / `appointment.invalid_status_transition` /
  `appointment.use_cancel_operation`; `401`; `403 appointment.complete_requires_doctor`;
  `404 appointment.not_found`.

---

## 4. Patients — 6 endpoints (Bearer, roles `Doctor`, `Secretary`)

Controller: `PatientsController` (`[Authorize(Roles = "Doctor,Secretary")]`). Doctors see their own
patients including `bloodType`; secretaries see patients of assigned doctors and **only**
administrative fields (`bloodType` is absent from responses and forbidden in requests). Details:
`backend/docs/api/PATIENTS_API.md`.

### POST http://localhost:5208/api/patients

- **Auth:** Bearer `Doctor` or `Secretary`. **Success:** `201 Created` + `Location` +
  `PatientResponse`. A doctor's owner is always the token user; a secretary **must** supply an
  assigned `doctorId`.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `doctorId` | body | `Guid?` | secretary only | Ignored for a doctor; must name an assigned doctor for a secretary |
| `fullName` | body | `string?` | yes | Non-empty, ≤ 200 |
| `dateOfBirth` | body | `DateOnly?` | yes | `yyyy-MM-dd`, ≥ 1900-01-01, not in the future |
| `gender` | body | `Gender?` | yes | `Male` or `Female` |
| `phone` | body | `string?` | no | ≤ 30; optional `+`, 7–20 chars of digits/spaces/dashes/parentheses, ≥ 7 digits |
| `address` | body | `string?` | no | ≤ 500 |
| `emergencyContact` | body | `string?` | no | ≤ 100 |
| `bloodType` | body | `BloodType?` | no | Doctor only; a secretary sending it → `403` |

```json
{
  "doctorId": "0f8f6f0a-6d4c-4a54-9a5e-2f2b0c9d1a11",
  "fullName": "Sara Ali",
  "dateOfBirth": "1990-04-12",
  "gender": "Female",
  "phone": "01001234567",
  "address": "12 Nile Street",
  "emergencyContact": "Mother 01111111111",
  "bloodType": "OPositive"
}
```

```json
{
  "id": "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
  "doctorId": "0f8f6f0a-6d4c-4a54-9a5e-2f2b0c9d1a11",
  "fullName": "Sara Ali",
  "dateOfBirth": "1990-04-12",
  "gender": "Female",
  "phone": "01001234567",
  "address": "12 Nile Street",
  "emergencyContact": "Mother 01111111111",
  "bloodType": "OPositive",
  "createdAt": "2026-11-19T09:00:00+00:00"
}
```

- **Errors:** `400 validation.failed` / `request.invalid`; `403 patient.medical_field_forbidden`
  (secretary + `bloodType`) or `identity.invalid`; `404 doctor.not_found`.

### GET http://localhost:5208/api/patients

- **Auth:** Bearer `Doctor` or `Secretary`. **Success:** `200 OK` —
  `PagedResult<PatientResponse>`, ordered by `fullName`, then `id`.
- `search` matches an exact patient id (GUID) otherwise name/phone contains. `bloodType` is
  omitted from every item for a secretary.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `search` | query | `string?` | no | ≤ 100 |
| `gender` | query | `Gender?` | no | `Male` or `Female` |
| `doctorId` | query | `Guid?` | no | Narrows scope only |
| `pageNumber` | query | `int` | no | ≥ 1, default `1` |
| `pageSize` | query | `int` | no | `1..50`, default `10` |

Response envelope as documented above; `items` contain `PatientResponse` objects as shown in the
create response.

- **Errors:** `400 validation.failed`; `401`; `403 identity.invalid`.

### GET http://localhost:5208/api/patients/lookup

- **Auth:** Bearer `Doctor` or `Secretary`. **Success:** `200 OK` — bounded picker list of
  `{id, fullName, phone}` (this is the `PatientsController` picker; the appointments controller
  also has `/api/appointments/lookups/patients`).

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `search` | query | `string?` | no | ≤ 100 |
| `doctorId` | query | `Guid?` | no | Narrows scope only |
| `limit` | query | `int` | no | `1..20`, default `10` |

```json
[
  { "id": "3f2504e0-4f89-41d3-9a0c-0305e82c3301", "fullName": "Sara Ali", "phone": "01001234567" }
]
```

- **Errors:** `400 validation.failed`; `401`; `403`.

### GET http://localhost:5208/api/patients/{id}

- **Auth:** Bearer `Doctor` or `Secretary`. **Success:** `200 OK` — `PatientResponse`.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `id` | route | `Guid` (`{id:guid}`) | yes | Patient id |

No query or body parameters. Response shape as in the create example.

- **Errors:** `400`; `401`; `403`; `404 patient.not_found` (also when out of scope).

### PUT http://localhost:5208/api/patients/{id}

- **Auth:** Bearer `Doctor` or `Secretary`. **Success:** `200 OK` — `PatientResponse`.
  Full replacement of editable fields. A doctor omitting `bloodType` clears it; a secretary never
  changes the stored `bloodType`.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `id` | route | `Guid` (`{id:guid}`) | yes | Patient id |
| `fullName` | body | `string?` | yes | Non-empty, ≤ 200 |
| `dateOfBirth` | body | `DateOnly?` | yes | ≥ 1900-01-01, not in the future |
| `gender` | body | `Gender?` | yes | `Male` or `Female` |
| `phone` | body | `string?` | no | ≤ 30, phone pattern |
| `address` | body | `string?` | no | ≤ 500 |
| `emergencyContact` | body | `string?` | no | ≤ 100 |
| `bloodType` | body | `BloodType?` | doctor only | Secretary must omit it |

```json
{
  "fullName": "Sara Ali",
  "dateOfBirth": "1990-04-12",
  "gender": "Female",
  "phone": "01001234567",
  "address": "15 Nile Street",
  "emergencyContact": "Mother 01111111111",
  "bloodType": "OPositive"
}
```

- **Errors:** `400 validation.failed`; `401`; `403 patient.medical_field_forbidden` / `identity.invalid`;
  `404 patient.not_found`.

### DELETE http://localhost:5208/api/patients/{id}

- **Auth:** Bearer `Doctor` **only** (a secretary gets `403`). **Success:** `204 No Content`.
  Hard delete allowed only when the patient has **no** related records; nothing is cascaded (Q-17).

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `id` | route | `Guid` (`{id:guid}`) | yes | Patient id |

No query or body parameters.

- **Errors:** `401`; `403 access.forbidden` for a secretary, `identity.invalid`;
  `404 patient.not_found`; `409 patient.has_related_records` (any appointment, visit, payment or
  other clinical record).

---

## 5. Payments — 6 endpoints (Bearer, roles `Doctor`, `Secretary`)

Controller: `PaymentsController` (`[Authorize(Roles = "Doctor,Secretary")]`). Doctors see own
payments; secretaries see payments of assigned doctors. Out-of-scope resources behave as `404`.
Only billing data and the patient name are returned. Model: `STATE.md` Q-01/Q-21.

### POST http://localhost:5208/api/payments

- **Auth:** Bearer `Doctor` or `Secretary`. **Success:** `201 Created` + `Location` +
  `PaymentResponse`. The new payment always has `status = "Pending"`.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `appointmentId` | body | `Guid` | yes | Payable appointment in scope (not Cancelled/No-Show, not already billed) |
| `amount` | body | `decimal?` | yes | `> 0` and `≤ 1000000`, at most 2 decimals |
| `method` | body | `PaymentMethod?` | yes | `Cash` or `Card` |
| `notes` | body | `string?` | no | ≤ 500 |

```json
{
  "appointmentId": "5a6b7c8d-9e0f-4a1b-8c2d-3e4f5a6b7c8d",
  "amount": 250.00,
  "method": "Cash",
  "notes": "Copay received at reception"
}
```

```json
{
  "id": "6b7c8d9e-0f1a-4b2c-9d3e-4f5a6b7c8d9e",
  "patientId": "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
  "patientName": "Sara Ali",
  "doctorId": "0f8f6f0a-6d4c-4a54-9a5e-2f2b0c9d1a11",
  "appointmentId": "5a6b7c8d-9e0f-4a1b-8c2d-3e4f5a6b7c8d",
  "appointmentDate": "2030-01-02",
  "amount": 250.00,
  "method": "Cash",
  "status": "Pending",
  "paidAt": null,
  "notes": "Copay received at reception"
}
```

- **Errors:** `400 validation.failed` / `request.invalid` (bad amount, method, notes);
  `401`; `403 identity.invalid`; `404 payment.appointment_not_found`; `409 payment.already_exists`.

### GET http://localhost:5208/api/payments/{id}

- **Auth:** Bearer `Doctor` or `Secretary`. **Success:** `200 OK` — `PaymentResponse`.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `id` | route | `Guid` (`{id:guid}`) | yes | Payment id |

No query or body parameters. Response shape as above.

- **Errors:** `401`; `403`; `404 payment.not_found`.

### GET http://localhost:5208/api/payments

- **Auth:** Bearer `Doctor` or `Secretary`. **Success:** `200 OK` —
  `PagedResult<PaymentResponse>`.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `search` | query | `string?` | no | ≤ 100 |
| `status` | query | `PaymentStatus?` | no | `Pending`, `Partial`, `Paid` |
| `method` | query | `PaymentMethod?` | no | `Cash`, `Card` |
| `patientId` | query | `Guid?` | no | Patient filter |
| `appointmentId` | query | `Guid?` | no | Appointment filter |
| `doctorId` | query | `Guid?` | no | Narrows scope only |
| `dateFrom` | query | `DateOnly?` | no | Appointment date, inclusive |
| `dateTo` | query | `DateOnly?` | no | Must be ≥ `dateFrom` |
| `pageNumber` | query | `int` | no | ≥ 1, default `1` |
| `pageSize` | query | `int` | no | `1..50`, default `10` |

Response envelope with `items` of `PaymentResponse` objects.

- **Errors:** `400 validation.failed`; `401`; `403`.

### GET http://localhost:5208/api/payments/payable-appointments

- **Auth:** Bearer `Doctor` or `Secretary`. **Success:** `200 OK` —
  `IReadOnlyList<PayableAppointmentResponse>` for the payment-form picker: appointments in scope
  that are not `Cancelled` / `NoShow` and have no payment yet.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `search` | query | `string?` | no | ≤ 100 |
| `limit` | query | `int` | no | `1..100`, default `50` |

```json
[
  {
    "id": "5a6b7c8d-9e0f-4a1b-8c2d-3e4f5a6b7c8d",
    "patientId": "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
    "patientName": "Sara Ali",
    "doctorId": "0f8f6f0a-6d4c-4a54-9a5e-2f2b0c9d1a11",
    "appointmentDate": "2030-01-02",
    "startTime": "10:00:00"
  }
]
```

- **Errors:** `400 validation.failed` (`limit` out of range, long `search`); `401`; `403`.

### PUT http://localhost:5208/api/payments/{id}

- **Auth:** Bearer `Doctor` or `Secretary`. **Success:** `200 OK` — `PaymentResponse`.
  Only amount, method and notes are editable; `Paid` payments are immutable (`409 payment.paid_final`).

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `id` | route | `Guid` (`{id:guid}`) | yes | Payment id |
| `amount` | body | `decimal?` | yes | `> 0` and `≤ 1000000`, ≤ 2 decimals |
| `method` | body | `PaymentMethod?` | yes | `Cash` or `Card` |
| `notes` | body | `string?` | no | ≤ 500 (omit or `null` to clear) |

```json
{
  "amount": 275.50,
  "method": "Card",
  "notes": "Card terminal receipt updated"
}
```

- **Errors:** `400 validation.failed` / `request.invalid`; `401`; `403`; `404 payment.not_found`;
  `409 payment.paid_final`.

### PUT http://localhost:5208/api/payments/{id}/status

- **Auth:** Bearer `Doctor` or `Secretary`. **Success:** `200 OK` — `PaymentResponse`.
  Transitions: `Pending → Partial|Paid`, `Partial → Paid`; `Paid` is final. Only `Paid` counts
  towards revenue.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `id` | route | `Guid` (`{id:guid}`) | yes | Payment id |
| `status` | body | `PaymentStatus?` | yes | `Pending`, `Partial`, `Paid` |

```json
{ "status": "Partial" }
```

- **Errors:** `400 validation.failed` / `request.invalid`; `401`; `403`; `404 payment.not_found`;
  `409 payment.invalid_status_transition`, `payment.paid_final`.

---

## 6. Medical visits — 12 endpoints (Bearer, role `Doctor` only)

Controller: `MedicalVisitsController` (`[Authorize(Roles = "Doctor")]`). The acting doctor comes
from the token; a Secretary gets `403` at the HTTP layer. Out-of-workspace records return `404`.
Details: `backend/docs/api/MEDICAL_RECORDS_API.md`.

### POST http://localhost:5208/api/medical-visits

- **Auth:** Bearer `Doctor`. **Success:** `201 Created` + `Location: /api/medical-visits/{visitId}`
  + `MedicalVisitResponse`.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `patientId` | body | `Guid` | yes | Patient owned by the acting doctor |
| `appointmentId` | body | `Guid?` | no | `null` or a non-empty GUID of an appointment of the same patient/doctor, not linked to another visit |
| `visitDate` | body | `DateTimeOffset` | yes | ISO-8601 UTC, not in the future (5-minute tolerance) |
| `chiefComplaint` | body | `string?` | no | ≤ 2000; whitespace → `null` |
| `symptoms` | body | `string?` | no | ≤ 2000; whitespace → `null` |
| `clinicalNotes` | body | `string?` | no | ≤ 4000; whitespace → `null` |

```json
{
  "patientId": "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
  "appointmentId": null,
  "visitDate": "2026-11-10T09:30:00+00:00",
  "chiefComplaint": "Headache",
  "symptoms": "Since yesterday",
  "clinicalNotes": "Rest and hydration"
}
```

```json
{
  "id": "9b2d4c1e-8a17-4f5b-9c2e-1a2b3c4d5e6f",
  "patientId": "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
  "doctorId": "0f8f6f0a-6d4c-4a54-9a5e-2f2b0c9d1a11",
  "appointmentId": null,
  "visitDate": "2026-11-10T09:30:00+00:00",
  "chiefComplaint": "Headache",
  "symptoms": "Since yesterday",
  "clinicalNotes": "Rest and hydration",
  "diagnoses": [],
  "medications": [],
  "vitalSigns": []
}
```

- **Errors:** `400 validation.failed` / `appointment.mismatch`; `401`; `403`; `404` for unknown
  patient/appointment; `409` when the appointment is already linked to another visit.

### GET http://localhost:5208/api/medical-visits/{visitId}

- **Auth:** Bearer `Doctor`. **Success:** `200 OK` — `MedicalVisitResponse` including diagnoses,
  medications and vital signs (diagnoses ordered by `diagnosisDate`, then `id`; medications by
  `medicationName`, then `id`).

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `visitId` | route | `Guid` (`{visitId:guid}`) | yes | Visit id |

No query or body parameters. Response shape as above.

- **Errors:** `401`; `403`; `404` (unknown visit or another doctor's visit).

### PUT http://localhost:5208/api/medical-visits/{visitId}

- **Auth:** Bearer `Doctor`. **Success:** `200 OK` — `MedicalVisitResponse`. Patient, doctor and
  appointment link are immutable and are not part of the body.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `visitId` | route | `Guid` (`{visitId:guid}`) | yes | Visit id |
| `visitDate` | body | `DateTimeOffset` | yes | ISO-8601 UTC, not in the future |
| `chiefComplaint` | body | `string?` | no | ≤ 2000 |
| `symptoms` | body | `string?` | no | ≤ 2000 |
| `clinicalNotes` | body | `string?` | no | ≤ 4000 |

```json
{
  "visitDate": "2026-11-10T10:00:00+00:00",
  "chiefComplaint": "Updated complaint",
  "symptoms": null,
  "clinicalNotes": "Updated notes"
}
```

- **Errors:** `400 validation.failed`; `401`; `403`; `404`.

### POST http://localhost:5208/api/medical-visits/{visitId}/diagnoses

- **Auth:** Bearer `Doctor`. **Success:** `201 Created` + `Location` pointing at the parent visit
  + `DiagnosisResponse` (there is no GET-diagnosis route).

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `visitId` | route | `Guid` (`{visitId:guid}`) | yes | Parent visit id |
| `diagnosisName` | body | `string` | yes | Non-empty, ≤ 200, trimmed |
| `description` | body | `string?` | no | ≤ 4000; whitespace → `null` |
| `diagnosisDate` | body | `DateOnly` | yes | `yyyy-MM-dd`, not in the future |

```json
{
  "diagnosisName": "Migraine",
  "description": "Throbbing unilateral pain",
  "diagnosisDate": "2026-11-09"
}
```

```json
{
  "id": "b1c2d3e4-f5a6-4b7c-8d9e-0f1a2b3c4d5e",
  "visitId": "9b2d4c1e-8a17-4f5b-9c2e-1a2b3c4d5e6f",
  "patientId": "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
  "diagnosisName": "Migraine",
  "description": "Throbbing unilateral pain",
  "diagnosisDate": "2026-11-09"
}
```

- **Errors:** `400 validation.failed`; `401`; `403`; `404`.

### PUT http://localhost:5208/api/medical-visits/{visitId}/diagnoses/{diagnosisId}

- **Auth:** Bearer `Doctor`. **Success:** `200 OK` — `DiagnosisResponse`. The diagnosis must belong
  to the visit in the route.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `visitId` | route | `Guid` (`{visitId:guid}`) | yes | Parent visit id |
| `diagnosisId` | route | `Guid` (`{diagnosisId:guid}`) | yes | Diagnosis id |
| `diagnosisName` | body | `string` | yes | Non-empty, ≤ 200 |
| `description` | body | `string?` | no | ≤ 4000 |
| `diagnosisDate` | body | `DateOnly` | yes | Not in the future |

```json
{
  "diagnosisName": "Migraine with aura",
  "description": "Follow-up description",
  "diagnosisDate": "2026-11-09"
}
```

- **Errors:** `400 validation.failed`; `401`; `403`; `404` (cross-visit or foreign diagnosis too).

### POST http://localhost:5208/api/medical-visits/{visitId}/medications

- **Auth:** Bearer `Doctor`. **Success:** `201 Created` + `Location` pointing at the parent visit
  + `MedicationResponse`.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `visitId` | route | `Guid` (`{visitId:guid}`) | yes | Parent visit id |
| `medicationName` | body | `string` | yes | Non-empty, ≤ 200 |
| `dosage` | body | `string` | yes | Non-empty, ≤ 100 |
| `frequency` | body | `string` | yes | Non-empty, ≤ 100 |
| `duration` | body | `string?` | no | ≤ 100; whitespace → `null` |
| `instructions` | body | `string?` | no | ≤ 4000; whitespace → `null` |

```json
{
  "medicationName": "Amoxicillin",
  "dosage": "500 mg",
  "frequency": "Every 8 hours",
  "duration": "7 days",
  "instructions": "Take after food"
}
```

```json
{
  "id": "c2d3e4f5-a6b7-4c8d-9e0f-1a2b3c4d5e6f",
  "visitId": "9b2d4c1e-8a17-4f5b-9c2e-1a2b3c4d5e6f",
  "patientId": "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
  "medicationName": "Amoxicillin",
  "dosage": "500 mg",
  "frequency": "Every 8 hours",
  "duration": "7 days",
  "instructions": "Take after food"
}
```

- **Errors:** `400 validation.failed`; `401`; `403`; `404`.

### PUT http://localhost:5208/api/medical-visits/{visitId}/medications/{medicationId}

- **Auth:** Bearer `Doctor`. **Success:** `200 OK` — `MedicationResponse`.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `visitId` | route | `Guid` (`{visitId:guid}`) | yes | Parent visit id |
| `medicationId` | route | `Guid` (`{medicationId:guid}`) | yes | Medication id |
| `medicationName` | body | `string` | yes | Non-empty, ≤ 200 |
| `dosage` | body | `string` | yes | Non-empty, ≤ 100 |
| `frequency` | body | `string` | yes | Non-empty, ≤ 100 |
| `duration` | body | `string?` | no | ≤ 100 |
| `instructions` | body | `string?` | no | ≤ 4000 |

```json
{
  "medicationName": "Amoxicillin",
  "dosage": "1000 mg",
  "frequency": "Every 12 hours",
  "duration": "5 days",
  "instructions": "Take after food"
}
```

- **Errors:** `400 validation.failed`; `401`; `403`; `404`.

### DELETE http://localhost:5208/api/medical-visits/{visitId}/medications/{medicationId}

- **Auth:** Bearer `Doctor`. **Success:** `204 No Content`. Permanent (hard) delete to correct a
  mistaken line (D-23); the visit and its other records are unaffected.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `visitId` | route | `Guid` (`{visitId:guid}`) | yes | Parent visit id |
| `medicationId` | route | `Guid` (`{medicationId:guid}`) | yes | Medication id |

No body.

- **Errors:** `401`; `403`; `404` (unknown, cross-visit or foreign record).

### DELETE http://localhost:5208/api/medical-visits/{visitId}/diagnoses/{diagnosisId}

- **Auth:** Bearer `Doctor`. **Success:** `204 No Content`. Permanent (hard) delete by the owning
  doctor (D-23, F4-v2); an unknown/cross-visit/foreign diagnosis returns the same `404`.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `visitId` | route | `Guid` (`{visitId:guid}`) | yes | Parent visit id |
| `diagnosisId` | route | `Guid` (`{diagnosisId:guid}`) | yes | Diagnosis id |

No body.

- **Errors:** `401`; `403`; `404`.

### POST http://localhost:5208/api/medical-visits/{visitId}/vital-signs

- **Auth:** Bearer `Doctor`. **Success:** `201 Created` + `Location: /api/medical-visits/{visitId}/vital-signs`
  + `VisitVitalSignsResponse`. Records are appended (1:M); client ids/ownership are ignored.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `visitId` | route | `Guid` (`{visitId:guid}`) | yes | Visit owned by the acting doctor |
| `bloodPressure` | body | `string?` | no (≥1 field required) | `systolic/diastolic`, ≤ 20 chars. **Code limits:** systolic `50..300`, diastolic `20..200`, systolic > diastolic |
| `heartRate` | body | `int?` | no | **Code limits:** `20..300` |
| `temperature` | body | `decimal?` | no | `30.0..45.0` (°C) |
| `respiratoryRate` | body | `int?` | no | **Code limits:** `4..80` |
| `weight` | body | `decimal?` | no | **Code limits:** `0.3..500` (kg) |
| `height` | body | `decimal?` | no | **Code limits:** `20..260` (cm) |
| `spO2` | body | `int?` | no | **Code limits:** `50..100` |

```json
{
  "bloodPressure": "120/80",
  "heartRate": 72,
  "temperature": 36.8,
  "respiratoryRate": 16,
  "weight": 70.5,
  "height": 175.0,
  "spO2": 98
}
```

```json
{
  "id": "d3e4f5a6-b7c8-4d9e-0f1a-2b3c4d5e6f70",
  "bloodPressure": "120/80",
  "heartRate": 72,
  "temperature": 36.8,
  "respiratoryRate": 16,
  "weight": 70.5,
  "height": 175.0,
  "spO2": 98
}
```

- **Errors:** `400 validation.failed` (all seven null, out-of-range, malformed BP); `401`; `403`;
  `404`. **There is no DELETE route** — `DELETE …/{vitalSignsId}` returns `405`.

> The ranges above are the **actual** values in `VitalSignsLimits.cs` (whose source comment calls
> them temporary). `STATE.md` Q-19 and `MEDICAL_RECORDS_API.md` list narrower ranges; see the
> discrepancy note at the end.

### GET http://localhost:5208/api/medical-visits/{visitId}/vital-signs

- **Auth:** Bearer `Doctor`. **Success:** `200 OK` — `PagedResult<VisitVitalSignsResponse>`,
  ordered by `id` (there is no creation timestamp on the record).

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `visitId` | route | `Guid` (`{visitId:guid}`) | yes | Visit id |
| `pageNumber` | query | `int` | no | ≥ 1, default `1` |
| `pageSize` | query | `int` | no | `1..50`, default `10` |

```json
{
  "items": [
    {
      "id": "d3e4f5a6-b7c8-4d9e-0f1a-2b3c4d5e6f70",
      "bloodPressure": "120/80",
      "heartRate": 72,
      "temperature": 36.8,
      "respiratoryRate": 16,
      "weight": 70.5,
      "height": 175.0,
      "spO2": 98
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1
}
```

- **Errors:** `400 validation.failed` (invalid paging); `401`; `403`; `404`.

### PUT http://localhost:5208/api/medical-visits/{visitId}/vital-signs/{vitalSignsId}

- **Auth:** Bearer `Doctor`. **Success:** `200 OK` — `VisitVitalSignsResponse`. Full replacement:
  omitted/null fields clear previous values; `id` and `visitId` stay unchanged; at least one
  measurement must still be supplied.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `visitId` | route | `Guid` (`{visitId:guid}`) | yes | Parent visit id |
| `vitalSignsId` | route | `Guid` (`{vitalSignsId:guid}`) | yes | Vital-signs record id |
| `bloodPressure` | body | `string?` | no | `systolic/diastolic`, ≤ 20, sys `50..300`, dia `20..200`, sys > dia |
| `heartRate` | body | `int?` | no | `20..300` |
| `temperature` | body | `decimal?` | no | `30.0..45.0` |
| `respiratoryRate` | body | `int?` | no | `4..80` |
| `weight` | body | `decimal?` | no | `0.3..500` |
| `height` | body | `decimal?` | no | `20..260` |
| `spO2` | body | `int?` | no | `50..100` |

```json
{
  "bloodPressure": "118/76",
  "heartRate": 68,
  "temperature": 36.7,
  "respiratoryRate": null,
  "weight": 70.2,
  "height": 175.0,
  "spO2": 99
}
```

- **Errors:** `400 validation.failed` / `request.invalid`; `401`; `403`; `404`.

---

## 7. Medical records — 3 endpoints (Bearer, role `Doctor` only)

Controller: `MedicalRecordsController` (`[Route("api/patients/{patientId:guid}")]`,
`[Authorize(Roles = "Doctor")]`). Read-only, patient-centric; a non-doctor gets `403` before any
data access, and any patient outside the doctor's workspace gives the same `404 patient.not_found`
as an unknown patient. Details: `backend/docs/api/PATIENT_MEDICAL_API.md`.

### GET http://localhost:5208/api/patients/{patientId}/medical-profile

- **Auth:** Bearer `Doctor`. **Success:** `200 OK` — `PatientMedicalProfileResponse`.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `patientId` | route | `Guid` (`{patientId:guid}`) | yes | Own patient |

No query or body parameters.

```json
{
  "patientId": "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
  "fullName": "Sara Ali",
  "dateOfBirth": "1990-04-12",
  "age": 36,
  "gender": "Female",
  "phone": "01001234567",
  "address": "12 Nile Street",
  "bloodType": "OPositive",
  "emergencyContact": "Mother 01111111111",
  "allergies": [
    { "id": "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", "name": "Penicillin", "severity": "Mild", "description": null, "notes": null }
  ],
  "chronicConditions": [
    { "id": "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", "name": "Diabetes mellitus type 2", "diagnosisDate": null, "status": "Active", "notes": null }
  ]
}
```

- **Errors:** `401 auth.unauthorized`; `403 access.forbidden` (or `identity.invalid` for an
  unusable doctor identity); `404 patient.not_found`.

### GET http://localhost:5208/api/patients/{patientId}/medical-visits

- **Auth:** Bearer `Doctor`. **Success:** `200 OK` — `PagedResult<MedicalVisitResponse>`
  (history shape), newest first by `VisitDate` with visit id as tie-breaker.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `patientId` | route | `Guid` (`{patientId:guid}`) | yes | Own patient |
| `pageNumber` | query | `int` | no | ≥ 1, default `1` |
| `pageSize` | query | `int` | no | `1..50`, default `10` |

```json
{
  "items": [
    {
      "visitId": "9b2d4c1e-8a17-4f5b-9c2e-1a2b3c4d5e6f",
      "visitDate": "2026-11-10T09:30:00+00:00",
      "doctorId": "0f8f6f0a-6d4c-4a54-9a5e-2f2b0c9d1a11",
      "doctorName": "Demo Doctor",
      "chiefComplaint": "Headache",
      "symptoms": "Since yesterday",
      "clinicalNotes": "Rest and hydration",
      "diagnoses": [
        { "id": "b1c2d3e4-f5a6-4b7c-8d9e-0f1a2b3c4d5e", "name": "Migraine", "description": "Throbbing unilateral pain", "diagnosisDate": "2026-11-09" }
      ],
      "medications": [
        { "id": "c2d3e4f5-a6b7-4c8d-9e0f-1a2b3c4d5e6f", "name": "Amoxicillin", "dosage": "500 mg", "frequency": "Every 8 hours", "duration": "7 days", "instructions": "Take after food" }
      ],
      "vitalSigns": [
        { "id": "d3e4f5a6-b7c8-4d9e-0f1a-2b3c4d5e6f70", "bloodPressure": "120/80", "heartRate": 72, "temperature": null, "respiratoryRate": null, "weight": null, "height": null, "spO2": null }
      ],
      "notes": [],
      "labResults": []
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1
}
```

Note this history DTO uses `visitId`/`name` (not the F4 write-side `id`/`diagnosisName`/
`medicationName`).

- **Errors:** `400 validation.failed` (invalid paging); `401`; `403`; `404 patient.not_found`.

### GET http://localhost:5208/api/patients/{patientId}/timeline

- **Auth:** Bearer `Doctor`. **Success:** `200 OK` — `PagedResult<PatientTimelineEventResponse>`.
  Event types: `Visit`, `Diagnosis`, `Medication`, `MedicalNote` (case-insensitive filter);
  labs/documents/vital-sign events are not in this contract. Newest first by `occurredAt`, then
  type ordinal, then event id.

| Name | In | C# type | Required | Notes |
|---|---|---|---|---|
| `patientId` | route | `Guid` (`{patientId:guid}`) | yes | Own patient |
| `eventType` | query | `string?` | no | One of `Visit`, `Diagnosis`, `Medication`, `MedicalNote` (case-insensitive) |
| `from` | query | `DateTimeOffset?` | no | ISO-8601 instant, inclusive; must be ≤ `to` |
| `to` | query | `DateTimeOffset?` | no | ISO-8601 instant, inclusive. Diagnoses are compared by UTC calendar day |
| `pageNumber` | query | `int` | no | ≥ 1, default `1` |
| `pageSize` | query | `int` | no | `1..50`, default `10` |

```json
{
  "items": [
    {
      "id": "e4f5a6b7-c8d9-4e0f-1a2b-3c4d5e6f7081",
      "type": "MedicalNote",
      "occurredAt": "2026-11-15T09:30:00+00:00",
      "title": "Clinical Note",
      "summary": "Follow up in one week.",
      "relatedVisitId": null
    },
    {
      "id": "b1c2d3e4-f5a6-4b7c-8d9e-0f1a2b3c4d5e",
      "type": "Diagnosis",
      "occurredAt": "2026-11-09T00:00:00+00:00",
      "title": "Migraine",
      "summary": "Throbbing unilateral pain",
      "relatedVisitId": "9b2d4c1e-8a17-4f5b-9c2e-1a2b3c4d5e6f"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 2,
  "totalPages": 1
}
```

- **Errors:** `400 validation.failed` (unknown `eventType`, `from > to`, bad paging); `401`;
  `403`; `404 patient.not_found`.

---

## 8. Doctor dashboard — 1 endpoint (Bearer, role `Doctor` only)

Controller: `DoctorDashboardController` (`[Route("api/dashboard/doctor")]`,
`[Authorize(Roles = "Doctor")]`). The service re-checks the doctor identity from token claims;
`TotalRevenue` sums only payments with `status = Paid` for the acting doctor.

### GET http://localhost:5208/api/dashboard/doctor

- **Auth:** Bearer `Doctor`. **Success:** `200 OK` — `DoctorDashboardResponse`.

No route, query or body parameters.

```json
{
  "doctorId": "0f8f6f0a-6d4c-4a54-9a5e-2f2b0c9d1a11",
  "doctorName": "Demo Doctor",
  "specialization": "General Medicine",
  "metrics": {
    "todayAppointmentsCount": 3,
    "todayVisitsCount": 1,
    "totalVisitsCount": 12,
    "totalPatientsCount": 8,
    "totalRevenue": 1250.00
  },
  "todayAppointments": [
    {
      "appointmentId": "5a6b7c8d-9e0f-4a1b-8c2d-3e4f5a6b7c8d",
      "patientId": "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
      "patientName": "Sara Ali",
      "startTime": "10:00:00",
      "endTime": "10:30:00",
      "reason": "Follow-up consultation",
      "status": "Confirmed"
    }
  ]
}
```

- **Errors:** `401 auth.unauthorized`; `403 access.forbidden` for a Secretary/Admin or
  `identity.invalid` for an unusable doctor identity; `404` from the service when the doctor
  record cannot be resolved.

---

## Endpoint count / checklist (48)

Auth — 7 (Public)
- [x] `POST /api/auth/login`
- [x] `POST /api/auth/register`
- [x] `POST /api/auth/verify-email`
- [x] `POST /api/auth/resend-verification`
- [x] `POST /api/auth/forgot-password`
- [x] `POST /api/auth/reset-password`
- [x] `POST /api/auth/google`

Admin — 5 (Bearer `Admin`)
- [x] `GET /api/admin/users`
- [x] `PATCH /api/admin/users/{id}/status`
- [x] `PUT /api/admin/users/{id}/role`
- [x] `GET /api/admin/secretaries/{secretaryId}/doctors`
- [x] `PUT /api/admin/secretaries/{secretaryId}/doctors`

Appointments — 8 (Bearer `Doctor`, `Secretary`)
- [x] `POST /api/appointments`
- [x] `GET /api/appointments/{id}`
- [x] `GET /api/appointments`
- [x] `GET /api/appointments/lookups/doctors`
- [x] `GET /api/appointments/lookups/patients`
- [x] `PUT /api/appointments/{id}/schedule`
- [x] `POST /api/appointments/{id}/cancel`
- [x] `PUT /api/appointments/{id}/status`

Patients — 6 (Bearer `Doctor`, `Secretary`)
- [x] `POST /api/patients`
- [x] `GET /api/patients`
- [x] `GET /api/patients/lookup`
- [x] `GET /api/patients/{id}`
- [x] `PUT /api/patients/{id}`
- [x] `DELETE /api/patients/{id}`

Payments — 6 (Bearer `Doctor`, `Secretary`)
- [x] `POST /api/payments`
- [x] `GET /api/payments/{id}`
- [x] `GET /api/payments`
- [x] `GET /api/payments/payable-appointments`
- [x] `PUT /api/payments/{id}`
- [x] `PUT /api/payments/{id}/status`

Medical visits — 12 (Bearer `Doctor`)
- [x] `POST /api/medical-visits`
- [x] `GET /api/medical-visits/{visitId}`
- [x] `PUT /api/medical-visits/{visitId}`
- [x] `POST /api/medical-visits/{visitId}/diagnoses`
- [x] `PUT /api/medical-visits/{visitId}/diagnoses/{diagnosisId}`
- [x] `DELETE /api/medical-visits/{visitId}/diagnoses/{diagnosisId}`
- [x] `POST /api/medical-visits/{visitId}/medications`
- [x] `PUT /api/medical-visits/{visitId}/medications/{medicationId}`
- [x] `DELETE /api/medical-visits/{visitId}/medications/{medicationId}`
- [x] `POST /api/medical-visits/{visitId}/vital-signs`
- [x] `GET /api/medical-visits/{visitId}/vital-signs`
- [x] `PUT /api/medical-visits/{visitId}/vital-signs/{vitalSignsId}`

Medical records — 3 (Bearer `Doctor`)
- [x] `GET /api/patients/{patientId}/medical-profile`
- [x] `GET /api/patients/{patientId}/medical-visits`
- [x] `GET /api/patients/{patientId}/timeline`

Doctor dashboard — 1 (Bearer `Doctor`)
- [x] `GET /api/dashboard/doctor`

**Total: 7 + 5 + 8 + 6 + 6 + 12 + 3 + 1 = 48 endpoints.**

---

## Source-vs-documentation notes (for testers)

1. `backend/docs/api/API_CATALOG.md` lists `PUT /api/admin/users/{id}/status`. The controller is
   `[HttpPatch("users/{id:guid}/status")]`; use **`PATCH`** (as done here and in `ADMIN_API.md`).
2. Vital-sign validation ranges in `VitalSignsLimits.cs` differ from `STATE.md` Q-19 and
   `backend/docs/api/MEDICAL_RECORDS_API.md`: code allows systolic `50..300` (doc `50..250`),
   diastolic `20..200` (doc `30..150`), heart rate `20..300` (doc `30..220`), respiratory rate
   `4..80` (doc `5..60`), SpO2 `50..100` (doc `0..100`), weight `0.3..500` (doc `1.0..500`) and
   height `20..260` (doc `30..250`). Temperature `30.0..45.0` matches. This document uses the
   **code** values; the source comment marks them temporary. The example values are valid under
   both sets.
3. Timeline `eventType` accepts four values in `PatientTimelineEventTypes.cs`
   (`Visit`, `Diagnosis`, `Medication`, `MedicalNote`); `STATE.md` discusses adding `VitalSigns`
   events under F10-C1-T4, but no such event type exists in the current source.
4. `MEDICAL_RECORDS_API.md` §1 still suggests `dotnet user-jwts` for a local token. Identity
   headers and locally minted tokens are no longer the documented path: sign in through
   `/api/auth/login` and use the returned bearer token.
