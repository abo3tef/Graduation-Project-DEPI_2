# Backend Gap Analysis — Medilog Phase 2

- **Date:** 2026-11-18
- **Type:** Read-only analysis. No application, infrastructure, API, test, or contract code was changed.
- **Scope:** Current working tree on `main`, including uncommitted F9/F11 work. Compared against
  `AGENTS.md`, `STATE.md`, and the approved F1/F3/F4/F6/F7/F8/F9/F10/F11 task files.
- **Baseline verification executed earlier in this session:** restore succeeded; build succeeded with
  0 errors; full suite passed **770/770** (Application 550, WebAPI 220); EF reported no pending model
  changes; `git diff --check` found no whitespace errors.
- **Credential handling:** credential values discovered during inspection are intentionally **not**
  reproduced in this report. Locations and severity only.

## 1. Executive summary

The backend is substantially implemented: all requested domain tables exist in EF Core migrations,
48 HTTP endpoints are present across eight controllers, and the last observed full test run was green.
However, it is **not complete** against the requested "entire backend in one execution" scope, for two
reasons:

1. **Repository rules forbid the requested execution model.** `AGENTS.md §8.3` says not to build the
   whole project or an entire large feature in one operation; `§8.2` requires an approved
   `plan → build → review → done` Cycle workflow. The user-approved step here is gap analysis only.
2. **Concrete approved-scope gaps remain.** The most important are: missing F5 revenue aggregation,
   missing VitalSigns timeline events, unauthenticated appointment endpoints, a stale EF migration
   designer, a real-looking remote database credential in a tracked config file, conflicting MySQL
   providers, and missing/stale API documentation.

Stripe PaymentIntent/webhook handling is **not implemented and is not currently approved**: `F7-C1.md`
explicitly puts payment gateways out of scope, and no decision for Stripe exists in `STATE.md §8`.

The dedicated `backend/docs/api/API_Endpoints_Summary.md` was **not** created in this read-only step;
the endpoint inventory below is the input for that future deliverable.

### Remediation status (2026-11-18)

**G-01, G-02, G-03 and G-04 are closed** by the approved bounded security/config remediation:
tracked configs contain only empty credential placeholders, the header-identity fallback is gone,
all appointment routes require Doctor/Secretary JWT authorization, one EF-10-compatible MySQL
provider remains, and the `AddPayments` designer target model matches the current snapshot.
Evidence, commands and actual results are in `docs/reports/SECURITY_CONFIG_REMEDIATION.md`.
Credential rotation remains an external owner action. G-05 … G-22 are unchanged.

---

## 2. Endpoint inventory (current code)

Common conventions (per `backend/docs/api/CONVENTIONS.md`): camelCase JSON, string enums, RFC 7807
`application/problem+json` errors, Q-04 paging (`pageNumber` request / `page` response, default 1/10,
max 50), and `PagedResult<T> = { items, page, pageSize, totalCount, totalPages }`.

Common status meanings: `400` validation/model-binding; `401` missing authentication where
`[Authorize]` exists; `403` role/application denial; `404` missing or out-of-scope resource;
`409` domain conflict; `201` create; `204` no-content delete/action.

### 2.1 Auth — `AuthController` — anonymous by design

| Method | Route | Request | Response / success | Declared errors |
|---|---|---|---|---|
| `POST` | `/api/auth/login` | `{ email, password }` | `200 AuthResponse` | `400`, `401`, `403`, `503` |
| `POST` | `/api/auth/register` | `{ fullName, email, password, phone?, role, specialization? }` | `201 AuthUserResponse`, no `Location` | `400`, `409`, `503` |
| `POST` | `/api/auth/verify-email` | `{ token }` | `204` | `400`, `401` |
| `POST` | `/api/auth/resend-verification` | `{ email }` | `204`, always same answer | `400`, `503` |
| `POST` | `/api/auth/forgot-password` | `{ email }` | `204`, always same answer | `400`, `503` |
| `POST` | `/api/auth/reset-password` | `{ token, newPassword }` | `204` | `400`, `401` |
| `POST` | `/api/auth/google` | `{ idToken }` | `200 AuthResponse` | `400`, `401`, `403`, `503` |

