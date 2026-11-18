# Admin Account API — F11-C1

- **Feature:** F11 — Admin Dashboard & Full E2E Integration
- **Cycle:** F11-C1 — Admin dashboard backend
- **Owner:** Abdullah
- **Status:** Implemented 2026-11-18
- **Contract decisions:** Q-04 (shared paged list envelope), Q-16 (secretary→doctor assignment),
  Q-22 (role editing and Admin protection), D-35 (claims contract)
- **Base route:** `/api/admin`
- **Implementation:** `backend/src/Medilog.WebAPI/Controllers/AdminController.cs`

This document is the written contract for the five account-administration endpoints. It is
intentionally narrow: no user creation, no role assignment to `Admin`, no audit log and no
frontend behavior are part of F11-C1.

## 1. Authentication and authorization

| Item | Value |
|---|---|
| Scheme | `Bearer` — the normal JWT issued by `POST /api/auth/login` |
| Required role | `Admin` (`[Authorize(Roles = "Admin")]` on the controller) |
| Acting identity | Resolved from the validated token `sub`/`role` claims; never from the route or body |
| Anonymous request | `401 Unauthorized` |
| Authenticated `Doctor`/`Secretary` | `403 Forbidden` |
| Service-side check | Every use case re-checks that the acting Admin account still exists and is `Active`; a stale token cannot keep administering |
| Deactivated accounts | Cannot sign in again until reactivated; an already-issued JWT is **not revoked** and stays cryptographically valid until its normal expiry, but the active-account check still applies |

A failed role check and a deactivated acting Admin both return the shared `ProblemDetails` envelope;
the latter uses `identity.invalid` with status `403`.

## 2. Endpoints

| # | Method | Route | Success | Notes |
|---|---|---|---|---|
| 1 | `GET` | `/api/admin/users?role=&status=&search=&pageNumber=&pageSize=` | `200 OK` | Q-04 paged account list |
| 2 | `PATCH` | `/api/admin/users/{id}/status` | `200 OK` | Full replacement of the account status |
| 3 | `PUT` | `/api/admin/users/{id}/role` | `200 OK` | Doctor↔Secretary transition (Q-22) |
| 4 | `GET` | `/api/admin/secretaries/{secretaryId}/doctors` | `200 OK` | Current assignment set (Q-16) |
| 5 | `PUT` | `/api/admin/secretaries/{secretaryId}/doctors` | `200 OK` | Full replacement of the assignment set (Q-16) |

`{id}` and `{secretaryId}` are GUID route values. All five responses are direct representations;
none sets a `Location` header.

## 3. Request contracts

### 3.1 List users — `GET /api/admin/users`

| Query | Type | Default | Rules |
|---|---|---|---|
| `role` | string? | none | `Admin`, `Doctor` or `Secretary`; any other value is `400` |
| `status` | string? | none | `Active` or `Inactive`; any other value is `400` |
| `search` | string? | none | Trimmed, ≤ 100 characters; a GUID is matched by id, otherwise a case-sensitive contains over full name, email and phone |
| `pageNumber` | int | `1` | ≥ 1 |
| `pageSize` | int | `10` | 1–50 (`PagingDefaults.MaxPageSize`); `51` is `400` |

Filtering, counting and ordering run in the data source over a union of the three account tables.
The list deliberately includes `Admin` accounts so the last-Admin protection is visible to the
dashboard. The deterministic order is `Email` (ordinal), then `Id` (stable tie-breaker). A page
past the end is `200` with an empty `items` array and unchanged metadata.

### 3.2 Change status — `PATCH /api/admin/users/{id}/status`

```json
{ "status": "Inactive" }
```

`status` is a string enum; the only accepted values are `Active` and `Inactive`. A request that
repeats the current status is a successful no-op and returns the current representation.

Rules:

- An Admin cannot deactivate their own account (`403 admin.self_deactivation_forbidden`).
- The last active Admin cannot be deactivated (`409 admin.last_admin_protected`). Self-protection
  normally makes this unreachable through the route; the service still enforces it directly.
- A missing account is `404 admin.user_not_found`.

### 3.3 Change role — `PUT /api/admin/users/{id}/role`

```json
{ "role": "Secretary" }
```

Only `Doctor` and `Secretary` can be requested; `Admin` is `400 validation.failed`. A target Admin
is `403 admin.role_change_forbidden`, and an Admin cannot change their own role
(`403 admin.self_role_change_forbidden`). A same-role request is a successful no-op.

A permitted transition is an account-table move, not a new identity. The same GUID, credentials,
name, email, phone, status, email-verification state and original `CreatedAtUtc` are carried to the
new table. Unconsumed one-time tokens follow the new account kind so an already-issued verification
or reset link keeps working; consumed tokens are left alone. The delete and insert run in one
database transaction on relational providers.

