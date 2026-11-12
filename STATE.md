# STATE.md — Medilog Project State

> **Live status of the current delivery.** Read after `AGENTS.md` at the start of every AI session.
> This file records *status, decisions, blockers, and next steps*. It does **not** define rules (that is `AGENTS.md`)
> and it must **not** change approved scope, task ownership, or acceptance criteria.
>
> **Who updates it:** the contributor at the end of each Cycle, through a PR reviewed by the Tech Lead.
> The AI only *proposes* the update (see `ElwakeelBuild.md` → `done`).

- **Last updated:** 2026-11-12
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
| F3 | Patient Medical Status (Doctor) | Member 3 — TBD | View patient medical data; previous visits; diagnoses and prescriptions; medical history within approved scope; medical-profile screens |
| F4 | Medical Updates & Prescriptions (Doctor) | Member 4 — TBD | Add visit data; record diagnosis; add prescription and medications; edit allowed medical data; medical entry/edit screens |
| F5 | Doctor Clinic Dashboard | Member 5 — TBD | Visit count; doctor's total payments; today's appointments; today's patient statuses; dashboard UI on real data |
| FS | Shared Technical Foundation & Integration | Member 6 — TBD | Unified filtering and pagination; FluentValidation setup + central registration; AutoMapper setup + central registration; entity-configuration conventions; API contract integration review; cross-feature integration and build health |

## 3. Phases (proposed — pending `ElwakeelManage` + Tech Lead approval)

Dependency order inferred from the features; this is a proposal, not an approved plan.

| Phase | Content | Depends on | Status |
|-------|---------|-----------|--------|
| P0 | FS: shared contracts and conventions (pagination/filter DTOs, validation + mapping registration, entity-configuration convention, error shape) | Decisions Q-04, Q-05 | Not started |
| P1 | F1 Appointments, F3 Medical Status (read side), F4 Medical Updates | P0 contracts | Not started |
| P2 | F2 Payments | P0 + approved payment model (Q-01) + F1 | Blocked |
| P3 | F5 Dashboard | F1, F2, F4 data | Blocked |
| P4 | Integration, regression, demo data, documentation | all | Not started |

## 4. Feature / Cycle Board

Status values: `Not started` · `Draft` · `Ready` · `Approved` · `In progress` · `In review` · `Done` · `Blocked`

| Feature | Cycles | Status | Blocker |
|---------|--------|--------|---------|
| F1 | F1-C1 Domain & EF Core (`docs/tasks/F1/F1-C1.md`) — **Done**; F1-C2 Application Service & Business Rules (`docs/tasks/F1/F1-C2.md`) — **Done**; F1-C3 WebAPI Controllers & Security (`docs/tasks/F1/F1-C3.md`) — **Done**; F1-C4 Frontend Screens (`docs/tasks/F1/F1-C4.md`) — Draft; further cycles to be generated | In progress (F1-C4 plan) | Q-07 (authentication) deferred by Tech Lead — API uses a temporary dev-only header identity; Q-04: temporary paging contract to be aligned with the shared one; F1-C4 needs doctor/patient lookup endpoints |
| F2 | _to be generated_ | Blocked | Q-01 (payment model) |
| F3 | _to be generated_ | Not started | Q-02, Q-03 |
| F4 | _to be generated_ | Not started | Q-02, Q-08 |
| F5 | _to be generated_ | Blocked | Q-01, Q-03 |
| FS | _to be generated_ | Not started | Q-04, Q-05, Q-06 |

## 5. Active Tasks

| Member | Active Cycle | Branch | Status | Last update |
|--------|--------------|--------|--------|-------------|
| Member 1 (Menna) | F1-C4 (F1-C1, F1-C2, F1-C3 Done) | `feature/menna-appointments` | Plan (Draft, awaiting approval) | 2026-11-12 |
| Member 2 | — | — | — | — |
| Member 3 | — | — | — | — |
| Member 4 | — | — | — | — |
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