### 2.2 Admin — `AdminController` — `[Authorize(Roles = "Admin")]` plus service-side active-Admin check

| Method | Route | Request / query | Response / success | Declared errors |
|---|---|---|---|---|
| `GET` | `/api/admin/users` | `role`, `status`, `search`, `pageNumber`, `pageSize` | `200 PagedResult<AdminUserResponse>` | `400`, `401`, `403` |
| `PATCH` | `/api/admin/users/{id}/status` | `{ status: "Active" \| "Inactive" }` | `200 AdminUserResponse` | `400`, `401`, `403`, `404`, `409` |
| `PUT` | `/api/admin/users/{id}/role` | `{ role: "Doctor" \| "Secretary" }` | `200 AdminUserResponse` | `400`, `401`, `403`, `404`, `409` |
| `GET` | `/api/admin/secretaries/{secretaryId}/doctors` | — | `200 SecretaryDoctorsResponse` | `401`, `403`, `404` |
| `PUT` | `/api/admin/secretaries/{secretaryId}/doctors` | `{ doctorIds: Guid[] }` | `200 SecretaryDoctorsResponse` | `400`, `401`, `403`, `404` |

### 2.3 Patients — `PatientsController` — `[Authorize(Roles = "Doctor,Secretary")]`

| Method | Route | Request / query | Response / success | Declared errors |
|---|---|---|---|---|
| `POST` | `/api/patients` | `CreatePatientRequest` | `201 PatientResponse` | `400`, `401`, `403`, `404` |
| `GET` | `/api/patients` | `ListPatientsQuery` | `200 PagedResult<PatientResponse>` | `400`, `401`, `403` |
| `GET` | `/api/patients/lookup` | `PatientPickerQuery` | `200 PatientLookupResponse[]` | `400`, `401`, `403` |
| `GET` | `/api/patients/{id}` | `id` | `200 PatientResponse` | `401`, `403`, `404` |
| `PUT` | `/api/patients/{id}` | `UpdatePatientRequest` | `200 PatientResponse` | `400`, `401`, `403`, `404` |
| `DELETE` | `/api/patients/{id}` | — | `204` | `401`, `403`, `404`, `409` |

### 2.4 Appointments — `AppointmentsController` — **no HTTP `[Authorize]` attribute**

The service still applies role/scope checks, so an anonymous caller normally receives `403
identity.invalid` rather than `401`. Intended roles are Doctor/Secretary.

| Method | Route | Request / query | Response / success |
|---|---|---|---|
| `POST` | `/api/appointments` | `BookAppointmentRequest` | `201 AppointmentResponse` + `Location` |
| `GET` | `/api/appointments/{id}` | `id` | `200 AppointmentResponse` |
| `GET` | `/api/appointments` | `ListAppointmentsQuery` | `200 PagedResult<AppointmentResponse>` |
| `GET` | `/api/appointments/lookups/doctors` | — | `200` lookup array |
| `GET` | `/api/appointments/lookups/patients` | — | `200` lookup array |
| `PUT` | `/api/appointments/{id}/schedule` | `RescheduleAppointmentRequest` | `200 AppointmentResponse` |
| `POST` | `/api/appointments/{id}/cancel` | `CancelAppointmentRequest` | `200 AppointmentResponse` |
| `PUT` | `/api/appointments/{id}/status` | `UpdateAppointmentStatusRequest` | `200 AppointmentResponse` |

### 2.5 Payments — `PaymentsController` — `[Authorize(Roles = "Doctor,Secretary")]`

| Method | Route | Request / query | Response / success | Declared errors |
|---|---|---|---|---|
| `POST` | `/api/payments` | `CreatePaymentRequest` | `201 PaymentResponse` + `Location` | `400`, `401`, `403`, `404`, `409` |
| `GET` | `/api/payments/{id}` | `id` | `200 PaymentResponse` | `401`, `403`, `404` |
| `GET` | `/api/payments` | `ListPaymentsQuery` | `200 PagedResult<PaymentResponse>` | `400`, `401`, `403` |
| `GET` | `/api/payments/payable-appointments` | `search`, `limit` | `200 PayableAppointmentResponse[]` | `400`, `401`, `403` |
| `PUT` | `/api/payments/{id}` | `UpdatePaymentRequest` | `200 PaymentResponse` | `400`, `401`, `403`, `404`, `409` |
| `PUT` | `/api/payments/{id}/status` | `UpdatePaymentStatusRequest` | `200 PaymentResponse` | `400`, `401`, `403`, `404`, `409` |

