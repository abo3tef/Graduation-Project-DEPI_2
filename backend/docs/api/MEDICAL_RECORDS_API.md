# Medical Records API — F4-C2b / F4-C3

- **Feature:** F4 — Medical Updates & Prescriptions (Doctor)
- **Cycles:** F4-C2 — Medical Visit & Diagnosis write API (slice C2b) · F4-C3 — Medication lines
- **Owner:** Mohamed Shalaby (Member 4)
- **Status:** C2b implemented 2026-11-11 · C3 implemented 2026-11-11
- **Contract decisions:** D-16 (JWT Bearer) · D-17 (single-owner workspace) · D-18 (MVC
  controllers) · D-20 (`201` location) · D-21 (DI wiring) · D-23 (medication hard delete) ·
  D-24 (`Medications` in the visit response) · D-25 (extend `IMedicalVisitService`) ·
  D-26 (medication request limits)
- **Base route:** `/api/medical-visits`

This document is the written contract for the eight endpoints implemented in
`backend/src/Medilog.WebAPI/Controllers/MedicalVisitsController.cs`. It is intentionally narrow:
the shared list/pagination contract (Q-04, see [`CONVENTIONS.md`](./CONVENTIONS.md)) is defined,
but F4 does not expose a list endpoint in the current scope.

## 1. Authentication and authorization

| Item | Value |
|---|---|
| Scheme | `Bearer` — JWT validation only, no token-issuing endpoint (D-16) |
| Required role | `Doctor` (`[Authorize(Roles = "Doctor")]` on the controller) |
| Acting identity | Resolved from the token `sub` claim; no request body or query accepts a doctor id |
| Anonymous request | `401 Unauthorized` |
| Authenticated non-doctor (e.g. `Secretary`) | `403 Forbidden` |
| Data scope | Only records of patients whose `Patient.DoctorId` equals the acting doctor (D-17) |

Out-of-scope medical records return `404 Not Found` rather than `403 Forbidden`, so the API does
not reveal that a record exists outside the caller's workspace.

### Local development token

```bash
# Use the doctor id as the token name: 'dotnet user-jwts' always writes the name into 'sub',
# and passing --claim "sub=..." would create a multi-valued claim.
dotnet user-jwts create \
  --project backend/src/Medilog.WebAPI \
  --name "<doctor-guid>" \
  --role Doctor

# then
curl -H "Authorization: Bearer <token>" http://localhost:5000/api/medical-visits/<visit-id>
```

The signing key is stored in user secrets (`Medilog.WebAPI.csproj` → `UserSecretsId`), never in
`appsettings*.json`. A deployment supplies the production issuer, audience and signing key
through configuration.

## 2. Endpoints

| # | Method | Route | Success | `Location` |
|---|---|---|---|---|
| 1 | `POST` | `/api/medical-visits` | `201 Created` | `/api/medical-visits/{visitId}` |
| 2 | `GET` | `/api/medical-visits/{visitId}` | `200 OK` | — |
| 3 | `PUT` | `/api/medical-visits/{visitId}` | `200 OK` | — |
| 4 | `POST` | `/api/medical-visits/{visitId}/diagnoses` | `201 Created` | `/api/medical-visits/{visitId}` (parent visit) |
| 5 | `PUT` | `/api/medical-visits/{visitId}/diagnoses/{diagnosisId}` | `200 OK` | — |
| 6 | `POST` | `/api/medical-visits/{visitId}/medications` | `201 Created` | `/api/medical-visits/{visitId}` (parent visit) |
| 7 | `PUT` | `/api/medical-visits/{visitId}/medications/{medicationId}` | `200 OK` | — |
| 8 | `DELETE` | `/api/medical-visits/{visitId}/medications/{medicationId}` | `204 No Content` | — |

Route identifiers are GUIDs (`{visitId:guid}`, `{diagnosisId:guid}`, `{medicationId:guid}`).

## 3. Request contracts

