# STATE.md — Medilog Project State

> **Live status of the current delivery.** Read after `AGENTS.md` at the start of every AI session.
> This file records *status, decisions, blockers, and next steps*. It does **not** define rules (that is `AGENTS.md`)
> and it must **not** change approved scope, task ownership, or acceptance criteria.
>
> **Who updates it:** the contributor at the end of each Cycle, through a PR reviewed by the Tech Lead.
> The AI only *proposes* the update (see `ElwakeelBuild.md` → `done`).

- **Last updated:** 2026-11-13
- **Tech Lead:** TBD
- **Integration branch:** TBD (do not assume `main`)
- **Instructor-approved scope:** five features (F1–F5) + shared technical requirements (FS)
- **Breakdown status:** Features and owners defined below. Phases/Cycles/Tasks **not generated yet** — run `ElwakeelManage`, then get Tech Lead approval.

## 1. Session Start Kit

Every AI session starts with these files, in this order:
1. `AGENTS.md`
2. `docs/PROJECT_OVERVIEW.md` — ⚠ **does not exist yet** (Tech Lead to create)
3. `STATE.md` (this file)
4. `.ai/skills/ElwakeelManage.md` (planning work) **or** `.ai/skills/ElwakeelBuild.md` (implementing a Cycle)
5. The Cycle task file `docs/tasks/<Feature>/<CycleId>.md` and only the code it touches

Workflow commands: `plan → build → review → done`. Human approval is required between `plan` and `build`.

## 2. Approved Features and Owners

| ID | Feature | Owner | Responsibilities (instructor breakdown) |
|----|---------|-------|------------------------------------------|
| F1 | Appointment & Booking Management (Secretary) | Member 1 — TBD | Create booking; list and details; update/reschedule; cancel; prevent same-doctor overlap; secretary appointment screens |
| F2 | Payment Management (Secretary) | Member 2 — TBD | Record patient payments; list and search; payment status; payment screens; link payment to the approved financial entity |
| F3 | Patient Medical Status (Doctor) | Member 3 — Mrehan Mohamed | View patient medical data; previous visits; diagnoses and prescriptions; medical history within approved scope; medical-profile screens |
| F4 | Medical Updates & Prescriptions (Doctor) | Member 4 — Mohamed Shalaby | Add visit data; record diagnosis; add prescription and medications; edit allowed medical data; medical entry/edit screens |
| F5 | Doctor Clinic Dashboard | Member 5 — TBD | Visit count; doctor's total payments; today's appointments; today's patient statuses; dashboard UI on real data |
| FS | Shared Technical Foundation & Integration | Member 6 — TBD | Unified filtering and pagination; FluentValidation setup + central registration; AutoMapper setup + central registration; entity-configuration conventions; API contract integration review; cross-feature integration and build health |

## 3. Phases (proposed — pending `ElwakeelManage` + Tech Lead approval)

Dependency order inferred from the features; this is a proposal, not an approved plan.

| Phase | Content | Depends on | Status |
|-------|---------|-----------|--------|
| P0 | FS: shared contracts and conventions (pagination/filter DTOs, validation + mapping registration, entity-configuration convention, error shape) | Decisions Q-04, Q-05 | In progress — shared paging + error contract delivered (`P0-C1…C3`, `P1-C1…C4`); Tech Lead review and merge commit pending |
| P1 | F1 Appointments, F3 Medical Status (read side), F4 Medical Updates | P0 contracts | Not started as a phase; F1 and F4 were delivered before this phase plan and are aligned to the P0 contract |
| P2 | F2 Payments | P0 + approved payment model (Q-01) + F1 | Blocked |
| P3 | F5 Dashboard | F1, F2, F4 data | Blocked |
| P4 | Integration, regression, demo data, documentation | all | Not started |

> **Cycle-ID note (2026-11-13):** the Cycle reports `P0-C1…C3` (merge recovery + backend/frontend
> foundation unification) and `P1-C1…C4` (shared paging contract, shared error envelope, API
> contract docs, frontend shared paging reader) are cycles **inside the P0/FS row** above. They do
> not renumber the phases in this table. Still open in the FS row: the filter contract for list
> endpoints that do not exist yet, and the FS integration review of each feature's list screen.

## 4. Feature / Cycle Board

Status values: `Not started` · `Draft` · `Ready` · `Approved` · `In progress` · `In review` · `Done` · `Blocked`