### 2.6 Medical visits, diagnoses, medications, vital signs — `MedicalVisitsController` — Doctor only

| Method | Route | Request / query | Response / success |
|---|---|---|---|
| `POST` | `/api/medical-visits` | `CreateMedicalVisitRequest` | `201 MedicalVisitResponse` |
| `GET` | `/api/medical-visits/{visitId}` | `visitId` | `200 MedicalVisitResponse` |
| `PUT` | `/api/medical-visits/{visitId}` | `UpdateMedicalVisitRequest` | `200 MedicalVisitResponse` |
| `POST` | `/api/medical-visits/{visitId}/diagnoses` | `CreateDiagnosisRequest` | `201 DiagnosisResponse` |
| `PUT` | `/api/medical-visits/{visitId}/diagnoses/{diagnosisId}` | `UpdateDiagnosisRequest` | `200 DiagnosisResponse` |
| `DELETE` | `/api/medical-visits/{visitId}/diagnoses/{diagnosisId}` | — | `204` (D-23 owning-doctor hard delete) |
| `POST` | `/api/medical-visits/{visitId}/medications` | `CreateMedicationRequest` | `201 MedicationResponse` |
| `PUT` | `/api/medical-visits/{visitId}/medications/{medicationId}` | `UpdateMedicationRequest` | `200 MedicationResponse` |
| `DELETE` | `/api/medical-visits/{visitId}/medications/{medicationId}` | — | `204` |
| `POST` | `/api/medical-visits/{visitId}/vital-signs` | `CreateVitalSignsRequest` | `201 VitalSignsResponse` |
| `GET` | `/api/medical-visits/{visitId}/vital-signs` | `pageNumber`, `pageSize` | `200 PagedResult<VitalSignsResponse>` |
| `PUT` | `/api/medical-visits/{visitId}/vital-signs/{vitalSignsId}` | `UpdateVitalSignsRequest` | `200 VitalSignsResponse` |

### 2.7 Medical profile / history / timeline — `MedicalRecordsController` — Doctor only

| Method | Route | Request / query | Response / success |
|---|---|---|---|
| `GET` | `/api/patients/{patientId}/medical-profile` | `patientId` | `200 PatientMedicalProfileResponse` |
| `GET` | `/api/patients/{patientId}/medical-visits` | `pageNumber`, `pageSize` | `200 PagedResult<MedicalVisitResponse>` |
| `GET` | `/api/patients/{patientId}/timeline` | `eventType`, `from`, `to`, `pageNumber`, `pageSize` | `200 PagedResult<PatientTimelineEventResponse>` |

Timeline event type allow-list is currently `Visit`, `Diagnosis`, `Medication`, `MedicalNote` only.

### 2.8 Doctor dashboard — `DoctorDashboardController` — `[Authorize(Roles = "Doctor")]`

| Method | Route | Request | Response / success |
|---|---|---|---|
| `GET` | `/api/dashboard/doctor` | — | `200 DoctorDashboardResponse` (`totalRevenue` currently always `null`) |

### 2.9 Principal DTO structures

- `AuthResponse` — `{ accessToken, expiresAtUtc, user: AuthUserResponse }`;
  `AuthUserResponse` — `{ id, fullName, email, role, doctorId? }`.
- `AdminUserResponse` — `{ id, role, fullName, email, phone?, specialization?, status, emailVerified,
  createdAtUtc }`; `SecretaryDoctorsResponse` — `{ secretaryId, doctors: [{ id, fullName, email }] }`.
- `PatientResponse` — `{ id, doctorId?, fullName, dateOfBirth, gender, phone?, address?,
  emergencyContact?, bloodType?, createdAt }`; `bloodType` is omitted for secretaries.
- `BookAppointmentRequest` — `{ patientId, doctorId, appointmentDate?, startTime?, endTime?, reason? }`;
  `ListAppointmentsQuery` — `{ doctorId?, patientId?, dateFrom?, dateTo?, status?, pageNumber, pageSize }`.
