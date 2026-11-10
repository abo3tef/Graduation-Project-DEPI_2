# Medilog — Backend

ASP.NET Core Web API for **Medilog — Smart Medical Records Platform**, organized with
Clean Architecture and Entity Framework Core.

This solution currently contains the architectural foundations and the
**persistence foundation** for the approved 13-class domain model. Use cases,
authentication, DTOs, controllers, and endpoints are not implemented yet.

## Stack

- .NET 10 / ASP.NET Core Web API
- C#
- Clean Architecture (Domain, Application, Infrastructure, WebAPI)
- Entity Framework Core `10.0.14`
- MySQL via Oracle `MySql.EntityFrameworkCore` `10.0.12` (`MySql.Data` `26.10.0`)
- FluentValidation `12.2.0` — request/command validation, centrally registered
- AutoMapper `16.3.0` — DTO mapping, centrally registered
- OpenAPI (development only)

> **License check:** the Oracle provider is licensed under
> `GPL-2.0-only WITH Universal-FOSS-exception-1.0`. Team/supervisor approval is
> required before treating this dependency as production-final or merging it into
> a protected branch. See `docs/database/PERSISTENCE_FOUNDATION.md`.

## Solution structure

```text
backend/
├── Medilog.sln
├── README.md
└── src/
    ├── Medilog.Domain/
    ├── Medilog.Application/
    ├── Medilog.Infrastructure/
    └── Medilog.WebAPI/
```

## Dependency direction

```text
WebAPI ──────► Application ──────► Domain
   │                                  ▲
   └──────────► Infrastructure ───────┘
```

- `Medilog.Domain` has no project references.
- `Medilog.Application` references `Medilog.Domain`.
- `Medilog.Infrastructure` references `Medilog.Application` and `Medilog.Domain`.
- `Medilog.WebAPI` references `Medilog.Application` and `Medilog.Infrastructure`.

## Layer responsibilities

| Layer | Responsibility |
| --- | --- |
| Domain | Entities, enums, domain contracts. No framework dependencies. |
| Application | Use cases, DTOs, abstractions, application services and validation. |
| Infrastructure | EF Core `DbContext`, entity configurations, migrations, external implementations. |
| WebAPI | Controllers/endpoints, middleware, DI composition, application entry point. |

## Commands

```bash
dotnet restore
dotnet build
dotnet run --project src/Medilog.WebAPI
```

Default development URLs (from `launchSettings.json`):

- HTTP: `http://localhost:5208`
- HTTPS: `https://localhost:7206`

OpenAPI document in development: `http://localhost:5208/openapi/v1.json`.

## Persistence foundation

The persistence layer maps the 13 approved domain classes to MySQL:

- Domain entities and enums: `src/Medilog.Domain/Entities`, `src/Medilog.Domain/Enums`.
- `MedilogDbContext` and 13 `DbSet`s: `src/Medilog.Infrastructure/Persistence/Context`.
- One `IEntityTypeConfiguration<T>` per entity:
  `src/Medilog.Infrastructure/Persistence/Configurations`.
- Date/time and enum converters: `src/Medilog.Infrastructure/Persistence/Converters`.
- Initial migration: `src/Medilog.Infrastructure/Persistence/Migrations`.
- ERD: [`docs/database/ERD.md`](docs/database/ERD.md).
- Full handoff and design notes:
  [`docs/database/PERSISTENCE_FOUNDATION.md`](docs/database/PERSISTENCE_FOUNDATION.md).

Highlights:

- 13 domain tables + the `doctor_secretaries` association table.
- Explicit PKs/FKs, composite alternate keys, indexes, and 9 check constraints.
- `RESTRICT` on every medical relationship; `CASCADE` only on the association table.
- `ID`/FK Guids stored as `char(36)`; tables use `utf8mb4` /
  `utf8mb4_0900_ai_ci`.
- `MedicalVisit.VisitDate` and audit/upload timestamps are stored as UTC
  `datetime(6)`; appointment date/start/end remain local `date`/`time(0)`.
- Enums are stored as specification labels, with database check constraints.