Q-22 conflict rules return `409 admin.role_change_blocked` before any data is changed:

| Current role | Blocks the transition |
|---|---|
| `Doctor` → `Secretary` | Any patient, medical visit, medical note, uploaded medical document, appointment, payment or secretary assignment owned by that doctor; or another `Secretary` account already using that email |
| `Secretary` → `Doctor` | Any appointment or payment created by that secretary, any doctors assigned to that secretary, or another `Doctor` account already using that email |

### 3.4 Read assignments — `GET /api/admin/secretaries/{secretaryId}/doctors`

Returns the complete doctor set for one secretary. Unknown secretary → `404 admin.secretary_not_found`.
Doctors are ordered by `Email` (ordinal), then `Id`.

### 3.5 Replace assignments — `PUT /api/admin/secretaries/{secretaryId}/doctors`

```json
{ "doctorIds": ["3f2504e0-4f89-41d3-9a0c-0305e82c3301"] }
```

`doctorIds` is a full replacement, so `[]` clears all assignments. It contains at most 100 GUIDs,
no duplicates and no `Guid.Empty`; violations are `400 validation.failed`. An unknown doctor is
`404 doctor.not_found`, and an unknown secretary is `404 admin.secretary_not_found`.

The `doctor_secretaries` join table has a composite primary key `(doctor_id, secretary_id)`, so a
unique pair is also enforced at the database boundary. After a replacement, a secretary can see
only patients of those assigned doctors (F8 scoping, Q-16/Q-18).

## 4. Response contracts

### 4.1 `AdminUserResponse`

```json
{
  "id": "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
  "role": "Doctor",
  "fullName": "Doctor Under Test",
  "email": "doctor@medilog.test",
  "phone": "01000000000",
  "specialization": "General Medicine",
  "status": "Active",
  "emailVerified": true,
  "createdAtUtc": "2026-11-18T09:00:00+00:00"
}
```

`role` and `status` serialize as strings. `phone` and `specialization` are `null` where the account
kind has no such field. **Password hashes, access/refresh tokens and one-time token hashes are
never part of this contract.**

### 4.2 Paged list envelope (Q-04)

```json
{
  "items": [ { "id": "…", "role": "Doctor", "status": "Active" } ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 4,
  "totalPages": 1
}
```

`items` contains `AdminUserResponse` objects; the abbreviated example above shows only the
envelope. `totalCount` is the count after filtering, before paging.

### 4.3 `SecretaryDoctorsResponse`

```json
{
  "secretaryId": "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
  "doctors": [
    { "id": "1f2504e0-4f89-41d3-9a0c-0305e82c3301", "fullName": "Doctor A", "email": "a@medilog.test" }
  ]
}
```

Only assignment-relevant doctor fields are returned; no medical data is exposed.

## 5. Error codes

All errors use the shared RFC 7807 `application/problem+json` envelope from
[`CONVENTIONS.md`](./CONVENTIONS.md). Clients switch on `status` and `code`.

| Status | `code` | Meaning |
|---|---|---|
| `400` | `validation.failed` | Invalid filter, paging value, enum, duplicate/empty doctor id or more than 100 assignments |
| `400` | `request.invalid` | Malformed JSON, wrong type or missing body |
| `401` | `auth.unauthorized` | No authenticated principal |
| `403` | `access.forbidden` | Authenticated non-Admin (controller policy) |
| `403` | `identity.invalid` | Acting Admin account is unknown or inactive |
| `403` | `admin.self_deactivation_forbidden` | Admin tried to deactivate themselves |
| `403` | `admin.self_role_change_forbidden` | Admin tried to change their own role |
| `403` | `admin.role_change_forbidden` | Target is an Admin, or `Admin` was requested as the new role |
| `404` | `admin.user_not_found` | Target account does not exist |
| `404` | `admin.secretary_not_found` | Secretary does not exist |
| `404` | `doctor.not_found` | One of the requested doctors does not exist |
| `409` | `admin.last_admin_protected` | Deactivating the target would remove the last active Admin |
| `409` | `admin.role_change_blocked` | Q-22 conflicting data prevents the role transition |

## 6. Verification checklist

- `dotnet build Medilog.sln` — 0 errors.
- `dotnet test Medilog.sln` — full suite green.
- Application tests cover list filters/paging/order, status protection, both role directions,
  token migration, assignment uniqueness/bounds and unknown ids.
- Real-pipeline tests cover 401/403 on all five routes, list metadata and response secrecy,
  status→login behavior, stale deactivated-Admin token behavior, role-table move and conflict cases,
  and the effect of assignments on secretary patient and appointment scope.
- No schema migration was introduced: the existing `admins` and `doctor_secretaries` tables from
  earlier cycles are used.
