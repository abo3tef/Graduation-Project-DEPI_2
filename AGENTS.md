# AGENTS.md

**Revision:** Phase 2 — 2026-11-14

> **Medilog — Shared Instructions for AI Coding Agents**
>
> This file is the single, repository-wide source of agreed engineering rules for every human
> contributor and AI assistant working on Medilog. Read it before planning, editing, reviewing,
> or testing code.
>
> **Authority:** The current instructor-approved delivery scope and the decisions explicitly
> approved by the team take precedence over older task assignments, old implementation attempts,
> generic AI suggestions, and assumptions made by an agent.
>
> **Working rule:** Never invent a requirement, silently change an architecture decision, or
> implement work outside the approved task.

---

## 1. Project Identity

- **Project:** Medilog — Smart Medical Records Platform.
- **Purpose:** A healthcare-management platform that supports doctor and secretary workflows
  around appointments, patients, medical visits, prescriptions, and clinic activity.
- **Repository:** `abo3tef/Graduation-Project-DEPI`.
- **Team:** Six contributors working in one shared Git repository, each using a dedicated branch.
- **Backend:** .NET 10 / ASP.NET Core Web API.
- **Frontend:** Next.js 16, React 19, TypeScript, `next-intl`, TanStack Query, native `fetch` (no Axios), and Tailwind CSS 4.
- **Backend architecture:** Layered Clean Architecture.
- **Frontend architecture:** Feature-oriented architecture.
- **Languages:** Arabic (`ar`, RTL) and English (`en`, LTR).

Do not downgrade the project to .NET 8, React 18, or another framework version because a
reusable guideline or example mentions an older version. The repository project files and lock
files are the technical source of truth for installed versions and dependencies.

## 2. Current Delivery Scope

### 2.0 Delivery status

- **Phase 1 (F1–F5)** is merged and integrated. Its feature descriptions stay as the baseline behavior: F1 Appointment & Booking, F2 Payment Management UI, F3 Patient Medical Status, F4 Medical Updates & Prescriptions, F5 Doctor Clinic Dashboard. All Phase 1 business rules in this file remain valid (appointment overlap rule, secretary medical-data restriction, doctor data scope, filtering/pagination, FluentValidation, AutoMapper, entity configurations).
- **Phase 2 (F6–F11)** is the current, team-approved scope for the final MVP integration, due **2026-11-15 (night)**. It replaces the Phase 1 member assignments. Do not revive Phase 1 assignments and do not expand scope without explicit Tech Lead approval.

### 2.1 Phase 2 features, owners, branches

Each feature has **at most 2 Cycles**: **C1 = Backend + database + tests**, **C2 = Frontend + integration**.

| ID | Feature | Owner | Branch |
|----|---------|-------|--------|
| F6 | Full Authentication & Identity System | Shalaby | `feature/shalaby-auth` |
| F4-v2 | Diagnosis Delete | Shalaby | `feature/shalaby-auth` |
| F7 | Payments Backend & Integration | Habiba | `feature/habiba-payments` |
| F8 | Patient Management & Search | Menna | `feature/menna-patients` |
| F9 | Vital Signs & Visit Enhancements | Anton | `feature/anton-vitals` |
| F10 | Patient Medical Profile & Timeline Integration | Merihan | `feature/merihan-medical-profile` |
| F11 | Admin Dashboard & Full E2E Integration | Abdullah | `feature/abdullah-admin-integration` |

### F6 — Full Authentication & Identity System
Login (`POST /api/auth/login`); Register (`POST /api/auth/register`) with role `Doctor` or `Secretary`, linking the account to its `Doctor`/`Secretary` profile and adding `DoctorId` to the JWT claims; Email Verification through SMTP; Forgot/Reset Password (`POST /api/auth/forgot-password`, `POST /api/auth/reset-password`); Google Sign-In (`POST /api/auth/google`, verifying a Firebase ID token); ready seed users (`Admin`, `Doctor`, `Secretary`); frontend screens (`/login`, `/register`, `/verify-email`, `/forgot-password`, `/reset-password`, Google button); central token handling in the API client and removal of `DevTokenPanel`.

### F4-v2 — Diagnosis Delete
`DELETE /api/medical-visits/{visitId}/diagnoses/{diagnosisId}`, backend and frontend, allowed only for the owning doctor (hard delete, decision D-23).