- `CreatePaymentRequest` — `{ appointmentId, amount?, method?, notes? }`;
  `PaymentResponse` — `{ id, patientId, patientName, doctorId, appointmentId, appointmentDate, amount,
  method, status, paidAt?, notes? }`.
- `CreateMedicalVisitRequest` — `{ patientId, appointmentId?, visitDate, chiefComplaint?, symptoms?,
  clinicalNotes? }`.
- `CreateDiagnosisRequest` — `{ diagnosisName, description?, diagnosisDate }`;
  `CreateMedicationRequest` — `{ medicationName, dosage, frequency, duration?, instructions? }`.
- `CreateVitalSignsRequest` / `UpdateVitalSignsRequest` — `{ bloodPressure?, heartRate?, temperature?,
  respiratoryRate?, weight?, height?, spO2? }`, at least one field required.
- `PatientTimelineEventResponse` — `{ id, type, occurredAt, title, summary, relatedVisitId? }`.

---

## 3. Compliance against approved requirements

| Area | Status | Evidence / note |
|---|---|---|
| F6 authentication | **Implemented** | Seven auth routes; PBKDF2 600k; JWT claims `sub`/`role`/`doctorId`; Firebase ID-token validator; SMTP + `.eml` sink; single-use hashed tokens; seed users. Live Firebase/SMTP/MySQL remain unverified. |
| F4-v2 diagnosis delete | **Implemented** | Owning-doctor hard delete, route + service checks, `204`. |
| F11-C1 admin | **Implemented, uncommitted** | Five Admin-only routes; Q-16/Q-22 enforced; active-Admin recheck; self/last-Admin protection; credential-free responses. |
| F7 payments | **Mostly implemented** | Q-01/Q-21 model, one payment per appointment, transitions, immutability, scoping and picker are present. F5 revenue aggregation is absent. |
| F8 patients | **Implemented** | CRUD, lookup, Q-17 delete rule, Q-18 secretary field restrictions and doctor-secretary scoping. |
| F9 vital signs | **Implemented** | Seven Q-19 fields, ranges, at-least-one rule, Q-04 paging, doctor ownership. |
| F10 timeline | **Partial** | Four Q-20 types implemented; VitalSigns events missing; Q-20 still open. |
| F1 appointments | **Business rules implemented, HTTP auth missing** | Overlap/boundary/status rules and doctor scoping are covered; controller has no `[Authorize]`. |
| F3/F4 medical records | **Implemented** | Profile/history/visits/diagnoses/medications; doctor-only; no medical data to secretaries. |
| EF Core model | **Schema complete** | All 16 requested entities plus `doctor_secretaries` are mapped and present in migrations. EF reports no pending model changes. |
| Q-04 paging | **Mostly consistent** | Shared envelope/defaults/validator; medical-history `int skip` is an outlier with overflow risk. |
| API documentation | **Partial** | Separate docs exist for auth, admin, patients, medical records, appointments, conventions. No `API_Endpoints_Summary.md`; no dedicated payments contract; appointments doc is stale. |

---

## 4. Gap register

### 4.1 Critical / high

