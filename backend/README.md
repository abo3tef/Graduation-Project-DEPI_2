# Medilog — Backend

ASP.NET Core Web API for **Medilog — Smart Medical Records Platform**, organized with
Clean Architecture and Entity Framework Core.

This solution currently contains infrastructure and architectural foundations only.
No business entities, use cases, authentication, or endpoints are implemented yet.

## Stack

- .NET 10 / ASP.NET Core Web API
- C#
- Clean Architecture (Domain, Application, Infrastructure, WebAPI)
- Entity Framework Core 10
- OpenAPI (development only)

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

## Entity Framework Core

EF Core and EF Core Design are installed. A `MedilogDbContext` exists in
`src/Medilog.Infrastructure/Persistence/Context`, entity configurations are discovered
from the Infrastructure assembly, and an empty migrations folder is prepared.

The context is registered in DI, but it cannot be resolved until a provider is added.
No current code resolves it, so the API starts normally.

**No database provider is configured**, because the BRD does not select one. To enable
migrations and persistence:

1. Choose a provider and add its EF Core package to `Medilog.Infrastructure`.
2. Register it in `Persistence/Context`-related DI code inside
   `Infrastructure/DependencyInjection.cs`.
3. Install the EF tool if needed: `dotnet tool install --global dotnet-ef`.
4. Create the first migration:

```bash
dotnet ef migrations add InitialCreate \
  --project src/Medilog.Infrastructure \
  --startup-project src/Medilog.WebAPI \
  --output-dir Persistence/Migrations
```

## Configuration

- `appsettings.json` — logging, allowed hosts, CORS origins, empty connection-string slot.
- `appsettings.Development.json` — development overrides.
- `Cors:AllowedOrigins` — frontend origins allowed to call the API.
- Use `dotnet user-secrets` for real secrets; never commit credentials.

## Current status

Implemented:

- Four-project Clean Architecture solution with enforced references.
- Layer composition roots (`AddApplication`, `AddInfrastructure`).
- EF Core `DbContext`, configuration discovery, migrations folder, Design package.
- Controllers and OpenAPI wiring, CORS policy, middleware folder.
- Domain base types (`BaseEntity`, `AuditableEntity`).

Not implemented yet:

- Authentication/authorization (doctors, secretaries, administrators).
- Patient, visit, diagnosis, medication, allergy, vital sign, lab result, note,
  document, timeline, and appointment features.
- Database provider and migrations.
- Tests.
