# AGENTS.md

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
- **Frontend:** Next.js 16, React 19, TypeScript, `next-intl`, and Tailwind CSS 4.
- **Backend architecture:** Layered Clean Architecture.
- **Frontend architecture:** Feature-oriented architecture.
- **Languages:** Arabic (`ar`, RTL) and English (`en`, LTR).

Do not downgrade the project to .NET 8, React 18, or another framework version because a
reusable guideline or example mentions an older version. The repository project files and lock
files are the technical source of truth for installed versions and dependencies.

## 2. Current Delivery Scope

The current instructor-requested delivery scope consists of the following five features.
These replace the previous six-member task assignments for the current delivery. Do not revive
old assignments or expand the scope without explicit team/instructor approval.

### Feature 1 — Secretary Appointment & Booking Management

The secretary must be able to manage appointment bookings, including the agreed create, view,
update/reschedule, and cancel workflows.

Relevant documented business constraints:
- An appointment is associated with a patient and a doctor.
- The appointment end time must be later than its start time.
- Appointments for the same doctor on the same date must not overlap.
- For intervals, an overlap exists when `newStart < existingEnd` and
  `newEnd > existingStart`.
- Appointment status changes must use only the statuses agreed in the project requirements
  and the implemented domain model.
- Preserve the appointment creator information when the domain model requires it.

### Feature 2 — Secretary Payment Management

The secretary must be able to manage patient payments within the agreed payment workflow.

Do not invent payment rules. In particular, do not assume:
- whether a payment belongs to an appointment, a medical visit, or an invoice;
- whether partial or multiple payments are supported;
- which payment methods or statuses are available;
- whether a payment can be edited, voided, refunded, or deleted;
- how revenue is attributed when more than one doctor is involved.

These are domain decisions. Use only the approved requirements and API/database contracts.
If a required decision is missing, record the question and ask the team/instructor before
implementing the behavior.

### Feature 3 — Doctor Views Patient Status

The doctor must be able to view the patient information and medical status needed for the
doctor's workflow, according to the approved medical-record model.

Medical information is confidential. A secretary must not gain access to diagnoses,
prescriptions, medical visits, allergies, medications, laboratory results, vital signs,
clinical notes, medical documents, or other medical-record data merely because the secretary
can manage appointments or payments.

### Feature 4 — Doctor Adds and Updates Medical Information

The doctor must be able to add and update the agreed medical information, including items such
as diagnoses, prescriptions, and visit/condition details.

Keep medical changes attributable to the correct patient and doctor. Follow the approved
entity relationships and business rules. Do not create a new medical workflow or extra medical
entity unless it is supported by the approved specifications or explicitly approved.

### Feature 5 — Doctor Clinic Dashboard

The doctor must be able to monitor the clinic information requested by the instructor:
- visit counts;
- payments/revenue received by that doctor, according to the approved payment attribution
  rule;
- patients and their appointment/visit statuses for the current day.

Dashboard values must come from persisted application data and must respect the authenticated
doctor's data scope. Do not use hard-coded demo numbers as production behavior.

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

Follow the existing backend solution under `Back_End/`:

```text
Back_End/
├── Medilog.slnx
└── src/
    ├── Medilog.Api/
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
- Follow the existing solution's established service/handler pattern. Do not introduce MediatR,
  CQRS, a repository abstraction, or a Result library unless the team explicitly approves it.

**Medilog.Infrastructure**
- Contains EF Core DbContext, entity configurations, migrations, persistence implementations,
  and external technical integrations.
- Implements Application abstractions where applicable.
- Keeps database-specific implementation details out of Domain and Application.
- Register Infrastructure services through the existing composition-root/extension conventions.

**Medilog.Api**
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

Authentication implementation details (identity provider, token model, refresh-token storage,
OTP, password hashing library, and role provisioning) must follow the existing approved project
decision. Do not add OTP, refresh-token flows, email verification, password reset, or a new
authentication provider unless explicitly included in the approved current scope.

## 6. Frontend Engineering Rules

### 6.1 Existing Structure

Follow the repository's existing frontend structure under `Front_End/`:

```text
Front_End/
├── app/
├── components/
├── constants/
├── data/
├── features/
├── i18n/
├── messages/
├── types/
└── package.json
```

Do not introduce a new `src/` directory or move existing folders just because a generic
Next.js template uses `src/`.

### 6.2 Feature-Oriented Architecture

- Keep route-level code in `app/`; pages/layouts should compose feature components and remain
  focused on routing and presentation.
- Keep business functionality inside its feature module under `features/`.
- Keep reusable, business-agnostic UI in `components/`.
- Keep shared API/data-access utilities in `data/` or the existing established data layer.
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

- Reuse the existing API/data-access implementation and dependencies after inspecting
  `package.json` and the current code.
- Do not create competing API clients or duplicate base URL/token/error-handling logic.
- Do not add Axios, TanStack Query, Zustand, Zod, or a toast package solely because a generic
  guideline recommends it. First verify whether it is already installed and adopted; any new
  shared dependency requires team approval.
- Keep server state separate from local UI state.
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
cd Back_End
dotnet restore
dotnet build Medilog.slnx
dotnet test Medilog.slnx
```

Before a frontend task is considered complete, run the relevant checks, normally:

```bash
cd Front_End
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

The following are explicitly **not current standalone delivery features**:
- Notifications.
- A separate, expanded Authorization project/task.
- Reimplementation of the old Member 1–6 task split.

Necessary access checks and role/data-scope enforcement remain mandatory as cross-cutting
security requirements for the current features.

Do not add features such as OTP, email verification, password recovery, audit dashboards,
real-time notifications, patient self-service, or payment refunds unless the instructor/team
explicitly adds them to the approved scope.

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
- it belongs to the currently approved five-feature scope or a shared technical requirement;
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

This section prevents AI agents from presenting assumptions as approved architecture.

### Confirmed from the current project/repository context

- Backend target is .NET 10 / ASP.NET Core Web API.
- Frontend uses Next.js 16, React 19, TypeScript, `next-intl`, and Tailwind CSS 4.
- Backend projects are Domain, Application, Infrastructure, and API.
- Frontend folders are `app`, `components`, `constants`, `data`, `features`, `i18n`,
  `messages`, and `types`.
- Current delivery has five features listed in Section 2.
- Filtering and pagination are shared technical requirements.
- FluentValidation is requested for the bonus.
- AutoMapper is the agreed mapping tool.
- Every persisted entity needs its own EF Core configuration class.
- One shared AI workflow and the instructor's `plan → build → review → done` process are required.
- Six contributors work in one repository using branches.

### Do not assume until the team approves or confirms from the codebase

- Whether the backend uses Controllers or Minimal APIs for new endpoints.
- Whether MediatR/CQRS is installed or will be adopted.
- Whether repositories are used, and where their interfaces belong.
- Whether the application uses a Result type/pattern.
- The exact authentication implementation and token lifecycle.
- The exact frontend API client, server-state library, local-state library, schema validator,
  and toast library.
- Exact payment lifecycle, payment methods, partial-payment/refund rules, and payment-to-visit
  relationship.
- Exact doctor-to-patient access relationship and dashboard revenue attribution.
- Exact API response envelope, pagination DTO names, page-size limit, and sorting defaults.
- Exact test framework/packages if not already present.
- Exact branch names and integration branch.

Before selecting any item in this list, inspect the repository and/or ask for team approval.
Once approved, update this file and keep all AI tools aligned with the same decision.

---

**Final instruction to every AI agent:** Read this file, inspect the smallest relevant part of
the codebase, identify the approved task, and follow the human-approved plan. If something is
unclear, stop and ask. Never guess, silently expand scope, or commit code the contributor cannot
explain.