### 3.1 Create a visit — `POST /api/medical-visits`

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

| Field | Type | Required | Rules |
|---|---|---|---|
| `patientId` | GUID | yes | Must identify a patient owned by the acting doctor |
| `appointmentId` | GUID? | no | Must belong to the same patient and doctor and not be linked to another visit |
| `visitDate` | `DateTimeOffset` | yes | Not in the future |
| `chiefComplaint` | string? | no | ≤ 2000 characters; whitespace becomes `null` |
| `symptoms` | string? | no | ≤ 2000 characters; whitespace becomes `null` |
| `clinicalNotes` | string? | no | ≤ 4000 characters; whitespace becomes `null` (D-22) |

The doctor id is never accepted from the client; it is set from the authenticated principal.

### 3.2 Update a visit — `PUT /api/medical-visits/{visitId}`

```json
{
  "visitDate": "2026-11-10T10:00:00+00:00",
  "chiefComplaint": "Updated complaint",
  "symptoms": null,
  "clinicalNotes": "Updated notes"
}
```

Patient, doctor and appointment link are immutable after creation, so they are not part of this
request and cannot be overwritten by a client.

### 3.3 Add a diagnosis — `POST /api/medical-visits/{visitId}/diagnoses`

```json
{
  "diagnosisName": "Migraine",
  "description": "Throbbing unilateral pain",
  "diagnosisDate": "2026-11-09"
}
```

| Field | Type | Required | Rules |
|---|---|---|---|
| `diagnosisName` | string | yes | Non-empty, ≤ 200 characters, trimmed |
| `description` | string? | no | ≤ 4000 characters; whitespace becomes `null` |
| `diagnosisDate` | `DateOnly` | yes | Not in the future |

The visit and patient identifiers are taken from the route and the persisted visit, never from
the request.

### 3.4 Update a diagnosis — `PUT /api/medical-visits/{visitId}/diagnoses/{diagnosisId}`

Same shape as §3.3. The diagnosis must belong to the visit in the route, and that visit must
belong to the acting doctor.

### 3.5 Add a medication line — `POST /api/medical-visits/{visitId}/medications`

```json
{
  "medicationName": "Amoxicillin",
  "dosage": "500 mg",
  "frequency": "Every 8 hours",
  "duration": "7 days",
  "instructions": "Take after food"
}
```

| Field | Type | Required | Rules |
|---|---|---|---|
| `medicationName` | string | yes | Non-empty, ≤ 200 characters, trimmed |
| `dosage` | string | yes | Non-empty, ≤ 100 characters, trimmed |
| `frequency` | string | yes | Non-empty, ≤ 100 characters, trimmed |
| `duration` | string? | no | ≤ 100 characters; whitespace becomes `null` |
| `instructions` | string? | no | ≤ 4000 characters; whitespace becomes `null` |

The visit and patient identifiers are taken from the route and the persisted visit, never from
the request. A visit outside the acting doctor's workspace returns `404`.

### 3.6 Update a medication line — `PUT /api/medical-visits/{visitId}/medications/{medicationId}`

Same shape as §3.5. The medication must belong to the visit in the route, and that visit must
belong to the acting doctor.

### 3.7 Delete a medication line — `DELETE /api/medical-visits/{visitId}/medications/{medicationId}`

No request body. The medication must belong to the visit in the route, and that visit must belong
to the acting doctor. The deletion is **permanent** (D-23): it is intended to correct a line that
was entered by mistake, and it does not affect the visit or its other child records.

## 4. Response contracts

### 4.1 Medical visit

```json
{
  "id": "9b2d4c1e-8a17-4f5b-9c2e-1a2b3c4d5e6f",
  "patientId": "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
  "doctorId": "2a1b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d",
  "appointmentId": null,
  "visitDate": "2026-11-10T09:30:00+00:00",
  "chiefComplaint": "Headache",
  "symptoms": "Since yesterday",
  "clinicalNotes": "Rest and hydration",
  "diagnoses": [],
  "medications": []
}
```

