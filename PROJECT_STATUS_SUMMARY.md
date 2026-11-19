# Medilog (شفاء) — Project Status Summary

> **Audit date:** 2026-11-18  
> **Audit basis:** repository working tree on `main`, `STATE.md` (last updated 2026-11-18), implementation/configuration inspection, execution reports, and the quality checks recorded below.  
> **Important status convention:** “implemented” means code is present in this workspace; it does **not** mean the feature is approved, merged, deployed, or verified against live MySQL/external services.

## 1. Executive Summary

Medilog (شفاء) is a Smart Medical Records Platform for clinic workflows. It supports doctors and secretaries managing appointments, patients, medical visits and records, prescriptions, vital signs, payments, and clinic activity. The solution is a .NET 10 ASP.NET Core Web API with a Clean Architecture backend and a Next.js 16 / React 19 frontend.

The repository contains substantial Phase 1 and Phase 2 implementation. The current delivery board, however, still records most Phase 2 work as **In review**, **In progress**, or **Approved** rather than Done. The main functional risks are external-service/database verification, outstanding integration/manual checks, and completion of the remaining approved frontend/E2E cycles.

### Delivery board

| Feature | Current documented status | Audit assessment |
|---|---|---|
| F1 — Appointment & Booking | Merged (Phase 1 baseline) | Implemented backend/frontend; includes schedule/status operations and overlap validation. |
| F2 — Payment Management UI | Merged (Phase 1 baseline) | UI exists; backend completion/integration is owned by F7. |
| F3 — Patient Medical Status | Merged (Phase 1 baseline) | Medical profile foundations exist; timeline completion is in F10. |
| F4 — Medical Updates & Prescriptions | Merged (Phase 1 baseline) | Medical-visit, diagnosis, medication, and related record workflows exist. |
| F5 — Doctor Clinic Dashboard | Merged (Phase 1 baseline) | Dashboard endpoint/UI exists; payment revenue aggregation remains deferred/out of scope for F7-C1. |
| F6 — Authentication & Identity | C1 In review; C2 In review | Auth API and frontend screens are present. Live SMTP/Firebase configuration and final integration verification remain required. |
| F4-v2 — Diagnosis delete | C1 In review; C2 In review | Doctor-only diagnosis hard-delete endpoint and frontend delete components are present. |
| F7 — Payments | C1 In review; C2 Approved | Payment API, local payment model, and frontend payment feature code are present; C2/E2E status is not Done. |
| F8 — Patients & picker | C1 In progress; C2 Approved | Patient API and patient-picker/front-end files are present, but the board has not accepted C1/C2. |
| F9 — Vital signs | C1 In review; C2 Approved | Vital-sign endpoints/UI code exists; live JWT/MySQL verification and C2 acceptance remain pending. |
| F10 — Profile & timeline | C1 In review; C2 In review | Timeline/profile backend and frontend exist; VitalSigns timeline events are explicitly deferred and manual checks remain. |
| F11 — Admin & E2E | C1 In review; C2 Approved | Admin-only backend endpoints are present and tested; admin frontend/full E2E remains not Done. |

## 2. Backend Architecture and Implemented Modules

### Clean Architecture layout

```text
backend/
├── src/Medilog.Domain/          # entities, enums, common domain types
├── src/Medilog.Application/     # services, DTOs, validators, mappings, abstractions
├── src/Medilog.Infrastructure/  # EF Core context/configuration, security, email, persistence
├── src/Medilog.WebAPI/          # MVC controllers, pipeline, DI/composition root
└── tests/
    ├── Medilog.Application.Tests/
    └── Medilog.WebAPI.Tests/
```

- The Web API uses thin MVC controllers, global `ProblemDetails` exception handling, OpenAPI in Development, CORS, JWT authentication, and authorization middleware.
- Application services use `IApplicationDbContext`, `ICurrentUser`, and focused abstractions; no MediatR/CQRS or extra generic repository layer was found.
- FluentValidation is registered centrally for request validation, and AutoMapper profiles are used for standard mappings (the timeline’s multi-source projection is explicitly hand-mapped).
- Infrastructure implements persistence/security concerns and the EF Core `MedilogDbContext`; entity configurations are applied by assembly scanning.

### Authentication and security (F6)

The following routes are implemented under `/api/auth`:

```text
POST /login
POST /register
POST /verify-email
POST /resend-verification
POST /forgot-password
POST /reset-password
POST /google
```

