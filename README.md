# Medilog — Smart Medical Records Platform

A web-based, doctor-centric platform for organizing patients' medical records in one
centralized place, with a chronological medical timeline that makes a patient's complete
history easier to review.

> **Project status:** architectural foundation only. The infrastructure, tooling, and
> project structure are ready for development; medical business features are **not**
> implemented yet. See [Current Implementation Status](#19-current-implementation-status).

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Business Problem](#2-business-problem)
3. [Proposed Solution](#3-proposed-solution)
4. [Business Objectives](#4-business-objectives)
5. [Target Users and Stakeholders](#5-target-users-and-stakeholders)
6. [Main System Modules](#6-main-system-modules)
7. [Core Business Rules](#7-core-business-rules)
8. [MVP Scope](#8-mvp-scope)
9. [Technology Stack](#9-technology-stack)
10. [Frontend Architecture](#10-frontend-architecture)
11. [Backend Clean Architecture](#11-backend-clean-architecture)
12. [Project Directory Structure](#12-project-directory-structure)
13. [Installation and Setup](#13-installation-and-setup)
14. [Running the Frontend](#14-running-the-frontend)
15. [Running the Backend](#15-running-the-backend)
16. [Environment Configuration](#16-environment-configuration)
17. [Development Conventions and Architectural Rules](#17-development-conventions-and-architectural-rules)
18. [Verification Results](#18-verification-results)
19. [Current Implementation Status](#19-current-implementation-status)
20. [Future Development Roadmap](#20-future-development-roadmap)
21. [Source of Truth](#21-source-of-truth)

---

## 1. Project Overview

**Medilog** is a graduation project in the healthcare domain. It is a web-based Smart
Medical Records Platform built around the doctor's workflow.

| Item | Value |
| --- | --- |
| Project type | Graduation project |
| Domain | Healthcare |
| System type | Web-based platform |
| Primary users | Doctors |
| Patient access | Not supported in the MVP |
| Core concept | **Doctor = System User \| Patient = Medical Record** |

Doctors create and manage patient records and record structured medical information:
visits, diagnoses, medications and prescriptions, allergies, chronic conditions, vital
signs, laboratory results, medical notes, and medical documents. A chronological medical
timeline presents the patient's history for efficient review.

Patients are represented only as records. They do not have accounts, credentials,
dashboards, or direct access to the platform in the MVP.

---

## 2. Business Problem

Doctors frequently need a patient's previous medical history when evaluating a current
condition. In traditional workflows, that information may be spread across paper files,
printed laboratory results, reports, prescriptions, separate digital documents, personal
notes, and verbal information from the patient.

This fragmented process creates:

- **Information fragmentation** — patient data exists in multiple locations and formats.
- **Slow information retrieval** — doctors must manually search through previous records.
- **Difficult historical tracking** — changes in conditions, medications, and lab results
  over time are hard to understand.
- **Risk of information loss** — paper and individual files can be lost or damaged.
- **Repetitive data collection** — doctors repeatedly ask about previous diagnoses,
  medications, allergies, and tests.

The result is additional time spent collecting information and a risk of missing
important historical details.

---

## 3. Proposed Solution

A centralized digital medical-record platform that lets doctors manage and review their
patients' medical information through one organized system.

The platform is planned to support:

- Creating patient records and searching for patients.
- Viewing a centralized patient profile.
- Recording visits, diagnoses, medications and prescriptions, allergies, chronic
  conditions, vital signs, laboratory results, and medical notes.
- Uploading, viewing, downloading, and associating medical documents with patients.
- Reviewing medical history chronologically through a medical timeline.
- Appointment scheduling managed by secretaries for their assigned doctors.

The MVP focuses on one core objective: **provide doctors with a centralized and organized
digital medical history for their patients.**

---

## 4. Business Objectives

| ID | Objective |
| --- | --- |
| BO-01 | Centralize medical information in one digital location. |
| BO-02 | Reduce the time required to retrieve previous patient information. |
| BO-03 | Improve medical-history visibility with structured, chronological presentation. |
| BO-04 | Reduce dependence on paper records. |
| BO-05 | Improve record organization across diagnoses, medications, visits, labs, documents, and notes. |
| BO-06 | Improve the doctor's workflow when retrieving and reviewing history. |
| BO-07 | Streamline appointment management for clinic secretaries. |

---

## 5. Target Users and Stakeholders

| Stakeholder | Role |
| --- | --- |
| **Doctor** | Primary user. Manages patient records and all medical information, records visits, and reviews timelines and own appointments. |
| **System Administrator** | Manages doctor and secretary accounts, activation/deactivation, and system-level administration. |
| **Secretary** | Manages appointments and scheduling for assigned doctors. Can search patients and manage basic patient information, but **cannot access medical records**. |
| **Medical Clinic** | Potential organization using the platform to manage doctors and patient records. |
| **Development Team** | Designs, builds, tests, and maintains the system. |
| **Patient** | Subject of the medical record; **not a user** of the MVP. |

### Role boundaries

- **Doctors** are the primary users and the only role that manages medical data.
- **Administrators** manage accounts and system-level administration.
- **Secretaries** are assigned to doctors by the administrator. They manage appointments
  and basic patient information only.
- **Patients** have no accounts, login, dashboard, or direct access in the MVP.

---

## 6. Main System Modules

All modules below are **planned business features**. Only their architectural placeholders
exist in the current codebase.

| Module | Description | Owner |
| --- | --- | --- |
| Doctor Management | Registration, login, authentication, logout, password recovery, basic profile. | Doctor / Administrator |
| System Administration | Doctor and secretary account management and system-level administration. | Administrator |
| Secretary Management | Secretary accounts created by the administrator, authentication, password recovery. | Administrator / Secretary |
| Patient Management | Create, edit, search, view patient information and patient list. | Doctor; limited basic info for Secretary |
| Medical History Management | Visits, diagnoses, medications, prescriptions, allergies, chronic conditions, vital signs, notes, and lab results. | Doctor |
| Medical Document Management | Upload, view, download, and associate documents with patients. | Doctor |
| Medical Timeline | Present medical events chronologically. | Doctor |
| Appointment Management | Create, view, reschedule, cancel appointments; schedule and status tracking. | Secretary; doctors view their own appointments |
| Dashboard | Role-appropriate entry point into doctor, secretary, and administrator workflows. | All staff roles |

---

## 7. Core Business Rules

These rules come from the BRD and must guide future implementation.

| Rule | Description |
| --- | --- |
| BRULE-01 | Only authenticated doctors can access the platform and its medical records. |
| BRULE-02 | Patients have no accounts or authentication credentials in the MVP. |
| BRULE-03 | A patient record created by a doctor belongs to that doctor's workspace. |
| BRULE-04 | A doctor can only access patient records they are authorized to access. |
| BRULE-05 | Every medical event must be associated with a specific patient. |
| BRULE-06 | Every medical visit must be associated with a patient and a doctor. |
| BRULE-07 | Every uploaded medical document must be associated with a specific patient. |
| BRULE-08 | Medical information is controlled and maintained by authorized doctors. |
| BRULE-09 | The platform organizes and presents information; it does not replace professional medical judgment. |
| BRULE-10 | Secretaries access only appointment data and basic patient information for assigned doctors, and must never access medical records. |
| BRULE-11 | Every appointment must be associated with a specific patient and doctor, and appointments for the same doctor must not overlap. |

Additional implementation-critical constraints:

- Medical records must be isolated per doctor (data isolation).
- Secretaries must be blocked from medical-record endpoints, not only hidden in the UI.
- Appointment conflicts for the same doctor must be validated.
- All medical events and documents must be tied to a patient.

---

## 8. MVP Scope

### In scope

- **Doctor management:** registration, login, authentication, logout, password recovery,
  basic profile.
- **Secretary management:** administrator-created accounts, login, logout, password
  recovery.
- **Patient management:** create, edit, search, view patient information and list.
- **Medical history management:** visits, diagnoses, medications, prescriptions,
  allergies, chronic conditions, vital signs, notes, and laboratory results.
- **Medical document management:** upload, view, download, and associate documents with
  patients.
- **Medical timeline:** display medical events chronologically.
- **Appointment management:** create, view, reschedule, cancel, schedule, and track status.

### Out of scope

- Patient accounts, login, dashboard, or patient app.
- Patient-facing online booking.
- Online payments.
- Doctor–patient messaging.
- Video consultations and telemedicine.
- Wearable device integration.
- Hospital system integration.
- Insurance integration.
- AI-based diagnosis or automated treatment recommendations.
- Emergency response systems.
- Advanced clinical decision support or advanced AI medical analysis.

### MVP success criterion

A doctor can complete the workflow **Create → Search → Open → Update → Review**.

---

## 9. Technology Stack

### Frontend

| Concern | Technology |
| --- | --- |
| Framework | Next.js 16 (App Router, Turbopack) |
| Language | TypeScript |
| Styling | Tailwind CSS 4 |
| Internationalization | next-intl 4 |
| Server state / data fetching | TanStack React Query 5 |
| Client state | Zustand 5 |
| Animation | Framer Motion |
| Notifications | React Toastify |
| Icons | React Icons |
| Font | Cairo via `next/font/google` |
| Linting | ESLint (Next.js config) |

### Backend

| Concern | Technology |
| --- | --- |
| Runtime / SDK | .NET 10 |
| Framework | ASP.NET Core Web API (controllers + OpenAPI) |
| Language | C# |
| Architecture | Clean Architecture (4 layers) |
| ORM | Entity Framework Core 10 |
| Database provider | **Not selected yet** (the BRD does not specify one) |

---

## 10. Frontend Architecture

The frontend uses a **feature-based architecture**. Application-wide concerns are shared;
business code lives inside self-contained feature modules.

### Key decisions

- **Arabic is the default locale.** `/` redirects to `/ar`; English is served from `/en`.
- Locale-based routing is handled by `next-intl` with an always-present locale prefix.
- `src/proxy.ts` performs locale negotiation and redirection (Next.js 16 replaced
  `middleware.ts` with `proxy.ts`).
- `src/app/[locale]/layout.tsx` sets `<html lang>` and `<html dir>` dynamically
  (`rtl` for Arabic, `ltr` for English).
- **Cairo** is loaded globally through `next/font/google` and exposed as `--font-cairo`.
- React Query uses a centralized `QueryClient` configuration and a client `QueryProvider`.
- Zustand holds global UI state; feature state belongs in feature stores.
- React Toastify is mounted once, in RTL mode for Arabic, and accessed through
  `@/lib/utils/toast`.
- Framer Motion variants live in `src/lib/animations`.
- No real API integration, authentication, or business workflows exist yet.

### Frontend structure

```text
frontend/
├── src/
│   ├── app/
│   │   ├── [locale]/
│   │   │   ├── layout.tsx
│   │   │   ├── page.tsx
│   │   │   └── providers.tsx
│   │   └── globals.css
│   ├── components/
│   │   ├── layout/
│   │   ├── shared/
│   │   └── ui/
│   ├── config/
│   ├── constants/
│   ├── features/
│   │   ├── auth/
│   │   │   ├── components/
│   │   │   ├── hooks/
│   │   │   ├── services/
│   │   │   ├── store/
│   │   │   ├── types/
│   │   │   └── index.ts
│   │   ├── patients/
│   │   ├── doctors/
│   │   ├── appointments/
│   │   ├── medical-records/
│   │   ├── medical-timeline/
│   │   └── dashboard/
│   ├── hooks/
│   ├── i18n/
│   │   ├── routing.ts
│   │   ├── navigation.ts
│   │   └── request.ts
│   ├── lib/
│   │   ├── animations/
│   │   ├── query-client/
│   │   └── utils/
│   ├── messages/
│   │   ├── ar.json
│   │   └── en.json
│   ├── schemas/
│   ├── services/
│   │   └── api/
│   ├── store/
│   ├── types/
│   └── proxy.ts
├── public/
├── next.config.ts
├── package.json
└── tsconfig.json
```

### Feature module rules

- Each feature exposes a public API through its `index.ts`.
- Feature-specific components, hooks, API services, stores, and types stay inside the
  feature folder.
- Shared UI primitives go in `components/ui`; cross-feature composition goes in
  `components/shared` and `components/layout`.
- Feature service modules may consume the shared API client in `services/api`, but must not
  duplicate HTTP configuration.

---

## 11. Backend Clean Architecture

The backend is a four-layer Clean Architecture solution. Dependencies point inward; the
Domain layer knows nothing about frameworks or other layers.

```text
Medilog.WebAPI
  ├── Medilog.Application
  └── Medilog.Infrastructure

Medilog.Infrastructure
  ├── Medilog.Application
  └── Medilog.Domain

Medilog.Application
  └── Medilog.Domain

Medilog.Domain
  └── (no dependencies)
```

### Layer responsibilities

| Layer | Contains | Must not contain |
| --- | --- | --- |
| **Domain** | Entities, enums, domain contracts, base types. | EF Core, ASP.NET Core, or references to other layers. |
| **Application** | Use cases, DTOs, interfaces/abstractions, application validation, feature contracts. | Database implementations, controllers, infrastructure concerns. |
| **Infrastructure** | `DbContext`, EF Core configurations, migrations, implementations of Application interfaces, external services. | Business rules that belong in Domain/Application. |
| **WebAPI** | Controllers/endpoints, middleware, DI composition, API configuration, entry point. | Business logic or direct database access. |

### Backend structure

```text
backend/
├── Medilog.sln
├── README.md
└── src/
    ├── Medilog.Domain/
    │   ├── Entities/
    │   ├── Enums/
    │   ├── Common/
    │   └── Interfaces/
    ├── Medilog.Application/
    │   ├── Common/
    │   ├── DTOs/
    │   ├── Interfaces/
    │   ├── Features/
    │   │   ├── Auth/
    │   │   ├── Doctors/
    │   │   ├── Secretaries/
    │   │   ├── Patients/
    │   │   ├── MedicalRecords/
    │   │   ├── MedicalTimeline/
    │   │   └── Appointments/
    │   └── DependencyInjection.cs
    ├── Medilog.Infrastructure/
    │   ├── Persistence/
    │   │   ├── Context/
    │   │   ├── Configurations/
    │   │   └── Migrations/
    │   ├── Services/
    │   └── DependencyInjection.cs
    └── Medilog.WebAPI/
        ├── Controllers/
        ├── Middleware/
        ├── Extensions/
        ├── Properties/
        ├── appsettings.json
        └── Program.cs
```

### Entity Framework Core setup

- `Microsoft.EntityFrameworkCore` and `Microsoft.EntityFrameworkCore.Design` **10.0.14**
  are installed.
- `MedilogDbContext` lives in `Infrastructure/Persistence/Context`.
- Entity configurations are discovered automatically from the Infrastructure assembly via
  `ApplyConfigurationsFromAssembly`, so new `IEntityTypeConfiguration<T>` classes in
  `Persistence/Configurations` are applied without editing the context.
- The DbContext is registered in `Infrastructure/DependencyInjection.cs`; resolving it
  requires a provider, and no current code resolves it.
- `Persistence/Migrations` is prepared but empty.
- **No provider package is installed** because the BRD does not specify a database.
  Migrations cannot be generated until a provider is chosen. The exact steps are documented
  in [`backend/README.md`](backend/README.md#entity-framework-core).

---

## 12. Project Directory Structure

```text
Medilog/
├── frontend/                 # Next.js application
│   ├── src/
│   ├── public/
│   ├── next.config.ts
│   ├── package.json
│   └── README.md
│
├── backend/                  # .NET solution
│   ├── Medilog.sln
│   ├── src/
│   │   ├── Medilog.Domain/
│   │   ├── Medilog.Application/
│   │   ├── Medilog.Infrastructure/
│   │   └── Medilog.WebAPI/
│   └── README.md
│
├── .gitignore
└── README.md                 # this file
```

Frontend and backend are independent applications with no shared runtime, build output, or
dependency graph. They communicate only over HTTP.

---

## 13. Installation and Setup

### Prerequisites

| Tool | Required version |
| --- | --- |
| Node.js | 20.9+ (developed with Node.js 24) |
| npm | 10+ (developed with npm 11) |
| .NET SDK | 10.0+ (developed with 10.0.400) |
| Git | Any recent version |

Optional:

- `dotnet-ef` global tool, required once a database provider is selected.
- A code editor (Visual Studio 2026 / VS Code / JetBrains Rider).

### Clone and enter the repository

```bash
git clone <repository-url>
cd Medilog
```

### Install frontend dependencies

```bash
cd frontend
npm install
```

### Restore backend dependencies

```bash
cd backend
dotnet restore
dotnet build
```

---

## 14. Running the Frontend

From the repository root:

```bash
cd frontend
npm run dev
```

Open **http://localhost:3000**. The root URL redirects to the default Arabic locale:
**http://localhost:3000/ar**. English is at **http://localhost:3000/en**.

Useful commands:

```bash
npm run build      # production build
npm run start      # serve the production build
npm run lint       # ESLint
npm run typecheck  # TypeScript without emit
```

---

## 15. Running the Backend

From the repository root:

```bash
cd backend
dotnet run --project src/Medilog.WebAPI
```

Development URLs:

- HTTP: **http://localhost:5208**
- HTTPS: **https://localhost:7206**

The OpenAPI document is exposed in development at
**http://localhost:5208/openapi/v1.json**.

Useful commands:

```bash
dotnet build Medilog.sln
dotnet run --project src/Medilog.WebAPI
dotnet run --project src/Medilog.WebAPI --launch-profile https
```

There are currently no business endpoints; the API is a runnable foundation.

---

## 16. Environment Configuration

### Frontend

No environment variables are required at this stage. Internationalization uses bundled
message files. When API integration begins, add the base URL and other configuration to a
local `.env.local` file (excluded from version control) and document it here.

### Backend

Configuration is loaded from `appsettings.json`, `appsettings.Development.json`,
environment variables, and user secrets in development.

| Key | Purpose |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | Placeholder for the future database connection string. |
| `Cors:AllowedOrigins` | Origins allowed to call the API. Defaults to `http://localhost:3000`. |

Use `dotnet user-secrets` for credentials:

```bash
cd backend/src/Medilog.WebAPI
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<connection-string>"
```

Do not commit passwords, connection strings, or API keys.

---

## 17. Development Conventions and Architectural Rules

### General

- Keep frontend and backend independent; no cross-directory imports or shared build output.
- Use clear, descriptive names and follow the existing folder conventions.
- Prefer small, focused files. Keep business rules out of UI components and controllers.
- Do not add a library without a concrete need.
- Keep commits scoped to one logical change.

### Frontend

- Use the App Router and React Server Components by default; add `'use client'` only when a
  component needs browser APIs, hooks, or interactivity.
- Keep feature code inside `src/features/<feature>` and expose it through `index.ts`.
- Put feature-specific data fetching in `features/<feature>/services` using React Query.
- Keep global state minimal; use feature stores for business state.
- Use `next-intl` navigation APIs (`Link`, `useRouter`, `usePathname` from
  `@/i18n/navigation`), not the raw Next.js equivalents, for localized routes.
- Every new user-visible string must be added to both `ar.json` and `en.json`.
- Use logical CSS properties/utilities (`ms-*`, `me-*`, `ps-*`, `pe-*`, `text-start`,
  `text-end`) so RTL and LTR both work.
- Never hardcode API URLs in components; centralize them in the API layer.
- Use `notify` from `@/lib/utils/toast` instead of calling `react-toastify` directly.

### Backend

- Respect the dependency direction. Domain must remain framework-free.
- Application defines interfaces; Infrastructure implements them.
- Controllers stay thin: validate input, call an application use case, return a result.
- Do not inject `DbContext` into controllers.
- Put EF Core entity configuration in `Persistence/Configurations` using
  `IEntityTypeConfiguration<T>`.
- Never expose domain entities directly from the API; use DTOs.
- Enforce authorization for every medical-record endpoint; never rely on UI hiding.
- Validate appointment overlaps for the same doctor.
- Do not add authentication, CQRS, MediatR, AutoMapper, or FluentValidation until the
  corresponding feature phase is scheduled.

### Security and privacy

- Authenticate all staff users.
- Isolate patient records per doctor.
- Restrict secretaries to appointments and basic patient information only.
- Validate input and protect uploaded documents.
- Use synthetic or fictional patient data during development and demonstration.
- Do not introduce real patient data into the repository.

---

## 18. Verification Results

The foundation was verified in the target environment:

| Check | Result |
| --- | --- |
| `npm run lint` | Passed |
| `npm run build` | Passed (Next.js 16.4.5, Turbopack) |
| `npm run typecheck` | Passed (part of build) |
| Root redirect | `GET /` → `307` → `/ar` |
| Arabic render | `lang="ar"`, `dir="rtl"` |
| English render | `lang="en"`, `dir="ltr"` |
| `dotnet build Medilog.sln` | Passed, 0 warnings / 0 errors |
| Backend startup | Succeeded on `http://localhost:5208` |
| OpenAPI endpoint | Returned `200` in development |
| Project references | Domain has none; direction enforced as specified |

---

## 19. Current Implementation Status

### Implemented — infrastructure and foundation

**Repository**

- Separate `frontend/` and `backend/` applications.
- Root documentation and ignore rules.

**Frontend**

- Next.js 16 App Router project with TypeScript, Tailwind CSS 4, ESLint, and `src/`.
- `next-intl` locale-based routing with Arabic default, English support, `proxy.ts`, and
  message files.
- RTL/LTR direction switching, Cairo font, and a locale switcher.
- Feature-based folder architecture for all planned modules.
- React Query provider and centralized client configuration.
- Zustand global UI store and feature store placeholders.
- React Toastify container with RTL support and a notification utility.
- Framer Motion variants and transitions.
- Shared components, constants, configuration, types, schemas, and API service folders.
- A technical verification home page (not a business dashboard).

**Backend**

- Four-project .NET 10 Clean Architecture solution with enforced references.
- Layer-specific dependency injection entry points.
- Domain base types, folder structure, and feature placeholders.
- EF Core 10 `DbContext`, configuration discovery, migrations folder, and Design package.
- Controllers/OpenAPI/CORS/middleware scaffolding.
- Configuration files for logging, CORS, and a future connection string.

### Not implemented — planned business features

- Authentication, authorization, JWT, and role policies.
- Doctor, secretary, and administrator management.
- Patient CRUD and search.
- Visits, diagnoses, medications, prescriptions, allergies, chronic conditions, vital
  signs, laboratory results, medical notes, and medical documents.
- Medical timeline aggregation.
- Appointment scheduling and overlap validation.
- API integration from the frontend.
- Database provider, migrations, and persistence.
- Automated tests, audit logging, and production hardening.

---

## 20. Future Development Roadmap

The roadmap follows the BRD scope; it is not a commitment to features outside the MVP.

1. **Foundation** — current state: project initialization, architecture, i18n, tooling.
2. **Identity and access** — doctor authentication, administrator-managed accounts,
   secretary authentication, role-based authorization, doctor data isolation.
3. **Patient management** — patient creation, editing, search, profile, and lists with
   doctor ownership.
4. **Medical history** — visits, diagnoses, medications/prescriptions, allergies, chronic
   conditions, vital signs, laboratory results, and medical notes.
5. **Medical documents** — secure upload, download, and patient association.
6. **Medical timeline** — chronological aggregation of medical events.
7. **Appointments** — secretary scheduling, rescheduling, cancellation, status tracking,
   and overlap validation per doctor.
8. **Dashboards** — role-specific entry points for doctors, secretaries, and administrators.
9. **Quality and hardening** — automated tests, validation, audit logging, backups,
   document storage security, and deployment configuration.

Future business opportunities from the BRD (outside the MVP): AI medical summary,
intelligent document extraction, medical-history search, health trend analysis, medication
interaction alerts, controlled record sharing, and clinic management.

---

## 21. Source of Truth

This document is based on the **Smart Medical Records Platform Business Requirements
Document (Version 1.0)**. The BRD remains the source of truth for business requirements,
scope, roles, and system behavior.

Where the BRD leaves a technical choice open, this repository uses a documented default:

- **Database provider:** intentionally not selected; EF Core is configured without a
  provider until the team decides.
- **Default language:** Arabic.
- **Locale routing:** locale prefix always present (`/ar`, `/en`).
- **API style:** ASP.NET Core controllers with OpenAPI.

Any change to these defaults should be recorded here and in the relevant package README.