| ID | Finding | Evidence | Recommended action |
|---|---|---|---|
| G-01 | **Real-looking remote MySQL credential is present in tracked `backend/src/Medilog.WebAPI/appsettings.json` as an uncommitted local edit.** HEAD has an empty connection string. | `appsettings.json:10`; earlier audit | Remove the value from the working tree, rotate the remote credential, and move it to environment variables/user-secrets. Never commit it. |
| G-02 | **Appointment endpoints have no HTTP authentication.** `Auth:AllowHeaderIdentity=true` in Development also enables forgeable `X-User-Id`/`X-User-Role`. | `AppointmentsController.cs:16`; `CurrentUser.cs:96-105`; `appsettings.Development.json:9` | Add `[Authorize(Roles = "Doctor,Secretary")]`, verify anonymous returns `401`, and remove the header fallback before delivery (currently assigned to F11-C2 per D-42). |
| G-03 | **Last migration designer is stale.** `20261115000221_AddPayments.Designer.cs` lacks `Admin`, `AuthToken`, and `email_verified_at_utc` from the preceding auth migration; current snapshot has them. | migration designer vs `20261114194949_AddAuthentication.cs` and snapshot | Regenerate/review the designer from the integrated model. Do not delete or rewrite shared migration history without Tech Lead approval. |
| G-04 | **Two MySQL providers are referenced.** `MySql.EntityFrameworkCore 10.0.12` and uncommitted `Pomelo.EntityFrameworkCore.MySql 9.0.0`; build emits `NU1608` and `NU1902`. | `Medilog.Infrastructure.csproj:16-17` | Choose one approved provider, remove the other, and resolve the vulnerability warning. This needs team agreement because it is a shared dependency decision. |
| G-05 | **F5 revenue aggregation is missing.** `DoctorDashboardService` returns `Task.FromResult<decimal?>(null)`; dashboard tests assert `null`. | `DoctorDashboardService.cs:137-142`; `F7-C1.md:98`; `F7-C2.md` | Implement `SUM(amount) WHERE status = 'Paid' AND doctor_id = @doctorId` for the authenticated doctor, plus tests and contract update. Requires approval to reopen the deferred F7-C1-T5 work. |
| G-06 | **VitalSigns timeline events are absent.** Q-20 proposes including them and F10-C1 acceptance requires them, but the allow-list and service cover only four types. | `PatientTimelineEventTypes.cs`; `MedicalRecordValidators.cs`; `MedicalRecordService.cs`; `STATE.md:154`; `F10-C1.md:26,31` | Obtain Q-20 decision, then add the fifth source with deterministic ordering and tests. |
| G-07 | **No complete endpoint catalogue.** `backend/docs/api/API_Endpoints_Summary.md` does not exist; there is no dedicated payments contract, and `appointments.md` still says auth is not implemented. | `backend/docs/api/` listing; `appointments.md:12-35` | After remediation, generate the requested catalogue using `CONVENTIONS.md` and the feature docs. Update the stale appointment statements. |

### 4.2 Medium

| ID | Finding | Evidence | Recommended action |
|---|---|---|---|
| G-08 | **Medical-history paging can overflow.** `GetMedicalHistoryRequest` has no upper bound and `MedicalRecordStore` computes `skip` with `int`; patient/vitals/timeline use `long`. | `MedicalRecordStore.cs:110-116`; `PagedQueryValidator.cs:14-16` | Use `long` skip and an empty-page guard; add a huge-page regression test. |
| G-09 | **Medical-visit write service does not re-check active doctor status**, unlike patient service and medical-record reads. A stale JWT from a deactivated doctor could still mutate visits. | `MedicalVisitService.cs:421-430` | Add the same active-account recheck used elsewhere. |
| G-10 | **Firebase is unconfigured in the current local config** (`Firebase` object empty), so `/api/auth/google` returns `503 auth.google_not_configured` by design. Live Firebase, SMTP, and MySQL are unverified. | `appsettings.json:34-36`; `FirebaseGoogleTokenValidator.cs:61-66`; F6 report | Supply non-secret config via env/user-secrets and perform the live smoke checks before demo. |
| G-11 | **Direct payment creation accepts Cancelled/NoShow appointments** even though the payable picker excludes them. | `PaymentService.cs:62-89`; `F7-C1.md:88` | Decide whether to enforce at create time; if yes, add domain/application rule and test. |
| G-12 | **Payment amount upper bound is not DB-enforced.** Application/domain enforce `<= 1,000,000`, but the migration CHECK only requires `amount > 0`. | `PaymentConfiguration.cs:22-24` | Add a reviewed migration/check constraint if defense in depth is desired. |
| G-13 | **Dev seed password is stored in `appsettings.Development.json`** and documented in `SEED_ACCOUNTS.md`. It is dev-only under D-40 but still a plaintext credential in version control. | `appsettings.Development.json:13`; `SEED_ACCOUNTS.md` | Replace with a user-secret/environment value or clearly rotate demo credentials before delivery. |
| G-14 | **F11-C1 remains uncommitted on `main`** despite the agreed dedicated-branch/PR workflow. | `git status --short --branch` | Review and merge per the team process after owner sign-off. |
| G-15 | **Task/STATE documentation drift:** F7-C1 task file is still `Approved` with unchecked tasks although its report says Done; F8-C1 also has unchecked boxes; F10-C1 VitalSigns item remains open. | task files vs `STATE.md` §5/§10 | Reconcile after owner review; do not silently change acceptance criteria. |