### F7 — Payments Backend & Integration
Complete the payment endpoints (`POST`, `GET` with search/pagination, `PUT` to update payment status), connect them to the payment screens (F2) and the doctor dashboard (F5), with JWT-based authorization. Payment rules must follow the payment model documented in `STATE.md` (see F7-C1); do not invent rules.

### F8 — Patient Management & Search
Full patient CRUD (`POST`, `GET` with search/pagination, `PUT`, `DELETE`) in backend and frontend, and a reusable patient picker (search/dropdown) used by the appointment (F1) and visit (F4) screens instead of manual GUID entry.

### F9 — Vital Signs & Visit Enhancements
Add and edit `VitalSigns` (blood pressure, temperature, heart rate, weight, and the other fields approved in the Class Spec) linked to a medical visit, backend and frontend, integrated into the visit screen.

### F10 — Patient Medical Profile & Timeline Integration
Complete the patient medical-history screen and the `Timeline` (F3) using real visits, medications, and vital signs, with JWT-based authorization.

### F11 — Admin Dashboard & Full E2E Integration
Admin screen to manage/activate doctor and secretary accounts and edit roles, plus branch review/merge and final seed data for the demo.

### 2.2 Phase 2 rules

- Every Phase 2 task must trace to an approved item above or to a Phase 1 requirement. Anything else is out of scope.
- F6-C1 login + JWT claims + seed users (checkpoint A) is merged **first**; other features use it for role-protected testing.
- Cross-feature file edits (for example F8 changing the F1 appointment form, F7 changing F5 dashboard code) must be coordinated with the owner of that file and kept to the minimum diff.

## 3. Shared Technical Requirements

These requirements apply across relevant features; they are not additional business features.

### 3.1 Filtering and Pagination

- Implement filtering and pagination consistently for list endpoints and their corresponding
  frontend screens.
- Agree on shared request and response contracts before separate features implement them.
- Filtering must be performed against the data source where practical; do not load an
  unbounded dataset into memory and filter it in the API or browser.
- Validate page number, page size, and filter inputs.
- Use deterministic ordering before applying pagination. Add a stable tie-breaker when the
  primary sort field is not unique.
- Return enough pagination metadata for the UI to display the current page and total results.
- Do not allow an unbounded page size. The maximum page size must be a team-approved constant.
- Do not give each feature a different pagination response shape without an approved reason.

The exact shared DTO/property names and maximum page size must be agreed in the API contracts;
do not independently invent competing versions.

### 3.2 FluentValidation

- FluentValidation is the agreed validation library for the backend validation bonus.
- Put request/command validation in the Application layer, close to the use case it validates.
- Keep validation rules focused on input shape and request-level constraints.
- Business invariants must also be enforced in the domain/application business flow; do not rely
  only on frontend validation or a validator that can be bypassed.
- Register validators centrally using the existing dependency-injection conventions.
- Return consistent, client-readable validation errors through the API error-handling strategy.
- Do not duplicate the same rule in multiple unrelated places without a reason.

### 3.3 AutoMapper

- AutoMapper is the agreed mapping library for this project; do not introduce a second object
  mapping library.
- Keep mapping configuration organized and discoverable in the agreed Application-layer
  convention.
- Use explicit mappings for non-trivial transformations and sensitive fields.
- Never map client-controlled input directly onto a tracked domain entity in a way that lets
  the client overwrite IDs, ownership, roles, payment state, audit fields, or protected medical
  properties.
- Do not use mapping to bypass domain methods or business rules.

### 3.4 Entity Configurations

- Every persisted EF Core entity must have its own configuration class implementing
  `IEntityTypeConfiguration<TEntity>`.
- Keep EF Core mapping, keys, relationships, indexes, constraints, precision, and delete
  behavior in Infrastructure.
- Apply configurations centrally from the DbContext using assembly scanning or the
  repository's agreed registration convention.
- Do not put EF Core attributes or EF Core references in the Domain layer.
- Ensure the configured relationships and constraints agree with the approved domain model.

## 4. Backend Engineering Rules

### 4.1 Layer Responsibilities

Follow the existing backend solution under `backend/`:

```text
backend/
├── Medilog.sln
└── src/
    ├── Medilog.WebAPI/
    ├── Medilog.Application/
    ├── Medilog.Domain/
    └── Medilog.Infrastructure/
```

