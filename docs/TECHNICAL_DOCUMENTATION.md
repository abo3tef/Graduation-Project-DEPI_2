# Medilog — Technical Documentation

> **Smart Medical Records Platform — complete engineering reference**
>
> This document describes the system that is actually implemented in this repository: the
> architecture, the domain model, the persistence design, the use cases, the HTTP contract,
> the frontend structure, the shared conventions, and the reasoning behind every non-obvious
> decision. It is written for a senior-engineer / professor review and for onboarding a new
> contributor.
>
> **Status date:** 2026-11-13
> **Branch state:** This document describes the current working tree as of the status date.
> The implementation includes the delivered F1 appointment and F4 medical-record slices,
> shared paging/error/transport work, and intentionally incomplete F2/F3/F5 surfaces. Commit
> and merge state remains a team workflow concern; see §8 for implementation gaps and risks.
> **Scope authority:** `AGENTS.md` §2 (five instructor-approved features) and `STATE.md`
> (decisions, blockers). This document does **not** change scope, rules, or architecture; it
> only describes what exists.

---

## Table of contents

- [0. How to read this document](#0-how-to-read-this-document)
- [1. System overview](#1-system-overview)
  - [1.1 What Medilog is](#11-what-medilog-is)
  - [1.2 Delivered vs. planned scope](#12-delivered-vs-planned-scope)
  - [1.3 Technology stack](#13-technology-stack)
  - [1.4 Repository layout (actual)](#14-repository-layout-actual)
  - [1.5 Documentation map](#15-documentation-map)
- [2. High-level architecture](#2-high-level-architecture)
  - [2.1 Backend: layered Clean Architecture](#21-backend-layered-clean-architecture)
  - [2.2 Why these layers exist](#22-why-these-layers-exist)
  - [2.3 End-to-end request lifecycle](#23-end-to-end-request-lifecycle)
  - [2.4 Frontend: feature-oriented Next.js App Router](#24-frontend-feature-oriented-nextjs-app-router)
  - [2.5 Cross-cutting concerns map](#25-cross-cutting-concerns-map)
- [3. Backend deep dive](#3-backend-deep-dive)
  - [3.1 Solution and project inventory](#31-solution-and-project-inventory)
  - [3.2 Domain model](#32-domain-model)
  - [3.3 Business rules](#33-business-rules)
  - [3.4 Persistence: EF Core + MySQL](#34-persistence-ef-core--mysql)
  - [3.5 Entity configurations, keys, and delete behavior](#35-entity-configurations-keys-and-delete-behavior)
  - [3.6 Application layer](#36-application-layer)
  - [3.7 Validation strategy (FluentValidation)](#37-validation-strategy-fluentvalidation)
  - [3.8 Mapping strategy (AutoMapper)](#38-mapping-strategy-automapper)
  - [3.9 WebAPI layer and the HTTP pipeline](#39-webapi-layer-and-the-http-pipeline)
  - [3.10 Error envelope](#310-error-envelope)
  - [3.11 Identity and authorization](#311-identity-and-authorization)
  - [3.12 Testing](#312-testing)
- [4. Frontend deep dive](#4-frontend-deep-dive)
  - [4.1 Runtime, configuration, and build](#41-runtime-configuration-and-build)
  - [4.2 Internationalization and RTL](#42-internationalization-and-rtl)
  - [4.3 Routing and route tree](#43-routing-and-route-tree)
  - [4.4 The API client layer](#44-the-api-client-layer)
  - [4.5 Server state and local state](#45-server-state-and-local-state)
  - [4.6 Feature modules in detail](#46-feature-modules-in-detail)
  - [4.7 Forms, validation, accessibility, and styling](#47-forms-validation-accessibility-and-styling)
  - [4.8 Frontend testing](#48-frontend-testing)
- [5. Shared conventions](#5-shared-conventions)
  - [5.1 Pagination and filtering](#51-pagination-and-filtering)
  - [5.2 Error contract](#52-error-contract)
  - [5.3 Identity contract](#53-identity-contract)
  - [5.4 Coding and naming conventions](#54-coding-and-naming-conventions)
- [6. Decision register](#6-decision-register)
- [7. Senior-engineer Q&A](#7-senior-engineer-qa)
- [8. Known gaps, risks, and open questions](#8-known-gaps-risks-and-open-questions)
- [9. Onboarding and local runbook](#9-onboarding-and-local-runbook)
- [10. Appendices](#10-appendices)

---

## 0. How to read this document

The document has three audiences:

| Audience | Read |
|---|---|
| **Reviewer / professor** | §1, §2, §7 (Q&A), §8 (gaps) |
| **Backend contributor** | §1, §2.1–2.3, §3, §5 |
| **Frontend contributor** | §1, §2.4, §4, §5 |

Conventions used below:

- **“Implemented”** means code exists and is exercised by tests or the running app.
- **“Mock-only”** means the UI exists but there is no backend; the data is in-memory.
- **“Not started”** means only an empty feature folder exists.
- File paths are relative to the repository root unless stated otherwise.
- Code excerpts are illustrative and may omit `using` directives and comments.
- Facts that conflict with `AGENTS.md` are called out explicitly rather than silently
  normalized (§1.4, §8). The repository is the technical source of truth for versions and
  folder names; `AGENTS.md` is the source of truth for rules and scope.

---

## 1. System overview

### 1.1 What Medilog is

Medilog is a healthcare-management platform for a small clinic. Two authenticated roles use it:

- a **doctor**, who owns a workspace of patients and their medical records;
- a **secretary**, who manages appointments and payments for the doctors they are assigned to,
  but must never see medical data.

The product is not a hospital information system. The instructor-approved delivery is a
focused slice: appointments, payments, doctor-facing patient status, doctor-entered medical
information, and a doctor clinic dashboard.

The system is split into:

- a **.NET 10 / ASP.NET Core Web API** using layered Clean Architecture;
- a **Next.js 16 / React 19 / TypeScript** frontend using a feature-oriented architecture and
  bilingual (Arabic RTL / English LTR) routing through `next-intl`;
- a **MySQL** database accessed through EF Core.

### 1.2 Delivered vs. planned scope

| Feature | Instructor scope | Backend | Frontend | Notes |
|---|---|---|---|---|
| **F1 — Secretary appointment & booking management** | create, view, reschedule, cancel, status changes, filtered paged list, lookups | **Implemented** (`AppointmentsController`, `AppointmentService`, `AppointmentStore`) | **Implemented** (`features/appointments`) | Full end-to-end path, including overlap rules and role rules. |
| **F2 — Secretary payment management** | payment workflow | **Not started** | **Mock-only** (`features/payments`) | Blocked by **Q-01** (payment model). The UI uses in-memory data and the shared paging envelope so the contract can be swapped later. |
| **F3 — Doctor views patient status** | patient information + medical status | Partially enabled by F4 data model | **Placeholder** (`features/patients`, `features/medical-timeline`) | “Patient status” semantics blocked by **Q-03**; medical entity scope blocked by **Q-08**. |
| **F4 — Doctor adds/updates medical information** | visits, diagnoses, medications | **Implemented** (`MedicalVisitsController`, `MedicalVisitService`) | **Implemented** (`features/medical-records`) | Full CRUD for visits, diagnoses, medication lines; one-to-one appointment link. |
| **F5 — Doctor clinic dashboard** | visit counts, revenue, today’s patients/statuses | **Not started** | **Placeholder** (`features/dashboard`) | Depends on Q-01 (revenue attribution) and Q-03 (status semantics). |
| **Shared technical requirements (FS)** | paging/filtering, FluentValidation, AutoMapper, entity configurations, error envelope | **Implemented** | **Implemented** (paging/error consumption) | Phase 0 and Phase 1 work; see §5. |

**What a reviewer should conclude:** F1 and F4 are real vertical slices. F2 is a deliberate
UI-first mock awaiting a domain decision. F3/F5 are not implemented. No code pretends
otherwise, and no hard-coded dashboard numbers are used as production behavior.

### 1.3 Technology stack

#### Backend

| Concern | Choice | Version (from project files) |
|---|---|---|
| Runtime | .NET | `net10.0` |
| Web framework | ASP.NET Core Web API (controllers) | 10.x |
| Language | C# | version implied by `net10.0` (not pinned down) |
| ORM | EF Core | `10.0.14` |
| Database provider | `MySql.EntityFrameworkCore` (Oracle) | `10.0.12` |
| Database | MySQL | 8.x target (utf8mb4 / `utf8mb4_0900_ai_ci`) |
| Validation | FluentValidation | `12.2.0` |
| Mapping | AutoMapper | `16.3.0` |
| Authentication | JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`) | `10.0.14` |
| OpenAPI | `Microsoft.AspNetCore.OpenApi` | `10.0.11` |
| Test framework | xUnit + `Microsoft.AspNetCore.Mvc.Testing` | `2.9.3` / `10.0.14` |

> **Licensing note (open item):** the Oracle MySQL EF Core provider is distributed under
> `GPL-2.0-only WITH Universal-FOSS-exception-1.0`. The team/supervisor must approve it
> before the project is distributed. This is recorded in `STATE.md` as an open decision.

#### Frontend

| Concern | Choice | Version (from `package.json`) |
|---|---|---|
| Framework | Next.js (App Router) | `16.4.5` |
| UI library | React | `19.3.0` |
| Language | TypeScript | 5.x (`tsconfig.json`) |
| Styling | Tailwind CSS 4 via `@tailwindcss/turbopack` | `^4` |
| i18n | `next-intl` | `^4.14.9` |
| Server state | TanStack Query | `^5.107.1` |
| Global UI state | Zustand | `^5.1.0` |
| Toasts | `react-toastify` | `^11.2.0` |
| Icons | `react-icons` | `^5.7.0` |
| Animation | `framer-motion` | `^14.10.1` |
| Tests | Vitest + Testing Library + jsdom | see `package.json` |

#### Tooling

| Concern | Choice |
|---|---|
| Solution file | `backend/Medilog.sln` |
| Backend tests | `backend/tests/Medilog.Application.Tests`, `backend/tests/Medilog.WebAPI.Tests` |
| Frontend tests | `npm run test` (Vitest), `npm run lint`, `npm run typecheck`, `npm run build` |
| API docs | `backend/docs/api/CONVENTIONS.md`, `appointments.md`, `MEDICAL_RECORDS_API.md` |
| Dev data | `backend/docs/dev/seed-dev-data.sql` (synthetic only) |

### 1.4 Repository layout (actual)

```text
Medilog/
├── AGENTS.md                     # shared engineering rules (authority for scope/rules)
├── STATE.md                      # live status, decisions, blockers, next steps
├── docs/
│   ├── TECHNICAL_DOCUMENTATION.md  # this file
│   ├── reports/                  # per-Cycle execution reports
│   └── tasks/                    # per-Cycle task definitions
├── backend/
│   ├── Medilog.sln
│   ├── docs/
│   │   ├── api/                  # HTTP contracts (shared + per feature)
│   │   └── dev/                  # synthetic seed data
│   ├── src/
│   │   ├── Medilog.Domain/
│   │   ├── Medilog.Application/
│   │   ├── Medilog.Infrastructure/
│   │   └── Medilog.WebAPI/
│   └── tests/
│       ├── Medilog.Application.Tests/
│       └── Medilog.WebAPI.Tests/
└── frontend/
    ├── package.json
    ├── next.config.ts
    ├── tsconfig.json
    ├── vitest.config.mts
    └── src/
        ├── app/                  # route-level code only
        ├── components/           # business-agnostic shared UI
        ├── config/
        ├── constants/
        ├── features/             # feature modules
        ├── i18n/                 # locale routing/request/navigation
        ├── lib/                  # API transport, query client, utilities
        ├── messages/             # ar.json / en.json
        ├── proxy.ts              # next-intl middleware (Next 16 naming)
        ├── store/                # global UI state
        └── types/                # genuinely shared types
```

> **Documented-vs-actual discrepancies (intentional honesty):**
>
> 1. `AGENTS.md` describes the frontend root as `Front_End/` with folders directly under it.
>    The actual frontend root is `frontend/`, and application code lives under
>    `frontend/src/`. The folder **names inside** `src/` match the agreed structure
>    (`app`, `components`, `constants`, `features`, `i18n`, `messages`, `types`), plus
>    `lib`, `store`, and `config`. This document describes reality.
> 2. `AGENTS.md` mentions `Medilog.slnx`; the actual solution file is `backend/Medilog.sln`.
> 3. `AGENTS.md` §6.1 lists `data/`; there is no populated `data/` folder today. Shared API
>    code lives in `lib/api/`.
> 4. `AGENTS.md` says not to introduce `src/` “just because a template uses it”. The
>    frontend already uses `src/`; moving it would be an unrelated, high-risk refactor and
>    is therefore out of scope for this document.

### 1.5 Documentation map

| Document | Purpose |
|---|---|
| `AGENTS.md` | Rules, scope, architecture constraints, workflow. |
| `STATE.md` | Current phase/cycle status, decision log, blockers, execution reports. |
| `backend/docs/api/CONVENTIONS.md` | Shared HTTP contract: JSON, paging, ordering, error envelope, identity. |
| `backend/docs/api/appointments.md` | F1 route/field/error contract. |
| `backend/docs/api/MEDICAL_RECORDS_API.md` | F4 route/field/error contract. |
| `docs/tasks/F1/*`, `docs/tasks/F4/*` | Approved Cycle plans and acceptance criteria. |
| `docs/reports/*` | What was actually changed/tested per Cycle. |
| **This file** | Cross-cutting technical narrative and rationale. |

---

## 2. High-level architecture

### 2.1 Backend: layered Clean Architecture

```text
┌──────────────────────────────────────────────────────────────────────┐
│ Medilog.WebAPI                                                       │
│  Controllers · Program.cs · CurrentUser · ApiProblem · ExceptionHandler│
│  Depends on Application + Infrastructure (composition root only)      │
└───────────────┬──────────────────────────────────────────────────────┘
                │ calls use cases
┌───────────────▼──────────────────────────────────────────────────────┐
│ Medilog.Application                                                  │
│  Services · DTOs/Contracts · Validators · Mapping profiles · Paging  │
│  Abstractions: IAppointmentStore, IApplicationDbContext, ICurrentUser│
│  Depends on Domain (+ EF Core queryable types; no DbContext)         │
└───────────────┬──────────────────────────────────────────────────────┘
                │ uses entities and domain rules
┌───────────────▼──────────────────────────────────────────────────────┐
│ Medilog.Domain                                                       │
│  Entities · Enums · Base types · DomainException · DomainErrorCodes  │
│  No ASP.NET Core, no EF Core, no Infrastructure, no HTTP             │
└──────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────┐
│ Medilog.Infrastructure                                               │
│  MedilogDbContext · Entity configurations · Migrations · Converters  │
│  AppointmentStore · ClinicClock · DI extension                       │
│  Depends on Application (implements its ports) + Domain              │
└──────────────────────────────────────────────────────────────────────┘
```

**Project references (actual):**

| Project | References |
|---|---|
| `Medilog.Domain` | none |
| `Medilog.Application` | `Medilog.Domain` |
| `Medilog.Infrastructure` | `Medilog.Application`, `Medilog.Domain` |
| `Medilog.WebAPI` | `Medilog.Application`, `Medilog.Infrastructure` |

The dependency direction is inward: Domain knows nothing; Application knows Domain;
Infrastructure knows both; WebAPI composes everything. Infrastructure depending on Application
is the standard “ports and adapters” inversion: the port (`IAppointmentStore`,
`IApplicationDbContext`) is declared in Application, and the adapter (EF Core) implements it in
Infrastructure.

### 2.2 Why these layers exist

A reviewer will ask “why not one project?”. The honest answer is a set of concrete benefits
that this codebase actually realizes:

1. **The domain rules are testable without a database.** `Appointment.Create`,
   `Reschedule`, `Cancel`, `UpdateStatus`, and `OverlapsInterval` are pure methods with no EF
   Core dependency. The 178 Application tests include domain-rule tests that run in
   milliseconds.
2. **The HTTP layer cannot accidentally become the business layer.** Controllers only bind,
   call a service, and shape a result. There is no query, no rule, and no `DbContext` in a
   controller. This is enforced by review and by the project reference graph (a controller
   *could* reference Infrastructure, but the convention and the tests keep it out).
3. **Persistence can be replaced or faked.** The Application tests use in-memory fakes for
   `IAppointmentStore` and `IApplicationDbContext`; the WebAPI tests use the same fakes behind
   `WebApplicationFactory`. No MySQL server is needed to verify the contract.
4. **Security boundaries are explicit.** `ICurrentUser` lives in Application and is
   implemented in WebAPI. Every use case takes the actor from that abstraction, never from a
   request body. This makes “who is acting?” a single, auditable seam.
5. **Cross-feature contracts have one home.** Paging (`PagedResult<T>`, `PagingDefaults`),
   validation (`RequestValidation`), and error codes (`ErrorCodes`, `DomainErrorCodes`) are
   shared types, not copies.

**Trade-off:** more projects and more indirection than a single-project API. For a
six-contributor graduation project with parallel features, the boundaries pay for themselves
in merge safety and review clarity.

### 2.3 End-to-end request lifecycle

Example: `POST /api/appointments` (book an appointment).

```text
HTTP request
  │
  ▼
[1] ASP.NET Core pipeline (Program.cs)
    UseExceptionHandler → UseStatusCodePages → HTTPS redirect → CORS
    → UseAuthentication → UseAuthorization → MapControllers
  │
  ▼
[2] AppointmentsController.Book([FromBody] BookAppointmentRequest, ct)
    - model binding + JSON enum/date parsing
    - on binding failure: InvalidModelStateResponseFactory → 400 request.invalid
  │
  ▼
[3] IAppointmentService.BookAsync(request, ct)          (Application)
    a. RequestValidation.ValidateAsync(bookValidator, request, ct)
       → on failure: RequestValidationException (400 validation.failed)
    b. RequireActorAsync(ct)  → ICurrentUser + IAppointmentStore.GetActorAsync
       → missing/inactive/unknown → ForbiddenException (403 identity.invalid)
    c. CanAccessDoctorAsync(doctorId)
       → doctor role: doctorId == current user
       → secretary role: doctor_secretaries assignment
       → false → NotFoundException (404 doctor.not_found)
    d. store.GetDoctorAsync → null → 404; inactive → ConflictException (409 doctor.unavailable)
    e. store.GetPatientAsync → null or patient.DoctorId != doctor.Id → 404 patient.not_found
    f. normalize times to whole seconds; IClinicClock rejects DST-gap times (400)
    g. Appointment.Create(...)                          (Domain)
       → end required, end > start, not in the past; else DomainException (400)
    h. store.BeginDoctorTransactionAsync(doctorId)
       → SELECT ... FOR UPDATE on the doctor row (per-doctor serialization)
    i. store.GetSlotHoldingAppointmentsAsync(...) + domain overlap check
       → overlap → ConflictException (409 appointment.overlap)
    j. store.AddAsync + SaveChangesAsync + CommitAsync
    k. reload the projected AppointmentResponse
  │
  ▼
[4] Controller returns 201 Created + Location + AppointmentResponse
  │
  ▼
[5] On any exception: GlobalExceptionHandler → ApiProblem → RFC 7807 JSON
```

The same shape applies to F4 (`MedicalVisitsController` → `MedicalVisitService` →
`IApplicationDbContext` → `MedicalVisit`/`Diagnosis`/`Medication`). The difference is the
persistence port: F4 uses `IApplicationDbContext` with EF Core queryables; F1 uses the
EF-free `IAppointmentStore` port. §3.6 explains why both exist.

### 2.4 Frontend: feature-oriented Next.js App Router

```text
frontend/src/
├── app/[locale]/                  # routing + layouts + page composition only
│   ├── layout.tsx                 # <html lang dir>, fonts, NextIntlClientProvider
│   ├── page.tsx                   # home
│   ├── appointments/              # /ar/appointments, /en/appointments
│   ├── medical-records/           # /ar/medical-records, visits/new, visits/[visitId]
│   └── providers.tsx              # QueryProvider + ToastContainer + dev identity
├── features/                      # business functionality
│   ├── appointments/              # components, hooks, services, types, utils, validation
│   ├── medical-records/           # api, guards, hooks, components, validation
│   ├── auth/                      # temporary dev identity
│   ├── payments/                  # mock-only
│   ├── home/
│   └── dashboard | doctors | patients | medical-timeline   # placeholders
├── components/                    # business-agnostic UI (layout, shared)
├── lib/                           # api transport, query client, utils
├── i18n/                          # routing, request config, navigation wrappers
├── messages/                      # ar.json, en.json
├── types/                         # shared types (paging)
├── store/                         # global UI state (sidebar)
├── config/                        # site metadata
└── proxy.ts                       # next-intl middleware
```

**Rules that are actually followed:**

- Route files contain no business logic. `app/[locale]/appointments/page.tsx` renders
  `<AppointmentsPage />` from the feature barrel inside a `Suspense` boundary.
- A feature exposes a **public barrel** (`features/<name>/index.ts`). Cross-feature imports go
  through the barrel; screens do not deep-import another feature's internals.
- Shared UI never imports a feature. `components/layout/Header.tsx` imports only `next-intl`,
  `react-icons`, `i18n/navigation`, and other shared components.
- Server state lives in React Query; local UI state lives in `useState`/small hooks;
  cross-page UI state (sidebar) lives in a tiny Zustand store.
- All user-facing text comes from `messages/{ar,en}.json`; no hard-coded strings in feature
  components.

### 2.5 Cross-cutting concerns map

| Concern | Backend home | Frontend home |
|---|---|---|
| Identity | `ICurrentUser` (Application) + `CurrentUser` (WebAPI) | `features/auth` + `lib/api/auth-headers.ts` |
| Authorization | Controller attributes + service-level scope checks | UI guards (`IdentityRequired`, `useActingIdentity`) — never the only defense |
| Validation | FluentValidation validators + domain guards | `validation/*.ts` + server field errors |
| Mapping | AutoMapper profiles | hand-written readers/guards at the API boundary |
| Paging | `PagedResult<T>`, `PagingDefaults`, `PagedQueryValidator<T>` | `types/paging.ts`, `lib/api/paging.ts` |
| Errors | `AppException` family + `DomainException` + `GlobalExceptionHandler` + `ApiProblem` | `ApiError` + `parseProblem` + per-feature error-key mapping |
| Localization | `code`/`errorCodes` in error bodies | `next-intl` messages keyed by `code` |
| Logging | `ILogger<T>`; no secrets/medical data | no logging of tokens |
| Configuration | `appsettings*.json`, user secrets, env vars | `NEXT_PUBLIC_API_BASE_URL`, `NEXT_PUBLIC_DEV_IDENTITY` |
| Testing | xUnit (domain/application/API) | Vitest + Testing Library |

---

## 3. Backend deep dive

### 3.1 Solution and project inventory

`backend/Medilog.sln` contains four source projects and two test projects.

#### Medilog.Domain

| Item | Value |
|---|---|
| Target | `net10.0` |
| References | none |
| Packages | none |
| Purpose | Entities, enums, base types, `DomainException`, `DomainErrorCodes` |

Domain has **zero** dependencies. It cannot reference ASP.NET Core, EF Core, or
Infrastructure — by design (`AGENTS.md` §4.1). That is what makes the appointment rules
testable as pure code.

#### Medilog.Application

| Package | Version | Why |
|---|---|---|
| `AutoMapper` | 16.3.0 | Agreed mapping library (`AGENTS.md` §3.3) |
| `FluentValidation` | 12.2.0 | Agreed validation library (`AGENTS.md` §3.2) |
| `FluentValidation.DependencyInjectionExtensions` | 12.2.0 | `AddValidatorsFromAssembly` |
| `Microsoft.EntityFrameworkCore` | 10.0.14 | `DbSet<T>`, `Include`, `FirstOrDefaultAsync`, `AnyAsync` |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.14 | `IServiceCollection` |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.14 | `ILogger<T>` |

> **Nuance a reviewer will notice:** Application references the EF Core **package** because
> `IApplicationDbContext` exposes `DbSet<T>` and the medical-records service uses EF query
> operators. It does **not** reference the EF Core **provider** (MySQL) and does **not**
> reference `Medilog.Infrastructure` or `MedilogDbContext`. The dependency rule is about
> the concrete infrastructure type, not about the abstraction package. This is documented
> rather than hidden.
>
> The F1 appointment path avoids even this package by using the `IAppointmentStore` port with
> plain records. Two persistence styles coexist deliberately (§3.6).

#### Medilog.Infrastructure

| Package | Version | Why |
|---|---|---|
| `Microsoft.EntityFrameworkCore` | 10.0.14 | ORM |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.14 | Migrations (`PrivateAssets=all`) |
| `MySql.EntityFrameworkCore` | 10.0.12 | MySQL provider |
| `Microsoft.Extensions.Configuration.Abstractions` | 10.0.14 | Connection string |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.14 | `AddInfrastructure` |

Contains `MedilogDbContext`, the 13 entity configurations, value converters, the migration,
`AppointmentStore`, `ClinicClock`, and `DependencyInjection.AddInfrastructure`.

#### Medilog.WebAPI

| Package | Version | Why |
|---|---|---|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.14 | JWT bearer authentication |
| `Microsoft.AspNetCore.OpenApi` | 10.0.11 | `/openapi/v1.json` in Development |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.14 | `dotnet ef` from the startup project |

`UserSecretsId` is set so `dotnet user-jwts` can sign development tokens (D-16). The id is
not a secret.

#### Test projects

| Project | Framework | Purpose |
|---|---|---|
| `Medilog.Application.Tests` | xUnit | Domain rules, services with in-memory fakes, validators, paging, mapping, access policy |
| `Medilog.WebAPI.Tests` | xUnit + `WebApplicationFactory` | HTTP status codes, error envelope, identity headers, endpoint contracts |

The WebAPI test project links `Fakes.cs` and `Scenario.cs` from the Application test project so
both layers are tested against the **same** contract without a database.

---

### 3.2 Domain model

#### Base types

```csharp
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

public abstract class CreatedEntity : BaseEntity
{
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public abstract class AuditableEntity : CreatedEntity
{
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
```

- **Client-side GUID generation** means the application knows an entity's id before the
  insert, which makes `CreatedAtRoute`/`CreatedAtAction` and response mapping simple.
- **`CreatedAtUtc` / `UpdatedAtUtc` are applied centrally** by `MedilogDbContext.ApplyAuditRules`
  (§3.4). Creation time is protected on update (`IsModified = false`).
- Only `MedicalNote` inherits `AuditableEntity` today. Other entities are immutable in
  practice or have their own timestamps (`MedicalDocument.UploadDate`).

#### The 13 entities

| # | Entity | Base | Table | Purpose and key relationships |
|---|---|---|---|---|
| 1 | `Doctor` | `CreatedEntity` | `doctors` | Authenticated doctor; owns patients, visits, notes, documents, appointments; many-to-many with secretaries. Unique `email`. |
| 2 | `Secretary` | `CreatedEntity` | `secretaries` | Authenticated non-medical user; assigned to doctors through `doctor_secretaries`. Unique `email`. |
| 3 | `Patient` | `CreatedEntity` | `patients` | Central medical-record subject. Optional `DoctorId` (nullable by schema; ownership enforced by the app, BRULE-03). No credentials. |
| 4 | `Appointment` | `CreatedEntity` | `appointments` | Booking between a patient and a doctor. `CreatedBy` is a polymorphic creator id with **no DB FK**. Holds the only state machine in the domain. |
| 5 | `MedicalVisit` | `BaseEntity` | `medical_visits` | One consultation; parent of diagnoses, medications, vital signs, notes, lab results. Optional 1:1 link to an appointment. |
| 6 | `Diagnosis` | `BaseEntity` | `diagnoses` | Diagnosis recorded during a visit. Belongs to `(VisitId, PatientId)` via composite FK. |
| 7 | `Medication` | `BaseEntity` | `medications` | Prescription line of a visit. Same composite ownership. |
| 8 | `Allergy` | `BaseEntity` | `allergies` | Patient allergy with optional severity. |
| 9 | `ChronicCondition` | `BaseEntity` | `chronic_conditions` | Long-term condition with optional status and diagnosis date. |
| 10 | `LaboratoryResult` | `BaseEntity` | `laboratory_results` | Test result; optional link to the producing visit. |
| 11 | `VitalSigns` | `BaseEntity` | `vital_signs` | Measurements for a visit; all measurements nullable. |
| 12 | `MedicalNote` | `AuditableEntity` | `medical_notes` | Clinical note by a doctor about a patient/visit; optional visit link. |
| 13 | `MedicalDocument` | `BaseEntity` | `medical_documents` | Uploaded PDF/image metadata (path/URL); optional uploader doctor. File handling itself is out of scope. |

#### Entity relationship sketch

```text
Doctor 1 ──── * Patient            (Patient.DoctorId nullable)
Doctor 1 ──── * Appointment
Patient 1 ─── * Appointment
Doctor * ──── * Secretary          (doctor_secretaries join table)

Patient 1 ─── * MedicalVisit
Doctor  1 ─── * MedicalVisit
Appointment 1 ─── 0..1 MedicalVisit   (optional, unique appointment_id)

MedicalVisit 1 ─── * Diagnosis       (FK: VisitId + PatientId)
MedicalVisit 1 ─── * Medication      (FK: VisitId + PatientId)
MedicalVisit 1 ─── * VitalSigns      (FK: VisitId)
MedicalVisit 1 ─── * MedicalNote     (optional FK: VisitId + PatientId)
MedicalVisit 1 ─── * LaboratoryResult(optional FK: VisitId + PatientId)

Patient 1 ─── * Allergy
Patient 1 ─── * ChronicCondition
Patient 1 ─── * LaboratoryResult
Patient 1 ─── * MedicalNote
Patient 1 ─── * MedicalDocument
Doctor  1 ─── * MedicalDocument      (optional UploadedBy)
```

#### Entity fields and invariants

The following catalog is the authoritative implementation-oriented reading guide for the
13 domain entities. “Database rule” means a rule represented by an EF configuration,
foreign key, index, or check constraint. “Application/domain rule” means the rule is
enforced by a service, access policy, or domain method because it depends on the acting
identity, current time, or a cross-row decision.

1. **`Doctor` — authenticated clinical owner**
   - Identity/profile fields: `Id`, `FullName`, `Email`, `PasswordHash`, optional `Phone`,
     optional `Specialization`, and `AccountStatus`.
   - Relationships: owns patients, medical visits, notes, documents, and appointments;
     participates in the doctor-secretary many-to-many association.
   - Database rules: unique email and an allowed-value check for account status.
   - Access rule: a doctor may operate only on records in that doctor’s workspace; the
     authenticated doctor id is never accepted from a request body.

2. **`Patient` — medical-record subject**
   - Identity/profile fields: `Id`, `FullName`, required `DateOfBirth` and `Gender`, optional
     `Phone`, `Address`, `BloodType`, and `EmergencyContact`.
   - Ownership: optional `DoctorId` at schema level because the class specification permits
     a nullable relationship; the Application layer treats the creating doctor’s workspace
     as the ownership boundary.
   - Relationships: appointments, visits, diagnoses, medications, allergies, chronic
     conditions, laboratory results, notes, and documents.
   - Confidentiality rule: a secretary may receive only the minimum administrative lookup
     fields required for booking; medical fields are not exposed by appointment responses.

3. **`Secretary` — administrative booking user**
   - Identity/profile fields: `Id`, `FullName`, `Email`, `PasswordHash`, optional `Phone`,
     and `AccountStatus`.
   - Relationships: assigned doctors through `doctor_secretaries`.
   - Database rules: unique email and allowed-value account status.
   - Authorization rule: assignment controls the doctor scope for appointment and lookup
     operations; the secretary role cannot access medical-visit endpoints.

4. **`Appointment` — scheduled doctor/patient interval**
   - Fields: `PatientId`, `DoctorId`, polymorphic `CreatedBy`, local `AppointmentDate`,
     `StartTime`, optional `EndTime` in the entity model, optional `Reason` and `Notes`,
     `Status`, optional `CancellationReason`, and inherited `CreatedAtUtc`.
   - Creation rule: the Application layer supplies the authenticated actor as `CreatedBy`;
     clients cannot set the creator, id, or initial status.
   - Interval rules: the effective booking request requires an end time; end must be later
     than start; the start cannot be in the past; clinic-local DST-gap times are rejected.
   - Conflict rule: for the same doctor and date, overlap is
     `newStart < existingEnd && newEnd > existingStart`. Adjacency is allowed, and
     cancelled/no-show appointments do not occupy a slot.
   - Lifecycle: new appointments are `Scheduled`; open states are `Scheduled` and
     `Confirmed`; `Completed`, `Cancelled`, and `NoShow` are final. Cancellation must
     carry a reason and cannot be performed through the generic status endpoint.

5. **`MedicalVisit` — consultation aggregate root for F4**
   - Fields: `PatientId`, `DoctorId`, optional `AppointmentId`, UTC-normalized `VisitDate`,
     and optional `ChiefComplaint`, `Symptoms`, and `ClinicalNotes`.
   - Relationships: owns/contains diagnoses, medications, vital signs, medical notes, and
     laboratory results through navigation collections.
   - Consistency rules: a linked appointment must belong to the same patient and doctor;
     an appointment can be linked to at most one visit; patient and doctor ownership are
     set from persisted data and are not client-controlled on update.
   - Access rule: every read and write query is filtered by the acting doctor.

6. **`Diagnosis` — diagnosis recorded during a visit**
   - Fields: `VisitId`, `PatientId`, required `DiagnosisName`, optional `Description`, and
     `DiagnosisDate`.
   - Database rule: the composite `(VisitId, PatientId)` foreign key points to the visit’s
     composite alternate key, preventing a diagnosis from being attached to a visit for a
     different patient.
   - Application rules: the service derives visit and patient ids from the route and
     persisted visit; the diagnosis date cannot be in the future; update requires that the
     diagnosis belongs to the route visit and the acting doctor.
   - Current lifecycle: create and update are supported; delete is intentionally not exposed.

7. **`Medication` — prescription line**
   - Fields: `VisitId`, `PatientId`, required `MedicationName`, `Dosage`, and `Frequency`,
     plus optional `Duration` and `Instructions`.
   - Database rule: the same composite visit/patient consistency constraint as diagnoses.
   - Application rules: ownership is inherited from the visit, text is trimmed and bounded
     by request validators, and child ids cannot be reassigned.
   - Current lifecycle: create, update, and permanent delete are supported; deletion is
     intended to correct a prescription line entered by mistake and does not delete the visit.

8. **`Allergy` — patient allergy**
   - Fields: `PatientId`, required `AllergyName`, optional `Severity`, `Description`, and
     `Notes`.
   - Database rule: patient foreign key is restrictive; severity is constrained to
     `Mild`, `Moderate`, or `Severe` when present.
   - Current implementation status: persisted domain model and configuration exist, but no
     public allergy use case is part of the delivered F1/F4 API slice.

9. **`ChronicCondition` — long-term patient condition**
   - Fields: `PatientId`, required `ConditionName`, optional `DiagnosisDate`, optional
     `Status`, and optional `Notes`.
   - Database rule: patient deletion is restricted and status is constrained to
     `Active`, `Controlled`, or `Resolved` when present.
   - Current implementation status: persisted domain model and configuration exist; a
     dedicated public management endpoint is outside the currently delivered slice.

10. **`VitalSigns` — measurements associated with a visit**
    - Fields: `VisitId`, optional `BloodPressure`, `HeartRate`, `Temperature`, `RespiratoryRate`,
      `Weight`, `Height`, and `SpO2`.
    - The specification allows all measurements to be nullable so a clinician can record
      only the measurements taken.
    - EF configuration applies decimal precision where needed and restricts deletion of the
      parent visit; no current public F4 endpoint writes vital signs.

11. **`LaboratoryResult` — patient test result**
    - Fields: `PatientId`, optional `VisitId`, required `TestName`, `TestDate`, `Value`, and
      `Unit`, plus optional `ReferenceRange` and `Notes`.
    - Database rules: patient ownership is mandatory; the optional visit link uses a
      composite visit/patient relationship so a result cannot point to another patient’s
      visit; delete behavior is restrictive.
    - Current implementation status: schema support exists, while laboratory-result CRUD is
      not part of the delivered public API.

12. **`MedicalNote` — doctor-authored clinical note**
    - Fields: `PatientId`, `DoctorId`, optional `VisitId`, required `NoteContent`, inherited
      `CreatedAtUtc`, and nullable `UpdatedAtUtc`.
    - Audit rule: it is the only current `AuditableEntity`; the DbContext sets update time
      server-side and protects creation time from client updates.
    - Access rule: both patient and doctor relationships are restrictive and must remain
      within the acting doctor’s workspace. File/API management is not yet exposed in F4.

13. **`MedicalDocument` — document metadata**
    - Fields: `PatientId`, optional `UploadedBy` doctor id, `FileName`, `MedicalDocumentType`,
      `Location`, `UploadDate`, and optional `Description`.
    - Database rules: patient and optional uploader relationships are restrictive; type is
      constrained to `PDF`, `JPG`, `JPEG`, or `PNG`; upload-date and patient indexes support
      chronological retrieval.
    - Design boundary: the entity stores metadata and a path/URL only. File upload, storage,
      scanning, and download authorization are not implemented by the current persistence
      foundation.

#### Domain enums

| Enum | Members | Stored label(s) |
|---|---|---|
| `AccountStatus` | `Active`, `Inactive` | `Active`, `Inactive` |
| `AppointmentStatus` | `Scheduled`, `Confirmed`, `Completed`, `Cancelled`, `NoShow` | `Scheduled`, `Confirmed`, `Completed`, `Cancelled`, **`No-Show`** |
| `Gender` | `Male`, `Female` | `Male`, `Female` |
| `BloodType` | `APositive` … `ONegative` | `A+`, `A-`, `B+`, `B-`, `AB+`, `AB-`, `O+`, `O-` |
| `AllergySeverity` | `Mild`, `Moderate`, `Severe` | same |
| `ChronicConditionStatus` | `Active`, `Controlled`, `Resolved` | same |
| `MedicalDocumentType` | `Pdf`, `Jpg`, `Jpeg`, `Png` | **`PDF`**, `JPG`, `JPEG`, `PNG` |
| `UserRole` (Application, not Domain) | `Doctor`, `Secretary` | not persisted |

**Why store labels instead of numbers?** The database stays readable, the allowed values are
visible in SQL and check constraints, and an accidental enum reorder cannot silently change
meaning. The converters are explicit two-way switches that throw on an unknown value, so a
corrupt row fails loudly instead of mapping to the wrong member.

#### Why the model is not “full DDD”

- Entities have public setters. The team chose a pragmatic model: invariants that matter
  (appointment lifecycle, overlap) are enforced through methods; simple child entities
  (allergy, medication) are plain data.
- No aggregates, no domain events, no repository-per-entity. Those would add ceremony without
  a current use case (`AGENTS.md` §4.1: “do not add abstractions or DDD patterns merely for
  ceremony”).
- `Appointment` is the exception because it owns real rules and a state machine, so it gets
  factory/behaviour methods and private guards.

---

### 3.3 Business rules

#### 3.3.1 Appointment interval rules (F1)

| Rule | Enforcement | Code |
|---|---|---|
| End time is required | `Appointment.Create` / `Reschedule` | `appointment.end_time_required` |
| End must be later than start | `EnsureValidInterval` | `appointment.invalid_interval` |
| Start cannot be in the past (clinic local time) | `EnsureNotInPast` with `IClinicClock.LocalNow` | `appointment.in_the_past` |
| Same doctor, same date, overlapping intervals are forbidden | `AppointmentService.EnsureNoConflictAsync` + `Appointment.OverlapsInterval` | `appointment.overlap` (409) |
| Back-to-back appointments are allowed | Overlap formula is strict `<` / `>` | — |
| `Cancelled` and `NoShow` free their slot | `OccupiesSlot` | — |
| A reschedule ignores its own previous slot | `excludeAppointmentId` + `Id == other.Id` guard | — |
| Times are whole seconds | `ToWholeSeconds` (column is `time(0)`) | — |
| DST-gap wall-clock times are rejected | `IClinicClock.IsNonExistentLocalTime` | `appointment.time_not_in_time_zone` |

**The overlap formula**, stated exactly as implemented:

```text
overlap  ⇔  newStart < existingEnd  AND  newEnd > existingStart
```

Why this formula and not “start is inside the other interval”:

- It handles **partial overlap** (09:00–10:00 vs 09:30–10:30).
- It handles **full containment** (09:00–11:00 vs 09:30–10:00 and the reverse).
- It handles **same-time** (09:00–10:00 vs 09:00–10:00).
- It correctly treats **adjacency** (09:00–10:00 vs 10:00–11:00) as non-overlap, because
  `10:00 < 10:00` is false.
- It rejects zero/negative intervals earlier, so the formula never sees `end <= start`.

`OverlapsWith(Appointment other)` additionally short-circuits when the ids match, the doctors
differ, or either appointment no longer occupies a slot.

#### 3.3.2 Appointment status machine

```text
Scheduled ──► Confirmed ──► Completed
    │             │
    ├────────────►Completed
    ├────────────►NoShow
    └────────────►NoShow

Cancelled  ──► (final)
Completed  ──► (final)
NoShow     ──► (final)
```

| From | Allowed to | Notes |
|---|---|---|
| `Scheduled` | `Confirmed`, `Completed`, `NoShow` | |
| `Confirmed` | `Completed`, `NoShow` | |
| `Completed` | — | final |
| `Cancelled` | — | final; reached only through `Cancel` |
| `NoShow` | — | final |

- Setting `Cancelled` through the status route throws `appointment.use_cancel_operation`.
  Cancellation must go through `Cancel(reason)` so a reason is always captured.
- Invalid transitions throw `appointment.invalid_status_transition` (400).
- Rescheduling is **not** a status change: the status is preserved.
- Only an **open** appointment (`Scheduled` or `Confirmed`) can be rescheduled, cancelled, or
  status-changed. `IsOpen`/`EnsureOpen` rejects a final state with `appointment.not_open`, so a
  completed or no-show appointment cannot be silently reopened.
- `IsOpen` is not the same predicate as `OccupiesSlot`: `Completed` still occupies its slot
  (the time was used) but is no longer open. Keeping the two concepts separate is what makes the
  overlap check correct after a visit is completed.

#### 3.3.3 Appointment role and scope rules

| Action | Doctor | Secretary |
|---|---|---|
| Book / reschedule / cancel / confirm / no-show | Own appointments | Appointments of assigned doctors |
| Mark **Completed** | Own appointments | **Forbidden** (`appointment.complete_requires_doctor`, 403) |
| Get / list | Own appointments | Assigned doctors' appointments |
| Doctor lookup | Themself | Assigned, **active** doctors |
| Patient lookup | Own patients | Patients of an assigned doctor |

Scope rules:

- The visible doctor set is computed **server-side** from `ICurrentUser`. A client-supplied
  `doctorId` filter can only narrow that set, never widen it.
- A secretary's assignment is read from `doctor_secretaries`. No assignment means an empty
  visible set, which yields an empty page rather than an error.
- An out-of-scope resource is reported as **404**, not 403, so the API does not reveal that
  another doctor's appointment/patient exists.
- An inactive or unknown user is **403** (`identity.invalid`).

#### 3.3.4 Appointment creator and audit

- `CreatedBy` is always the authenticated actor's id (`actor.Id`), never a body field.
- The class specification declares `created_by` as a polymorphic FK (doctor **or** secretary).
  MySQL cannot enforce one FK against two tables, so the column has **no database FK**;
  referential integrity is the application's responsibility. This is documented on the
  entity and is an accepted schema limitation.
- `createdAtUtc` is set by the DbContext audit hook, not by the client.

#### 3.3.5 Medical-record rules (F4)

| Rule | Enforcement |
|---|---|
| Only a doctor may read/write medical records | `[Authorize(Roles = Roles.Doctor)]` **and** `RequireActingDoctorId()` |
| A doctor may only access patients in their workspace | `MedicalRecordAccessPolicy.CanAccessPatient(patient.DoctorId, doctorId)` |
| A visit is scoped by its own `DoctorId` | Every query filters `visit.DoctorId == actingDoctorId` |
| The acting doctor is never taken from the request | Service resolves it from `ICurrentUser` |
| Patient, doctor, and appointment link are immutable after creation | `UpdateMedicalVisitRequest` excludes them; mapping ignores them |
| A linked appointment must belong to the same patient **and** doctor | `EnsureAppointmentCanBeLinkedAsync` → `appointment.mismatch` (400) |
| An appointment can be linked to at most one visit | Pre-check + unique `uq_medical_visits_appointment_id` |
| Diagnoses/medications inherit the visit's patient | Service sets `PatientId = visit.PatientId`; mapping ignores it |
| Child resources must belong to the route's visit | Queries include `VisitId == visitId` |
| Only medication lines can be deleted (entered by mistake) | `DeleteMedicationAsync` (D-23); no diagnosis delete |
| Text is trimmed; blank optional text becomes `null` | `MedicalRecordText.Normalize` |
| Request-level text limits | `MedicalRecordText` constants (D-14) |
| Visit date is persisted as UTC | `VisitDate.ToUniversalTime()` in the mapping profile |

**Why the service re-checks the doctor role** even though the controller has
`[Authorize]`: defence in depth. A future caller (job, test, another controller, a
misconfigured route) cannot bypass the rule by not going through the attribute. The
attribute protects the endpoint; the service protects the use case.

#### 3.3.6 Confidentiality boundary

The secretary path never touches a medical entity:

- `AppointmentResponse` contains only administrative appointment data (patient name/phone,
  doctor name, date/time, status, reason, notes, cancellation reason, creator, timestamp).
- Patient lookups return only `id`, `fullName`, `phone`.
- There is no secretary-reachable route that reads `MedicalVisit`, `Diagnosis`, `Medication`,
  `Allergy`, `ChronicCondition`, `LaboratoryResult`, `VitalSigns`, `MedicalNote`, or
  `MedicalDocument`.
- `MedicalVisitsController` is doctor-only at the framework level and again in the service.

---

### 3.4 Persistence: EF Core + MySQL

#### Provider and why

`MySql.EntityFrameworkCore` (Oracle) is the installed provider. It targets MySQL 8 and supports
`utf8mb4`, `datetime(6)`, `time(0)`, check constraints, and `FOR UPDATE`. The alternative
(Pomelo) is not installed; introducing a second provider would be an unapproved dependency
change. The Oracle provider's GPL-with-FOSS-exception license is an open approval item (§8).

#### `MedilogDbContext`

```csharp
public sealed class MedilogDbContext : DbContext, IApplicationDbContext
{
    public DbSet<Doctor> Doctors => Set<Doctor>();
    // … 12 more DbSets …

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MedilogDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var entityBuilder = modelBuilder.Entity(entityType.Name);
            entityBuilder.ForMySQLHasCharset("utf8mb4");
            entityBuilder.ForMySQLHasCollation("utf8mb4_0900_ai_ci");
        }

        base.OnModelCreating(modelBuilder);
    }
}
```

- **Assembly scanning** means a new `IEntityTypeConfiguration<T>` is applied automatically.
  There is no manual registration list to forget.
- **utf8mb4 + `utf8mb4_0900_ai_ci`** are applied to every entity, including the join table, so
  Arabic names and emoji are stored correctly and comparisons are accent/case-insensitive.
- The provider's `HasCharSet`/`HasCollation` are surfaced through
  `MySQLEntityTypeBuilderExtensions` — a small compile shim in the migrations folder that
  bridges the provider API to the current EF Core version. It is not a second configuration
  system.

#### Audit rules

```csharp
private void ApplyAuditRules()
{
    var utcNow = DateTimeOffset.UtcNow;

    foreach (EntityEntry<CreatedEntity> entry in ChangeTracker.Entries<CreatedEntity>())
    {
        switch (entry.State)
        {
            case EntityState.Added:
                entry.Entity.CreatedAtUtc = utcNow;
                break;
            case EntityState.Modified:
                entry.Property(nameof(CreatedEntity.CreatedAtUtc)).IsModified = false;
                break;
        }
    }

    foreach (EntityEntry<AuditableEntity> entry in ChangeTracker.Entries<AuditableEntity>())
    {
        switch (entry.State)
        {
            case EntityState.Added:
                entry.Entity.UpdatedAtUtc = null;
                break;
            case EntityState.Modified:
                entry.Entity.UpdatedAtUtc = utcNow;
                break;
        }
    }
}
```

Why this design:

- One mechanism, no interceptor registration, no duplicate audit path.
- Creation timestamps cannot be overwritten by an update.
- Update timestamps are always server time, never client time.
- `MedicalDocument.UploadDate` is deliberately **not** covered: it is a domain timestamp set
  by the upload flow, not a generic audit field.

#### Value converters

| Domain type | Stored as | Converter | Why |
|---|---|---|---|
| `DateTimeOffset` | `datetime(6)` UTC `DateTime` | `UtcDateTimeOffsetConverter` | MySQL has no timezone-aware type; normalize to UTC |
| `DateTimeOffset?` | nullable `datetime(6)` | `NullableUtcDateTimeOffsetConverter` | same |
| `DateOnly` | `date` | `DateOnlyConverter` | provider does not map `DateOnly` natively |
| `DateOnly?` | nullable `date` | `NullableDateOnlyConverter` | same |
| `TimeOnly` | `time(0)` `TimeSpan` | `TimeOnlyConverter` | provider maps `TimeSpan`; `time(0)` stores whole seconds |
| `TimeOnly?` | nullable `time(0)` | `NullableTimeOnlyConverter` | same |
| All domain enums | `varchar(20)` labels | `EnumValueConverters` | readable, explicit, order-safe |

The round trip is **lossless for the domain's needs**: appointment times are whole seconds by
rule, and UTC timestamps are stored without offset because the clinic's only wall-clock
concept is the appointment date/time itself.

#### Connection string resolution and design-time factory

`AddInfrastructure` resolves the connection string in this order:

1. `ConnectionStrings:DefaultConnection` from configuration (user secrets / environment).
2. `MEDILOG_CONNECTION_STRING` environment variable.

If both are empty, startup throws a clear `InvalidOperationException`. `appsettings.json`
ships with an **empty** connection string on purpose: no local credentials are committed.

`MedilogDbContextFactory` (design-time) uses a placeholder connection string so
`dotnet ef migrations add` works without a live server. It is only used by tooling.

#### Migration status

| Item | Status |
|---|---|
| `20261109211333_InitialCreate` | Generated and inspected. Creates all 13 tables, keys, indexes, and FKs. |
| Applied to a database | **No.** No local MySQL/Docker/WSL is available in the current environment. |
| Model snapshot | Present (`MedilogDbContextModelSnapshot.cs`). |
| Migration shim | `MySQLEntityTypeBuilderExtensions.cs` exists because the provider emits `HasCharSet`/`HasCollation` calls that need the current EF Core signature. |

**Honest statement:** the schema has never been executed against MySQL in this environment.
The migration is reviewed structurally (table/FK/index list matches the configurations), but
a real round-trip smoke test is still required on a machine with MySQL.

#### Concurrency and the per-doctor lock

Booking and rescheduling are the only operations that can race on the same doctor's schedule.
The store serializes them per doctor:

```text
BEGIN TRANSACTION
  SELECT `id` FROM `doctors` WHERE `id` = ? FOR UPDATE   -- first statement
  -- read existing slot-holding appointments
  -- domain overlap check
  -- insert/update
COMMIT
```

- The locking read is the **first** statement so later reads see everything committed before
  the lock was granted.
- MySQL lock-wait timeout (1205) and deadlock (1213) are translated to
  `ConflictException` → `409 schedule.busy` (“Please try again”).
- Why a row lock and not a unique index? Overlap is an interval predicate, not equality; a
  unique index cannot express it. A serializable transaction or an application lock would
  also work, but a per-doctor row lock is the smallest correct mechanism and keeps
  concurrency for different doctors.
- The domain rule remains the source of truth; the lock only prevents two requests from
  passing the check simultaneously.

#### Query behavior

- All read projections use `AsNoTracking()` unless the entity will be modified.
- List filtering and paging are executed in SQL (`WHERE`, `CountAsync`, `OrderBy`, `Skip`,
  `Take`), never in memory.
- `GetSlotHoldingAppointmentsAsync` filters out `Cancelled`/`NoShow` in SQL.
- Patient search uses `Contains` (translated to `LIKE`) and takes `limit` rows in the database.
- The appointment projection joins patient and doctor only to read `FullName`/`Phone`.

---

### 3.5 Entity configurations, keys, and delete behavior

Every persisted entity has its own `IEntityTypeConfiguration<T>` in
`Infrastructure/Persistence/Configurations`. Shared base configurations handle `id`,
`created_at_utc`, and `updated_at_utc`.

| Table | Primary key | Alternate key | Notable FKs | Delete behavior | Indexes / constraints |
|---|---|---|---|---|---|
| `doctors` | `id` | — | — | — | unique `uq_doctors_email`; check `account_status` |
| `secretaries` | `id` | — | — | — | unique `uq_secretaries_email`; check `account_status` |
| `doctor_secretaries` | `(doctor_id, secretary_id)` | — | doctor, secretary | **Cascade** on both | `ix_doctor_secretaries_secretary_id` |
| `patients` | `id` | — | `doctor_id` → doctors | Restrict | `ix_patients_doctor_id_full_name`, `ix_patients_phone`; checks gender/blood type |
| `appointments` | `id` | `(id, patient_id, doctor_id)` | patient, doctor; `created_by` **no FK** | Restrict | `ix_appointments_doctor_id_appointment_date_start_time`, `ix_appointments_patient_id_appointment_date`; checks status and `end_time > start_time` |
| `medical_visits` | `id` | `(id, patient_id)` | patient, doctor; `(appointment_id, patient_id, doctor_id)` → appointments alternate key | Restrict | unique `uq_medical_visits_appointment_id`; `ix_medical_visits_patient_id_visit_date` |
| `diagnoses` | `id` | — | `(visit_id, patient_id)` → visits alternate key; patient | Restrict | — |
| `medications` | `id` | — | `(visit_id, patient_id)` → visits alternate key; patient | Restrict | — |
| `allergies` | `id` | — | patient | Restrict | check severity |
| `chronic_conditions` | `id` | — | patient | Restrict | check status |
| `laboratory_results` | `id` | — | `(visit_id, patient_id)` → visits (optional); patient | Restrict | `ix_laboratory_results_patient_id_test_date` |
| `vital_signs` | `id` | — | visit | Restrict | precision on temperature/weight/height |
| `medical_notes` | `id` | — | patient, doctor; `(visit_id, patient_id)` → visits (optional) | Restrict | — |
| `medical_documents` | `id` | — | patient; `uploaded_by` → doctors (optional) | Restrict | `ix_medical_documents_patient_id_upload_date`; check type |

#### Why composite foreign keys?

The schema enforces **cross-entity consistency**, not just “the row exists”:

```text
diagnoses.(visit_id, patient_id)  →  medical_visits.(id, patient_id)
```

- This guarantees a diagnosis cannot claim a patient different from its visit's patient.
- It is enforced by the database, not only by application code.
- `medical_visits` declares `(id, patient_id)` as an **alternate key** to be a valid principal
  for those composite FKs.
- The visit↔appointment link uses the same idea with three columns
  (`appointment_id, patient_id, doctor_id`), so a visit cannot link to an appointment that
  belongs to another patient or another doctor.
- The unique index on `appointment_id` turns the relationship into **1 appointment → 0..1
  visit**, which matches the requirement.

#### Why `Restrict` almost everywhere?

Medical and financial history must not disappear because a parent row was deleted.
`Restrict` makes an accidental cascade fail loudly and forces an explicit business decision.
The only `Cascade` is the `doctor_secretaries` join table, where a row has no meaning without
both sides.

#### Why `char(36)` GUIDs?

- Stable, non-guessable identifiers across API and database.
- `char(36)` is the provider-friendly hyphenated form and avoids binary/string conversion
  surprises in tests and SQL.
- The trade-off (storage/index size vs. `binary(16)`) is accepted for this project's scale.

---

### 3.6 Application layer

#### Two persistence styles (and why both exist)

| Style | Port | Used by | Characteristics |
|---|---|---|---|
| **EF-free store** | `IAppointmentStore` (+ `IAppointmentTransaction`) | F1 appointments | Plain records (`ActorInfo`, `DoctorInfo`, `PatientInfo`), explicit methods, no `IQueryable` leaks. The service never sees EF Core. |
| **DbContext abstraction** | `IApplicationDbContext` | F4 medical records | Exposes `DbSet<T>` and `SaveChangesAsync`; the service composes LINQ queries and uses `Include`. |

This is a deliberate, documented coexistence, not an accident:

- F1 was built first with a strict port so the appointment rules could be tested without any
  EF Core package in the test path.
- F4 needed richer query composition (`Include` children, project to nested responses) and was
  built against the DbContext abstraction approved for the medical-records Cycle (D-09).
- Both respect the layer rule: Application never references the concrete `MedilogDbContext`.
  `AddInfrastructure` registers `IApplicationDbContext` as a scoped alias of the context.

**Reviewer note:** a future cleanup could converge F1 onto `IApplicationDbContext`, or move
F4 onto a dedicated store. Either is a refactor with no current requirement; doing it now
would be an unapproved architectural change.

#### F1 use-case inventory

`IAppointmentService`:

| Method | Behaviour |
|---|---|
| `BookAsync` | validate → actor → doctor scope → doctor active → patient belongs to doctor → local times exist → `Appointment.Create` → lock → overlap check → insert → commit → reload response |
| `RescheduleAsync` | validate → normalize times → `ModifyAsync` (scope → lock → tracked read → `Reschedule` → overlap check excluding self → save) |
| `CancelAsync` | validate → `ModifyAsync` → `Cancel(reason)` |
| `UpdateStatusAsync` | validate → `ModifyAsync` → doctor-only `Completed` guard → `UpdateStatus` |
| `GetAsync` | actor → load projection → scope check → 404 if missing/out of scope |
| `ListAsync` | validate paging/filters → actor → compute visible doctor ids → narrow by optional filter → empty short-circuit → store list |
| `GetDoctorLookupsAsync` | actor → own id or assigned ids → active doctors ordered by name |
| `SearchPatientsAsync` | validate → actor → doctor scope → search patients of that doctor (id/name/phone only) |

`ModifyAsync` is the shared state-change template:

```text
RequireActor
  → GetAppointmentDoctorId (no tracking)
  → CanAccessDoctor? (404 if not)
  → BEGIN + SELECT … FOR UPDATE on doctor
  → tracked read of the appointment
  → domain mutation
  → SaveChanges + COMMIT
  → reload projected response
```

The two-phase read (untracked id/scope check, then tracked read inside the lock) is what makes
the concurrency story correct: the decision uses the latest committed row.

#### F4 use-case inventory

`IMedicalVisitService`:

| Method | Behaviour |
|---|---|
| `CreateVisitAsync` | validate → acting doctor → patient exists → workspace policy → optional appointment consistency → map → set doctor/patient → save → map response |
| `GetVisitAsync` | acting doctor → visit by `id` + `DoctorId` → include diagnoses/medications → map |
| `UpdateVisitAsync` | validate → acting doctor → tracked visit → map only mutable fields → save |
| `AddDiagnosisAsync` | validate → acting doctor → visit in scope → map → set visit/patient → save |
| `UpdateDiagnosisAsync` | validate → acting doctor → diagnosis belongs to visit **and** visit belongs to doctor → map → save |
| `AddMedicationAsync` | validate → acting doctor → visit in scope → map → set visit/patient → save |
| `UpdateMedicationAsync` | validate → acting doctor → medication belongs to visit **and** visit belongs to doctor → map → save |
| `DeleteMedicationAsync` | acting doctor → same ownership query → remove → save |

#### No MediatR / CQRS

The team explicitly did **not** adopt MediatR, CQRS, a Result library, or a generic repository
(D-08/D-09). Reasons:

1. **No requirement needs it.** There is no pipeline behaviour (logging, transactions,
   authorization) that MediatR would centralize better than the existing middleware/handler.
2. **Debuggability.** A controller → service → store call stack is obvious to every
   contributor and to a professor reviewing the code. A mediator adds indirection without a
   current payoff.
3. **Consistency with the approved architecture.** `AGENTS.md` §4.1 says not to introduce
   MediatR/CQRS/Result unless explicitly approved. It has not been approved.
4. **Plain services are testable.** The tests instantiate `AppointmentService` with fakes
   directly; no DI container or mediator setup is needed.

If the team later wants MediatR, it should be a deliberate, documented decision with a
migration plan — not a drive-by refactor.

#### Shared paging

```csharp
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public static class PagingDefaults
{
    public const int MinPageNumber = 1;
    public const int DefaultPageNumber = 1;
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 50;
}
```

`PagedQueryValidator<T>` enforces `pageNumber >= 1` and `1 <= pageSize <= 50` for every list
query that implements `IPagedQuery`. Feature validators derive from it and add only their own
filter rules. The request field is `pageNumber`; the response field is `page` (Q-04,
intentional).

#### Application exceptions

```csharp
public abstract class AppException : Exception
{
    protected AppException(string message, string code) : base(message) => Code = code;
    public string Code { get; }
}

public sealed class NotFoundException : AppException { /* 404 */ }
public sealed class ForbiddenException : AppException { /* 403 */ }
public sealed class ConflictException : AppException { /* 409 */ }
public sealed class RequestValidationException : AppException { /* 400, field map */ }
```

- `DomainException` (Domain) covers business invariants; `AppException` covers application
  outcomes (not found, forbidden, conflict, validation).
- The WebAPI exception handler maps both families to the same envelope (§3.10).
- Codes are stable strings in `ErrorCodes` / `DomainErrorCodes`; clients localize by code and
  never display the English message.

#### Dependency registration

```csharp
services.AddScoped<IAppointmentService, AppointmentService>();
services.AddScoped<IMedicalVisitService, MedicalVisitService>();
services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
services.AddAutoMapper(cfg => cfg.LicenseKey = configuration["AutoMapper:LicenseKey"], assembly);
```

- Validators and mapping profiles are discovered by assembly scanning.
- The AutoMapper commercial license key is read from configuration; it is not committed.

---

### 3.7 Validation strategy (FluentValidation)

Validation is intentionally **layered**:

| Layer | What it checks | Example |
|---|---|---|
| **Frontend** | UX shape: required fields, formats, max length, disabled submit | `appointment.validation.ts` |
| **FluentValidation** | Request shape: non-empty ids, enum membership, lengths, date/time sanity | `BookAppointmentRequestValidator` |
| **Domain** | Invariants that must hold regardless of caller | `Appointment.Create`, `Cancel`, `UpdateStatus` |
| **Database** | Structural guarantees | check constraints, FKs, unique indexes |

The frontend is **never** treated as security. The API re-validates everything.

#### Shared runner

```csharp
public static async Task ValidateAsync<T>(
    IValidator<T> validator,
    T instance,
    CancellationToken cancellationToken)
{
    var result = await validator.ValidateAsync(instance, cancellationToken);
    if (result.IsValid) return;

    var errors = result.Errors
        .GroupBy(failure => failure.PropertyName)
        .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());

    var codes = result.Errors
        .GroupBy(failure => failure.PropertyName)
        .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorCode).ToArray());

    throw new RequestValidationException(errors, codes);
}
```

Every public use-case method calls `RequestValidation.ValidateAsync` **before** touching
persistence. That is why validation cannot be bypassed by calling the service from another
entry point.

#### What validators deliberately do **not** do

- They do not query the database (existence, ownership, appointment consistency are state
  rules handled by the service).
- They do not duplicate domain rules. For example, “end later than start” is validated for
  shape in the request and enforced again by the domain; the domain is the authority.
- They do not know about roles or the current user.

#### Validator inventory

| Feature | Validators |
|---|---|
| Appointments | `BookAppointmentRequest`, `RescheduleAppointmentRequest`, `CancelAppointmentRequest`, `UpdateAppointmentStatusRequest`, `ListAppointmentsQuery` (derives `PagedQueryValidator`), `PatientLookupQuery` |
| Medical records | `CreateMedicalVisitRequest`, `UpdateMedicalVisitRequest`, `CreateDiagnosisRequest`, `UpdateDiagnosisRequest`, `CreateMedicationRequest`, `UpdateMedicationRequest` |
| Shared | `PagedQueryValidator<T>` (base class) |

`MedicalRecordText` centralizes the text limits (chief complaint 2000, symptoms 2000, clinical
notes 4000, diagnosis name 200, diagnosis description 4000, medication name 200, dosage/
frequency/duration 100, instructions 4000). They are request-level guards; the MySQL `text`
columns have no practical per-column limit.

---

### 3.8 Mapping strategy (AutoMapper)

AutoMapper is used **only** in F4 today. F1 uses explicit LINQ projections in
`AppointmentStore.Project`, which is also explicit and avoids materializing entities.

#### F4 maps

| Map | Explicit decisions |
|---|---|
| `CreateMedicalVisitRequest → MedicalVisit` | Ignore `Id`, `DoctorId`, `PatientId`, all navigations and child collections; normalize text; convert `VisitDate` to UTC |
| `UpdateMedicalVisitRequest → MedicalVisit` | Same ignores **plus** `AppointmentId` (immutable); only four mutable members are written |
| `MedicalVisit → MedicalVisitResponse` | Order diagnoses by `DiagnosisDate, Id`; order medications by `MedicationName, Id` |
| `Diagnosis ↔ requests/responses` | Ignore ids/ownership/navigation; trim name; normalize description |
| `Medication ↔ requests/responses` | Same; trim name/dosage/frequency; normalize duration/instructions |

Why the ignore lists matter (AGENTS.md §3.3):

- A client cannot overwrite `Id`, `PatientId`, `DoctorId`, or `AppointmentId`.
- A client cannot attach child collections or navigation properties.
- A client cannot claim another patient's visit by sending a different `PatientId`; the
  service sets it from the persisted visit.
- Update maps are applied to a **tracked** entity, so any unignored field would be a mass
  assignment vulnerability. The ignore list is the defense.

`MappingProfile` (the shared profile) is intentionally empty: feature profiles own their maps,
and no implicit convention maps are enabled.

---

### 3.9 WebAPI layer and the HTTP pipeline

#### Pipeline order (`Program.cs`)

```text
UseExceptionHandler()          // catches everything downstream, writes the envelope
UseStatusCodePages(...)        // body-less 401/404/405/415/5xx get the envelope
if Development: MapOpenApi()
UseHttpsRedirection()
UseCors("Frontend")
UseAuthentication()            // must run before authorization
UseAuthorization()
MapControllers()
```

Why this order:

- `UseExceptionHandler` is first so it wraps the whole request.
- `UseStatusCodePages` is early so framework-generated responses get a body **before** CORS
  writes headers; the comment in `Program.cs` records this reasoning.
- `UseCors` must run before authentication so preflight requests are answered.
- `UseAuthentication` must run before `UseAuthorization` so `[Authorize(Roles = …)]` sees a
  principal.
- `MapOpenApi` is Development-only.

#### Fail-fast clinic clock

```csharp
_ = app.Services.GetRequiredService<IClinicClock>();
```

`ClinicClock` validates `Clinic:TimeZone` at construction. Resolving it at startup turns a
misconfiguration into a startup failure instead of a 500 on the first booking.

#### JSON configuration

```csharp
.AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(allowIntegerValues: false)))
```

- Enums are strings (`"Scheduled"`, `"NoShow"`); numbers are rejected with 400.
- camelCase is the default (`JsonSerializerDefaults.Web`).
- `DateOnly`/`TimeOnly` serialize as `yyyy-MM-dd` / `HH:mm:ss`.

#### Model-state factory

```csharp
options.SuppressMapClientErrors = true;

options.InvalidModelStateResponseFactory = context =>
{
    var errors = context.ModelState
        .Where(entry => entry.Value is { Errors.Count: > 0 })
        .ToDictionary(
            entry => entry.Key,
            entry => entry.Value!.Errors
                .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? "The value is invalid."
                    : error.ErrorMessage)
                .ToArray());

    var problem = ApiProblem.Validation(
        context.HttpContext, errors, errorCodes: null,
        ErrorCodes.RequestInvalid, "The request could not be read.");

    return ApiProblem.ToResult(problem);
};
```

- Malformed JSON, wrong types, and missing bodies produce `400 request.invalid` with the
  shared envelope, not MVC's default body.
- `SuppressMapClientErrors` leaves 415/other client errors body-less so
  `UseStatusCodePages` can emit the same envelope (with `code`/`traceId`).

#### Controllers

Both controllers are thin adapters:

- `AppointmentsController` — 8 actions (book, get, list, doctor lookup, patient lookup,
  reschedule, cancel, status). No `[Authorize]` yet (**Q-07 TODO**); the service enforces
  identity and role.
- `MedicalVisitsController` — `[Authorize(Roles = Roles.Doctor)]` at the class level; 8
  actions (create/get/update visit, add/update diagnosis, add/update/delete medication).
  `CreatedAtRoute` uses a named route (`MedicalVisits.GetVisit`) because MVC suppresses the
  `Async` suffix in action names by default.

#### CORS

```csharp
policy.WithOrigins(allowedOrigins)   // default ["http://localhost:3000"]
      .AllowAnyHeader()
      .AllowAnyMethod();
```

`AllowAnyHeader` is required because the development identity uses custom headers
(`X-User-Id`, `X-User-Role`) and the dev token uses `Authorization`.

#### OpenAPI

`/openapi/v1.json` is served in Development. It is the machine-readable companion to
`backend/docs/api/*.md`.

---

### 3.10 Error envelope

One envelope for every error path:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "instance": "/api/appointments",
  "code": "validation.failed",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "errors": { "patientId": ["A patient identifier is required."] },
  "errorCodes": { "patientId": ["NotEmptyValidator"] }
}
```

#### Pieces

| Piece | Responsibility |
|---|---|
| `ApiProblem` | Builds `ProblemDetails`/`ValidationProblemDetails`, normalizes field keys, writes JSON, maps status codes to default codes/titles |
| `GlobalExceptionHandler` (`IExceptionHandler`) | Maps exceptions to the envelope; logs 500s with the exception; logs 4xx as information |
| `InvalidModelStateResponseFactory` | Binding failures → `400 request.invalid` |
| `UseStatusCodePages` | Framework responses without a body → `ApiProblem.FromStatusCode` |
| `SuppressMapClientErrors` | Prevents MVC from writing its own ProblemDetails without `code`/`traceId` |

#### Exception → status mapping

| Exception | Status | `code` |
|---|---|---|
| `RequestValidationException` | 400 | `validation.failed` (or a domain field code) |
| `ValidationException` (safety net) | 400 | `validation.failed` |
| `NotFoundException` | 404 | specific, e.g. `appointment.not_found` |
| `ForbiddenException` | 403 | specific, e.g. `identity.invalid` |
| `ConflictException` | 409 | specific, e.g. `appointment.overlap` |
| `DomainException` | 400 | `DomainErrorCodes.*` |
| anything else | 500 | `server.error` (generic body) |

#### Field-key normalization

`ApiProblem.NormalizeFieldKey` strips the JSON-path prefix and lower-cases the first letter:

```text
$.status  → status
EndTime   → endTime
```

This makes FluentValidation keys (`PatientId`), model-binding keys (`$.status`), and the JSON
contract (`patientId`) agree, so the frontend can map field errors without special cases.

#### Security properties

- 500 bodies never contain the exception message, stack trace, SQL, or patient data.
- 4xx messages are application-authored English text; clients translate by `code`.
- The exception is logged with the same `traceId` that is returned to the client, so support
  can correlate without exposing internals.
- Client-disconnect (499) is handled explicitly: if the request was aborted, no response is
  written.

---

### 3.11 Identity and authorization

#### `ICurrentUser`

```csharp
public interface ICurrentUser
{
    Guid? UserId { get; }
    UserRole? Role { get; }
}
```

- Nullable so “unauthenticated” is representable.
- Application services that need an actor call `RequireActorAsync` /
  `RequireActingDoctorId` and reject `null`.
- No use case accepts a user/doctor id from the request body.

#### `CurrentUser` implementation

Resolution order:

1. `ClaimTypes.NameIdentifier` (JWT `sub`) or raw `sub`.
2. `ClaimTypes.Role` or raw `role`.
3. **Development only:** `X-User-Id` / `X-User-Role` when
   `Auth:AllowHeaderIdentity = true`.

Rules:

- `UserId` must parse as a hyphenated GUID and must not be the empty GUID.
- `Role` must be exactly `Doctor` or `Secretary` (case-sensitive); `"1"` or `"doctor"` is
  rejected.
- Header identity is `false` in `appsettings.json` and `true` in
  `appsettings.Development.json`.
- Header values are never logged.

**Why headers are acceptable temporarily:** authentication is not in the approved current
scope (Q-07). Headers let F1/F4 be built and tested end-to-end. They are explicitly
development-only, documented as forgeable, and must be removed when real authentication
lands.

#### JWT bearer

- `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer()` is registered so
  `[Authorize]` has a principal.
- Development tokens are produced by `dotnet user-jwts` (D-16). The frontend medical-records
  feature stores one in `sessionStorage` via the `DevTokenPanel` and publishes it through the
  auth-header provider registry.
- The token is never logged, rendered, or sent anywhere except the Medilog API.

#### Authorization matrix (current code)

| Endpoint | Framework | Service-level |
|---|---|---|
| `POST/GET/PUT /api/appointments/**` | **none yet** (Q-07) | `RequireActorAsync`, role checks, doctor/secretary scope |
| `POST/GET/PUT/DELETE /api/medical-visits/**` | `[Authorize(Roles = "Doctor")]` | `RequireActingDoctorId`, workspace policy |

**Risk:** without `[Authorize]` on appointments, the only defense is the service's
`ICurrentUser` check. That check is solid (it rejects missing/invalid identity), but the
framework-level 401 is not produced for unauthenticated calls; they become 403
`identity.invalid` from the service. Adding the attribute is the Q-07 follow-up.

---

### 3.12 Testing

#### Backend test inventory

| Project | Files | Focus |
|---|---|---|
| `Medilog.Application.Tests` | `BookingAndOverlapTests`, `LifecycleTests`, `LookupAndErrorCodeTests`, `ScopeAndRoleTests`, `ValidatorTests`, `Paging/PagingContractTests`, `Mapping/MedicalRecordsMappingProfileTests`, `Security/MedicalRecordAccessPolicyTests`, `Services/MedicalVisitServiceTests`, `Services/MedicationUseCaseTests`, `Support/Fakes`, `Support/Scenario`, `Support/MedicalVisitServiceFixture` | Domain rules, service flows, scope/roles, validators, paging, mapping, access policy |
| `Medilog.WebAPI.Tests` | `AppointmentsEndpointTests`, `ErrorEnvelopeTests`, `HeaderIdentityTests`, `LookupAndErrorCodeEndpointTests`, `Api/MedicalVisitsApiTests`, `Api/MedicationsApiTests`, `Support/WebApiFactory`, `Support/TestAuthHandler`, `Support/ApiData`, `ApiTestSupport` | HTTP contracts, status codes, envelope, identity, authorization |

#### Last observed results

| Command | Result |
|---|---|
| `dotnet build` | 0 warnings, 0 errors |
| `dotnet test` | **272 passed** (178 Application + 94 WebAPI) |
| Frontend `npm run typecheck` | clean |
| Frontend `npm run lint` | clean |
| Frontend `npm run test` | **288 passed** (15 files) |
| Frontend `npm run build` | success (21 static pages) |

> These are the results of the last actual run recorded in the Phase 1 report. They are not
> re-run by writing this document.

#### What the tests deliberately cover

- Appointment end > start; exact boundary adjacency; partial overlap; full containment;
  same-time overlap; different doctors/dates.
- Secretary cannot mark `Completed`; secretary cannot see another doctor's appointment.
- Doctor cannot retrieve another doctor's medical visit.
- Secretary cannot retrieve medical records (no route; and the doctor-only policy is tested).
- Paging: invalid page values, max page size, stable ordering, total count, page beyond the
  end.
- Error envelope: 400/401/403/404/405/409/415/500.
- Validation rules and the fact that business invariants still hold when validation is
  bypassed.

#### What the tests cannot cover here

- Real MySQL behavior (locking, collation, migration execution) — no server in this
  environment. The store/transaction logic is tested through fakes; the SQL path is reviewed
  but not executed.

---

## 4. Frontend deep dive

### 4.1 Runtime, configuration, and build

#### Scripts (`frontend/package.json`)

| Script | Command | Purpose |
|---|---|---|
| `dev` | `next dev` | Development server on port 3000 |
| `build` | `next build` | Production build (also runs type checking) |
| `start` | `next start` | Serve the production build |
| `lint` | `eslint` | Flat-config ESLint (Next.js core rules) |
| `typecheck` | `tsc --noEmit` | Strict TypeScript check without emit |
| `test` | `vitest run` | One-shot test run |
| `test:watch` | `vitest` | Watch mode |

#### `next.config.ts`

```ts
const withNextIntl = createNextIntlPlugin();

const nextConfig: NextConfig = {
  cacheComponents: true,
  partialPrefetching: true,
  agentRules: false,
  turbopack: {
    rules: {
      '*.css': {loaders: ['@tailwindcss/turbopack'], as: '*.css'}
    }
  }
};

export default withNextIntl(nextConfig);
```

- `withNextIntl()` is required so `getRequestConfig` is discovered.
- `cacheComponents` / `partialPrefetching` enable Next 16's component-level caching and
  partial prefetch behaviour.
- Tailwind 4 is wired through the Turbopack CSS loader rather than a PostCSS plugin.
- `agentRules: false` keeps the build output free of generated agent instructions.

#### Environment variables

| Variable | Scope | Meaning |
|---|---|---|
| `NEXT_PUBLIC_API_BASE_URL` | browser | API origin, e.g. `http://localhost:5208` |
| `NEXT_PUBLIC_DEV_IDENTITY` | browser | Enables the temporary header identity (`true`/`false`) |
| `NEXT_PUBLIC_DEV_USER_ID` | browser | Default synthetic user GUID |
| `NEXT_PUBLIC_DEV_USER_ROLE` | browser | Default role (`Doctor`/`Secretary`) |
| `NEXT_PUBLIC_CLINIC_TIME_ZONE` | browser | Must match backend `Clinic:TimeZone` (e.g. `Africa/Cairo`) |

`.env.local` is git-ignored; only `NEXT_PUBLIC_*` values reach the browser, and the template
explicitly warns never to place a secret there. Two rules follow from this:

1. The API base URL is read with a **literal property access**
   (`process.env.NEXT_PUBLIC_API_BASE_URL`) so Next.js can inline it at build time. A dynamic
   lookup would silently be `undefined` in the browser bundle.
2. There is **no** secret in the frontend. Real authentication must therefore use a token that
   the browser legitimately holds (e.g. a short-lived JWT), not a server secret.

#### TypeScript and linting

- `strict` mode is on; `any` is not used. API responses arrive as `unknown` and are narrowed by
  hand-written readers/guards (`readAppointment`, `readPagedResult`, `expectMedicalVisit`).
- ESLint uses the Next.js flat config; `npm run lint` is part of the definition of done.

#### Why no extra libraries

`AGENTS.md` §6.4 forbids adding Axios, TanStack Query, Zustand, Zod, or a toast package merely
because a guideline recommends them. The repository already contains TanStack Query, Zustand,
and `react-toastify`; the document records where each is actually used:

| Library | Actual use |
|---|---|
| TanStack Query | All server state (appointments, medical records) |
| Zustand | **Only** the sidebar UI store (`store/ui-store.ts`) |
| `react-toastify` | Mutation success/failure toasts |
| `framer-motion` | Home landing animations only |
| `react-icons` | Icons |

No new shared dependency was added for the frontend contract work.

### 4.2 Internationalization and RTL

#### Locale routing (`i18n/routing.ts`)

```ts
export const routing = defineRouting({
  locales: ['ar', 'en'],
  defaultLocale: 'ar',
  localePrefix: 'always'
});
```

- **Arabic is the default** because the primary users are Arabic-speaking clinic staff.
- **`localePrefix: 'always'`** means every URL carries its locale (`/ar/...`, `/en/...`). There
  is no ambiguous “bare” URL that has to guess a language, and sharing a link preserves the
  language.
- The two locales are the only supported ones; an unknown segment yields `notFound()`.

#### Request configuration (`i18n/request.ts`)

```ts
export default getRequestConfig(async ({locale}) => {
  if (!locale) {
    const paramValue = await rootParams.locale();
    if (hasLocale(routing.locales, paramValue)) locale = paramValue;
    else notFound();
  }
  return {
    locale,
    messages: (await import(`../messages/${locale}.json`)).default
  };
});
```

- Messages are imported **per request** from `messages/ar.json` / `messages/en.json`, so a
  locale only ships its own strings.
- `hasLocale` is a type guard, so `locale` is narrowed to `'ar' | 'en'` before use.
- When the locale is not provided by `next-intl`, it is read from Next 16's `rootParams()`.

#### Navigation wrappers (`i18n/navigation.ts`)

```ts
export const {Link, redirect, usePathname, useRouter, getPathname} =
  createNavigation(routing);
```

Screens import `Link` from `@/i18n/navigation`, **not** from `next/link`. That is what makes
`Link href="/appointments"` automatically become `/ar/appointments` or `/en/appointments`.
Using `next/link` directly would drop the locale prefix and break the language.

#### Middleware (`src/proxy.ts`)

```ts
export default createMiddleware(routing);

export const config = {
  matcher: '/((?!api|trpc|_next|_vercel|.*\\..*).*)'
};
```

- Next 16 renamed the middleware file convention; the project uses `proxy.ts`. The behaviour is
  still `next-intl` middleware: it detects/redirects the locale and rewrites the request.
- The matcher excludes API routes, Next internals, and any path with a file extension
  (favicon, images), so static assets are never locale-redirected.

#### RTL/LTR at the document level (`app/[locale]/layout.tsx`)

```tsx
const direction = locale === 'ar' ? 'rtl' : 'ltr';

return (
  <html lang={locale} dir={direction} className={`${cairo.variable} h-full antialiased`}>
    <body className="min-h-full bg-background font-sans text-foreground">
      <NextIntlClientProvider>
        <Providers locale={locale}>{children}</Providers>
      </NextIntlClientProvider>
    </body>
  </html>
);
```

- `dir` is set on `<html>` once, so the entire tree (including portals) inherits it.
- `lang` is set for screen readers and browser translation.
- The Cairo font variable is attached to `<html>`; Tailwind's `font-sans` picks it up.
- The layout validates the locale with `hasLocale` and calls `notFound()` otherwise, so an
  unsupported locale never renders a half-translated page.
- Metadata sets `openGraph.locale` to `ar_EG` / `en_US` and a canonical `/${locale}`.

#### Logical CSS properties

RTL correctness is not achieved by a global CSS flip. Feature components use **logical**
utilities (`ms-*`, `me-*`, `ps-*`, `pe-*`, `start-*`, `end-*`, `text-start`, `text-end`) and
`rtl:`/`ltr:` variants only where a physical property is unavoidable. There is a dedicated test
(`features/appointments/components/rtl-conventions.test.ts`) that guards this convention, so a
new physical `ml-*`/`left-*` in that feature fails the suite.

#### Translations

- `messages/ar.json` and `messages/en.json` must both contain every key. A missing key throws at
  render time in development and is caught by `messages/messages.test.ts`, which asserts key
  parity and non-empty values.
- No user-facing string is hard-coded in a component; UI text is always `t('...')`.
- Server error text is **not** displayed. Screens translate the machine `code` (or the
  `ApiError.kind`) into a local message (§4.4).

### 4.3 Routing and route tree

```text
frontend/src/app/
├── globals.css
├── favicon.ico
└── [locale]/
    ├── layout.tsx                     # <html lang dir>, Cairo font, providers
    ├── providers.tsx                  # QueryProvider + ToastContainer + dev identity
    ├── page.tsx                       # home
    ├── appointments/
    │   ├── layout.tsx                 # appointments section shell
    │   ├── page.tsx                   # list + filters + dialogs + day schedule
    │   └── new/page.tsx               # booking form
    └── medical-records/
        ├── layout.tsx
        ├── page.tsx                   # workspace landing
        └── visits/
            ├── new/page.tsx           # create visit
            └── [visitId]/page.tsx     # visit detail + diagnoses + medications
```

Rendered URLs:

| URL | Screen |
|---|---|
| `/ar`, `/en` | Home landing |
| `/ar/appointments` | Appointment list, filters, pagination, cancel/reschedule/status dialogs |
| `/ar/appointments/new` | Booking form |
| `/ar/medical-records` | Medical-records workspace |
| `/ar/medical-records/visits/new` | New visit |
| `/ar/medical-records/visits/{id}` | Visit detail (diagnoses, medications) |

**Route-file rule:** page files only compose. Example (the real
`app/[locale]/appointments/page.tsx`):

```tsx
export async function generateMetadata({params}: PageProps): Promise<Metadata> {
  const {locale} = await params;
  const t = await getTranslations({locale, namespace: 'Appointments.meta'});
  return {title: t('listTitle')};
}

export default function AppointmentsRoute() {
  // The screen reads the current clinic time, so it is streamed behind Suspense.
  return (
    <Suspense fallback={<PageFallback />}>
      <AppointmentsPage />
    </Suspense>
  );
}
```

The `Suspense` boundary is required because the screen computes the current clinic time on the
client, and that value must not be baked into a static render. `PageFallback` is exported from the
feature barrel, so even the fallback stays feature-owned. `generateMetadata` is the only
server-side logic in the file: it reads translations for the page title.

**Why `[locale]` at the top:** a single layout owns `<html>`/`<body>` and the direction, and
every route is guaranteed to have a locale. It also means the `proxy.ts` rewrite and the App
Router tree agree by construction.

### 4.4 The API client layer

All HTTP goes through one module: `frontend/src/lib/api/`. It has five parts.

#### 4.4.1 `client.ts` — the only transport

```ts
export async function apiRequest<T>(path: string, options: ApiRequestOptions = {}): Promise<T> {
  const {method = 'GET', query, body, token, signal} = options;
  const url = `${getApiBaseUrl()}${path}${query ? buildQueryString(query) : ''}`;

  const headers: Record<string, string> = {
    Accept: 'application/json',
    ...getAuthHeaders()          // providers are consulted on EVERY request
  };

  if (token) headers.Authorization = `Bearer ${token}`;
  if (body !== undefined) headers['Content-Type'] = 'application/json';

  const response = await fetch(url, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
    signal,
    cache: 'no-store',           // doctor-scoped data must not be HTTP-cached
    credentials: 'omit'          // identity travels in headers, never ambient cookies
  });

  if (!response.ok) {
    const problem = isJsonResponse(response) ? await readJson(response) : undefined;
    throw parseProblem(response.status, problem);
  }

  if (response.status === 204) return undefined as T;
  if (!isJsonResponse(response)) throw createInvalidResponseError(response.status);

  const data = await readJson(response);
  if (data === undefined) throw createInvalidResponseError(response.status);
  return data as T;
}
```

Design points a reviewer will probe:

| Choice | Reason |
|---|---|
| `cache: 'no-store'` | Appointments and medical records are per-doctor and must never be served from a shared HTTP cache. |
| `credentials: 'omit'` | The API authenticates with headers. Sending cookies would create a CSRF surface the backend does not defend. |
| `Accept: application/json` always | Makes the error `Content-Type` negotiation predictable. |
| `AbortError` is rethrown unchanged | React Query recognises aborts and must not convert them into a visible error. |
| A non-JSON success body is an error | A 200 with HTML usually means a proxy/login page; silently returning it would break screens later. |
| 204 returns `undefined` | `DELETE` medication has no body. |
| Network failure → `ApiError` with status `0` | Screens get one error type instead of `TypeError: Failed to fetch`. |
| Raw response body never surfaces | `parseProblem` extracts only known fields; an HTML error page cannot leak into the UI. |

`apiRequestVoid` is the same function for `204` endpoints. `lib/api/index.ts` also re-exports
`apiRequest as request` / `apiRequestVoid as requestVoid` because the medical-records feature
was written with those names; it is the **same** transport, not a second client.

#### 4.4.2 `errors.ts` — one error type

```ts
export type ApiErrorKind =
  | 'network' | 'invalid-response'
  | 'unauthorized' | 'forbidden' | 'not-found'
  | 'conflict' | 'validation' | 'server';
```

`ApiError` carries:

| Field | Use |
|---|---|
| `kind` | Coarse UI branch (network vs. 4xx vs. 5xx) |
| `status` | Numeric HTTP status (0 for network) |
| `code` | Backend machine code, e.g. `appointment.overlap` |
| `fieldErrors` / `fieldErrorCodes` | Per-field validation messages and codes |
| `traceId` | Support correlation; shown in a “reference” line, never the server message |
| `message` / `title` / `detail` | English server text; **kept for logs/debugging, never rendered** |

`kindFromStatus` maps 401/403/404/409/400/5xx to the `kind` union; `parseProblem` reads the
RFC 7807 body defensively (a missing field is `null`, not a crash). `normalizeFieldKey` mirrors
the backend's key normalization, so `EndTime`, `endTime`, and `$.endTime` all land on the same
key and can be matched to a form field.

**Why not display `message`:** the API is English-only and the product is bilingual. Rendering
server English inside an Arabic RTL page is a defect. Screens translate `code`/`kind` instead.

#### 4.4.3 `auth-headers.ts` — the identity seam

```ts
const providers = new Set<AuthHeadersProvider>();

export function registerAuthHeadersProvider(provider: AuthHeadersProvider): () => void { … }
export function getAuthHeaders(): AuthHeaders { /* merge in registration order */ }
```

- There is exactly **one** place where “who is calling” is attached to a request.
- Providers are consulted on every call, so a changed identity is picked up without
  re-registration.
- Later providers win on a shared header name (documented merge order).
- Today two development mechanisms register here:
  - the temporary `X-User-Id` / `X-User-Role` headers (appointments);
  - the development `Authorization: Bearer …` token (medical records, `use-dev-token.ts`).
- When real authentication arrives, it registers one provider. **No screen or hook changes.**
  That is the whole point of the registry.

#### 4.4.4 `query-string.ts` and `paging.ts`

```ts
buildQueryString({pageNumber: 1, status: undefined, search: ''})  // → "?pageNumber=1"
```

- Skips `undefined`, `null`, and empty strings, so “no filter” is genuinely absent from the URL
  instead of being sent as `status=`.
- `URLSearchParams` handles encoding; key order is preserved for readable logs.

```ts
export function readPagedResult<T>(value: unknown, readItem: (item: unknown) => T): PagedResult<T>
```

- Validates the shared envelope (`items`, `page`, `pageSize`, `totalCount`, `totalPages`) and
  each item.
- A malformed envelope throws `invalid-response` rather than producing `undefined` rows.
- `types/paging.ts` owns `DEFAULT_PAGE_SIZE = 10`, `MAX_PAGE_SIZE = 50`, and the `PagedResult<T>`
  shape. Features may not redefine them (Q-04).

#### 4.4.5 Per-feature readers

Each feature narrows its own response shapes at the boundary:

- appointments: `services/appointment-readers.ts` (`readAppointment`, `readAppointmentPage`,
  `readDoctorLookups`, `readPatientLookups`);
- medical records: `guards.ts` (`expectMedicalVisit`, `expectDiagnosis`, `expectMedication`).

This is hand-written because the project has **no schema-validation library** (Q-06/D-29 was not
approved for Zod). The readers live in the feature, not in `lib/api`, because only the feature
knows its contract.

### 4.5 Server state and local state

#### React Query configuration (`lib/query-client/query-client.ts`)

```ts
new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 60_000,
      gcTime: 5 * 60_000,
      retry: 1,
      refetchOnWindowFocus: false
    },
    mutations: {retry: 0}
  }
});
```

- `staleTime` 60 s: appointment lists change rarely enough that a minute of caching is safe and
  removes flicker when navigating back.
- `gcTime` 5 min: keeps an inactive list warm without unbounded memory.
- `refetchOnWindowFocus: false`: clinic staff switch tabs constantly; refetching on every focus
  would create noise and extra load.
- `retry: 1` at the client level; features narrow it further (below).
- Mutations never retry: a booking is not idempotent, and a blind retry could double-book.

The client is created **once per browser session** via `useState(createQueryClient)` in
`QueryProvider`. Creating it at module scope would share cache between requests on the server.

#### Query keys are scoped by actor

```ts
export const appointmentKeys = {
  all: (actor: string) => ['appointments', actor] as const,
  list: (actor: string, filters: AppointmentFilters) =>
    [...appointmentKeys.all(actor), 'list', filters] as const,
  day: (actor: string, doctorId: string, date: string) =>
    [...appointmentKeys.all(actor), 'day', doctorId, date] as const,
  doctors: (actor: string) => [...appointmentKeys.all(actor), 'lookups', 'doctors'] as const,
  patients: (actor: string, doctorId: string, search: string) =>
    [...appointmentKeys.all(actor), 'lookups', 'patients', doctorId, search] as const
};
```

**Why the actor is part of the key:** today an evaluator can switch between a doctor and a
secretary in the dev identity dialog. If the key were only `['appointments', filters]`, React
Query would happily serve the previous user's cached rows to the new user for up to `staleTime`.
Including the actor id makes cross-identity leakage structurally impossible. The same reasoning
applies when real sign-in arrives.

`medicalRecordKeys` is deliberately **not** actor-scoped: medical screens only run with a
development bearer token and the route itself is doctor-only. The key is
`['medical-records', 'visit', visitId]`.

#### `keepPreviousData` and pagination

The list hook uses `placeholderData: keepPreviousData`, so changing page or filter keeps the
previous rows visible while the next page loads instead of collapsing to a spinner. Combined with
the `Pagination` primitive (which shows the current page and total pages from the envelope), the
table never jumps.

#### Retry policy is error-aware

```ts
function retryOnce(failureCount: number, error: unknown): boolean {
  return isRetryableError(error) && failureCount < 1;
}
```

- `isRetryableError` (in `services/appointment-errors.ts`) is true only for `network` and
  `server` failures.
- A 400/403/404/409 is never retried: retrying a validation failure or an overlap conflict just
  repeats the same answer.
- This is why the global `retry: 1` is not enough on its own.

#### Local state

- `useState` for dialogs, form fields, and the selected day.
- `useAppointmentFilters` keeps filters in **local React state** (the same approach the payments
  feature uses). Its default view is “today onward” in the clinic time zone, computed once per
  mount so the default does not shift while the page is open. Every filter change resets
  `pageNumber` to 1, and a malformed or half-typed date is not sent to the API.
- `useDebouncedValue(value, 300)` for the patient search, so typing does not fire a request per
  keystroke.
- Zustand is used **only** for `isSidebarOpen`. Business state is never global.

Trade-off worth stating: because filters are local, a filtered view is **not** shareable by URL
and is lost on reload. URL-backed filters would be a small enhancement, but they are not in the
approved scope and would change the page's Suspense behaviour; the current choice is documented
rather than implied.

#### Mutation → invalidation

After a successful mutation the feature invalidates the smallest correct key:

- booking/reschedule/cancel/status → invalidate the list root and the day schedule for that
  doctor/date;
- adding/updating/deleting a diagnosis or medication → invalidate the **visit** key, because the
  visit response already contains its diagnoses and medications (D-24). There is no separate
  child cache to keep in sync.

This avoids both stale UI and a cache-invalidation web.

### 4.6 Feature modules in detail

#### 4.6.1 Appointments (F1) — fully wired

```text
features/appointments/
├── index.ts                    # public barrel
├── components/
│   ├── AppointmentsPage.tsx    # screen: filters, table, dialogs, pagination
│   ├── AppointmentsTable.tsx
│   ├── AppointmentFilters.tsx
│   ├── AppointmentStatusBadge.tsx
│   ├── AppointmentSummary.tsx
│   ├── AppointmentsAppBar.tsx
│   ├── BookingForm.tsx
│   ├── RescheduleDialog.tsx
│   ├── CancelDialog.tsx
│   ├── StatusDialog.tsx
│   ├── DaySchedulePanel.tsx
│   ├── PatientCombobox.tsx
│   ├── IdentityRequired.tsx     # “pick an identity first” guard
│   ├── PageFallback.tsx
│   └── primitives/              # Button, Dialog, Field, Alert, Skeleton, Pagination
├── hooks/
│   ├── useAppointments.ts       # actor, list, day schedule, keys, retry
│   ├── useAppointmentFilters.ts # URL-backed filters
│   ├── useLookups.ts            # doctors + debounced patient search
│   └── useAppointmentMessages.ts
├── services/
│   ├── appointments.api.ts      # 8 endpoints
│   ├── appointment-readers.ts   # boundary narrowing
│   └── appointment-errors.ts    # retryability + error → translation key
├── types/appointment.types.ts
├── utils/{date-time.ts,status-actions.ts}
└── validation/appointment.validation.ts
```

Screen behaviour:

1. If no acting identity is available, `IdentityRequired` is rendered and **no request is sent**
   (`enabled: ready` on every query). This is a UX guard, not a security control.
2. The list query sends `pageNumber`, `pageSize`, `doctorId`, `status`, `dateFrom`, `dateTo`.
3. The table renders status via `AppointmentStatusBadge`, and the available row actions come from
   `utils/status-actions.ts`, which encodes the role/status matrix (e.g. a secretary never sees
   “Mark completed”).
4. Dialogs are controlled by local state and call the matching mutation; server field errors are
   mapped back onto the form, and business codes (`appointment.overlap`,
   `appointment.in_past`, …) become localized messages.
5. `DaySchedulePanel` shows the selected doctor/date timeline (bounded to 50 rows by the shared
   page-size cap — a documented limitation, §8).

The form validation (`validation/appointment.validation.ts`) mirrors request shape only:
required patient/doctor/date, `HH:mm` time format, end after start. Overlap and “in the past” are
**not** computed in the browser; the server is the authority and its code is displayed.

#### 4.6.2 Medical records (F4) — fully wired

```text
features/medical-records/
├── api.ts                     # 8 endpoints against /api/medical-visits
├── guards.ts                  # response narrowing
├── keys.ts                    # query keys
├── types.ts
├── validation.ts              # request-shape validation + shared limits
├── errors.ts                  # ApiError → translation-key segment
├── use-dev-token.ts           # sessionStorage token bridge + auth provider
├── hooks/
│   ├── use-medical-visit.ts
│   ├── use-visit-mutations.ts
│   ├── use-diagnosis-mutations.ts
│   └── use-medication-mutations.ts
└── components/
    ├── MedicalRecordsWorkspace.tsx
    ├── MedicalVisitScreen.tsx
    ├── NewVisitScreen.tsx
    ├── VisitForm.tsx / VisitDetails.tsx
    ├── DiagnosisForm.tsx / DiagnosisSection.tsx
    ├── MedicationForm.tsx / MedicationSection.tsx
    ├── DevTokenPanel.tsx
    ├── AsyncState.tsx / MutationAlert.tsx
    ├── ConfirmDeleteDialog.tsx
    └── FormField.tsx / form-styles.ts
```

- Every call passes an explicit `token` taken from `useDevToken()`. `use-dev-token.ts` also
  registers an `Authorization` provider with the shared transport, so any request that omits the
  explicit option still carries the bearer credential. When both are present they set the same
  header, and the explicit `token` wins because `apiRequest` assigns it after merging the
  provider headers.
- The token lives in `sessionStorage` under `medilog.dev-token`: one tab, cleared on close, never
  logged, never rendered, never sent anywhere except the Medilog API.
- `DevTokenPanel` is a development affordance for pasting a `dotnet user-jwts create` token. It
  is not an authentication implementation.
- `errors.ts` maps `ApiError.kind` to a `QueryFailureKind`/`MutationFailureKind`, which is used
  as a translation-key segment. No server text is rendered.
- After a child mutation the **visit** query is invalidated, so the screen re-reads the visit with
  its children in one request.

#### 4.6.3 Auth (temporary dev identity)

```text
features/auth/
├── dev-identity/dev-identity.ts   # storage + external store + header provider
├── hooks/useActingIdentity.ts     # loading / none / ready
├── components/DevIdentityChip.tsx # “Acting as …” dialog
└── index.ts
```

`dev-identity.ts` is a small external store built on `useSyncExternalStore`:

- `getDevIdentity()` returns a **referentially stable** object while nothing changes, so it is a
  valid snapshot (returning a fresh object would cause an infinite render loop).
- Identity is read from `localStorage`, else from `NEXT_PUBLIC_DEV_USER_ID` /
  `NEXT_PUBLIC_DEV_USER_ROLE`, else `null`.
- Values are validated: the id must be a hyphenated GUID (not the empty GUID), and the role must
  be exactly `Doctor` or `Secretary`.
- `isDevIdentityEnabled()` requires a non-production build **and**
  `NEXT_PUBLIC_DEV_IDENTITY === 'true'`. The API independently refuses the headers outside
  Development.
- `devAuthHeadersProvider()` returns `{}` when disabled, so a production build sends no identity
  headers at all.
- Storage failures (private mode) degrade to “no identity” rather than throwing.

`useActingIdentity()` distinguishes three states, which matters for server rendering:

| State | Meaning | UI behaviour |
|---|---|---|
| `loading` | Not hydrated yet (server render / first client render) | Show a skeleton, send no request |
| `none` | Hydrated, but no identity | `IdentityRequired` |
| `ready` | Identity available | Normal screens |

This avoids the hydration mismatch that would occur if the server rendered “no identity” and the
client immediately rendered a user.

**Remove this module when Q-07 delivers real authentication.** It is isolated on purpose so that
removal is a small, mechanical change.

#### 4.6.4 Payments (F2) — mock-only by design

```text
features/payments/
├── components/aymentStatusBadge.tsx   # (sic) existing filename
├── hooks/usePayments.ts
├── hooks/usePaymentFilters.ts
├── services/payments.service.ts       # in-memory implementation
├── services/payments.mock.ts          # synthetic rows
└── types/payment.types.ts
```

The service **looks like** a real API module: async functions, simulated latency (400 ms),
typed error codes (`NETWORK`, `INVALID_INPUT`, `APPOINTMENT_NOT_FOUND`,
`APPOINTMENT_ALREADY_PAID`, `PAYMENT_NOT_FOUND`), and the shared paged envelope with
`page`/`pageSize`/`totalCount`/`totalPages`, clamped to `MAX_PAGE_SIZE`.

That is deliberate: when Q-01 is answered, the implementation is replaced with `apiRequest` calls
and the hooks/screens do not change shape. The mock is **not** a claim about payment rules; the
rules it enforces (one payment per appointment, `paid` status, amount range) are placeholders
that must be re-approved, not treated as requirements.

`NEXT_PUBLIC_PAYMENTS_MOCK_FAIL=true` forces the network-failure path so the error UI can be
demonstrated. No backend payment endpoint exists.

#### 4.6.5 Placeholders

`features/dashboard`, `features/doctors`, `features/patients`, `features/medical-timeline` exist
as folders with no production screens. They are placeholders for F3/F5 and contain no invented
rules, no hard-coded statistics, and no fake clinical data.

### 4.7 Forms, validation, accessibility, and styling

#### Forms

- Booking, visit, diagnosis, and medication forms are controlled components with explicit
  `useState` per field.
- Submit is disabled while a mutation is pending, which prevents double-submit.
- On failure, `MutationAlert` shows a localized message; validation failures are mapped onto the
  matching fields using `ApiError.fieldErrors` (already normalized to camelCase keys).
- Success is reported with a toast and the relevant query is invalidated.

#### Client validation

`features/appointments/validation/appointment.validation.ts` and
`features/medical-records/validation.ts` implement **shape** rules only (required, max length,
time format, end-after-start). They exist for fast feedback. They are not a security boundary,
and the API re-validates every request (AGENTS.md §3.2).

#### Accessibility

- Inputs use `<label>` via the `Field`/`FormField` wrappers, so a label is associated with its
  control.
- Dialogs are built on the native `<dialog>` element with `showModal()`: the browser provides
  the focus trap, Escape handling, inert background, and focus return. The primitive wires
  `aria-labelledby`/`aria-describedby` to a real heading and description, and a backdrop click
  closes it.
- Tables use semantic `<table>`, `<th scope="col">`, and a caption or heading.
- Status is not conveyed by colour alone: the badge always contains text.
- Loading and empty states are announced through text, not just spinners.
- Buttons are real `<button>` elements; links that navigate are `<Link>`.

#### Styling

- Tailwind CSS 4 utilities, no separate CSS-in-JS system.
- A small set of feature-local primitives (`Button`, `Dialog`, `Field`, `Alert`, `Skeleton`,
  `Pagination`) keeps the look consistent without pulling in a component library.
- RTL-aware utilities as described in §4.2.
- `react-toastify` is positioned `top-left` in RTL and `top-right` in LTR, with `rtl` set, so
  notifications do not cover the wrong side of the screen.

### 4.8 Frontend testing

| Test file | What it protects |
|---|---|
| `lib/api/api.test.ts` | Transport: URL building, headers, error parsing, 204, network failure, abort |
| `lib/api/paging.test.ts` | Shared envelope reader and limits |
| `features/appointments/components/AppointmentsPage.test.tsx` | Screen: identity guard, list, pagination, dialogs, error states |
| `AppointmentsTable.test.tsx` | Row rendering, status badge, action visibility |
| `BookingForm.test.tsx` | Form validation, submit payload, server field errors |
| `dialogs.test.tsx` | Reschedule/cancel/status behaviour |
| `rtl-conventions.test.ts` | No physical-direction utilities in the feature |
| `services/appointment-errors.test.ts` | Retryability and error-code mapping |
| `services/appointment-readers.test.ts` | Boundary narrowing rejects malformed responses |
| `utils/date-time.test.ts` | Time normalization and formatting |
| `utils/status-actions.test.ts` | Role/status → allowed actions matrix |
| `validation/appointment.validation.test.ts` | Shape rules |
| `features/auth/components/DevIdentityChip.test.tsx` | Identity dialog |
| `features/auth/dev-identity/dev-identity.test.ts` | Storage, validation, stable snapshot, provider output |
| `messages/messages.test.ts` | ar/en key parity and non-empty values |

Tooling: Vitest + jsdom + Testing Library + `user-event`. Tests render components with a
per-test `QueryClient` and a mocked `fetch`, so they exercise the real transport and the real
readers instead of stubbing them out.

**Last observed run:** 288 tests in 15 files, all passing; `tsc --noEmit` clean; `eslint` clean;
`next build` produced 21 static pages.

---

## 5. Shared conventions

These are the cross-feature contracts. They are defined once in
`backend/docs/api/CONVENTIONS.md`; this section restates them so a reviewer does not have to
cross-reference. Features must not define competing versions (`AGENTS.md` §3.1, §4.4).

### 5.1 Pagination and filtering

#### Request

```http
GET /api/<resource>?<filters>&pageNumber=1&pageSize=10
```

| Parameter | Type | Rule |
|---|---|---|
| `pageNumber` | integer | `>= 1`; default `1` |
| `pageSize` | integer | `1..50`; default `10` |
| `<filter>` | feature-defined | optional camelCase domain field |

Rules:

- Filters combine with `AND` and are applied **at the data source**. The API never loads an
  unbounded set and filters it in memory, and the browser never receives an unfiltered dataset to
  filter locally.
- An omitted or empty filter means “no filter”. The UI sentinel `"all"` is never sent; the
  frontend converts it to `undefined` (see `listAppointments`).
- Date ranges are inclusive.
- Invalid values produce `400` with `code = validation.failed` and camelCase `errors`.
- Limits live in one place: `PagingDefaults` (backend) and `types/paging.ts` (frontend).
  `MaxPageSize = 50` is a hard cap, not a suggestion.

#### Response

```json
{
  "items": [],
  "page": 1,
  "pageSize": 10,
  "totalCount": 0,
  "totalPages": 0
}
```

- The **request** field is `pageNumber`; the **response** field is `page`. This asymmetry is
  intentional and was explicitly confirmed in Q-04 (the alternative would have been to change
  the already-published request contract).
- `totalPages = ceil(totalCount / pageSize)`, and `0` when there are no rows.
- A `pageNumber` beyond the last page returns an empty `items` array with the correct counts
  (not a 404).
- `totalPages` is computed, not stored, so it can never disagree with `totalCount`/`pageSize`.

#### Ordering

- Fixed per endpoint and deterministic: primary sort field **plus** a stable tie-breaker
  (normally `id`).
- F1: `appointmentDate ASC, startTime ASC, id ASC`.
- Why the tie-breaker: without it, two rows with the same date/time could swap between pages,
  causing a row to appear twice or be skipped. The tie-breaker is a correctness requirement, not
  a nicety.
- User-supplied sorting is out of scope (`CONVENTIONS.md` §7).

#### Where it is implemented

| Layer | Type |
|---|---|
| Application | `PagedResult<T>`, `PagingDefaults`, `IPagedQuery`, `PagedQueryValidator<T>` |
| Infrastructure | SQL `Where`/`OrderBy`/`Skip`/`Take`/`CountAsync` in `AppointmentStore` |
| Frontend | `types/paging.ts`, `lib/api/paging.ts` (`readPagedResult`), `Pagination` primitive |
| Tests | `PagingContractTests` (backend), `lib/api/paging.test.ts` (frontend) |

### 5.2 Error contract

Every error — application exception, model-binding failure, or framework-generated response —
uses **one** RFC 7807 `application/problem+json` envelope.

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "instance": "/api/appointments",
  "code": "validation.failed",
  "traceId": "00-…",
  "errors": {"patientId": ["A patient identifier is required."]},
  "errorCodes": {"patientId": ["NotEmptyValidator"]}
}
```

| Member | Always | Meaning |
|---|---|---|
| `type` | yes | Problem class URI |
| `title` | yes | Short English summary |
| `status` | yes | Mirrors the HTTP status |
| `instance` | yes | Request path |
| `code` | yes | Stable machine identifier |
| `traceId` | yes | Log correlation |
| `detail` | sometimes | English explanation; never internals |
| `errors` | validation only | camelCase field → messages |
| `errorCodes` | validation only | camelCase field → validator codes |

Rules:

- Clients switch on `status`, `code`, and `errorCodes`; they **never** display `title`/`detail`
  as localized UI text.
- Field keys are always camelCase. `ApiProblem.NormalizeFieldKey` strips JSON-path prefixes
  (`$.status` → `status`) and lower-cases the first letter (`EndTime` → `endTime`), so
  FluentValidation, model binding, and the frontend agree.
- Codes are **append-only**: never rename an existing code, add a new one.
- A `500` never contains the exception message, stack trace, SQL, or patient data.

| Status | Default `code` |
|---|---|
| 400 (validation) | `validation.failed` |
| 400 (binding) | `request.invalid` |
| 401 | `auth.unauthorized` |
| 403 | `access.forbidden` or a domain code |
| 404 | `resource.not_found` or a domain code |
| 405 | `request.method_not_allowed` |
| 409 | `resource.conflict` or a domain code |
| 415 | `request.unsupported_media_type` |
| 500 | `server.error` |

Feature/domain codes (`appointment.overlap`, `doctor.unavailable`, `identity.invalid`, …) are
documented in the feature contract and declared in `DomainErrorCodes`/`ErrorCodes`.

### 5.3 Identity contract

- The acting user and role come from the **server-side** principal. A client-supplied user id,
  doctor id, secretary id, role, or ownership field is never trusted (`AGENTS.md` §5).
- Doctor-facing data is scoped to the authenticated doctor; secretary-facing data is scoped to
  the secretary's assigned doctors.
- An out-of-scope resource behaves like a missing one (`404`) unless the caller is not allowed to
  use the endpoint at all (`403`). This prevents existence disclosure.
- The frontend identity seam is `lib/api/auth-headers.ts`; screens never construct auth headers
  themselves.
- The temporary development mechanisms (`X-User-Id`/`X-User-Role`, `Authorization: Bearer` from
  the dev token panel) are **development-only** and rejected by the API outside Development.
  They are documented as forgeable and must be removed when Q-07 lands.

### 5.4 Coding and naming conventions

| Area | Convention |
|---|---|
| C# namespaces | `Medilog.<Layer>.<Area>`; features under `Features.<Name>` |
| Async methods | `Async` suffix; `CancellationToken` threaded through |
| DTO naming | `*Request` / `*Response` / `*Query`; contracts live in the feature folder |
| JSON | camelCase (default); enums as strings; `DateOnly` `yyyy-MM-dd`; `TimeOnly` `HH:mm:ss` |
| Database | snake_case tables/columns; `ix_`/`uq_`/`fk_`/`ck_`/`ak_` prefixes |
| TypeScript | strict, no `any`; `unknown` + narrowing at boundaries |
| React | function components; `'use client'` only where needed |
| Files | one public type/component per file; barrels for feature public surface |
| Translation keys | grouped by feature; ar/en parity enforced by a test |
| Commits / branches | one reviewed Cycle per commit; never commit `bin/`, `obj/`, `.next/`, `node_modules/`, secrets |

---

## 6. Decision register

The full, dated log lives in `STATE.md` §7. This table is the engineering summary a reviewer
needs to understand *why* the code looks the way it does.

| ID | Decision | Consequence in the code |
|---|---|---|
| **D-08** | Plain application services; **MediatR not adopted** | `AppointmentService` / `MedicalVisitService` are injected directly; no mediator, no handlers, no pipeline behaviours |
| **D-09** | Application depends on `IApplicationDbContext` + `DbSet<T>`; no repository abstraction | `MedicalVisitService` composes LINQ; `MedilogDbContext` implements the interface (D-21 registers it) |
| **D-10** | Typed application exceptions mapped to `ProblemDetails` | `NotFoundException`, `ForbiddenException`, `ConflictException`, `RequestValidationException` + one handler |
| **D-11** | `ICurrentUser`; the acting user comes from server-side identity, never the request | `RequireActorAsync` / `RequireActingDoctorId` in every use case |
| **D-12** | No diagnosis delete | Only `UpdateDiagnosis` exists; no `DELETE /diagnoses/{id}` |
| **D-13** | xUnit project `Medilog.Application.Tests` | Domain + service tests without a database |
| **D-14** | Request text limits + whitespace→null normalization | `MedicalRecordText.Normalize`, length validators |
| **D-15** | EF Core InMemory for service tests; `Relational` pinned to 10.0.14 | One EF version across projects |
| **D-16** | JWT Bearer validation; no login endpoint; dev tokens via `dotnet user-jwts` (`sub`, `role`) | `AddJwtBearer()`; `CurrentUser` reads `sub`/`role`; frontend token panel |
| **D-17** | Single-owner patient workspace (`Patient.DoctorId == actingDoctorId`) | `MedicalRecordAccessPolicy.CanAccessPatient`; no schema change |
| **D-18** | MVC controllers; no Minimal APIs, no Result library | `Controllers/*.cs` |
| **D-19** | `Medilog.WebAPI.Tests` with `WebApplicationFactory` + `TestAuthHandler` | HTTP-level contract tests |
| **D-20** | `201 Created` location targets the parent visit for child resources | `CreatedAtRoute(MedicalVisits.GetVisit, …)` |
| **D-21** | `AddInfrastructure` registers `IApplicationDbContext` → `MedilogDbContext` | Medical-records DI works |
| **D-22** | F4 keeps `ClinicalNotes` as implemented | No schema change |
| **D-23** | Medication delete is a **hard delete**, scoped to the owning doctor | `DELETE …/medications/{id}` → 204; retention risk flagged for ratification |
| **D-24** | `MedicalVisitResponse` includes `Medications` (name, id) | No separate medication list route; child mutations invalidate the visit key |
| **D-25** | Medication use cases live on `IMedicalVisitService` | No new service |
| **D-26** | Medication request limits (200/100/4000) | Validators |
| **D-27** | One native-`fetch` wrapper in `frontend/src/lib/api`; no Axios | `lib/api/client.ts` |
| **D-28** | Development-only JWT bridge (`sessionStorage`, `Authorization: Bearer`) | `use-dev-token.ts` + `DevTokenPanel` |
| **D-29** | TanStack Query for server state; React state for forms; no Zustand for server data, no Zod | Hooks + `useState`; hand-written guards |
| **D-30** | F4 routes: `/medical-records`, `/visits/new?patientId=`, `/visits/[visitId]` | Route tree in §4.3 |
| **D-31** | Discovery bridge: “open by id” form + one localized Header link | No business logic in shared components |
| **F1-C1** | Cancelled/No-Show free the slot; `doctor_secretaries` enforced; `EndTime` required; no default duration; past bookings rejected; `Africa/Cairo`; `CancellationReason` mandatory ≤ 500 | Domain + service + validators |
| **F1-C2** | `IAppointmentStore` port; typed exceptions; per-doctor `FOR UPDATE`; out-of-scope → 404; inactive doctors not bookable; Completed = doctor only | `AppointmentStore`, `AppointmentService` |
| **F1-C3** | No `[Authorize]` until Q-07; temporary header identity; routes; string enums; new WebAPI test project | `CurrentUser` fallback, `AppointmentsController` |
| **F1-C3b/C4** | Read-only lookups; machine-readable codes; synthetic seed; fetch-based `lib/api`; Vitest + Testing Library + jsdom approved | `LookupAndErrorCode*` tests, `features/appointments` |
| **Q-04 (2026-11-13)** | `pageNumber`/`pageSize`, default 10, max 50; `PagedResult<T>` with `page`; deterministic ordering | `PagingDefaults`, `PagedResult<T>`, frontend `types/paging.ts` |
| **Phase 1 (2026-11-13)** | Response `page` vs request `pageNumber`; F4 validation through one `ValidateAsync`; framework responses get the envelope via `UseStatusCodePages`; Cycle-by-cycle | `RequestValidation`, `ApiProblem`, `Program.cs` |
| **Frontend source of truth** | `frontend/src`, not `Front_End/`; CORS origin `http://localhost:3000` | Actual repo layout |

**Supersession note:** `STATE.md` Q-11 (“no frontend test runner; deferred”) was superseded by
the F1-C4 decision that approved Vitest + Testing Library + jsdom. The runner is installed and
288 tests pass. The Q-11 row remains in the log as history; the effective state is the F1-C4
decision.

---

## 7. Senior-engineer Q&A

Each answer is written the way it should be given in a review: state the decision, then the
reason, then the trade-off.

---

### Architecture

**Q1. Why four projects instead of one Web API project?**

Because the rules that matter most are the ones that must survive a change of transport or
storage. `Appointment` and its interval/state rules are pure C# with no EF Core reference, so
they are tested in milliseconds and cannot be broken by a controller change. The project
reference graph enforces the direction: Domain references nothing, Application references
Domain, Infrastructure references both, WebAPI composes. A single project would let a controller
call `DbContext` directly; here that would be a visible, reviewable dependency violation.

Trade-off: more files and more project references. For six contributors working in parallel on
separate features, the boundaries reduce merge conflicts and make ownership obvious.

**Q2. Isn't `IApplicationDbContext` just a leaky repository?**

It is an abstraction over the unit of work, not a repository. It exposes `DbSet<T>` and
`SaveChangesAsync`, which keeps query composition in the use case (where the business question
is) while hiding the concrete context, provider, and connection. The Application project never
sees `MedilogDbContext` or MySQL. The alternative — a repository per entity — would either
duplicate LINQ or force dozens of narrowly-shaped methods, which is exactly the ceremony
`AGENTS.md` §4.1 warns against.

**Q3. Why do F1 and F4 use different persistence styles?**

They were built in different Cycles with different needs. F1 needed strict control over a
`FOR UPDATE` transaction and wanted its tests to avoid EF entirely, so it uses the plain-record
`IAppointmentStore` port. F4 needed `Include` and nested projection, so it uses
`IApplicationDbContext`. Both obey the same layer rule. Converging them is a refactor with no
current requirement; doing it now would be an unapproved architectural change. It is recorded as
a known inconsistency rather than hidden.

**Q4. Why no MediatR/CQRS?**

There is no cross-cutting pipeline behaviour that MediatR would centralize better than the
existing middleware and exception handler, and no requirement for command/query separation. A
controller → service → store stack is directly debuggable. `AGENTS.md` §4.1 explicitly says not
to introduce MediatR without approval; it has not been approved.

**Q5. How would you add a new feature without breaking this?**

Add a feature folder in Application (`Features/<Name>`) with request/response contracts,
validators, a service interface and implementation; add an entity configuration in
Infrastructure if new persistence is needed; add a thin controller; add a feature folder in
`frontend/src/features` with its own api/readers/hooks/components and a barrel; add ar/en keys;
add tests at each layer. No shared type needs to change unless the contract itself changes.

---

### Domain and business rules

**Q6. Why is the overlap check `newStart < existingEnd && newEnd > existingStart`?**

Because it is the only formulation that handles all the required cases with one expression:

| Case | Example | Result |
|---|---|---|
| Adjacent | 09:00–10:00 vs 10:00–11:00 | no overlap (`10:00 < 10:00` false) |
| Partial | 09:00–10:00 vs 09:30–10:30 | overlap |
| Containment | 09:00–11:00 vs 09:30–10:00 | overlap |
| Reverse containment | 09:30–10:00 vs 09:00–11:00 | overlap |
| Identical | 09:00–10:00 vs 09:00–10:00 | overlap |
| Disjoint | 09:00–10:00 vs 11:00–12:00 | no overlap |

“Start is inside the other interval” fails containment; “any endpoint inside” fails identical
intervals. Zero/negative intervals are rejected before this check, so it never sees `end <= start`.

**Q7. Why is there no database constraint for overlap?**

Overlap is an interval predicate over two rows, not an equality. A unique index can express “one
row per (doctor, date, start time)” but not “no intersecting ranges”. MySQL 8 has no exclusion
constraint. The correct options are a serializable transaction, an application lock, or a
per-doctor row lock. The project chose `SELECT … FOR UPDATE` on the doctor row because it is the
smallest mechanism that is actually correct and still allows different doctors to book
concurrently.

**Q8. What happens when two secretaries book the same slot at the same time?**

Both begin a transaction. The first obtains the row lock on the doctor; the second blocks. The
first inserts and commits; the second's subsequent read sees the committed appointment, the
domain overlap check fails, and it returns `409 appointment.overlap`. If the wait times out or a
deadlock occurs, MySQL 1205/1213 are translated to `409 schedule.busy`. No double booking is
possible.

**Q9. Why does cancelling have its own endpoint instead of `PUT /status {status: "Cancelled"}`?**

Because cancellation requires a reason, and a generic status update has no place to carry one.
`Cancel(reason)` enforces a non-empty reason and records it. The status route explicitly rejects
`Cancelled` with `appointment.use_cancel_operation`, so there is exactly one way to reach that
state.

**Q10. Why can a secretary not mark an appointment `Completed`?**

`Completed` asserts that the consultation happened, which is a clinical fact only the doctor can
confirm. The service enforces this (`appointment.complete_requires_doctor`, 403) even though the
frontend also hides the action — defence in depth, and the UI is not a security boundary.

**Q11. Why is `CreatedBy` not a foreign key?**

The class specification models it as polymorphic (doctor **or** secretary). MySQL cannot enforce
one FK against two tables. Options were: two nullable FKs plus a check constraint, a discriminator
column, or no FK. The team chose no FK and documented it; referential integrity for this column
is the application's responsibility because the value always comes from the authenticated actor.
It is an accepted schema limitation, not an oversight.

**Q12. Why does a patient have a nullable `DoctorId` if the workspace is single-owner?**

The schema allows a patient to exist before assignment (administrative intake). The **access**
rule does not: `MedicalRecordAccessPolicy.CanAccessPatient` returns false for an unowned patient,
so an unassigned patient is inaccessible to every doctor until assigned. This satisfies D-17
without a schema change.

**Q13. Why store enum labels instead of integers?**

Readability in SQL and check constraints, and immunity to enum reordering. The converters are
explicit two-way switches that throw on unknown values, so a corrupt row fails loudly. The
trade-off is slightly larger storage and the need to keep the label maps in sync with the domain
enums — which the check constraints make visible.

**Q14. Why `Restrict` everywhere?**

Because medical and financial history must not vanish because a parent row was deleted. `Restrict`
turns an accidental cascade into a loud failure and forces a deliberate business decision. The
only `Cascade` is `doctor_secretaries`, a pure join table with no independent meaning.

**Q15. Why the composite foreign keys on diagnoses/medications?**

`diagnoses.(visit_id, patient_id) → medical_visits.(id, patient_id)` makes it impossible for a
diagnosis to claim a patient different from its visit's patient. This is enforced by the database,
not only by code, so a future bug or a manual SQL insert cannot corrupt the record. It requires
`medical_visits` to declare `(id, patient_id)` as an alternate key. The same pattern links a visit
to an appointment with `(appointment_id, patient_id, doctor_id)`.

---

### Persistence

**Q16. Why `char(36)` GUIDs and not `binary(16)` or auto-increment?**

GUIDs are generated client-side, so the application knows the id before insert (needed for
`Location` headers and response mapping) and ids are not guessable across tenants. `char(36)` is
the provider-friendly hyphenated form, avoiding binary/string conversion surprises in tests and
raw SQL. The storage/index cost is accepted at this scale; `binary(16)` would be the optimization
if the dataset grew.

**Q17. How are timestamps handled?**

`CreatedAtUtc`/`UpdatedAtUtc` are `DateTimeOffset` in the domain and stored as UTC `datetime(6)`
via a converter. `MedilogDbContext.ApplyAuditRules` sets them centrally on `SaveChanges`, and
marks `CreatedAtUtc` as not-modified on update. Clients cannot supply them. Appointment
`Date`/`StartTime`/`EndTime` are clinic wall-clock values, not instants, which is why they use
`DateOnly`/`TimeOnly` and a clinic clock rather than UTC conversion.

**Q18. Why is the migration not applied?**

No MySQL server, Docker, or WSL is available in the development environment used to build these
Cycles. The migration was generated and structurally reviewed (tables, keys, indexes, FKs match
the configurations) but never executed. A real round-trip smoke test on a machine with MySQL is
an explicit remaining task (§8).

**Q19. How does the app behave with no connection string?**

`AddInfrastructure` throws a clear `InvalidOperationException` at startup. `appsettings.json`
ships with an empty connection string on purpose so no local credentials are committed; the real
value comes from user secrets or `MEDILOG_CONNECTION_STRING`. The design-time factory uses a
placeholder so `dotnet ef` works without a server.

**Q20. Why is `utf8mb4` applied to every entity?**

Arabic text and emoji require 4-byte UTF-8. `utf8mb4_0900_ai_ci` also gives accent- and
case-insensitive comparisons, which matches how names are searched. Applying it centrally in
`OnModelCreating` means a new entity cannot forget it.

---

### API, errors, and identity

**Q21. Why one error envelope, and how do framework errors get it?**

Because a client should not need two parsers. Application exceptions go through
`GlobalExceptionHandler`; model-binding failures go through
`InvalidModelStateResponseFactory`; framework-generated responses (401 challenge, unmatched-route
404, 405, 415) have no body, so `UseStatusCodePages` fills them in with `ApiProblem`. All three
paths call the same `ApiProblem` builder, which is what guarantees identical members including
`code` and `traceId`.

**Q22. Why `SuppressMapClientErrors = true`?**

Otherwise MVC writes its own `ProblemDetails` for some client errors, without `code` or
`traceId`, and the envelope would be inconsistent. Suppressing it lets `UseStatusCodePages` be
the single writer.

**Q23. Why does `AppointmentsController` have no `[Authorize]`?**

Authentication is out of the approved current scope (Q-07). The controller has an explicit TODO,
and the service still enforces identity and role via `ICurrentUser`. The honest consequence is
that an unauthenticated call is rejected by the service with `403 identity.invalid` rather than
by the framework with `401`. Adding the attribute is the Q-07 follow-up; it is a one-line change
plus tests.

**Q24. Is the development header identity a security hole?**

It is a hole by construction, and that is why it is fenced on both sides: the API honours the
headers only when `Auth:AllowHeaderIdentity` is true (set only in
`appsettings.Development.json`), and the frontend sends them only in a non-production build with
`NEXT_PUBLIC_DEV_IDENTITY=true`. Header values are never logged. It exists so F1/F4 can be built
and demonstrated before authentication is delivered, and it is documented as forgeable.

**Q25. How do you stop a secretary from reading medical data?**

Three independent barriers: there is no secretary-reachable route to any medical entity;
`MedicalVisitsController` is `[Authorize(Roles = Doctor)]`; and the service re-checks the acting
role and patient ownership. The tests assert both the framework 403/401 and the service-level
behaviour, so removing one barrier does not silently open the door.

**Q26. How is "not found" vs "forbidden" decided?**

If the caller may not use the endpoint at all (wrong role), it is `403`. If the caller may use the
endpoint but the specific resource is outside their scope, it is `404`, so the API does not
disclose that another doctor's record exists. Both behaviours are tested.

---

### Frontend

**Q27. Why is the locale prefix always present?**

`localePrefix: 'always'` removes ambiguity: every URL states its language, shared links keep the
language, and there is no bare URL that must guess. Arabic is the default because it is the
primary user language. `Link`/`useRouter` come from `i18n/navigation` so the prefix is never
dropped by accident.

**Q28. Why is the actor part of the React Query key?**

Because identity can change at runtime (today via the dev identity dialog, later via sign-in). If
the key were only `['appointments', filters]`, React Query would serve the previous user's cached
rows to the new user for up to `staleTime`. Scoping the key by actor id makes cross-identity
leakage structurally impossible, not just unlikely.

**Q29. Why a provider registry for auth headers?**

So there is exactly one place where “who is calling” is attached to a request, and exactly one
place to change when real authentication lands. Two temporary mechanisms register today (headers
for appointments, bearer token for medical records); the registry merges them and lets the
explicit `token` option win. No screen or hook needs to know which mechanism is active.

**Q30. Why hand-written response readers instead of Zod?**

Zod is not installed and adding it is a shared dependency requiring approval (Q-06/D-29 was not
ratified for a schema library). The readers are small, explicit, and live at the boundary, so a
malformed response fails loudly with `invalid-response` instead of producing a broken screen. If
Zod is later approved, the readers are the only code to replace.

**Q31. Why `cache: 'no-store'` and `credentials: 'omit'`?**

Doctor-scoped data must never be served from a shared HTTP cache, and the API authenticates with
headers, not cookies. Omitting credentials removes a CSRF surface the backend does not defend.

**Q32. Why are server messages not displayed?**

The API is English-only; the product is bilingual and RTL-aware. Rendering English server text in
an Arabic page is a defect. Screens translate `code`/`kind`; `traceId` is shown only as a support
reference.

---

### Testing and process

**Q33. What do the 272 backend tests actually prove?**

They cover domain invariants (interval rules, state machine), service flows with in-memory fakes
(scope, roles, locks via a fake transaction), validators, paging, mapping, access policy, and HTTP
contracts through `WebApplicationFactory` (status codes, envelope, identity headers,
authorization). What they cannot prove is MySQL-specific behaviour (row locking, collation,
migration execution) because there is no server in this environment. That gap is stated, not
papered over.

**Q34. Why are there tests for error codes rather than just messages?**

Because clients localize by code. A test that only asserted an English message would pass even if
the code were renamed, breaking every translation. `ErrorEnvelopeTests` asserts status + `code` +
`traceId` presence, which is the actual contract.

**Q35. How do you avoid the tests merely repeating the implementation?**

Expected values are derived from the approved rules (the overlap table, the status matrix, the
role matrix, the paging limits), not from reading the code. Where a rule is ambiguous, it was
raised as a question instead of being encoded.

**Q36. What is not done, honestly?**

See §8. The short version: F2 backend, F3, F5; real authentication; the migration has never run
against MySQL; the provider license needs approval; the two persistence styles coexist; and the
appointments controller lacks `[Authorize]` until Q-07.

---

## 8. Known gaps, risks, and open questions

This section is deliberately blunt. A reviewer should be able to see exactly what is finished and
what is not.

### 8.1 Feature completeness

| Feature | State | What is missing |
|---|---|---|
| F1 appointments | Backend + frontend complete | `[Authorize]` (Q-07); real MySQL verification |
| F2 payments | **Mock only** | Everything backend; the payment domain model itself (Q-01) |
| F3 patient status | Not implemented | Definition of “patient status” (Q-03); medical entity scope (Q-08) |
| F4 medical records | Backend + frontend complete for visits/diagnoses/medications | No list route (by design); allergies/conditions/labs/vitals/notes/documents have schema but no endpoints |
| F5 dashboard | Not implemented | Depends on Q-01 (revenue attribution) and Q-03 (status semantics) |

### 8.2 Technical debt and inconsistencies

1. **Two persistence styles.** `IAppointmentStore` (F1) and `IApplicationDbContext` (F4)
   coexist. Both are layer-correct; converging them is an unapproved refactor.
2. **`frontend/src` vs `AGENTS.md` `Front_End/`.** The repository uses `frontend/` with
   application code in `src/`. The internal folder names match the agreed structure. Moving to
   `Front_End/` would be a large, unrelated, high-risk refactor.
3. **`Medilog.sln` vs `AGENTS.md` `Medilog.slnx`.** The actual solution file is
   `backend/Medilog.sln`. The `AGENTS.md` reference is stale.
4. **No populated `data/` folder.** Shared API code lives in `lib/api/`.
5. **`AppointmentsController` has no `[Authorize]`.** Documented TODO (Q-07). The service still
   enforces identity/role, but the framework-level 401 is absent.
6. **`payments/components/aymentStatusBadge.tsx`** has a typo in its filename. Renaming it is
   safe but unrelated; it is left as-is to avoid churn in an uncommitted merge.
7. **`STATE.md` Q-11** still says “no frontend test runner”; that was superseded by the F1-C4
   approval of Vitest. The decision log is append-only, so the stale row remains as history.
8. **`MaxPageSize` caps the day-schedule helper at 50 rows.** The F1 day view fetches one page of
   at most 50 appointments for a doctor/date. A busier day would need a pagination loop or a
   dedicated endpoint; neither is in the approved scope, so the limitation is documented rather
   than silently worked around.
9. **All Phase 0/1 work is uncommitted** on `main`, and a merge is in progress
   (`.git/MERGE_HEAD` present). The merge commit must be completed by the contributor/Tech Lead,
   not by an agent.

### 8.3 Environment limitations

- **No MySQL, Docker, or WSL** in the development environment. Therefore:
  - migration `20261109211333_InitialCreate` has **never been applied**;
  - the `FOR UPDATE` locking path, collation behaviour, and check constraints have **never been
    executed** against a real server;
  - only EF InMemory fakes exercise the service/store contracts.
- The repository contains **no real credentials or patient data**. `appsettings.json` has an
  empty connection string; dev data is synthetic (`backend/docs/dev/seed-dev-data.sql`).
- Do not connect the project to FreeDB or any shared remote database for testing.

### 8.4 Open decisions (blocking)

| ID | Question | Blocks |
|---|---|---|
| **Q-01** | Payment model: what a payment attaches to, partial/multiple payments, methods, statuses, void/edit, fee source | F2 backend, F5 revenue |
| **Q-03** | Meaning of “patient status” and “patients' statuses today” | F3, F5 |
| **Q-08** | Wider medical scope for F3 (which entities are in/out) | F3 |
| **Q-06** | Formal Tech Lead ratification of the frontend library set | FS sign-off |
| **Q-10** | Database engine confirmation, integration branch name, branch naming, migration ownership | FS / release process |
| **Q-07** | Real authentication and role provisioning | Replacing dev identity; `[Authorize]` on appointments |

### 8.5 Licensing

`MySql.EntityFrameworkCore` (Oracle) is licensed under
`GPL-2.0-only WITH Universal-FOSS-exception-1.0`. This must be approved by the team/supervisor
before distribution. The alternative (Pomelo) is a different dependency decision and has not been
approved.

### 8.6 Risk register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Overlap logic behaves differently on real MySQL | Low (logic is in C#, not SQL) | High | Add a DB-backed smoke test on a MySQL machine |
| Migration fails on first apply | Medium | High | Review the generated SQL; apply to a scratch database before integration |
| Dev header identity reaches production | Low (double-gated) | Critical | Configuration review; remove when Q-07 lands |
| Payment rules invented by UI mock are mistaken for requirements | Medium | High | Mock is labelled; Q-01 is the authority |
| Secret committed in `.env.local` | Low | High | `.gitignore` + review; only `NEXT_PUBLIC_*` allowed |
| Two persistence styles drift further apart | Medium | Medium | Documented; converge only with approval |
| AutoMapper license key missing in CI | Medium | Medium | Key read from configuration; build/tests do not require it |

---

## 9. Onboarding and local runbook

### 9.1 Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 10.x | `dotnet --version` |
| Node.js | 20 LTS or newer | Next 16 requirement |
| npm | bundled with Node | `npm ci` uses the lockfile |
| MySQL | 8.x | Optional for the contract tests; required for a real round-trip |
| Git | any recent | Work on a feature branch, never directly on the integration branch |

### 9.2 Backend

```bash
cd backend

dotnet restore
dotnet build Medilog.sln
dotnet test  Medilog.sln
```

> `AGENTS.md` §7 shows `Medilog.slnx`; the actual file is **`Medilog.sln`**. Use the file that
> exists. The three commands above are the real quality gate; the last observed result was
> 0 warnings / 0 errors and 272 passing tests.

Run the API:

```bash
dotnet run --project src/Medilog.WebAPI
```

The `http` launch profile serves `http://localhost:5208` **without** an HTTPS redirect, which is
what the frontend expects. If you use the `https` profile, set
`NEXT_PUBLIC_API_BASE_URL=https://localhost:7206`, trust the dev certificate, and expect a CORS
preflight failure if the API answers with a redirect instead of the origin.

### 9.3 Database (optional but required for a real smoke test)

1. Create an empty MySQL 8 database, e.g. `medilog`, with `utf8mb4`.
2. Provide the connection string **outside** the repository:

   ```bash
   cd backend/src/Medilog.WebAPI
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
     "server=localhost;port=3306;database=medilog;user=medilog;password=<local-only>"
   ```

   or set the `MEDILOG_CONNECTION_STRING` environment variable.

3. Apply the migration:

   ```bash
   cd backend
   dotnet ef database update \
     --project src/Medilog.Infrastructure \
     --startup-project src/Medilog.WebAPI
   ```

4. Load synthetic data:

   ```bash
   mysql -u medilog -p medilog < docs/dev/seed-dev-data.sql
   ```

   The seed contains **only synthetic** patients, doctors, and secretaries. Never load real
   patient data into a development database.

> If you change the model, add a migration with the same `--project`/`--startup-project` pair and
> review the generated SQL before applying. Do not edit or delete shared migrations.

### 9.4 Frontend

```bash
cd frontend

npm ci
cp .env.example .env.local     # then edit if needed
npm run dev
```

`.env.local` is git-ignored. The defaults in `.env.example` already point at
`http://localhost:5208`, enable the dev identity, and set the clinic time zone to `Africa/Cairo`.

Quality gate:

```bash
npm run typecheck
npm run lint
npm run test
npm run build
```

### 9.5 Identity for local use

**Appointments (F1)** use the temporary header identity:

```env
NEXT_PUBLIC_DEV_IDENTITY=true
NEXT_PUBLIC_DEV_USER_ID=aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa
NEXT_PUBLIC_DEV_USER_ROLE=Secretary
```

Pick any synthetic id/role from `backend/docs/dev/seed-dev-data.sql`, or use the in-app
“Acting as …” dialog. The API accepts the headers only in Development with
`Auth:AllowHeaderIdentity=true` (already set in `appsettings.Development.json`).

**Medical records (F4)** use a development JWT:

```bash
dotnet user-jwts create \
  --project backend/src/Medilog.WebAPI \
  --name "<doctor-guid>" \
  --role Doctor
```

`--name` becomes the `sub` claim; do **not** pass `--claim "sub=..."` (it would create a
multi-valued claim). Paste the token into the in-app dev token panel; it is stored in
`sessionStorage` for that tab only.

### 9.6 Smoke-test checklist

After both servers are up:

1. `GET http://localhost:5208/openapi/v1.json` returns a document (Development only).
2. Open `http://localhost:3000` → redirected to `/ar` (Arabic default).
3. Switch to `/en`; the layout flips to LTR.
4. `/ar/appointments` with a secretary identity lists appointments; pagination shows page and
   total pages.
5. Book an appointment that overlaps an existing one → localized “overlap” error, HTTP 409.
6. Book back-to-back (10:00–11:00 after 09:00–10:00) → succeeds.
7. As a secretary, the “Mark completed” action is not offered; calling the API directly returns
   403.
8. `/ar/medical-records` without a token → 401/identity-required UI; with a doctor token → the
   workspace loads.
9. Create a visit, add a diagnosis and a medication, delete the medication → 204 and the visit
   refreshes.
10. Stop the API and reload a list → localized “network” error, not a raw `TypeError`.

### 9.7 Common failure modes

| Symptom | Likely cause | Fix |
|---|---|---|
| Startup throws “connection string is not configured” | No user secret / env var | Set `ConnectionStrings:DefaultConnection` or `MEDILOG_CONNECTION_STRING` |
| Browser CORS error on every request | API base URL uses the HTTPS profile, or the frontend runs on a port other than 3000 | Use the `http` profile (`:5208`) or update the CORS origin; do not disable CORS |
| Every appointment request returns 403 `identity.invalid` | Dev identity disabled, or role not exactly `Doctor`/`Secretary` | Set `NEXT_PUBLIC_DEV_IDENTITY=true`, pick a valid GUID and exact role |
| Medical-records request returns 401 | No/invalid bearer token, or token expired | Re-run `dotnet user-jwts create` and paste again |
| Medical-records request returns 403 | Token role is not `Doctor` | Create the token with `--role Doctor` |
| `NEXT_PUBLIC_API_BASE_URL is not set` thrown in the browser | `.env.local` missing, or the dev server was started before the file existed | Create `.env.local` and restart `npm run dev` |
| Build fails on a client-time-dependent screen | A page lost its `Suspense` boundary | Restore the boundary (see §4.3) |
| Arabic text renders as `?` | Database/table not `utf8mb4` | Recreate the database with `utf8mb4`; the app applies it to every entity |
| Migration fails with a charset error | MySQL version older than 8 | Use MySQL 8 (the collation is `utf8mb4_0900_ai_ci`) |

### 9.8 Contribution workflow

1. Read `AGENTS.md`, `STATE.md`, and the task file for the Cycle.
2. Work on a dedicated branch; do not commit to the protected integration branch.
3. Follow `plan → build → review → done`; get the plan approved before coding.
4. Keep the diff focused; do not refactor unrelated files.
5. Run the real quality gates and record the actual results.
6. Write the Execution Report and propose the `STATE.md` update.
7. Open a PR with purpose, requirement link, files/layers changed, tests run, contract/migration
   changes, and known limitations.
8. The contributor must be able to explain every committed line.

---

## 10. Appendices

### Appendix A — Backend file index

```text
backend/src/Medilog.Domain/
├── Common/            BaseEntity, CreatedEntity, AuditableEntity, DomainException, DomainErrorCodes
├── Entities/          13 entities
└── Enums/             AccountStatus, AppointmentStatus, Gender, BloodType, AllergySeverity,
                       ChronicConditionStatus, MedicalDocumentType

backend/src/Medilog.Application/
├── Common/
│   ├── Abstractions/  IApplicationDbContext, ICurrentUser
│   ├── Exceptions/    AppException, NotFound/Forbidden/Conflict/RequestValidation
│   ├── Paging/        PagedResult, PagingDefaults, IPagedQuery, PagedQueryValidator
│   ├── Security/      Roles, UserRole, MedicalRecordAccessPolicy
│   └── Validation/    RequestValidation
├── Features/Appointments/   service, port, rules, requests, responses, validators,
│                            lookups, store contracts, criteria
├── Features/MedicalRecords/ service, contracts, validators, mapping profile, text rules
├── ErrorCodes.cs
└── DependencyInjection.cs

backend/src/Medilog.Infrastructure/
├── Persistence/Context/         MedilogDbContext, MedilogDbContextFactory
├── Persistence/Configurations/  13 entity configs + 3 base configs + ColumnLengths
├── Persistence/Converters/      Utc/DateOnly/TimeOnly converters, EnumValueConverters
├── Persistence/Appointments/    AppointmentStore, AppointmentTransaction
├── Persistence/Migrations/      20261109211333_InitialCreate + snapshot + shim
├── Time/                        ClinicClock
└── DependencyInjection.cs

backend/src/Medilog.WebAPI/
├── Program.cs
├── Controllers/       AppointmentsController, MedicalVisitsController
├── Common/            ApiProblem, GlobalExceptionHandler, CurrentUser
├── appsettings.json / appsettings.Development.json
└── Properties/launchSettings.json
```

### Appendix B — Frontend file index

```text
frontend/src/
├── app/[locale]/            route tree (§4.3)
├── components/              shared, business-agnostic UI (layout, Header, …)
├── config/site.ts
├── constants/
├── features/
│   ├── appointments/        full F1 slice
│   ├── medical-records/     full F4 slice
│   ├── auth/                temporary dev identity
│   ├── payments/            mock-only F2
│   ├── home/                landing
│   └── dashboard|doctors|patients|medical-timeline   placeholders
├── i18n/                    routing.ts, request.ts, navigation.ts
├── lib/
│   ├── api/                 client, errors, auth-headers, query-string, paging
│   ├── query-client/        query-client.ts, query-provider.tsx
│   ├── animations/
│   └── utils/toast.ts
├── messages/                ar.json, en.json
├── store/ui-store.ts        sidebar only
├── types/paging.ts
├── proxy.ts                 next-intl middleware
└── app/globals.css
```

### Appendix C — Endpoint inventory

| Method | Route | Auth | Success |
|---|---|---|---|
| `POST` | `/api/appointments` | service-level identity | 201 + `Location` |
| `GET` | `/api/appointments` | service-level identity | 200 paged |
| `GET` | `/api/appointments/{id}` | service-level identity | 200 |
| `PUT` | `/api/appointments/{id}/schedule` | service-level identity | 200 |
| `POST` | `/api/appointments/{id}/cancel` | service-level identity | 200 |
| `PUT` | `/api/appointments/{id}/status` | service-level identity | 200 |
| `GET` | `/api/appointments/lookups/doctors` | service-level identity | 200 |
| `GET` | `/api/appointments/lookups/patients` | service-level identity | 200 |
| `POST` | `/api/medical-visits` | `Doctor` | 201 + `Location` |
| `GET` | `/api/medical-visits/{visitId}` | `Doctor` | 200 |
| `PUT` | `/api/medical-visits/{visitId}` | `Doctor` | 200 |
| `POST` | `/api/medical-visits/{visitId}/diagnoses` | `Doctor` | 201 + parent `Location` |
| `PUT` | `/api/medical-visits/{visitId}/diagnoses/{diagnosisId}` | `Doctor` | 200 |
| `POST` | `/api/medical-visits/{visitId}/medications` | `Doctor` | 201 + parent `Location` |
| `PUT` | `/api/medical-visits/{visitId}/medications/{medicationId}` | `Doctor` | 200 |
| `DELETE` | `/api/medical-visits/{visitId}/medications/{medicationId}` | `Doctor` | 204 |

There is no payments endpoint and no patient/status or dashboard endpoint in the current scope.

### Appendix D — Glossary

| Term | Meaning in this project |
|---|---|
| **F1 … F5** | The five instructor-approved features (`AGENTS.md` §2) |
| **Cycle** | A bounded unit of work with its own plan, acceptance criteria, and report |
| **FS** | Shared technical requirements (paging, validation, mapping, configurations, errors) |
| **Slot-holding appointment** | An appointment whose status is not `Cancelled`/`NoShow` |
| **Clinic local time** | Wall-clock time in `Clinic:TimeZone` (`Africa/Cairo`), not UTC |
| **Alternate key** | A unique non-primary key that a composite FK can target |
| **Out-of-scope resource** | An existing row the caller may not see; reported as 404 |
| **Dev identity** | Temporary `X-User-Id`/`X-User-Role` headers, Development only |
| **Dev token** | Development JWT pasted into the UI and stored in `sessionStorage` |
| **Envelope** | The single RFC 7807 error shape and the single paged-list shape |
| **Actor** | The authenticated user performing a use case |

### Appendix E — Verification commands and last observed results

```bash
# Backend
cd backend
dotnet restore
dotnet build Medilog.sln
dotnet test  Medilog.sln

# Frontend
cd frontend
npm ci
npm run typecheck
npm run lint
npm run test
npm run build
```

| Check | Last observed result |
|---|---|
| `dotnet build` | 0 warnings, 0 errors |
| `dotnet test` | 272 passed (178 Application + 94 WebAPI) |
| `npm run typecheck` | clean |
| `npm run lint` | clean |
| `npm run test` | 288 passed (15 files) |
| `npm run build` | success, 21 static pages |

> These results were observed before this document was written and are recorded in
> `docs/reports/P1-C1-C4.md`. Writing documentation does not re-run them. Anything not executed
> is stated as such in §8.

### Appendix F — Document maintenance

- This file describes the **implemented** system. When a Cycle changes behaviour, update the
  relevant section in the same PR and add a line to the change log below.
- Do not change scope, business rules, or architecture here. Scope lives in `AGENTS.md` §2;
  decisions live in `STATE.md` §7; HTTP contracts live in `backend/docs/api/`.
- If this document and the code disagree, **the code wins** and this document is the bug.

| Date | Change |
|---|---|
| 2026-11-13 | Initial version: §0–§10 covering Phase 0/1 state, F1/F4 implemented, F2 mock, F3/F5 pending |