- Registration accepts the approved Doctor or Secretary roles; Admin accounts are seeded/admin-managed rather than self-registered.
- Account checks prevent login for inactive or unverified accounts. Passwords are PBKDF2-HMAC-SHA256 hashes; reset/verification tokens are random, SHA-256-hashed at rest, expiring, single-use, and supersede earlier unused tokens.
- JWT validation is configured for HS256. The implemented claims contract is `sub`, `role`, and `doctorId` for doctor accounts, plus standard issuer/audience/lifetime claims. The acting identity is derived from the validated bearer principal.
- Google sign-in is behind `IGoogleTokenValidator`; application code validates the token server-side, requires Google-verified email, finds an existing account by normalized email, and does not auto-create accounts or trust a client role.
- Development-only seed support exists behind `Seed:Enabled`; seeded accounts are documented separately and should never be enabled in production.
- Production remains dependent on configuration of a signing key, SMTP, and Firebase Project ID. No secrets are populated in `appsettings.json`.

### Database and persistence

- Target runtime: `net10.0`; ORM: EF Core `10.0.14`.
- Database: MySQL using **`MySql.EntityFrameworkCore` 10.0.12** and `UseMySQL()`. This is a material correction to the requested “Pomelo/MySQL” wording: Pomelo is not the active provider in the inspected project file, and `STATE.md` records removal of an uncommitted Pomelo reference.
- Migrations present:

```text
20261109211333_InitialCreate
20261114194949_AddAuthentication
20261115000221_AddPayments
```

- `MedilogDbContext` exposes and configures Doctors, Secretaries, Admins, AuthTokens, Patients, MedicalVisits, Diagnoses, Medications, Allergies, ChronicConditions, VitalSigns, LaboratoryResults, MedicalNotes, MedicalDocuments, Appointments, and Payments. The secretary-doctor association is also configured.
- Configurations use individual `IEntityTypeConfiguration<TEntity>` classes and MySQL `utf8mb4` / `utf8mb4_0900_ai_ci` settings. Financial and medical relationships are designed with restrictive delete behavior; approved hard delete is limited to an owning doctor correcting a diagnosis. Patient deletion is restricted to patients with no related records.
- Historical reports consistently flag **no reachable real MySQL instance**. EF migration/model synchronization was reported clean for F11-C1, but production SQL behavior, indexes, and migrations still require execution against a real MySQL environment.

### Completed API surface observed

| Area | Controller routes observed |
|---|---|
| Auth | The seven F6 routes listed above. |
| Patients | `POST /api/patients`, paged `GET /api/patients`, `GET /lookup`, `GET /{id}`, `PUT /{id}`, `DELETE /{id}`. Secretary field/scope restrictions are documented in `STATE.md`. |
| Appointments | Create, get/list, doctor/patient lookups, reschedule, cancel, and status update under `/api/appointments`; business service includes overlap rules. |
| Medical visits | Create/get/update visit; create/update/delete diagnosis; create/update/delete medication under `/api/medical-visits`. Diagnosis deletion is the F4-v2 owning-doctor hard-delete workflow. |
| Vital signs | `POST`, paged `GET`, and `PUT` at `/api/medical-visits/{visitId}/vital-signs`; Q-19 requires at least one of seven measurements and documents approved ranges. |
| Medical profile/timeline | `GET /api/patients/{patientId}/medical-profile`, `/medical-visits`, and `/timeline`; the timeline has validated filters, deterministic ordering, and pagination. |
| Payments | Create, get one/list, payable-appointment lookup, full update, and status update under `/api/payments`. One payment per appointment, a constrained status transition model, and role-based scope are documented. |
| Dashboard | `GET /api/dashboard/doctor`. Revenue remains `null` because F7-C1 dashboard aggregation was explicitly deferred. |
| Administration (F11-C1) | Paged user list, account status patch, Doctor↔Secretary role change, and secretary-to-doctor assignment read/replace under `/api/admin`; these routes are Admin-only. |

## 3. Frontend Architecture and Feature Status

The frontend is a Next.js `16.4.5` App Router application with React `19.3.0`, TypeScript, Tailwind CSS 4, and `next-intl` 4. The `[locale]` route layout provides English LTR and Arabic RTL support. Server-state infrastructure uses TanStack React Query; `zustand` is installed and used for UI state. The shared native-fetch API layer is under `src/lib/api`.

```text
frontend/src/
├── app/[locale]/                # route composition
├── components/                  # shared presentation
├── features/                    # feature-oriented modules
├── i18n/ and messages/          # locale routing and translations
├── lib/api/                     # fetch client, auth headers, errors, paging
├── lib/query-client/            # React Query provider/client
└── store/                       # Zustand UI store
```