**Medilog.Domain**
- Contains core entities, enums, value objects where needed, and domain rules.
- Must not depend on ASP.NET Core, EF Core, Infrastructure, HTTP, or presentation concerns.
- Do not put persistence attributes or database-specific code here.
- Prefer controlled state changes through meaningful domain methods where the existing model
  supports that approach.
- Do not add abstractions or DDD patterns merely for ceremony.

**Medilog.Application**
- Contains use cases, DTOs, application services/handlers, abstractions, and validators.
- Orchestrates use cases and coordinates domain behavior with persistence abstractions.
- Must not depend directly on the concrete Infrastructure DbContext.
- Keep use-case logic out of controllers.
- Architecture decision (Q-05, approved): **MVC Controllers**, **plain Application services**,
  **no MediatR / CQRS**, **no additional repository layer** above `IApplicationDbContext`.
  Application services depend on `IApplicationDbContext` (implemented in Infrastructure); they
  must not reference the concrete DbContext. Do not introduce MediatR, CQRS, a generic
  repository, or a Result library without new team approval.

**Medilog.Infrastructure**
- Contains EF Core DbContext, entity configurations, migrations, persistence implementations,
  and external technical integrations.
- Implements Application abstractions where applicable.
- Keeps database-specific implementation details out of Domain and Application.
- Register Infrastructure services through the existing composition-root/extension conventions.

**Medilog.WebAPI**
- Is the HTTP entry point.
- Contains controllers or endpoints, middleware, API configuration, and dependency-injection
  composition.
- Endpoints must remain thin: parse/accept HTTP input, invoke an Application use case, and map
  the result to an HTTP response.
- Do not put business rules or database queries directly in controllers/endpoints.
- Infrastructure may be referenced by the composition root for dependency registration; do not
  make API business logic depend on Infrastructure implementations.

### 4.2 General C# Rules

- Follow the language version configured by the project files. Do not force C# 12 if the .NET 10
  project is configured for a newer language version.
- Enable and respect nullable reference types.
- Use clear, domain-oriented names and consistent naming conventions.
- Use `async`/`await` for asynchronous I/O and add the `Async` suffix to asynchronous methods.
- Do not use `async void`, `.Wait()`, or `.Result` in application code.
- Pass `CancellationToken` through asynchronous request and persistence operations where
  supported by the existing call chain.
- Avoid unnecessary abstractions, premature generalization, duplicated logic, and oversized
  classes/methods.
- Do not catch and suppress exceptions. Preserve useful diagnostic context and use the agreed
  global error-handling approach.
- Do not throw exceptions for expected validation/business outcomes if the approved application
  error/result convention provides a normal failure path. Do not introduce a new Result Pattern
  package or convention without team approval.
- Do not add NuGet packages without checking whether an existing dependency or framework feature
  already solves the need and obtaining agreement for shared architectural dependencies.

### 4.3 Persistence and Database

- EF Core belongs only in Infrastructure.
- Use Fluent API configuration classes for every persisted entity.
- Define primary keys, foreign keys, required/optional fields, indexes, precision, unique
  constraints, and delete behaviors explicitly where relevant.
- Avoid accidental cascade deletion of important medical or financial history.
- Do not store plain-text passwords, tokens, or secrets.
- Do not commit local database credentials or secrets.
- Migrations must be created from the agreed integration state and reviewed before merge.
- Do not independently delete, rewrite, or rename shared migrations or reset shared database
  history.
- Seed only safe, clearly identified development/demo data; never seed real patient data.
- **EF Core Code First** is the approved approach (Q-10).
- **Hard delete (D-23):** approved only for correcting an entry by the **owning doctor**,
  currently: deleting a diagnosis (F4-v2). Any other hard delete (including patient delete in F8)
  must follow the rule approved in `STATE.md` (Q-17) and must not cascade over medical or
  financial history. A new hard-delete behavior needs Tech Lead approval before implementation.

### 4.4 API Contracts and HTTP

- Agree on request DTOs, response DTOs, routes, status codes, and error shapes before parallel
  frontend/backend implementation.
- Keep API contracts explicit and stable. If a contract must change, coordinate with all
  affected contributors and update the shared contract documentation.