### 4.3 Decision hygiene / lower severity

| ID | Finding | Note |
|---|---|---|
| G-16 | **Stripe is requested but not approved.** No implementation, package, config, webhook, or decision. `F7-C1.md` explicitly excludes payment gateways. | Requires a Tech Lead decision and a new/reopened Cycle before code. |
| G-17 | **Q-20 remains open** even though four event types are implemented and VitalSigns are deferred. | Record the decision before implementation. |
| G-18 | **Q-02, Q-08, Q-09 remain unrecorded** in `STATE.md §8`, though code implements a doctor-relationship rule. | Tech Lead must record actual Phase 1 decisions; agents must not guess. |
| G-19 | **Medication hard-delete route remains** while AGENTS names diagnosis delete as the approved D-23 example. | Ratify or adjust; requires Tech Lead decision. |
| G-20 | **No automatic `Database.Migrate()`** is present; schema application depends on an external/manual step. | Operational risk only; verify the deployment procedure before demo. |
| G-21 | **Google account linking uses email only**; no provider subject is stored. | Aligned with D-32/D-43; explicit provider linking would need a new decision and migration. |
| G-22 | **JWTs are not revoked on deactivation/role change.** | Documented; admin use cases re-check active Admin. Consider the same recheck consistently for other roles (see G-09). |

---

## 5. Decisions required before further implementation

1. **Stripe:** approve or reject Stripe PaymentIntent + webhook as a new scope item, including package,
   secrets, refund/out-of-scope rules, and ownership. Current approved model has only `Cash|Card`.
2. **Q-20:** confirm whether VitalSigns are timeline events and their `occurredAt` convention.
3. **Reopen cycles:** authorize remediation of deferred F7-C1-T5 and F10-C1-T4, plus an F1 security
   remediation for `[Authorize]`.
4. **MySQL provider:** approve one provider and removal of the other.
5. **Credentials:** confirm the remote database credential has been rotated and that local configs are
   cleaned before any commit.
6. **Phase 1 decisions:** record Q-02/Q-08/Q-09 and ratify/remove the medication hard-delete route.
7. **Branch/PR:** confirm how uncommitted F11-C1 should move onto `feature/abdullah-admin-integration`.

---

## 6. Recommended bounded next steps

Each item needs its own approved plan before `build` (AGENTS.md §8.2/§8.3).

1. **P0 — Security/config remediation:** remove/rotate secrets, clean `appsettings.json`, choose one
   MySQL provider, fix the stale designer, add appointment `[Authorize]`, and remove the header
   identity fallback.
2. **P0 — F5 revenue:** implement the approved `SUM(Paid)` query and replace the `null` dashboard
   value, with service/API tests and a payments contract doc.
3. **P1 — F10 completion:** Q-20 decision + VitalSigns timeline events and tests.
4. **P1 — Q-04 consistency:** fix the medical-history `skip` overflow and add the regression test.
5. **P1 — Active-state consistency:** add active-doctor rechecks to medical write services.
6. **P2 — Documentation:** create `backend/docs/api/API_Endpoints_Summary.md`, add a payments
   contract, refresh `appointments.md`, reconcile task files/STATE, then run the full quality gates.

---

## 7. Verification performed in this analysis

- The four read-only audits inspected auth/admin, payments/appointments/dashboard,
  patients/medical/timeline, and EF/configuration. They did **not** execute builds or tests.
- The build/test/EF/diff results quoted in §"Baseline verification executed earlier in this session"
  were actually executed and observed earlier in the session, before this analysis.
- No code was generated or modified. This report is the only new file.

## 8. Deferred deliverable

`backend/docs/api/API_Endpoints_Summary.md` remains the requested final deliverable. It should be
produced only after the remediation above, using the complete endpoint inventory in §2 and the shared
format in `backend/docs/api/CONVENTIONS.md`, so its authorization and status-code claims match the
final code.