| Module | Observed implementation status |
|---|---|
| `auth` | Login, register, verification, resend, forgot/reset password, Google button, session store, role guard, and auth API/hook/test files are present. |
| `patients` | Patient pages, CRUD API/hooks, forms, list components, and reusable `PatientSelect` picker are present. |
| `doctors` | Feature module exists; appointment/dashboard role-aware composition exists. |
| `appointments` | List/new pages, API, lookup hooks, filters, messages, tests, and session helpers are present. |
| `medical-records` | Visit workspaces, diagnosis/medication CRUD UI, vital-sign form/section, API/hooks, access guards, and patient-picker integration tests are present. |
| `medical-timeline` | Medical profile/timeline API, filters, pagination, query hooks, profile panels, event list, guards, and tests are present. |
| `dashboard` | Doctor dashboard screen/query hook and shared dashboard components are present; some older mock/dashboard-data files remain alongside the API-connected code. |
| `payments` | Payment API adapters, query hooks, filters, status/form dialogs, enum conversion, validation, and preview components are present. |
| Admin UI | No dedicated `features/admin` module or admin route was identified in the inspected frontend tree; this aligns with F11-C2 remaining Approved rather than Done. |

### Frontend audit notes

- Localization infrastructure is present, but all new UI strings still need final English/Arabic and RTL manual verification as identified in F10-C2.
- The current fetch layer includes `auth-headers.ts`. Source comments/tests and `frontend/README.md` still mention legacy `X-User-Id` / `X-User-Role` development headers, while backend decision D-42 removes production header-identity fallback and uses JWT only. This is a documentation/cleanup risk that should be reconciled before delivery; test-only header authentication is expected in the backend test host.

## 4. Testing and Quality Gates

### Current audit execution (2026-11-18)

| Command | Result |
|---|---|
| `backend: dotnet build Medilog.sln --no-restore -v:q` | **Passed** — 0 warnings, 0 errors. |
| `backend: dotnet test Medilog.sln --no-restore -v:q` | **Passed** — 563/563 Application tests and 225/225 WebAPI tests; **788/788 total**, 0 failed, 0 skipped. |
| `frontend: npm run lint` | **Passed** — ESLint exited 0. |
| `frontend: npm run typecheck` | **Passed** — `tsc --noEmit` exited 0. |
| `frontend: npm run build` | **Not completed** — Next exited 1 because another Next build process/lock was already running. This is an execution-environment contention result, not evidence of a source build failure. |
| `frontend: npm test` | **Not completed** — Vitest started but produced no test result for more than 60 seconds after a prior overlapping invocation; it was interrupted to avoid leaving a hung process. |

The current backend suite is newer than the 770-test result recorded in the F11-C1 execution report, so this audit records the directly observed **788** passing backend tests. Historical reports also record successful frontend typecheck/lint/build and 316 tests for F10-C2, but those are not a substitute for a clean, sequential final frontend build/test run on this workspace.

## 5. Pending Items and Recommended Next Steps

1. **Close acceptance/review gates.** F6, F4-v2, F7, F9, F10, and F11 backend work should be reviewed/accepted according to `STATE.md`; F8-C1 remains In progress. Do not label the MVP complete solely because implementation files are present.
2. **Finish C2/E2E work.** F11-C2 (admin frontend and full integration) remains approved; F7-C2, F8-C2, and F9-C2 need acceptance/integration. F10-C2 still calls for manual Doctor/Secretary/cross-doctor, live-data, and Arabic RTL checks.
3. **Resolve the timeline gap.** F10’s VitalSigns timeline event source was deliberately deferred pending F9-C1 finalization; implement/verify it only after the agreed F9 contract is accepted.
4. **Provision secure configuration.** Configure MySQL connection details, `Jwt:SigningKey`, SMTP (`Email:*`), Firebase `ProjectId`/server verification setup, frontend public Firebase configuration if Google UI is enabled, CORS origins, and seed settings through environment variables or user secrets—not committed JSON. Stripe is not implemented or required by the approved local payment model; do not add it without a scope decision.
5. **Run real-environment verification.** Apply migrations to a non-production MySQL database; test auth with real JWTs, SMTP delivery, Firebase ID token verification, doctor/secretary/admin scoping, and the generated SQL/index behavior. Use synthetic demo data only.
6. **Run clean final frontend gates sequentially.** Ensure no `next dev`/`next build` process owns `.next`, then run `npm run lint`, `npm run typecheck`, `npm run build`, and `npm test` one at a time. Investigate any persistent Vitest hang rather than deleting lock/state files blindly.
7. **Prepare delivery artifacts.** Update missing `docs/PROJECT_OVERVIEW.md` (currently named mandatory but absent), reconcile stale legacy-header documentation, finalize API contracts/translation keys, review migrations, seed demo accounts/assignments, produce deployment configuration, and complete PR/Execution Report/`STATE.md` approvals.

## 6. Audit Limitations

- `docs/PROJECT_OVERVIEW.md`, required by the repository workflow, is absent.
- This audit did not connect to a real MySQL server, SMTP server, Firebase project, or deployed frontend/API.
- The report does not assert PR merge state or deployment state beyond the current working tree and `STATE.md`.
- The frontend production build and test suite were not given a clean final result because of active-process contention during this audit; this must be rerun in a clean local environment.