- Use appropriate HTTP status codes, including 200, 201, 204, 400, 401, 403, 404, and 409 where
  applicable.
- Do not expose EF entities directly as API responses.
- Do not return internal exception messages, stack traces, secrets, or unnecessary sensitive
  patient information.
- Document API changes in the agreed API contract/OpenAPI source.

## 5. Security, Roles, and Healthcare Data

Security is a cross-cutting requirement for the five features, not permission to reintroduce
the old standalone Authorization or Notifications tasks.

- Enforce access control on the backend for every protected use case. Frontend hiding or route
  guards alone are not security.
- Determine the acting user and role from the authenticated server-side identity; never trust a
  client-supplied user ID, doctor ID, secretary ID, role, or ownership field as proof of access.
- Scope doctor-facing records and dashboard queries to the authenticated doctor.
- A secretary may perform only the approved secretary workflows: appointment/booking management
  and payment management, plus only the minimum patient administrative information explicitly
  required by the approved workflow.
- A secretary must not read or modify medical records, diagnoses, prescriptions, visits,
  allergies, medications, lab results, vital signs, clinical notes, or medical documents.
- A doctor may access and update only the patients/records allowed by the approved doctor-patient
  relationship and project rules.
- Do not infer that a doctor can access every patient in the database.
- Apply ownership/relationship checks in the Application/business flow and data query, not only
  in UI components.
- Do not log passwords, access tokens, refresh tokens, payment secrets, or sensitive medical
  content.
- Use synthetic data for development and testing.

### Authentication (approved for Phase 2, feature F6)

In scope: login, registration with role selection (`Doctor` or `Secretary` only), email verification via SMTP, forgot/reset password, Google Sign-In through Firebase ID token verification, seed users, and a central token on the frontend API client.

Rules:
- **Registration:** the role comes from a closed allow-list (`Doctor`, `Secretary`). `Admin` can never be created through registration or Google Sign-In; Admin accounts are seeded or created by an Admin.
- **Passwords:** store only strong hashes; never log, return, or email passwords. Apply the approved password policy on register and reset.
- **One-time tokens** (email verification, password reset): generate with a cryptographically secure generator, store only a hash, enforce expiry and single use, and invalidate previous tokens when a new one is issued. Expiry values are configuration, approved in `STATE.md` (Q-15).
- **No account enumeration:** forgot-password and resend-verification return the same response whether or not the email exists. Login failures use a generic message.
- **Account state:** inactive or unverified accounts cannot obtain a token (per Q-11).
- **JWT:** short-lived access token; the claims contract must stay compatible with what Phase 1 authorization already reads and adds `DoctorId` for doctors (Q-14). The server derives role and ownership from the validated token only. No refresh-token flow unless approved (Q-13).
- **Google Sign-In:** validate the Firebase ID token on the server (signature, issuer, audience, expiry). The role is never taken from the client for Google users. Account creation/linking behavior follows Q-12.
- **Secrets:** SMTP credentials, JWT signing key, and Firebase configuration values are read from environment variables / user-secrets and are never committed. Only non-secret public Firebase web config may be in frontend env files.
- **Frontend:** a single central place attaches the token to API calls; route guards are UX only. `DevTokenPanel` and any dev-token code must be removed before delivery.
- **Not in scope:** OTP codes by SMS, multi-factor authentication, patient login, social providers other than Google.

## 6. Frontend Engineering Rules

### 6.1 Existing Structure

Follow the repository's existing frontend structure under `frontend/` (all source lives in
`frontend/src/`):

```text
frontend/
├── src/
│   ├── app/
│   ├── components/
│   ├── config/
│   ├── constants/
│   ├── features/
│   ├── hooks/
│   ├── i18n/
│   ├── lib/
│   ├── messages/
│   ├── schemas/
│   ├── services/
│   ├── store/
│   └── types/
└── package.json
```

Treat this `src/` layout as established: do not move source folders back to the `frontend/` root,
and do not introduce a second source directory. Put new code in the existing folder that matches
its responsibility.

### 6.2 Feature-Oriented Architecture

- Keep route-level code in `app/`; pages/layouts should compose feature components and remain
  focused on routing and presentation.