Diagnoses are ordered by `diagnosisDate`, then by `id` as a stable tie-breaker. Medications are
ordered by `medicationName`, then by `id`. No EF entity, password hash, patient contact data or
navigation property is returned.

### 4.2 Diagnosis

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

### 4.3 Medication line

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

## 5. Status codes

| Status | When |
|---|---|
| `200 OK` | Successful `GET`/`PUT` |
| `201 Created` | Successful `POST`; includes a `Location` header |
| `204 No Content` | Successful medication `DELETE` |
| `400 Bad Request` | Request-shape validation failure, or an appointment that does not match the patient/doctor |
| `401 Unauthorized` | Missing/invalid token, or a token without an identity |
| `403 Forbidden` | Authenticated caller is not a doctor, or the patient is outside the doctor's workspace |
| `404 Not Found` | Visit/diagnosis/medication/patient/appointment not found for the acting doctor |
| `405 Method Not Allowed` | List routes are not part of the current Cycles |
| `409 Conflict` | The appointment is already linked to another visit |

## 6. Error responses (`ProblemDetails`)

Errors use the shared RFC 7807 `application/problem+json` envelope defined in
[`CONVENTIONS.md`](./CONVENTIONS.md): `type`, `title`, `status`, `instance`, `code` and `traceId`,
plus camelCase `errors`/`errorCodes` on validation failures. The exception message, stack trace,
SQL text and patient data are never written to a response.

### 6.1 Validation — `400`, `code = validation.failed`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "instance": "/api/medical-visits",
  "code": "validation.failed",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "errors": {
    "patientId": ["A patient identifier is required."],
    "appointmentId": ["The appointment must belong to the same patient and the acting doctor."]
  },
  "errorCodes": {
    "patientId": ["NotEmptyValidator"],
    "appointmentId": ["appointment.mismatch"]
  }
}
```

A malformed body or wrong value type produces the same envelope with
`code = request.invalid` and camelCase `errors` (no `errorCodes`).

### 6.2 Other failures

| Status | `title` | Default `code` |
|---|---|---|
| `401` | `Unauthorized.` | `auth.unauthorized` |
| `403` | `Forbidden.` | `access.forbidden` (or a domain code) |
| `404` | `Resource not found.` | `resource.not_found` (or a domain code) |
| `405` | `Method not allowed.` | `request.method_not_allowed` |
| `409` | `The request conflicts with the current state.` | `resource.conflict` |
| `415` | `Unsupported media type.` | `request.unsupported_media_type` |
| `500` | `An unexpected error occurred.` | `server.error` |

Every response also carries `instance` (the request path) and `traceId`. The `500` body never
contains the exception details; the exception is logged on the server.

## 7. Explicitly out of scope

- `GET /api/medical-visits` (list/filter/pagination) — Q-04 now defines the shared contract, but
  the F4 list endpoint is not part of the current delivery scope.
- `GET` for a single diagnosis or medication.
- `GET …/medications` as a separate list route.
- `DELETE` for visits or diagnoses (D-12); medication delete is in scope (D-23).
- Restoring a deleted medication line (no soft delete).
- `login`, `register`, `refresh`, password-reset and OTP routes.
- Secretary access to medical records.

## 8. Verification

The contract is exercised end-to-end by `backend/tests/Medilog.WebAPI.Tests` through the real
HTTP pipeline (`WebApplicationFactory`, `TestAuthHandler`, EF Core InMemory):

- success `201`/`200` for all visit/diagnosis routes and both `Location` values;
- success `201`/`200`/`204` for the medication routes, with the parent visit as `Location`;
- anonymous `401`, secretary `403`, foreign visit `404`, foreign patient `403`;
- appointment mismatch `400`, duplicate appointment linkage `409`;
- foreign or cross-visit medication `404`;
- `ProblemDetails` shape with no stack trace or exception text;
- absent list routes (`405`).