| Feature | Cycles | Status | Blocker |
|---------|--------|--------|---------|
| F1 | F1-C1 Domain & EF Core (`docs/tasks/F1/F1-C1.md`) — **Done**; F1-C2 Application Service & Business Rules (`docs/tasks/F1/F1-C2.md`) — **Done**; F1-C3 WebAPI Controllers & Security (`docs/tasks/F1/F1-C3.md`) — **Done**; F1-C3b Lookups, error codes, dev seed (`docs/tasks/F1/F1-C3b.md`) — **Done**; F1-C4 Frontend Screens (`docs/tasks/F1/F1-C4.md`) — **Done** (F1 cycles complete; commit/PR and manual verification pending) | Done (verification pending) | Q-07 (authentication) deferred by Tech Lead — API uses a temporary dev-only header identity; paging and error contract aligned to Q-04 on 2026-11-13; F1-C4 needs doctor/patient lookup endpoints |
| F2 | _to be generated_ | Blocked | Q-01 (payment model) |
| F3 | F3-C1 Medical Profile & Medical History, Application + Infrastructure read side (`docs/tasks/F3/F3-C1.md`) — **Done** (merged via PR #4, merge commit `407f34c`); F3-C2 Medical Read APIs (`docs/tasks/F3/F3-C2.md`) — **In progress** (PR #7 open); F3-C3a Patient-Level Notes & Lab Results (`docs/tasks/F3/F3-C3.md`) — **Committed, in review** (`519f00e`/`c812ac7`/`9fa9261` on `feature/mrehan-medical-notes-labs`, stacked on F3-C2); F3-C4 Patient Medical Status Screens (frontend) (`docs/tasks/F3/F3-C4.md`) — **F3-C4a in progress** on `feature/mrehan-patient-status` (stacked on F3-C3a) | In progress | F3-C1: approved decisions (task file §0 + §9 post-merge addendum). F3-C2: team approval communicated by Mrehan (2026-11-14), incl. P-1…P-6; Tech Lead ratification not separately recorded; API-level secretary/ownership tests delivered in F3-C2 (AGENTS §7 release dependency). F3-C3a: approved 2026-11-14 incl. **Q-08b** (patient-level notes/labs in the approved F3 read scope) and P-A/P-B/P-C; Timeline + MedicalDocuments deferred to F3-C3b pending Q-12/Q-13. F3-C4: plan approved 2026-11-14 (D-1..D-5; D-1 uses the F1 lookup contract after a verified server-side authorization check); C4a scope = finder + profile + allergies/conditions + paged visit history; notes/labs tabs = C4b; Timeline/Documents stay out of scope |
| F4 | F4-C1 (Done, merged); F4-C2 (C2a Done; C2b In review); F4-C3 (In review — implemented, 82 tests); F4-C4 **In review** — C4a `5f6eb85`, C4b `8312d64` committed; C4c (diagnoses, medications, delete confirmation) delivered on the working tree | In review | No visit list endpoint in the current scope (Q-04 contract is now defined); Tech Lead ratification of D-23 and D-27…D-31 still pending |
| F5 | _to be generated_ | Blocked | Q-01, Q-03 |
| FS | P0-C1 (merge recovery) — **Done**; P0-C2 (backend foundation unification) — **Done**; P0-C3 (frontend transport/error/identity unification) — **Done**; P1-C1 (shared paging contract) — **Done**; P1-C2 (shared error envelope) — **Done**; P1-C3 (API contract docs) — **Done**; P1-C4 (frontend shared paging reader) — **Done** | Done (review + merge commit pending) | Q-04 resolved; remaining FS work is the filter contract for not-yet-existing list endpoints and the per-feature list-screen integration review |

## 5. Active Tasks

| Member | Active Cycle | Branch | Status | Last update |
|--------|--------------|--------|--------|-------------|
| Member 1 (Menna) | F1-C3b → F1-C4 (F1-C1, F1-C2, F1-C3 Done) | `feature/menna-appointments` | F1 complete: C1–C4 and C3b Done; commit/PR and verification against real MySQL + browser pending | 2026-11-13 |
| Member 2 | — | — | — | — |
| Member 3 (Mrehan) | F3-C4a (Patient Medical Status Screens — finder, profile, visit history) | `feature/mrehan-patient-status` | F3-C1 Done (PR #4 `407f34c`); F3-C2 In review — PR #7 open on `feature/mrehan-medical-apis` `1325a89`; F3-C3a committed (`519f00e`/`c812ac7`/`9fa9261`) on `feature/mrehan-medical-notes-labs`, in review; F3-C4a in progress on a branch **stacked on** the C3a branch — plan approved 2026-11-14 (communicated by Mrehan; D-1 lookup authorization verified) | 2026-11-14 |
| Member 4 | — | — | — | — |
| Member 4 | F4-C4 (C4c: diagnoses, medications, delete confirmation) | `feature/shalaby-visits` | C4a `5f6eb85` + C4b `8312d64` committed; C4c delivered 2026-11-12 (typecheck/lint/build green, manual M-8…M-10 + M-14 runtime matrix, AR/EN + RTL verified); awaiting review/commit · C3 In review | 2026-11-12 |
| Member 5 | — | — | — | — |
| Member 6 | — | — | — | — |

## 6. Requirement Source Mapping (preliminary — to be verified by `ElwakeelManage`)

| Feature | Related requirements found in SRS / BRD / Class Spec | Coverage note |
|---------|-------------------------------------------------------|---------------|
| F1 | FR-43 to FR-48, FR-49, FR-51; BR-18, BR-19, BR-20; BRULE-10, BRULE-11; Class Spec §4.13, §4.14 | Covered. Doctor-created appointments also exist in the SRS |
| F2 | **None** | **GAP** — no payment entity/FR; "online payments" is out of scope in the BRD (manual recording is a new need) |
| F3 | FR-09 to FR-13, FR-15, FR-18, FR-20; BR-04, BR-14, BR-16; BRULE-03, BRULE-04 | Covered; "patient status" meaning is not defined (Q-03) |
| F4 | FR-14 to FR-21, FR-26, FR-30, FR-31; BR-05 to BR-07, BR-09, BR-11; BRULE-05, BRULE-06, BRULE-08 | Covered |
| F5 | FR-39 (dashboard: patients, visits, today's appointments) | Partial — payments/revenue part has no requirement |
| FS | Instructor technical requirements; NFR-06, NFR-07, NFR-15, NFR-16 | Defined in `AGENTS.md` §3 |

## 7. Decisions Log

### Approved
| Date | Decision | Source |
|------|----------|--------|
| 2026-11-08 | Backend .NET 10; frontend Next.js 16 / React 19 / TypeScript / next-intl / Tailwind 4 | AGENTS.md §12 |
| 2026-11-08 | Backend projects: Domain, Application, Infrastructure, Api | AGENTS.md §12 |
| 2026-11-08 | Five-feature delivery scope, owners as in §2 | Instructor scope |
| 2026-11-08 | Filtering + pagination are shared technical requirements | Instructor |
| 2026-11-08 | FluentValidation (bonus), AutoMapper, one EF configuration class per entity | Instructor / AGENTS.md §3 |
| 2026-11-08 | Workflow `plan → build → review → done` with `ElwakeelManage` and `ElwakeelBuild` | Instructor / AGENTS.md §8 |
| 2026-11-12 | F1 (Q-09, Tech Lead via Menna): Cancelled and No-Show free their slot; `doctor_secretaries` assignment enforced; `EndTime` required in Domain/Application/API with no default duration and no schema change; past bookings rejected; clinic timezone `Africa/Cairo` in `Clinic:TimeZone`; `CancellationReason` mandatory on cancel, max 500; no test project for now | `docs/tasks/F1/F1-C1.md` §0 |
| 2026-11-12 | F1-C2 (Tech Lead via Menna): Application-owned `IAppointmentStore` port implemented in Infrastructure (Application stays EF-free); `ICurrentUser` (UserId, Role); typed Application exceptions (NotFound/Forbidden/Conflict/validation) alongside `DomainException`; temporary lightweight `PagedResult<T>` + list query in Application until Q-04 is decided (scoped to F1 only, not the shared contract); xUnit project `backend/tests/Medilog.Application.Tests` added to `Medilog.sln` (supersedes the C1 "no test project" decision); Completed = doctor only, No-Show/Confirmed = doctor or assigned secretary; patient must belong to the booking doctor; legacy null `end_time` treated as a point at start with a warning log; per-doctor `FOR UPDATE` transaction; out-of-scope ids → NotFound; inactive doctors not bookable; secretaries may filter all statuses | `docs/tasks/F1/F1-C2.md` §0 |
| 2026-11-12 | F1-C3 (Tech Lead Abdullah via Menna): **no authentication, JWT packages, `UseAuthentication()` or `[Authorize]` until Q-07 is implemented by its owner**; temporary `HeaderCurrentUser` reads `X-User-Id` / `X-User-Role` (missing/invalid → 403) and is enabled only when `Auth:AllowHeaderIdentity` = true (Development); routes `POST/GET /api/appointments`, `GET /{id}`, `PUT /{id}/schedule`, `POST /{id}/cancel`, `PUT /{id}/status`; `IExceptionHandler` + ProblemDetails (404/403/409/400/500); string enums, `yyyy-MM-dd`, `HH:mm:ss`; new `Medilog.WebAPI.Tests` using `WebApplicationFactory`; contract doc `backend/docs/api/appointments.md` | `docs/tasks/F1/F1-C3.md` §0 |
| 2026-11-12 | F1-C3b / F1-C4 (Tech Lead via Menna): backend mini-cycle adds read-only lookups `GET /api/appointments/lookups/doctors` and `/lookups/patients` (id, name, phone only; scoped like the service) and machine-readable `code`s on error responses; synthetic dev seed `backend/docs/dev/seed-dev-data.sql`; frontend uses a fetch-based `src/lib/api` that attaches dev identity headers from env/dev config in one place (inactive in production); Vitest + Testing Library + jsdom approved as dev dependencies; UI primitives stay inside `features/appointments/components`; routes `/appointments` and `/appointments/new` | `docs/tasks/F1/F1-C4.md` §0 |
| 2026-11-13 | F3-C1 (team approval via Mrehan): base branch is `main`; cycle scope = Medical Profile (all Class Spec §4.2 Patient attributes incl. `EmergencyContact` and `BloodType`) + visit-based Medical History, newest first, paged; **Timeline, MedicalDocuments, unlinked notes/labs deferred to F3-C2** (explicit scope decision, Q-08 partial); doctor-only access with `medical.access_denied` before any data access; single-owner workspace via `Patient.DoctorId` (Q-02 application for this cycle); no `PatientStatus` field (Q-03 application); Application-owned store port `IMedicalRecordStore` in Infrastructure; F1's temporary `PagedResult`/`PagingDefaults` until Q-04; child-collection ordering in the Application service (string-stored enums cannot be ordered in SQL) | `docs/tasks/F3/F3-C1.md` §0 |
| 2026-11-14 | **F3-C2** (team approval communicated by Mrehan Mohamed, Member 3): WebAPI read endpoints `GET /api/patients/{patientId}/medical-profile` and `GET /api/patients/{patientId}/medical-visits` (P-1) on a new thin `MedicalRecordsController` (P-2); additive paging defaults on `GetMedicalHistoryRequest` mirroring F1's list-query binding (P-3); contract doc `backend/docs/api/PATIENT_MEDICAL_API.md`, the cross-link inside `MEDICAL_RECORDS_API.md` deferred until Member 4 agrees (P-4); integration suite `MedicalRecordsApiTests` covering the AGENTS §7 API security cases (secretary 403, cross-doctor 404, unauthenticated 401, invalid paging) (P-5); STATE.md duplicate F3/Member-3 row cleanup + F3-C1 marked Done via PR #4 merge commit `407f34c` (P-6). Contributor-communicated approval; **no Tech Lead name or separate ratification date recorded** (STATE.md Tech Lead field is "TBD") | `docs/tasks/F3/F3-C2.md` |
| 2026-11-14 | **F3-C3a** (team approval communicated by Mrehan Mohamed, Member 3; **Q-08b answered**: patient-level notes/labs are in the approved F3 read scope): read-only endpoints `GET /api/patients/{patientId}/notes` and `GET /api/patients/{patientId}/lab-results` (P-A) on the existing `MedicalRecordsController`; **all** patient records included (visit-linked + unlinked) with nullable `visitId` (contributor decisions C-1…C-6); contract sections appended to `PATIENT_MEDICAL_API.md` (P-B); ordering notes `CreatedAtUtc` desc / labs `TestDate` desc with id tie-breakers (P-C). Timeline and MedicalDocuments remain deferred to F3-C3b pending Q-12/Q-13. Implemented on `feature/mrehan-medical-notes-labs` **stacked on** the F3-C2 branch (PR #7 untouched); merges only after PR #7. Contributor-communicated approval; **no Tech Lead name or separate ratification date recorded** | `docs/tasks/F3/F3-C3.md` |
| 2026-11-14 | **F3-C4** plan approved and registered (communicated by Mrehan Mohamed, Member 3): doctor-facing patient medical status screens; decisions **D-1** consume the existing F1 lookup contract `GET /api/appointments/lookups/patients` **after a verified authorization check** (actor resolved server-side; a `Doctor` caller may pass only `doctorId == own id`, else `404`; store filters `patient.DoctorId == doctorId` and returns only id/name/phone), **D-2** `/patients` entry route, shared `Header.tsx` untouched, **D-3** shared identity/API-client conventions only, **D-4** tabs on one patient status screen, **D-5** split into C4a (finder + profile + allergies/conditions + paged visit history) and C4b (notes + lab-results tabs); Timeline/MedicalDocuments remain out of scope (F3-C3b, Q-12/Q-13). Implemented on new branch `feature/mrehan-patient-status` **stacked on** `feature/mrehan-medical-notes-labs` @ `9fa9261`; PR #7 and the C3a commits untouched | `docs/tasks/F3/F3-C4.md` |
| 2026-11-10 | **D-08** — plain application services (`IMedicalVisitService`); **MediatR NOT adopted** (backend pattern part of Q-05) | Member 4 |
| 2026-11-10 | **D-09** — Application depends on `IApplicationDbContext` + `DbSet<T>`; no repository abstraction; `MedilogDbContext` implements it | Member 4 |
| 2026-11-10 | **D-10** — typed application exceptions (`NotFoundException`, `ForbiddenException`, `ConflictException`) mapped to `ProblemDetails` (HTTP mapping implemented in C2b) | Member 4 |
| 2026-11-10 | **D-11** — `ICurrentUser` abstraction; acting doctor comes from server-side claims, never from a request | Member 4 |
| 2026-11-10 | **D-12** — no diagnosis delete in F4-C2 (update only) | Member 4 |
| 2026-11-10 | **D-13** — new xUnit project `backend/tests/Medilog.Application.Tests` added to `Medilog.sln` (closes D-05) | Member 4 |
| 2026-11-10 | **D-14** — request text limits + whitespace→null normalization for medical records | Member 4 |
| 2026-11-10 | **D-15** — EF Core InMemory is the test provider for Application service tests; `Microsoft.EntityFrameworkCore.Relational` 10.0.14 pinned in the test project to keep one EF version | Member 4 |
| 2026-11-11 | **D-16** — Q-07 answered: JWT Bearer validation (`Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.14); no login endpoint; dev tokens via `dotnet user-jwts` (`sub` + `role`); no secrets committed | Member 4 / Cycle owner |
| 2026-11-11 | **D-17** — Q-02 answered: single-owner workspace (`Patient.DoctorId == actingDoctorId`); unowned patient inaccessible; no schema change | Member 4 / Cycle owner |
| 2026-11-11 | **D-18** — Q-05 remainder answered: MVC controllers for the medical endpoints; no Minimal APIs, no MediatR, no Result library | Member 4 / Cycle owner |
| 2026-11-11 | **D-19** — new test project `backend/tests/Medilog.WebAPI.Tests` with `Microsoft.AspNetCore.Mvc.Testing` 10.0.14, `WebApplicationFactory`, `TestAuthHandler`, EF InMemory | Member 4 / Cycle owner |
| 2026-11-11 | **D-20** — `201 Created` location: visit → `GET /api/medical-visits/{id}`; diagnosis → parent visit (no diagnosis `GET` route) | Member 4 / Cycle owner |
| 2026-11-11 | **D-21** — `AddInfrastructure` registers `IApplicationDbContext` → `MedilogDbContext` (fixes the missing persistence dependency discovered before C2b) | Member 4 |
| 2026-11-11 | **D-22** — Q-08 answered for F4: keep `ClinicalNotes` exactly as implemented in C2a | Member 4 / Cycle owner |
| 2026-11-11 | **D-23** — medication delete is a **hard delete** (`DELETE …/medications/{id}` → 204), scoped to the owning doctor, no schema change. Cycle-owner addition beyond the instructor's “add and update”; retention risk must be ratified by the Tech Lead before merge | Member 4 / Cycle owner |
| 2026-11-11 | **D-24** — `MedicalVisitResponse` gains a `Medications` collection (ordered by name then id); no separate medication list route | Member 4 / Cycle owner |
| 2026-11-11 | **D-25** — medication use cases are added to the existing `IMedicalVisitService`, not a new service | Member 4 / Cycle owner |
| 2026-11-11 | **D-26** — medication request limits: name ≤ 200, dosage/frequency/duration ≤ 100, instructions ≤ 4000 | Member 4 |
| 2026-11-11 | **D-27** — frontend HTTP layer: one typed native-`fetch` wrapper in `frontend/src/lib/api`, `NEXT_PUBLIC_API_BASE_URL` (dev default `http://localhost:5208`), normalised `ApiError` + `ProblemDetails`; no Axios | Member 4 / Cycle owner |
| 2026-11-11 | **D-28** — development-only token bridge: pasted JWT → `sessionStorage`, `Authorization: Bearer`, never rendered/logged, hidden in production; no login flow added | Member 4 / Cycle owner |
| 2026-11-11 | **D-29** — frontend server state via the installed TanStack Query; forms use React state; no Zustand for server data, no Zod/form library, no new dependency | Member 4 / Cycle owner |
| 2026-11-11 | **D-30** — F4-C4 routes: `/medical-records`, `/medical-records/visits/new?patientId=`, `/medical-records/visits/[visitId]`; no list route until Q-04 | Member 4 / Cycle owner |
| 2026-11-11 | **D-31** — F4-C4 discovery bridge: “open by id” form + one localised `Header` link; no business logic in shared components | Member 4 / Cycle owner |
| 2026-11-11 | **Frontend source of truth** — `frontend/src` (not `Front_End/`); CORS origin for local dev is `http://localhost:3000`; no CORS/backend change | Member 4 / Cycle owner |
| 2026-11-13 | **Q-04 answered** — shared paging contract: query `pageNumber`/`pageSize`, `DefaultPageSize = 10`, `MaxPageSize = 50`; `PagedResult<T>` = `items`, `page`, `pageSize`, `totalCount`, `totalPages`; deterministic ordering with a stable tie-breaker | Cycle owner / Tech Lead |
| 2026-11-13 | **Phase 1 (FS) confirmations** — response field is `page` while the request stays `pageNumber`; F4 request validation moves to `RequestValidationException` in the Application layer through one shared `ValidateAsync` helper; framework-generated responses (unmatched route `404`, `401` challenge, `405`, `415`) also carry the envelope via `UseStatusCodePages`; work proceeds one Cycle at a time | Repository owner |

### Pending approval (blocks work — do NOT assume)
| ID | Question | Affects | Needed from | Status |
|----|----------|---------|-------------|--------|
| Q-01 | **Payment model:** what a payment attaches to (appointment / visit / invoice); partial or multiple payments; methods; statuses; void/edit rules; whether the fee amount exists and where. Not in SRS/BRD/Class Spec/ERD | F2, F5 | Instructor / Tech Lead | Open |
| Q-02 | **Doctor–patient access relationship** (Patient is owned by one doctor in the Class Spec; can several doctors treat the same patient?) | F3, F4, F5 | Tech Lead | **Answered 2026-11-11 — D-17:** single-owner workspace confirmed for F4; F3/F5 still need to apply the same rule |
| Q-03 | **Meaning of "patient status"** (F3) and **"patients' statuses today"** (F5): appointment status, visit state, or a new field | F3, F5 | Tech Lead / Instructor | Open |
| Q-04 | **Shared list contract:** pagination DTO names, response envelope, default page size, max page size, default sort and tie-breaker, filter parameter style | all list endpoints | Member 6 → Tech Lead | **Answered 2026-11-13** (see Approved table): `pageNumber`/`pageSize`, default 10, max 50; `PagedResult<T>` = items, page, pageSize, totalCount, totalPages |
| Q-05 | **Backend patterns in use:** Controllers vs Minimal APIs; MediatR/CQRS yes/no; repositories yes/no; Result type yes/no (AGENTS.md says do not assume). Note: `backEnd_skill.md` assumes MediatR + Result Pattern — decide whether that file still applies | FS, all backend | Tech Lead (inspect codebase) | **Answered 2026-11-11 — D-08…D-10 + D-18:** plain services, typed exceptions, no MediatR/repositories/Result, MVC controllers |
| Q-06 | **Frontend libraries:** API client, server-state, local-state, schema validation, toast. Note: `frontend_skill.md` assumes Axios, TanStack Query, Zustand, Zod, sonner, which the README does not list — decide whether that file still applies | FS, all frontend | Tech Lead (inspect `package.json`) | **Partially answered 2026-11-11 — D-27/D-29** (native `fetch`, TanStack Query, React-state forms, no new dependency); formal Tech Lead ratification for FS still pending |
| Q-07 | **Authentication and role provisioning** details (token model, roles, how Doctor/Secretary/Admin log in, seed users); no OTP/password-reset in scope | all | Tech Lead | **Answered 2026-11-11 — D-16:** JWT Bearer validation + `dotnet user-jwts`; no login endpoint; production IdP config still supplied by deployment |
| Q-08 | **Medical scope for F3/F4:** which entities are in (visit, diagnosis, medication/prescription, vital signs, notes) and whether allergies, chronic conditions, labs, documents, timeline are excluded | F3, F4 | Tech Lead / Instructor | **Partially answered 2026-11-11 — D-22:** F4 keeps `ClinicalNotes`; the wider F3 scope is still open |
| Q-09 | **Appointment details:** do cancelled (and no-show) appointments free their slot; are secretary-assigned doctors (SecretaryDoctor) enforced in this delivery | F1 | Tech Lead | **Answered 2026-11-12** (see Approved table) |
| Q-10 | **Database engine**, integration branch name, branch naming, migration ownership | FS | Tech Lead | Open |
| Q-11 | **Frontend test framework:** the repository has none installed (`frontend/package.json` has no test runner). Adding Vitest/Playwright is a new shared dependency | FS, all frontend | Tech Lead | **Answered 2026-11-11 — deferred:** F4-C4 is verified by `typecheck`/`lint`/`build` + a manual matrix; no runner is added |

## 8. Known Inconsistencies in Source Documents (for ElwakeelManage to resolve with approval)

- Class Spec: blood type `O` should be `O-`; `specialization` vs SRS `Specialty`.
- No System Administrator class in the Class Spec though SRS/BRD define the role.
- SRS FR-01 (doctor self-registration) vs SRS §5.2 (Admin creates doctor accounts).
- Final ERD is referenced by the Class Spec but not available in the repository docs provided.
- `backEnd_skill.md` (.NET 8, MediatR, Result Pattern) and `frontend_skill.md` (`src/` layout, Axios/TanStack/Zustand/Zod) conflict with the repository reality and with `AGENTS.md` §6.1 / §12. `AGENTS.md` wins until Q-05 / Q-06 are answered.
- `AGENTS.md` §8.1 requires `docs/PROJECT_OVERVIEW.md`, which does not exist yet.
- `AGENTS.md` §6.1 documents `Front_End/` with root-level `app/`, `components/`, `data/`, `types/`,
  but the repository actually contains **`frontend/src/`** with no `data/` or `types/` folder.
  F4-C4 follows the real layout; §6.1 / Q-06 need the Tech Lead's correction (do not move the code
  to satisfy stale documentation).

## 9. Blockers

| Date | Blocker | Blocking | Owner to resolve |
|------|---------|----------|------------------|
| 2026-11-08 | Q-01 payment model not approved | F2, F5 | Tech Lead / Instructor |
| 2026-11-08 | Q-04 shared list contract not agreed | F1–F5 list endpoints | Member 6 — **resolved 2026-11-13** (see Decisions Log) |
| 2026-11-08 | `docs/PROJECT_OVERVIEW.md` missing | Session Start Kit | Tech Lead |
| 2026-11-10 | SRS/BRD/Class Spec/ERD texts still absent from `docs/`; F4 rule IDs remain inferred | field-level verification | Tech Lead |
| 2026-11-11 | No reachable MySQL instance; the EF model is verified only by `has-pending-model-changes` and InMemory tests | schema-level verification | Tech Lead / FS |
| 2026-11-11 | Q-06 formally open for FS; F4-C4 D-27/D-29 were ratified by the Cycle owner on 2026-11-11 using only the installed stack | FS ratification | Tech Lead |

## 10. Execution Reports

Index of one report per AI-assisted Cycle (store under `docs/reports/<CycleId>.md`).

| Cycle | Contributor | Date | Result | Report |
|-------|-------------|------|--------|--------|
| F1-C1 | Menna | 2026-11-12 | Done — build 0 warnings/0 errors; EF model in sync; no tests (no test project by decision) | `docs/reports/F1-C1.md` |
| F1-C2 | Menna | 2026-11-12 | Done — build 0 warnings/0 errors; 94 tests pass; EF model in sync; real-MySQL lock verification still open | `docs/reports/F1-C2.md` |
| F1-C3 | Menna | 2026-11-12 | Done — build 0 warnings/0 errors; 94 + 43 tests pass; EF model in sync; no authentication (temporary dev-only header identity, gated by `Auth:AllowHeaderIdentity`); real-MySQL verification still open | `docs/reports/F1-C3.md` |
| F1-C3b + F1-C4 | Menna | 2026-11-13 | Done — backend 115 + 57 tests, frontend 282 tests, lint/typecheck/build clean; real MySQL + browser verification still open | `docs/reports/F1-C4.md` |
| F3-C1 | Mrehan | 2026-11-13 | Done for the cycle — build 0 warnings/0 errors; Application 156 + WebAPI 57 tests pass (36 new); EF model unchanged; real-MySQL store verification **not** performed (no DB available); API-level security tests deferred to the next F3 cycle | `docs/reports/F3-C1.md` |
| F4-C1 | Mohamed Shalaby | 2026-11-10 | Done — merged with `main` | `docs/reports/F4-C1.md` |
| F4-C2 (C2a) | Mohamed Shalaby | 2026-11-10 | Done for C2a (build 0/0, 32 tests, no model drift) | `docs/reports/F4-C2.md` |
| F4-C2 (C2b) | Mohamed Shalaby | 2026-11-11 | In review — five endpoints + 18 WebAPI tests; build 0/0, 50 tests, no model drift | `docs/reports/F4-C2.md` |
| F4-C3 | Mohamed Shalaby | 2026-11-11 | In review — three medication endpoints + 32 tests; build 0/0, 82 tests, no model drift | `docs/reports/F4-C3.md` |
| F4-C4 | Mohamed Shalaby | 2026-11-12 | In review — frontend visit create/detail/edit + diagnoses + medications (C4a `5f6eb85`, C4b `8312d64` committed, C4c on the working tree); typecheck/lint/build green, manual M-8…M-10 + M-14 verified | `docs/reports/F4-C4.md` |
| P0-C1 + P0-C2 | AI draft (pending contributor review) | 2026-11-13 | Done — finished the in-progress merge without discarding either branch: 7 conflicted files resolved, solution/test projects deduplicated, backend foundation unified (one `ICurrentUser`, one claims+dev-header `CurrentUser`, one `GlobalExceptionHandler`, JWT registered), duplicate exception types removed; `dotnet build` 0 warnings/0 errors, 171 + 83 tests pass | `docs/reports/P0-C1-C2.md` |
| P0-C3 | AI draft (pending contributor review) | 2026-11-13 | Done — one frontend transport (`lib/api/client.ts`), one `ApiError` model, one identity-header registry (dev `X-User-*` + dev bearer token); `api-client.ts`/`env.ts`/`problem-details.ts` removed; typecheck/lint/build clean, 283 frontend tests pass | `docs/reports/P0-C3.md` |
| P1-C1 … P1-C4 | AI draft (pending contributor review) | 2026-11-13 | Done — shared paging contract (`pageNumber`/`pageSize`, 10/50, response `page`, `totalPages`) and one error envelope (`type`/`title`/`status`/`instance`/`code`/`traceId`, camelCase `errors`/`errorCodes`) applied to backend and frontend, with `CONVENTIONS.md` as the source of truth; `dotnet build` 0/0, 178 + 94 backend tests pass; typecheck/lint clean, 288 frontend tests pass, production build succeeds | `docs/reports/P1-C1-C4.md` |

## 11. Next Step

> **Phase 0 + Phase 1 status (2026-11-13):** P0-C1 (merge recovery), P0-C2 (backend foundation
> unification), P0-C3 (frontend transport/error/identity unification) and P1-C1…P1-C4 (shared
> paging contract, shared error envelope, API contract docs, frontend shared paging reader) are
> complete. `dotnet build` is 0 warnings/0 errors with 272 backend tests passing; the frontend
> typecheck/lint/build are clean with 288 tests passing. Nothing has been committed yet — the
> merge commit and the review are the contributor/Tech Lead's decision.

1. Review the working tree (merge resolution + Phase 0/1 changes) and make the merge commit; it
   was intentionally not committed by the AI.
2. Ratify **D-23 (hard delete of medication lines)** against the instructor's “add and update”
   wording — it blocks the F4-C3/F4-C4 merge, and confirm the frontend library decisions
   D-27/D-29 (Q-06) are accepted project-wide, not only for F4.
3. Review and merge F4-C2b (Tech Lead ratification of the first API convention) and the new
   `backend/docs/api/CONVENTIONS.md`.
4. Tech Lead answers the remaining blocking questions (Q-01, Q-03, Q-06) and confirms Q-10.
   Q-04 is answered; the shared paging/error contract is implemented and documented.
5. Create `docs/PROJECT_OVERVIEW.md` and place the SRS/BRD/Class Spec/ERD under `docs/`.
6. Next feature work: F2 (payments) needs Q-01; F3 needs Q-03/Q-08; F5 needs Q-01/Q-03. F1 and F4
   are aligned to the shared contract but still need a real-MySQL round-trip and a browser pass.

## 12. Changelog

| Date | Change | By |
|------|--------|----|
| 2026-11-08 | Initial STATE.md aligned with AGENTS.md: features, owners, decisions log, open questions, no Cycles yet | AI draft — pending Tech Lead review |
| 2026-11-13 | F3-C1 approved and registered (board, active task, decisions log); owner confirmed as Mrehan Mohamed; task file `docs/tasks/F3/F3-C1.md` created | Member 3 — Mrehan Mohamed |
| 2026-11-14 | F3-C2 approved and registered (board, active task, decisions log); approved STATE.md cleanup applied: duplicate `Member 3 — TBD` feature row and empty `Member 3` active-task row removed, F3-C1 set to Done (merged via PR #4, merge commit `407f34c`); branch `feature/mrehan-medical-apis` created from `origin/main` `dc2be5d` | Member 3 — Mrehan Mohamed |
| 2026-11-14 | F3-C3a approved and registered (**Q-08b answered**: patient-level notes/labs in the approved F3 read scope; P-A/P-B/P-C approved); task file `docs/tasks/F3/F3-C3.md` finalized; branch `feature/mrehan-medical-notes-labs` created **stacked on** `feature/mrehan-medical-apis` @ `1325a89` (PR #7 untouched); Timeline/MedicalDocuments deferred to F3-C3b pending Q-12/Q-13 | Member 3 — Mrehan Mohamed |
| 2026-11-14 | F3-C4a approved and registered; task file `docs/tasks/F3/F3-C4.md` finalized (D-1 lookup authorization verified before approval); branch `feature/mrehan-patient-status` created **stacked on** `feature/mrehan-medical-notes-labs` @ `9fa9261` (C3a commits and PR #7 untouched) | Member 3 — Mrehan Mohamed |
| 2026-11-13 | F3-C1 implemented: `IMedicalRecordService`/`IMedicalRecordStore` (+ Infrastructure store), validators, `medical.access_denied` code, 36 unit tests; build 0/0, Application 156 + WebAPI 57 tests pass; EF model unchanged; pending review/commit/PR | Member 3 — Mrehan Mohamed |
| 2026-11-10 | F4-C1 delivered and merged with `main` (medical domain + EF configurations, shared persistence foundation) | Member 4 |
| 2026-11-10 | F4-C2a delivered: visit/diagnosis Application services, DTOs, validators, AutoMapper profile, access policy, xUnit test project (D-08…D-15) | Member 4 |
| 2026-11-11 | F4-C2b delivered and verified: JWT Bearer + MVC controllers + `ProblemDetails` + WebAPI integration tests (D-16…D-22); build 0/0, 50 tests, no model drift | Member 4 |
| 2026-11-11 | F4-C3 plan approved (D-23 hard delete · D-24 `Medications` in visit response · D-25 extend `IMedicalVisitService` · D-26 request limits) | Member 4 |
| 2026-11-11 | F4-C3 delivered and verified: medication create/update/delete use cases + endpoints + 32 tests (D-23…D-26); build 0/0, 82 tests, no model drift | Member 4 |
| 2026-11-11 | F4-C4 plan written as `Draft` (`docs/tasks/F4/F4-C4.md`): D-27…D-31 proposed, Q-11 opened; frontend reality (`frontend/src`) recorded against the `Front_End/` documentation conflict; no code written | Member 4 |
| 2026-11-11 | F4-C4 plan **approved** (D-27…D-31, Q-11 deferred, `frontend/src` + CORS `http://localhost:3000` confirmed); C4a started | Member 4 / Cycle owner |
| 2026-11-11 | F4-C4 **C4a delivered**: `lib/api` client + guards, 8-call feature API, sessionStorage token bridge, `/medical-records` workspace, AR/EN keys, `.env.example`; typecheck/lint/build green, dev + production smoke verified | Member 4 |
| 2026-11-12 | F4-C4 **C4b committed** (`8312d64`): visit create (`?patientId=` seed), visit detail with inline edit, `401/403/404/network/server` states, `Header` workspace link; typecheck/lint/build green (15 routes) | Member 4 |
| 2026-11-12 | F4-C4 **C4c delivered**: diagnosis + medication sections with inline forms, `ConfirmDeleteDialog` for the D-23 permanent delete, mutation hooks invalidating `MedicalVisitResponse` (D-24), shared form styles/banner; manual M-8/M-9/M-10/M-14 runtime matrix verified (AR/RTL + `DateOnly` UTC), child `500` path verified, typecheck/lint/build green | Member 4 |
| 2026-11-13 | **Phase 0 merge recovery (P0-C1 + P0-C2)**: finished the in-progress `feature/shalaby-visits` → `main` merge without discarding either branch; resolved 7 conflicted files, deduplicated the solution/test projects, unified `ICurrentUser` + `CurrentUser` (claims with Development-only header fallback), merged exception handling into one `GlobalExceptionHandler`, registered JWT Bearer, removed duplicate exception types; Q-04 paging contract recorded; build 0 warnings/0 errors, 171 + 83 backend tests pass | AI draft — pending Tech Lead review |
| 2026-11-13 | **Phase 0 frontend unification (P0-C3)**: `lib/api/client.ts` is the single transport (`apiRequest`/`apiRequestVoid`, with `request`/`requestVoid` aliases for medical records); one `ApiError` model (`kind` derived from status + transport failures); `auth-headers.ts` is a provider registry so the dev `X-User-*` headers and the dev bearer token share one identity surface; removed `api-client.ts`, `env.ts`, `problem-details.ts`; typecheck/lint/build clean, 283 frontend tests pass | AI draft — pending Tech Lead review |
| 2026-11-13 | **Phase 1 shared contract (P1-C1…P1-C4)**: backend `PagingDefaults` (1/1/10/50), `PagedResult<T>` with response `page` and computed `totalPages`, `IPagedQuery` + `PagedQueryValidator<T>`; one `RequestValidation.ValidateAsync` helper so F4 throws `RequestValidationException`; `ApiProblem` + `GlobalExceptionHandler` produce one envelope (`type`/`title`/`status`/`instance`/`code`/`traceId`, camelCase `errors`/`errorCodes`) including framework-generated `401/404/405/415`; `CONVENTIONS.md` added and F1/F4 docs aligned; frontend `types/paging.ts` + `lib/api/paging.ts` replace per-feature paging; build 0/0, 272 backend + 288 frontend tests pass | AI draft — pending Tech Lead review |