- Keep business functionality inside its feature module under `features/`.
- Keep reusable, business-agnostic UI in `components/`.
- Keep shared API/data-access utilities in the established data layer (`services/`, `lib/`).
- Keep shared TypeScript models in `types/` when they are genuinely shared.
- Keep locale configuration in `i18n/` and translations in `messages/`.
- Avoid circular dependencies and cross-feature implementation coupling.
- Expose only the intended public surface of a feature. Prefer a feature `index.ts` barrel for
  cross-feature imports when that convention is established; do not deep-import another
  feature's internals.
- Shared UI components must not import business features.

### 6.3 TypeScript and React

- Use strict TypeScript and preserve the project's compiler settings.
- Do not use `any`. Use `unknown`, narrowing, and type guards when the type is not known.
- Prefer functional React components and focused custom hooks.
- Avoid unnecessary client components; use Server Components by default where appropriate.
- Do not place business rules in page components.
- Keep component responsibilities small and props explicitly typed.
- Handle loading, empty, success, and error states for data-driven screens.
- Preserve accessible labels, keyboard interaction, semantic HTML, and appropriate ARIA
  attributes.

### 6.4 API and Server State

- The approved frontend stack (Q-06): **native `fetch`** wrapped in one shared API client in the existing data layer (no Axios) and **TanStack Query** for server state.
- Do not create competing API clients or duplicate base URL/token/error-handling logic.
- Zustand, Zod, and a toast library are **not** approved unless already installed and adopted in the repo; check `package.json` first, and get team approval before adding them.
- The Firebase JS SDK is approved **only** for F6 Google Sign-In, and only in the auth feature.
- Parse and validate untrusted API responses at a defined boundary when the project has adopted
  a schema-validation library.
- Handle API errors explicitly and present useful, non-sensitive messages to the user.
- Never treat frontend validation or route protection as a replacement for backend enforcement.

### 6.5 Localization and Styling

- Support both Arabic and English for new user-facing text.
- Add matching translation keys to `messages/ar.json` and `messages/en.json`.
- Preserve Arabic RTL and English LTR behavior.
- Follow the existing Tailwind CSS 4 setup and shared styling conventions.
- Avoid hard-coded user-facing strings in feature components.
- Keep forms accessible and provide clear validation feedback.

## 7. Testing and Quality Gates

- Tests must verify behavior and business rules, not merely implementation details.
- Follow the test framework and conventions already present in the repository. Do not introduce
  a second test framework without approval.
- Use Arrange–Act–Assert for readable tests.
- Prioritize tests for domain invariants, Application use cases, authorization/data scoping,
  payment calculations once defined, and dashboard aggregation.
- Add tests for both valid and invalid cases.
- Appointment tests must cover:
  - end time later than start time;
  - no overlap;
  - exact boundary adjacency (one appointment ends when the next begins);
  - partial overlap;
  - full containment;
  - same-time overlap;
  - different doctors and different dates.
- Test that a secretary cannot retrieve medical records by calling the API directly.
- Test that a doctor cannot retrieve another doctor's restricted data.
- Test filtering and pagination, including invalid page values, maximum page size, stable
  ordering, and total-count correctness.
- Test FluentValidation rules and ensure business invariants cannot be bypassed by skipping
  the frontend.
- Do not claim a test passed unless it was actually executed and its result was observed.
- Do not write a test that simply repeats the implementation's assumption. Derive expected
  behavior from an approved requirement or business rule.
- When fixing a bug, add or update a regression test. Where practical, demonstrate that the test
  fails before the fix and passes after it.

Before a backend task is considered complete, run the relevant checks, normally:

```bash
cd backend
dotnet restore
dotnet build Medilog.sln
dotnet test Medilog.sln
```

Before a frontend task is considered complete, run the relevant checks, normally:

```bash
cd frontend
npm run lint
npm run build
```

Run only commands supported by the repository's actual scripts and test projects. Report any
check that could not be run and why.

## 8. Shared AI Development Workflow

Every contributor must use the same repository instructions, regardless of whether the AI tool
is ChatGPT, Claude, Gemini, Copilot, or another assistant.

### 8.1 Required Reading

At the start of a task/session, the AI must read only the relevant project context files first,
rather than scanning or summarizing the entire repository.

The shared starter context is:

1. `AGENTS.md` — mandatory rules and approved decisions.
2. `docs/PROJECT_OVERVIEW.md` — concise product context and current scope (create/maintain as
   agreed by the team).