### Connection string

`AddInfrastructure` reads `ConnectionStrings:DefaultConnection` first, then the
`MEDILOG_CONNECTION_STRING` environment variable. If neither is set, it throws a
clear error. Do not commit secrets; use User Secrets or the environment variable.

```bash
# PowerShell
$env:MEDILOG_CONNECTION_STRING = "Server=127.0.0.1;Port=3306;Database=medilog_dev;User=medilog;Password=***;"
```

### EF Core tooling

The repository pins the local EF tool to `10.0.14` through
`backend/.config/dotnet-tools.json`:

```bash
dotnet tool restore
dotnet ef --version        # 10.0.14
```

Create a future migration (after inspecting the model):

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Medilog.Infrastructure \
  --startup-project src/Medilog.Infrastructure \
  --output-dir Persistence/Migrations
```

Render the SQL without connecting to a database:

```bash
dotnet ef migrations script 0 <MigrationName> \
  --project src/Medilog.Infrastructure \
  --startup-project src/Medilog.Infrastructure \
  --output ./migration.sql
```

Apply it only to a local disposable database:

```bash
dotnet ef database update \
  --project src/Medilog.Infrastructure \
  --startup-project src/Medilog.Infrastructure
```

Never point migrations at FreeDB. The initial migration was generated and its SQL
inspected, but it has **not** been applied to a database in this environment.

## Application foundations

- `AddApplication` registers every FluentValidation `IValidator<T>` in the
  Application assembly through `AddValidatorsFromAssembly`, so validators live
  next to the use case they validate (AGENTS.md §3.2).
- `AddApplication` registers AutoMapper profiles through `AddAutoMapper`. Add
  explicit `CreateMap<TSource, TDestination>()` calls to
  `src/Medilog.Application/Common/Mapping/MappingProfile.cs` (or to additional
  `Profile` classes in that assembly) (AGENTS.md §3.3).
- AutoMapper 16 requires a commercial license key. Supply it through the
  `AUTOMAPPER_LICENSE_KEY` environment variable; never commit a key.
- The API registers `ProblemDetails` and `UseExceptionHandler`, so unhandled
  errors return the framework-standard RFC 7807 shape. The final shared error
  envelope is still pending decision Q-04 in `STATE.md`.

## Configuration

- `appsettings.json` — logging, allowed hosts, CORS origins, empty connection-string slot.
- `appsettings.Development.json` — development overrides.
- `Cors:AllowedOrigins` — frontend origins allowed to call the API.
- `ConnectionStrings:DefaultConnection` or `MEDILOG_CONNECTION_STRING` — local MySQL
  connection; do not commit a real value.
- Use `dotnet user-secrets` for real secrets; never commit credentials.

## Current status

Implemented:

- Four-project Clean Architecture solution with enforced references.
- Layer composition roots (`AddApplication`, `AddInfrastructure`).
- Domain base types (`BaseEntity`, `CreatedEntity`, `AuditableEntity`).
- The 13 approved domain entities and 7 enums.
- EF Core `DbContext`, 13 `DbSet`s, configuration discovery, converters, and audit rules.
- Explicit MySQL model: keys, composite alternate keys, FKs, indexes, check
  constraints, and delete behavior.
- MySQL provider registration and design-time factory.
- Initial migration (`20261109211333_InitialCreate`) and inspected SQL.
- Local `dotnet-ef` tool manifest.
- ERD and persistence handoff documentation.
- FluentValidation and AutoMapper registered centrally in `AddApplication`.
- RFC 7807 `ProblemDetails` error responses wired in the API.
- Controllers and OpenAPI wiring, CORS policy, middleware folder.

Not implemented yet:

- Authentication/authorization (doctors, secretaries, administrators).
- Application use cases, DTOs, repositories, controllers, and endpoints.
- Shared filtering/pagination contract (pending decision Q-04 in `STATE.md`).
- Payment, admin, notifications, and medical-timeline features.
- File-upload implementation and clinic timezone configuration.
- Database round-trip smoke test and dedicated test project.