### Pending approval (blocks work — do NOT assume)
| ID | Question | Affects | Needed from | Status |
|----|----------|---------|-------------|--------|
| Q-01 | **Payment model:** what a payment attaches to (appointment / visit / invoice); partial or multiple payments; methods; statuses; void/edit rules; whether the fee amount exists and where. Not in SRS/BRD/Class Spec/ERD | F2, F5 | Instructor / Tech Lead | Open |
| Q-02 | **Doctor–patient access relationship** (Patient is owned by one doctor in the Class Spec; can several doctors treat the same patient?) | F3, F4, F5 | Tech Lead | Open |
| Q-03 | **Meaning of "patient status"** (F3) and **"patients' statuses today"** (F5): appointment status, visit state, or a new field | F3, F5 | Tech Lead / Instructor | Open |
| Q-04 | **Shared list contract:** pagination DTO names, response envelope, default page size, max page size, default sort and tie-breaker, filter parameter style | all list endpoints | Member 6 → Tech Lead | Open |
| Q-05 | **Backend patterns in use:** Controllers vs Minimal APIs; MediatR/CQRS yes/no; repositories yes/no; Result type yes/no (AGENTS.md says do not assume). Note: `backEnd_skill.md` assumes MediatR + Result Pattern — decide whether that file still applies | FS, all backend | Tech Lead (inspect codebase) | Open |
| Q-06 | **Frontend libraries:** API client, server-state, local-state, schema validation, toast. Note: `frontend_skill.md` assumes Axios, TanStack Query, Zustand, Zod, sonner, which the README does not list — decide whether that file still applies | FS, all frontend | Tech Lead (inspect `package.json`) | Open |
| Q-07 | **Authentication and role provisioning** details (token model, roles, how Doctor/Secretary/Admin log in, seed users); no OTP/password-reset in scope | all | Tech Lead | Open |
| Q-08 | **Medical scope for F3/F4:** which entities are in (visit, diagnosis, medication/prescription, vital signs, notes) and whether allergies, chronic conditions, labs, documents, timeline are excluded | F3, F4 | Tech Lead / Instructor | Open |
| Q-09 | **Appointment details:** do cancelled (and no-show) appointments free their slot; are secretary-assigned doctors (SecretaryDoctor) enforced in this delivery | F1 | Tech Lead | **Answered 2026-11-12** (see Approved table) |
| Q-10 | **Database engine**, integration branch name, branch naming, migration ownership | FS | Tech Lead | Open |

## 8. Known Inconsistencies in Source Documents (for ElwakeelManage to resolve with approval)

- Class Spec: blood type `O` should be `O-`; `specialization` vs SRS `Specialty`.
- No System Administrator class in the Class Spec though SRS/BRD define the role.
- SRS FR-01 (doctor self-registration) vs SRS §5.2 (Admin creates doctor accounts).
- Final ERD is referenced by the Class Spec but not available in the repository docs provided.
- `backEnd_skill.md` (.NET 8, MediatR, Result Pattern) and `frontend_skill.md` (`src/` layout, Axios/TanStack/Zustand/Zod) conflict with the repository reality and with `AGENTS.md` §6.1 / §12. `AGENTS.md` wins until Q-05 / Q-06 are answered.
- `AGENTS.md` §8.1 requires `docs/PROJECT_OVERVIEW.md`, which does not exist yet.

## 9. Blockers

| Date | Blocker | Blocking | Owner to resolve |
|------|---------|----------|------------------|
| 2026-11-08 | Q-01 payment model not approved | F2, F5 | Tech Lead / Instructor |
| 2026-11-08 | Q-04 shared list contract not agreed | F1–F5 list endpoints | Member 6 |
| 2026-11-08 | `docs/PROJECT_OVERVIEW.md` missing | Session Start Kit | Tech Lead |

## 10. Execution Reports

Index of one report per AI-assisted Cycle (store under `docs/reports/<CycleId>.md`).

| Cycle | Contributor | Date | Result | Report |
|-------|-------------|------|--------|--------|
| F1-C1 | Menna | 2026-11-12 | Done — build 0 warnings/0 errors; EF model in sync; no tests (no test project by decision) | `docs/reports/F1-C1.md` |
| F1-C2 | Menna | 2026-11-12 | Done — build 0 warnings/0 errors; 94 tests pass; EF model in sync; real-MySQL lock verification still open | `docs/reports/F1-C2.md` |
| F1-C3 | Menna | 2026-11-12 | Done — build 0 warnings/0 errors; 94 + 43 tests pass; EF model in sync; no authentication (temporary dev-only header identity, gated by `Auth:AllowHeaderIdentity`); real-MySQL verification still open | `docs/reports/F1-C3.md` |

## 11. Next Step

1. Tech Lead confirms owners, integration branch, and answers the blocking questions (Q-01, Q-02, Q-04, Q-05, Q-06 first).
2. Create `docs/PROJECT_OVERVIEW.md` and place the SRS/BRD/Class Spec/ERD under `docs/`.
3. Run `ElwakeelManage` per feature → Phases/Cycles/Tasks, traceability matrix, task files.
4. Tech Lead approves the breakdown → fill §3–§5 with real Cycles → start with FS Cycles (P0).

## 12. Changelog

| Date | Change | By |
|------|--------|----|
| 2026-11-08 | Initial STATE.md aligned with AGENTS.md: features, owners, decisions log, open questions, no Cycles yet | AI draft — pending Tech Lead review |