3. `STATE.md` — current approved phases, feature/cycle status, active task, and blockers (create
   only after the team approves the breakdown).

Then read only the specific task file and the code files needed for that task. If one of these
files does not exist yet, do not pretend it exists; identify the missing prerequisite and ask
the Tech Lead/team to create or approve it.

### 8.2 Four Workflow Commands

Use the instructor's four-command workflow:

- **`plan`** — inspect the relevant requirements and code, list applicable BR/FR/NFR constraints,
  propose one Cycle plan, affected files, dependencies, and acceptance criteria. Do not write
  implementation code.
- **`build`** — implement only the approved task/Cycle. Follow this file, avoid unrelated
  refactors, and present a focused diff.
- **`review`** — review the actual diff against requirements, architecture, security, tests, and
  acceptance criteria. Report findings with severity and file/line references. Do not silently
  make unrelated fixes.
- **`done`** — summarize delivered changes, files changed, commands/tests actually run, results,
  deviations, remaining risks, and the proposed STATE update. Do not claim completion if checks
  or acceptance criteria remain unmet.

A contributor must approve the plan before the AI starts `build`. The AI must not interpret a
single command as permission to skip the earlier approval gates.

### 8.3 Planning and Cycle Rules

- The requirements-management skill (`ElwakeelManage`) is used to analyze the approved SRS and
  arrange requirements into Phases, Features, Cycles, and Tasks.
- The build skill (`ElwakeelBuild`) is used to execute one approved Cycle at a time.
- These skills propose and guide work; they do not override this file or team/instructor decisions.
- Every task must trace back to an approved requirement or an explicitly approved technical
  requirement.
- Every Cycle must have a bounded scope, dependencies, acceptance criteria, affected layers,
  and a test plan.
- A Cycle should be small enough to understand, review, test, and commit independently.
- Do not ask an AI to "build the whole project" or an entire large feature in one operation.
- Do not begin implementation until the task file/plan is reviewed and approved.
- If a requirement is ambiguous or conflicts with another source, stop and raise the conflict;
  do not silently resolve it by guessing.

### 8.4 Diff-First and State Updates

- Prefer focused diffs over rewriting entire files.
- Before editing, inspect the current implementation and identify existing patterns to preserve.
- Do not overwrite another contributor's work or discard uncommitted changes.
- At the end of each completed session/Cycle, prepare an update to `STATE.md` with status,
  completed work, test results, blockers, decisions, and next step.
- Do not change approved scope, task ownership, or acceptance criteria through a STATE update.
- Keep an Execution Report for each AI-assisted Cycle, including files changed, tests run,
  outcomes, deviations from plan, and unresolved issues.
- A task is not `done` merely because code was generated. It is done only when acceptance
  criteria and relevant quality checks are satisfied and the contributor understands the code.

### 8.5 Human Responsibility

- The human contributor owns every change made on their branch.
- The contributor must read, understand, and be able to explain every committed line.
- If a contributor cannot explain a generated line, they must ask the AI to explain it, study
  it, or remove/rewrite it before committing.
- AI output is a proposal, not proof of correctness.
- Never paste credentials, real patient data, private keys, or secrets into an AI prompt.
- Do not send confidential data to an AI tool unless the project/team has explicitly approved
  that tool and data handling.

## 9. Git and Team Collaboration

- Work in the shared repository using a dedicated branch per contributor/task as agreed by the
  team.
- Do not commit directly to the protected integration branch.
- Branch from the team-approved current integration branch; do not assume `main` is the base
  if the team has selected another branch.
- Keep a branch focused on its approved Cycle/task.
- Before editing shared files, check whether another contributor is changing the same files and
  coordinate ownership.
- Pull/rebase/merge only according to the team's agreed Git procedure. Never force-push shared
  history or overwrite another contributor's work.
- Commit one completed, reviewed Cycle at a time where practical.
- Use clear commit messages describing the change and scope.
- Open a Pull Request with:
  - purpose and linked requirement/task;
  - summary of behavior changed;
  - important files/layers changed;
  - tests and commands run, with actual results;
  - migrations/configuration/API contract changes;
  - known limitations or deviations.
- Review conflicts deliberately. Do not resolve a conflict by blindly choosing "ours" or
  "theirs"; understand both changes and preserve the approved behavior.
