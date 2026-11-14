# F3-C3b Task — Patient Medical Timeline & Medical Documents (read-only)

> **Status: Approved — the team approval of F3-C3b (including Q-12, Q-13 and proposals D-T1…D-T6)
> was communicated by Mrehan Mohamed (Member 3) on 2026-11-14.** Per the team's convention, the
> approval is contributor-communicated; **no Tech Lead name or separate ratification date is
> recorded.** **Build precondition honored:** F3-C3b is implemented on a branch *stacked on*
> `feature/mrehan-patient-status` @ `294d27b` (PR #9 head) — the C4a/C4b commits, PR #7, PR #8
> and PR #9 are untouched, and this cycle merges only after the earlier PRs in the chain.

- **Feature:** F3 — Patient Medical Status (Doctor) · **Owner:** Mrehan Mohamed (Member 3)
- **Predecessors:** F3-C1 (merged, PR #4) · F3-C2 (PR #7) · F3-C3a (PR #8) · F3-C4a/C4b (PR #9)
- **Base branch (actual):** `feature/mrehan-medical-timeline-docs` created from
  `feature/mrehan-patient-status` @ `294d27b` — stacked, conflict-free with PR #9 (C3b edits the
  same status-screen/messages files C4 created; a branch below C4 would conflict).

---

## 0. Decisions

### 0.1 Already approved (reused as-is — no new decision)

| Source | Decision |
|---|---|
| F3-C1 §0 + F3-C2 (P-1…P-6) + F3-C3a (C-1…C-6, P-A…P-C) | Doctor-only boundary (`403` before any data access), actor from `ICurrentUser` only, uniform `404 patient.not_found`, `MedicalRecordAccessPolicy`, Application-owned `IMedicalRecordStore` port, thin controllers on the existing patient-centric route prefix, contract doc append-only, deterministic ordering with stable tie-breakers |
| Q-04 / CONVENTIONS.md | Shared paging: `pageNumber`/`pageSize`, defaults 1/10, max 50, `PagedQueryValidator<T>`, response `items`/`page`/`pageSize`/`totalCount`/`totalPages` |
| Error envelope | `ApiProblem`/`GlobalExceptionHandler`; `validation.failed`, `patient.not_found`, `medical.access_denied`, `identity.invalid` |
| D-16/D-17/D-19 | JWT Bearer + `[Authorize(Roles.Doctor)]`; `TestAuthHandler` integration tests |
| AGENTS §7 | API-level security tests on every new endpoint |
| Member 4's write side | Untouched — C3b adds read-only queries; no F4 files change |

### 0.2 Scope answers approved by the team (communicated by Mrehan, 2026-11-14)

| ID | Decision |
|---|---|
| **Q-12** (registered & answered) | Timeline composition: **visits (summaries) + patient-level notes + patient-level lab results + medical documents (metadata)**; newest first by `occurredAtUtc`; tie-breakers `eventType` asc then `id` asc; event payloads are summaries — never full visit detail |
| **Q-13** (registered & answered) | Medical documents: **metadata listing only**. `view()`/`download()` are **deferred** until secure storage and an authenticated, ownership-checked file-serving flow exist (none exists: no upload endpoint, no `IFormFile` handling, no file-serving middleware; SRS §11 size limits are not in the repository). `upload()` is out of F3 (no upload task in the approved scope). The `location` field is **never exposed** (API or UI); no UI download control |
| **D-T1** | Timeline payload detail: visit = summary + child counts; note/lab = full content (already fully exposed via §3.3/§3.4) |
| **D-T2** | Note content in the timeline: full, not truncated |
| **D-T3** | Documents appear in the timeline feed as metadata events |
| **D-T4** | Frontend lives in `features/patients` as two new tabs (⑥ Timeline, ⑦ Documents); the empty `features/medical-timeline` skeleton stays untouched |
| **D-T5** | Documents with `UploadedBy = null` (Class Spec §4.11 allows) omit the uploader name; no error |
| **D-T6** | Timeline paging is the standard Q-04 contract; no "load all" mode |

## 1. Goal

Complete the Class Spec §4.2 `Patient.getTimeline()` and §4.11 MedicalDocument *read* surface
(D-11 deferral): a paged chronological timeline of the patient's persisted medical events and a
paged listing of the patient's document metadata — doctor-only, reusing every approved F3
convention, with zero schema change and no file access.

## 2. Applicable requirements and rules

- Class Spec §4.2 (`getTimeline()`), §4.11 (document attributes/methods/rule), §2 relationships
  (Patient *has* MedicalDocument; Doctor *uploads*), business rules "every document belongs to a
  specific patient" and "a doctor sees only authorized patient records"; BRULE-01/05/10.
- SRS §11 (file size/format limits) is **not in the repository** (F3-C1 D-11) — one reason file
  handling stays out of scope (Q-13).
- FR-level mapping to be verified by `ElwakeelManage` (same status as previous F3 cycles).
- AGENTS §3.1, §3.4, §4.4, §5, §7.

## 3. Current code state (verified on the stacked base `294d27b`)

- `IMedicalRecordStore` already exposes doctor/owner/profile/visits/notes/labs reads — the
  extension point; authorization block is shared in `MedicalRecordService`.
- `MedicalDocument` (Domain) is already persisted: `PatientId` req, `UploadedBy` nullable,
  `FileName` req, `Type` (PDF/JPG/JPEG/PNG), `Location` req (never exposed), `UploadDate` req,
  `Description` optional; index `(PatientId, UploadDate)`; in the `InitialCreate` migration.
- `MedicalRecordsController` route prefix `api/patients/{patientId:guid}`, `[Authorize(Roles.Doctor)]`,
  actions: `medical-profile`, `medical-visits`, `notes`, `lab-results`. No `timeline`/`documents`
  route exists.
- No `MedicalTimeline` entity exists and none is added — the timeline is a composition of
  existing persisted events (Class Spec defines no timeline entity).
- No file storage, upload, or download infrastructure exists anywhere in the backend (verified).
- Frontend: the status screen has five tabs; `features/medical-timeline` is an empty skeleton.

## 4. Design

### 4.1 Endpoints

| # | Method | Route | Success | Ordering |
|---|--------|-------|---------|----------|
| 1 | `GET` | `/api/patients/{patientId:guid}/timeline?pageNumber=&pageSize=` | `200` `PagedResult<PatientTimelineEventResponse>` | `occurredAtUtc` desc, `eventType` asc, `id` asc |
| 2 | `GET` | `/api/patients/{patientId:guid}/documents?pageNumber=&pageSize=` | `200` `PagedResult<PatientDocumentResponse>` | `uploadDateUtc` desc, `id` asc |

`PatientTimelineEventResponse(id, eventType, occurredAtUtc, visitId?, payload…)` — one of
`visit` (visitDate, doctorName, chiefComplaint?, counts), `note` (doctorName, noteContent,
visitId?), `lab_result` (testName, value, unit, visitId?), `document` (fileName, type,
description?, uploadedByDoctorName?). A single nullable `visitId` field carries the F4
deep-link target where applicable.

`PatientDocumentResponse(id, fileName, type, uploadDateUtc, description?, uploadedByDoctorName?)`
— **no `location`, no path, no URL, ever.**

### 4.2 Authorization order (identical to F3-C1/C2/C3a — no new logic)

Role ≠ Doctor → `403 medical.access_denied`; missing/empty user id → `403 identity.invalid`;
request shape via `PagedQueryValidator<T>`; doctor missing/inactive → `403 identity.invalid`;
patient unknown / no owner / another doctor's → uniform `404 patient.not_found`; only then read.

### 4.3 Error contract

Same as F3-C2/C3a: 401 `auth.unauthorized`, 403 `access.forbidden` (framework role check) with
the Application `medical.access_denied` boundary behind it, 404 `patient.not_found`,
400 `validation.failed`.

### 4.4 Timeline composition (store-side)

The store projects the four event sources into one common shape
(`occurredAtUtc`, `eventType`, `id`, payload fields) and applies the global ordering and paging
in SQL/EF (AGENTS §3.1 — filter and order at the data source; no unbounded in-memory merge).
The exact composition technique is chosen during build for correct EF translation on both the
InMemory test provider and MySQL, preserving SQL-side ordering.

## 5. Files

**Extend** — `IMedicalRecordStore.cs`, `MedicalRecordStore.cs`, `IMedicalRecordService.cs`,
`MedicalRecordService.cs`, `MedicalRecordRequests.cs`, `MedicalRecordResponses.cs`,
`MedicalRecordValidators.cs`, `MedicalRecordsController.cs`,
`backend/docs/api/PATIENT_MEDICAL_API.md`, `STATE.md`,
`backend/tests/Medilog.Application.Tests/*` (store fakes/scenarios + new tests),
`backend/tests/Medilog.WebAPI.Tests/Api/MedicalRecordsApiTests.cs`,
`frontend/src/features/patients/**` (types, readers, api, keys, hooks, components, screen,
messages en/ar, tests).

**Create** — `docs/tasks/F3/F3-C3b.md` (this file).

**Not touched:** all F4 files, other members' features, Domain entities, EF configurations,
migrations (`has-pending-model-changes` must stay clean), shared auth code, the
`features/medical-timeline` skeleton (D-T4), file upload/download/serving (Q-13).

## 6. Test plan (per endpoint — extends the C3a matrix)

1. **Authorized doctor:** 200; timeline contains all four event types for the patient; document
   metadata complete incl. `UploadedBy = null` case (D-T5); `visitId` correct where applicable.
2. **Ordering:** newest first; deterministic tie-breakers proven with equal-timestamp events.
3. **Omitted query:** defaults 1/10 from `PagingDefaults`.
4. **Beyond last page:** 200 empty `items`, correct metadata.
5. **Secretary:** 403 with zero medical data in the body (both endpoints).
6. **Cross-doctor:** uniform `404 patient.not_found` (both endpoints).
7. **Unauthenticated:** 401 (both endpoints).
8. **Invalid paging:** `pageNumber=0`, `pageSize=0`, `pageSize=51` → 400 `validation.failed`.
9. **Unknown patient:** same 404 shape as cross-doctor.
10. **Empty patient:** 200 with empty `items`, `totalPages` 0.
11. **Privacy:** document responses (list and timeline events) never contain `location`, raw
    paths or file URLs — asserted directly on serialized bodies.
12. **Isolation:** rows of other patients' visits/documents never appear; visit-linked events
    carry only their own visit's `visitId`.
13. **Frontend:** gate tests (no request without a ready doctor identity / as a secretary),
    API-order rendering, Q-04 pagination incl. page 2, visit deep-links only for valid GUIDs
    with "unlinked" badges otherwise, empty/error/retry states, AR locale, malformed-payload
    readers throw, messages parity.

## 7. Acceptance criteria

- **AC-1** Both endpoints are thin adapters; all authorization/validation in the service (reuse, no duplication).
- **AC-2** Timeline covers visit/note/lab_result/document events with `occurredAtUtc` desc + stable tie-breakers; payloads are summaries (D-T1…D-T3).
- **AC-3** Exact Q-04 envelope on both endpoints.
- **AC-4** Secretary 403 / cross-doctor 404 / unauthenticated 401 proven by integration tests through the real pipeline; failure bodies contain no medical data.
- **AC-5** `location`/paths/URLs never exposed in any response (Q-13); no upload/download/file-serving code exists.
- **AC-6** F4 and other members' functionality untouched; zero schema/migration change; `has-pending-model-changes` clean.
- **AC-7** Contract doc appended (§3.5/§3.6); `MEDICAL_RECORDS_API.md` untouched; STATE.md registers Q-12/Q-13 as answered.
- **AC-8** `dotnet build` 0/0; full backend suite passes; frontend lint/typecheck/tests/build green.
- **AC-9** Real-MySQL verification attempted if a DB is available; otherwise recorded honestly as not performed.

## 8. Dependencies, blockers, risks

- **Sequence:** merges only after PR #7 → #8 → #9 (stacked chain; PR #10 targets `feature/mrehan-patient-status`).
- **Q-12/Q-13:** answered and recorded here (team approval communicated by Mrehan, 2026-11-14); registered in STATE.md.
- **Risk R-1:** timeline composition must order/paginate at the data source — store-side composition with a common projection; verified against InMemory tests and (when available) real MySQL.
- **Risk R-2:** in-memory test DB vs real MySQL (same accepted risk as previous cycles).
- **Risk R-3:** `location` confidentiality — enforced by DTO shape (the field is simply absent from the read models), not by serialization filtering, so it cannot leak by configuration accident.
- **Assumption:** `UserRole.Doctor`/`Secretary` unchanged; dev-token mechanism unchanged; existing domain timestamp semantics (`VisitDate`, `CreatedAtUtc`, `TestDate`, `UploadDate`) unchanged.
