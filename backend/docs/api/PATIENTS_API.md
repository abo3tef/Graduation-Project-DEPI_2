# Patients API (F8-C1)

> Status: implemented in F8-C1. Shared rules (JSON, errors, paging) are in [`CONVENTIONS.md`](CONVENTIONS.md).
> Roles: `Doctor` and `Secretary`. Any other role (e.g. `Admin`) gets `403`; no token gets `401`.

## Scope and visibility

| Caller | Patients reachable | Fields |
|---|---|---|
| Doctor | Own patients (`doctorId` = token user) | All, including `bloodType` |
| Secretary | Patients of assigned doctors (`doctor_secretaries`) | Administrative only (Q-18): `fullName`, `dateOfBirth`, `gender`, `phone`, `address`, `emergencyContact`. `bloodType` is **absent** from every secretary response and rejected (`403 patient.medical_field_forbidden`) in a secretary request |

A patient outside the caller's scope behaves like a missing one: `404 patient.not_found`. The owner is never taken from the body of a doctor's request (a secretary's `doctorId` on create must be one of their assigned doctors).

## Routes

| Method | Route | Success | Notes |
|---|---|---|---|
| `POST` | `/api/patients` | `201` + `Location` | Doctor: owner = token. Secretary: body `doctorId` required, must be assigned (else `404 doctor.not_found`) |
| `GET` | `/api/patients` | `200` `PagedResult<Patient>` | Query: `search`, `gender`, `doctorId` (narrows only), `pageNumber`, `pageSize` (1..50) |
| `GET` | `/api/patients/lookup` | `200` `[{id, fullName, phone}]` | Query: `search`, `doctorId`, `limit` (1..20, default 10). Picker source (F1/F4 forms) |
| `GET` | `/api/patients/{id}` | `200` | |
| `PUT` | `/api/patients/{id}` | `200` | Full replacement of the editable fields. Doctor: omitted `bloodType` clears it. Secretary: stored `bloodType` is never changed |
| `DELETE` | `/api/patients/{id}` | `204` | Doctor only (`403` for a secretary). `409 patient.has_related_records` if the patient has any appointment, visit, payment or other clinical record (Q-17). Hard delete, no cascade |

`search`: exact patient id (GUID), otherwise name or phone contains the text (case-insensitive under the MySQL `utf8mb4_0900_ai_ci` collation). Order: `fullName`, then `id` (stable tie-breaker).

## Bodies

```json
// POST body (PUT is the same without doctorId)
{ "doctorId": "guid (secretary only)", "fullName": "Sara Ali", "dateOfBirth": "1990-04-12",
  "gender": "Female", "phone": "01001234567", "address": "12 Nile Street",
  "emergencyContact": "Mother 01111111111", "bloodType": "OPositive" }
```

Validation: `fullName` required, <= 200; `dateOfBirth` required, not in the future (clinic time zone), not before 1900-01-01; `gender` required (`Male`/`Female`); `phone` optional, <= 30, 7+ digits, optional leading `+`, spaces/dashes/parentheses allowed; `address` <= 500; `emergencyContact` <= 100; `bloodType` optional enum (doctor only). Failures: `400 validation.failed` with camelCase `errors`.

## Response

```json
{ "id": "guid", "doctorId": "guid", "fullName": "Sara Ali", "dateOfBirth": "1990-04-12",
  "gender": "Female", "phone": "...", "address": "...", "emergencyContact": "...",
  "bloodType": "OPositive", "createdAt": "2026-11-17T09:00:00+00:00" }
```

`bloodType` is omitted when the caller is a secretary or when no blood type is recorded.

## Error codes added

`patient.has_related_records` (409), `patient.medical_field_forbidden` (403); reused: `patient.not_found`, `doctor.not_found`, `identity.invalid`, `validation.failed`.

## Route note

`/api/patients/{patientId:guid}/medical-profile|medical-visits|timeline` (F3/F10, doctor-only) are unchanged; the new routes are the collection, the literal `lookup` and a bare `{id:guid}`, so they do not overlap.