- Do not commit generated or local-only folders such as `bin/`, `obj/`, `.next/`,
  `node_modules/`, IDE caches, local databases, or secrets.

## 10. Scope Control and Decision Management

The following remain **not** in scope: Notifications, real-time notifications, audit dashboards,
patient self-service, payment refunds, refresh-token flow (unless Q-13 approves it), multi-factor
authentication, and reimplementation of the Phase 1 member assignments.

**Now in scope (Phase 2):** OTP-style/link email verification, password recovery, and the full
authentication system (F6), as defined in §2 and §5.

Phase 2 freeze: after the Phase 2 cycles are approved, no new feature is added before the final
delivery without Tech Lead approval; unfinished work is cut at the Cycle boundary rather than
merged half-done.

When a task requires a decision not present in the approved sources:
1. State the missing decision precisely.
2. Explain which feature, entity, API, or test it affects.
3. Offer a small number of viable alternatives only if useful.
4. Ask the Tech Lead/team/instructor for a decision.
5. Record the approved decision in the project documentation before implementation.

Do not treat old documentation as automatically invalid. Preserve relevant existing business
rules unless they directly conflict with the instructor's new scope or are explicitly replaced.
If sources conflict, identify the conflict and request a decision.

## 11. Definition of Ready and Definition of Done

### Definition of Ready

A task is ready only when:
- it belongs to the approved Phase 2 scope (§2) or a shared technical requirement;
- its source requirement is identified;
- dependencies and affected layers are known;
- relevant business rules and security constraints are listed;
- API/data contracts are agreed when other features depend on them;
- acceptance criteria and test cases are written;
- the task plan is approved by the responsible human/Tech Lead.

### Definition of Done

A task/Cycle is done only when:
- all approved acceptance criteria are met;
- implementation follows this file and the current architecture;
- relevant validation, error, loading, empty, and success states are handled;
- tests are added/updated and relevant checks actually pass;
- no unrelated changes or unexplained generated code remain;
- API contracts, translations, and configuration are updated when affected;
- the contributor understands and can explain the changes;
- the Execution Report is complete;
- the proposed `STATE.md` update is reviewed;
- the change is committed and submitted through the agreed PR process.

---

## 12. Known Decisions vs. Decisions Still Requiring Approval

### Confirmed
- Backend: .NET 10 Web API, **MVC Controllers**, **plain Application services** (no MediatR/CQRS), **no extra repository** above `IApplicationDbContext`, FluentValidation, AutoMapper, **EF Core Code First**. (Q-05, Q-10)
- Frontend: Next.js 16, React 19, TypeScript, `next-intl` (ar RTL / en LTR), **TanStack Query**, **native fetch, no Axios**. (Q-06)
- Phase 1 (F1–F5) merged; Phase 2 (F6–F11) approved with the owners, branches, and 2-Cycle rule in §2.
- Full authentication system, email verification (SMTP), password recovery, Google Sign-In (Firebase ID token) are in the approved scope. (Q-07)
- **D-23:** hard delete for entry correction by the owning doctor (diagnosis delete).
- Filtering/pagination, FluentValidation, AutoMapper, entity configuration per entity, `plan → build → review → done` workflow.

### Still requires approval — see `STATE.md` §8 for status and owners
Q-11 account activation policy · Q-12 Google account creation/linking · Q-13 token lifetime, storage, refresh · Q-14 identity storage and JWT claims contract · Q-15 SMTP provider, token expiry values · Q-16 secretary-to-doctor assignment · Q-17 patient delete rule · Q-18 secretary-visible patient fields · Q-19 vital-sign fields and ranges · Q-20 timeline event types · Q-21 payment model (record the Phase 1 reality) · Q-22 admin role-change rules · DB engine name and integration branch name (record what the repo uses).

Phase 1 questions Q-01, Q-02, Q-03, Q-08, Q-09 were resolved by implementation; their written decisions must be recorded in `STATE.md` (§8) by the Tech Lead. Agents must not guess them: read the merged code and ask.

---

**Final instruction to every AI agent:** Read this file, inspect the smallest relevant part of
the codebase, identify the approved task, and follow the human-approved plan. If something is
unclear, stop and ask. Never guess, silently expand scope, or commit code the contributor cannot
explain.
