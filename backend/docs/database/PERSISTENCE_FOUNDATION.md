# Medilog — Persistence Foundation

> Status: **implemented, built, model-verified, and migrated (migration generated,
> not applied to a database).**
> This document is the handoff and learning reference for the persistence layer.
> The ERD is in [`ERD.md`](./ERD.md).

## 1. What this foundation is

This layer maps the **13 classes defined by the Class Specification** to MySQL
through EF Core, with explicit keys, relationships, indexes, constraints, audit
behavior, and an initial migration.

It deliberately does **not** contain:

- authentication, authorization, JWT, or refresh tokens;
- DTOs, repositories, Unit of Work, controllers, endpoints;
- CQRS/MediatR, use cases, or application services;
- file-upload handling, payment integration, admin, or medical-timeline features;
- any entity or column beyond the 13 classes and the Secretary–Doctor join table.

The 13 domain classes are:

`Doctor`, `Patient`, `MedicalVisit`, `Diagnosis`, `Medication`, `Allergy`,
`ChronicCondition`, `VitalSigns`, `LaboratoryResult`, `MedicalNote`,
`MedicalDocument`, `Secretary`, `Appointment`.

## 2. Technology and provider

| Item | Version / value |
|---|---|
| .NET SDK | `10.0.400` |
| Target framework | `net10.0` |
| EF Core | `10.0.14` |
| EF Core Relational | `10.0.14` (transitive) |
| EF Core Design | `10.0.14` |
| MySQL provider | Oracle `MySql.EntityFrameworkCore` `10.0.12` |
| MySQL ADO.NET driver | `MySql.Data` `26.10.0` (resolved transitively) |
| Local EF tool | `dotnet-ef` `10.0.14` via `backend/.config/dotnet-tools.json` |

EF Core was **not downgraded**, and the provider was **not** swapped for Pomelo.

### 2.1 License gate — still requires approval

The Oracle `MySql.EntityFrameworkCore` package is licensed under
**`GPL-2.0-only WITH Universal-FOSS-exception-1.0`**. The provider is currently
used as the persistence implementation, but **team/supervisor approval is still
required before this dependency is treated as production-final or merged into a
shared/protected branch**. Do not describe the package as MIT/Apache or
otherwise license-neutral. If the team rejects this license, the migration and
model are provider-agnostic in shape but the provider package and a small number
of MySQL-specific calls (`UseMySQL`, charset/collation annotations, the
migration SQL) would need to be revisited with an approved alternative.

## 3. File map

### Domain

| File | Purpose |
|---|---|
| `src/Medilog.Domain/Common/BaseEntity.cs` | `Guid Id` shared by every entity. |
| `src/Medilog.Domain/Common/CreatedEntity.cs` | Adds `CreatedAtUtc` (`DateTimeOffset`). |
| `src/Medilog.Domain/Common/AuditableEntity.cs` | Inherits `CreatedEntity` and adds `UpdatedAtUtc` (`DateTimeOffset?`). |
| `src/Medilog.Domain/Entities/*.cs` | The 13 entity classes. |
| `src/Medilog.Domain/Enums/*.cs` | 7 enums: `AccountStatus`, `Gender`, `BloodType`, `AllergySeverity`, `ChronicConditionStatus`, `MedicalDocumentType`, `AppointmentStatus`. |

### Infrastructure

| File | Purpose |
|---|---|
| `Persistence/Context/MedilogDbContext.cs` | 13 `DbSet`s, assembly-scanned configurations, per-entity charset/collation, audit rules. |
| `Persistence/Configurations/BaseEntityConfiguration.cs` | Shared `id` mapping (`char(36)`, PK). |
| `Persistence/Configurations/CreatedEntityConfiguration.cs` | Shared `created_at_utc` mapping + UTC converter. |
| `Persistence/Configurations/AuditableEntityConfiguration.cs` | Shared `updated_at_utc` mapping + UTC converter. |
| `Persistence/Configurations/ColumnLengths.cs` | One source of truth for shared `varchar` lengths. |
| `Persistence/Configurations/<Entity>Configuration.cs` | One `IEntityTypeConfiguration<T>` per entity (13 files). |
| `Persistence/Converters/ValueConverters.cs` | `DateOnly`, `TimeOnly`, and UTC `DateTimeOffset` conversions. |
| `Persistence/Converters/EnumValueConverters.cs` | Enum ↔ specification label conversions. |
| `Persistence/MedilogDbContextFactory.cs` | Design-time factory for `dotnet ef`; uses `MEDILOG_CONNECTION_STRING` or a clearly local placeholder. |
| `Persistence/Migrations/20261109211333_InitialCreate.cs` | Initial migration (`Up`/`Down`). |
| `Persistence/Migrations/20261109211333_InitialCreate.Designer.cs` | Model snapshot for that migration. |
| `Persistence/Migrations/MedilogDbContextModelSnapshot.cs` | Current model snapshot. |
| `Persistence/Migrations/MySQLEntityTypeBuilderExtensions.cs` | Compatibility shim for a provider code-generator defect (see §11). |
| `DependencyInjection.cs` | `AddInfrastructure` registers `MedilogDbContext` with `UseMySQL`. |
| `Medilog.Infrastructure.csproj` | Provider, EF Core, Design, configuration/DI abstractions. |

### Tooling and documentation

| File | Purpose |
|---|---|
| `backend/.config/dotnet-tools.json` | Pins local `dotnet-ef` to `10.0.14`. |
| `backend/docs/database/ERD.md` | Implemented ERD and constraint reference. |
| `backend/docs/database/PERSISTENCE_FOUNDATION.md` | This document. |
| `backend/README.md` | Updated run/setup/migration instructions. |

## 4. Model conventions

| Concern | Convention |
|---|---|
| Table names | lowercase `snake_case` (`medical_visits`, `laboratory_results`). |
| Column names | lowercase `snake_case` (`full_name`, `created_at_utc`). |
| Primary key | `id`, CLR `Guid`, SQL `char(36)`. |
| Foreign keys | `<entity>_id`, SQL `char(36)`. |
| FK constraint names | explicit `fk_<table>_<relationship>` (avoids MySQL's 64-character truncation). |
| Guid storage | `char(36)`, not `binary(16)`, for readable SQL and simple tooling. |
| Text | SQL `text` for free text (`description`, `notes`, `symptoms`, clinical text). |
| Short strings | explicit `varchar` lengths from `ColumnLengths`. |
| Decimals | explicit precision/scale, never provider defaults. |
| Enums | stored as specification labels in `varchar(20)` + `CHECK` constraint. |
| Date | `DateOnly` ↔ `date`. |
| Time | `TimeOnly` ↔ `time(0)` (appointment start/end). |
| Instant | `DateTimeOffset` ↔ `datetime(6)`, normalized to UTC. |
| Charset/collation | every application table: `utf8mb4` / `utf8mb4_0900_ai_ci`. |
| Delete behavior | explicit on every relationship; `RESTRICT` for medical data, `CASCADE` only for `doctor_secretaries`. |
| Nullability | driven by the specification table; documented exceptions in §10. |

## 5. Entity reference

### 5.1 `Doctor` — table `doctors`

The authenticated doctor and owner of a workspace. Inherits `CreatedEntity`
(`Id`, `CreatedAtUtc`).

| Property | CLR type | Column | SQL type | Required |
|---|---|---|---|---|
| `FullName` | `string` | `full_name` | `varchar(200)` | yes |
| `Email` | `string` | `email` | `varchar(320)` | yes, unique |
| `PasswordHash` | `string` | `password_hash` | `varchar(255)` | yes |
| `Phone` | `string?` | `phone` | `varchar(30)` | no |
| `Specialization` | `string?` | `specialization` | `varchar(150)` | no |
| `AccountStatus` | `AccountStatus` | `account_status` | `varchar(20)` | yes |

Relationships: patients, visits, notes, documents, appointments, and the
secretary many-to-many.

### 5.2 `Patient` — table `patients`

The medical-record entity. Patients have no credentials. Inherits `CreatedEntity`.

| Property | CLR type | Column | SQL type | Required |
|---|---|---|---|---|
| `DoctorId` | `Guid?` | `doctor_id` | `char(36)` | no (see §10) |
| `FullName` | `string` | `full_name` | `varchar(200)` | yes |
| `DateOfBirth` | `DateOnly` | `date_of_birth` | `date` | yes |
| `Gender` | `Gender` | `gender` | `varchar(20)` | yes |
| `Phone` | `string?` | `phone` | `varchar(30)` | no |
| `Address` | `string?` | `address` | `varchar(500)` | no |
| `BloodType` | `BloodType?` | `blood_type` | `varchar(20)` | no |
| `EmergencyContact` | `string?` | `emergency_contact` | `varchar(100)` | no |

### 5.3 `MedicalVisit` — table `medical_visits`

A consultation; parent of diagnoses, medications, vital signs, notes, and lab
results. Inherits `BaseEntity`.

| Property | CLR type | Column | SQL type | Required |
|---|---|---|---|---|
| `PatientId` | `Guid` | `patient_id` | `char(36)` | yes |
| `DoctorId` | `Guid` | `doctor_id` | `char(36)` | yes |
| `AppointmentId` | `Guid?` | `appointment_id` | `char(36)` | no |
| `VisitDate` | `DateTimeOffset` | `visit_date` | `datetime(6)` | yes (UTC) |
| `ChiefComplaint` | `string?` | `chief_complaint` | `text` | no |
| `Symptoms` | `string?` | `symptoms` | `text` | no |
| `ClinicalNotes` | `string?` | `clinical_notes` | `text` | no |

### 5.4 `Diagnosis` — table `diagnoses`

A diagnosis recorded during a visit. Inherits `BaseEntity`.

| Property | CLR type | Column | SQL type | Required |
|---|---|---|---|---|
| `VisitId` | `Guid` | `visit_id` | `char(36)` | yes |
| `PatientId` | `Guid` | `patient_id` | `char(36)` | yes |
| `DiagnosisName` | `string` | `diagnosis_name` | `varchar(200)` | yes |
| `Description` | `string?` | `description` | `text` | no |
| `DiagnosisDate` | `DateOnly` | `diagnosis_date` | `date` | yes |

### 5.5 `Medication` — table `medications`

A prescription item. Inherits `BaseEntity`.

| Property | CLR type | Column | SQL type | Required |
|---|---|---|---|---|
| `VisitId` | `Guid` | `visit_id` | `char(36)` | yes |
| `PatientId` | `Guid` | `patient_id` | `char(36)` | yes |
| `MedicationName` | `string` | `medication_name` | `varchar(200)` | yes |
| `Dosage` | `string` | `dosage` | `varchar(100)` | yes |
| `Frequency` | `string` | `frequency` | `varchar(100)` | yes |
| `Duration` | `string?` | `duration` | `varchar(100)` | no |
| `Instructions` | `string?` | `instructions` | `text` | no |

### 5.6 `Allergy` — table `allergies`

A known allergy. Inherits `BaseEntity`.

| Property | CLR type | Column | SQL type | Required |
|---|---|---|---|---|
| `PatientId` | `Guid` | `patient_id` | `char(36)` | yes |
| `AllergyName` | `string` | `allergy_name` | `varchar(200)` | yes |
| `Severity` | `AllergySeverity?` | `severity` | `varchar(20)` | no |
| `Description` | `string?` | `description` | `text` | no |
| `Notes` | `string?` | `notes` | `text` | no |

### 5.7 `ChronicCondition` — table `chronic_conditions`

A long-term condition. Inherits `BaseEntity`.

| Property | CLR type | Column | SQL type | Required |
|---|---|---|---|---|
| `PatientId` | `Guid` | `patient_id` | `char(36)` | yes |
| `ConditionName` | `string` | `condition_name` | `varchar(200)` | yes |
| `DiagnosisDate` | `DateOnly?` | `diagnosis_date` | `date` | no |
| `Status` | `ChronicConditionStatus?` | `status` | `varchar(20)` | no |
| `Notes` | `string?` | `notes` | `text` | no |

### 5.8 `VitalSigns` — table `vital_signs`

Measurements taken during a visit. All measurements are nullable because the
specification marks none as required. Inherits `BaseEntity`.

| Property | CLR type | Column | SQL type | Required |
|---|---|---|---|---|
| `VisitId` | `Guid` | `visit_id` | `char(36)` | yes |
| `BloodPressure` | `string?` | `blood_pressure` | `varchar(20)` | no |
| `HeartRate` | `int?` | `heart_rate` | `int` | no |
| `Temperature` | `decimal?` | `temperature` | `decimal(5,2)` | no |
| `RespiratoryRate` | `int?` | `respiratory_rate` | `int` | no |
| `Weight` | `decimal?` | `weight` | `decimal(6,2)` | no |
| `Height` | `decimal?` | `height` | `decimal(5,2)` | no |
| `SpO2` | `int?` | `spo2` | `int` | no |

### 5.9 `LaboratoryResult` — table `laboratory_results`

A lab test result. Inherits `BaseEntity`.

| Property | CLR type | Column | SQL type | Required |
|---|---|---|---|---|
| `PatientId` | `Guid` | `patient_id` | `char(36)` | yes |
| `VisitId` | `Guid?` | `visit_id` | `char(36)` | no |
| `TestName` | `string` | `test_name` | `varchar(200)` | yes |
| `TestDate` | `DateOnly` | `test_date` | `date` | yes |
| `Value` | `decimal` | `value` | `decimal(18,4)` | yes |
| `Unit` | `string` | `unit` | `varchar(50)` | yes |
| `ReferenceRange` | `string?` | `reference_range` | `varchar(100)` | no |
| `Notes` | `string?` | `notes` | `text` | no |

### 5.10 `MedicalNote` — table `medical_notes`

A clinical note written by a doctor. Inherits `AuditableEntity`, so it stores
`CreatedAtUtc` and `UpdatedAtUtc` (both UTC).

| Property | CLR type | Column | SQL type | Required |
|---|---|---|---|---|
| `PatientId` | `Guid` | `patient_id` | `char(36)` | yes |
| `DoctorId` | `Guid` | `doctor_id` | `char(36)` | yes |
| `VisitId` | `Guid?` | `visit_id` | `char(36)` | no |
| `NoteContent` | `string` | `note_content` | `text` | yes |

### 5.11 `MedicalDocument` — table `medical_documents`

Document metadata; file-upload handling is out of scope. Inherits `BaseEntity`,
so it does **not** get automatic audit timestamps. `UploadDate` is an explicit
domain timestamp that the future upload flow must set.

| Property | CLR type | Column | SQL type | Required |
|---|---|---|---|---|
| `PatientId` | `Guid` | `patient_id` | `char(36)` | yes |
| `UploadedBy` | `Guid?` | `uploaded_by` | `char(36)` | no |
| `FileName` | `string` | `file_name` | `varchar(260)` | yes |
| `Type` | `MedicalDocumentType` | `type` | `varchar(20)` | yes |
| `Location` | `string` | `location` | `varchar(2048)` | yes |
| `UploadDate` | `DateTimeOffset` | `upload_date` | `datetime(6)` | yes (UTC) |
| `Description` | `string?` | `description` | `text` | no |

### 5.12 `Secretary` — table `secretaries`

An authenticated non-medical user. Inherits `CreatedEntity`.

| Property | CLR type | Column | SQL type | Required |
|---|---|---|---|---|
| `FullName` | `string` | `full_name` | `varchar(200)` | yes |
| `Email` | `string` | `email` | `varchar(320)` | yes, unique |
| `PasswordHash` | `string` | `password_hash` | `varchar(255)` | yes |
| `Phone` | `string?` | `phone` | `varchar(30)` | no |
| `AccountStatus` | `AccountStatus` | `account_status` | `varchar(20)` | yes |

Relationships: many-to-many with `Doctor` through `doctor_secretaries`.

### 5.13 `Appointment` — table `appointments`

A booking on a clinic-local date/time. Inherits `CreatedEntity`.

| Property | CLR type | Column | SQL type | Required |
|---|---|---|---|---|
| `PatientId` | `Guid` | `patient_id` | `char(36)` | yes, immutable after save |
| `DoctorId` | `Guid` | `doctor_id` | `char(36)` | yes, immutable after save |
| `CreatedBy` | `Guid` | `created_by` | `char(36)` | yes, no FK (polymorphic) |
| `AppointmentDate` | `DateOnly` | `appointment_date` | `date` | yes (local) |
| `StartTime` | `TimeOnly` | `start_time` | `time(0)` | yes (local) |
| `EndTime` | `TimeOnly?` | `end_time` | `time(0)` | no (local) |
| `Reason` | `string?` | `reason` | `text` | no |
| `Status` | `AppointmentStatus` | `status` | `varchar(20)` | yes |
| `Notes` | `string?` | `notes` | `text` | no |
| `CancellationReason` | `string?` | `cancellation_reason` | `text` | no |

## 6. Relationships in plain language

1. **A doctor owns many patients.** A patient's `doctor_id` may be null because
   the specification table does not mark it required.
2. **A doctor conducts many visits, writes many notes, uploads many documents,
   and has many appointments.**
3. **A patient has many visits, diagnoses, medications, allergies, chronic
   conditions, lab results, notes, documents, and appointments.**
4. **An appointment can produce at most one visit.** The visit links back with
   `(appointment_id, patient_id, doctor_id)`; this guarantees the visit's
   patient and doctor match the appointment's.
5. **A visit has many diagnoses, medications, vital-sign rows, notes, and lab
   results.** Child rows that carry `patient_id` use a composite FK to the
   visit plus a direct FK to the patient.
6. **Doctors and secretaries have a many-to-many assignment.** The
   `doctor_secretaries` table holds only the two IDs.

## 7. Audit behavior

Audit is applied once, centrally, in `MedilogDbContext.ApplyAuditRules()` during
`SaveChanges` / `SaveChangesAsync`:

| Entity state | `CreatedEntity.CreatedAtUtc` | `AuditableEntity.UpdatedAtUtc` |
|---|---|---|
| Added | set to `DateTimeOffset.UtcNow` | set to `null` |
| Modified | marked not modified (cannot change) | set to `DateTimeOffset.UtcNow` |

Consequences:

- `Doctor`, `Patient`, `Secretary`, and `Appointment` get `created_at_utc` and
  no automatic `updated_at_utc`.
- `MedicalNote` gets both `created_at_utc` and `updated_at_utc`.
- `MedicalDocument` is not audited automatically; its `UploadDate` is explicit.
- Inherited properties are declared only on the base types; no entity duplicates
  `Id` or audit columns.

## 8. UTC date/time policy (A-26 V1)

`MedicalVisit.VisitDate` and all audit/upload instants are stored as UTC:

- CLR type: `DateTimeOffset`.
- SQL type: `datetime(6)`.
- Write: `.UtcDateTime` normalizes any source offset.
- Read: the provider `DateTime` is wrapped with
  `DateTime.SpecifyKind(value, DateTimeKind.Utc)`, so the materialized
  `DateTimeOffset` has offset `+00:00`.
- A non-zero source offset is normalized, not discarded.

Appointments stay local wall-clock values:

- `AppointmentDate` is `DateOnly` (`date`).
- `StartTime` / `EndTime` are `TimeOnly` (`time(0)`).
- No timezone column or clinic entity is added in this scope.

### 8.1 Clinic timezone boundary

Converting an appointment's local date/time into a UTC instant requires a clinic
timezone. That timezone is **not** persisted in this foundation. The future
appointment/visit flow must obtain it from configuration (for example
`Clinic:TimeZone`) and use a proper timezone library/API.

### 8.2 Daylight-saving constraints

When the conversion is implemented:

- **Invalid local time** (spring-forward gap): the requested wall-clock time does
  not exist; the flow must reject it or apply an explicitly documented policy.
  It must not silently shift it.
- **Ambiguous local time** (fall-back overlap): the same wall-clock time occurs
  twice; the flow must choose an offset explicitly (for example the earlier or
  later occurrence) and document the choice.
- **Nonexistent `TimeZoneInfo`**: fail clearly; do not fall back to UTC silently.

These cases are documented here because the persistence foundation intentionally
does not implement the application-level conversion yet.

## 9. MySQL-specific details

- Every application table is emitted with
  `CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci`.
- `Guid` columns are `char(36)`, not `binary(16)`.
- `DateOnly` and `TimeOnly` use explicit converters because the Oracle provider
  does not materialize them through every reader path.
- The generated SQL contains **no** `MATCH SIMPLE`, `MATCH FULL`, or
  `MATCH PARTIAL` token.
- Every medical FK is `ON DELETE RESTRICT`; only `doctor_secretaries` cascades.
- `medical_visits.appointment_id` is `char(36) NULL`; the unique index permits
  multiple `NULL`s, as MySQL requires.
- The FK constraint names are explicit and short enough for MySQL's
  64-character identifier limit.

## 10. Deliberate limitations and ambiguities

These are implemented literally or conservatively and must not be silently
"fixed" by inventing a business rule.

1. **`Patient.DoctorId` is nullable.** The specification table omits the
   required marker, although BRULE-03 implies ownership. The application flow
   must set and enforce ownership on creation. If the team approves a required
   relationship, this becomes a schema change and a new migration.
2. **`Appointment.CreatedBy` has no FK.** It is polymorphic (doctor or
   secretary), which a single MySQL FK cannot represent. The application must
   validate that the ID refers to an existing doctor or secretary.
3. **`Appointment.PatientId` / `DoctorId` are immutable after save.** They are
   part of the alternate key `(id, patient_id, doctor_id)`, so EF marks them
   `AfterSaveBehavior.Throw`. Reassignment requires cancelling and recreating
   the appointment (A-25 R3).
4. **`MedicalVisit.PatientId` is also immutable after save.** It participates in
   the alternate key `(id, patient_id)`. This is a side effect of the composite
   FK design and is intentional: moving a visit to another patient would break
   historical consistency.
5. **`Gender` values are `Male` / `Female`.** The specification declares an enum
   but does not enumerate values. These are the minimal conventional values and
   remain subject to team confirmation.
6. **Blood type uses `O+` / `O-`.** The specification text contains an `O`
   inconsistency; the implemented check constraint uses the eight standard
   values and separates `O+`/`O-`.
7. **`MedicalDocument` is not auto-audited.** It stays `BaseEntity` with an
   explicit `UploadDate`, per the approved decision.
8. **No database round-trip was executed.** See §12.

## 11. Provider code-generator compatibility shim

`MySql.EntityFrameworkCore` `10.0.12` generates this call in migration snapshots
and designers:

```csharp
MySQLEntityTypeBuilderExtensions.HasCharSet(b, "utf8mb4");
```

However, the provider type with that name is **not public**, and the generated
file does not import its namespace. The generated migration therefore does not
compile out of the box.

The repository contains a deliberate compatibility shim at
`Persistence/Migrations/MySQLEntityTypeBuilderExtensions.cs`:

- It lives in the `Medilog.Infrastructure.Persistence.Migrations` namespace, so
  every generated file in that namespace resolves it.
- It writes the same `MySQL:Charset` annotation the provider reads.
- It keeps auto-generated files untouched and makes future migration scaffolds
  compile without manual edits.

Do not delete this shim while the provider remains at `10.0.12`. Re-evaluate it
when the provider is upgraded.

## 12. Verification report

Checks were executed from the real model and real migration. Nothing below is
claimed unless it was actually run.

| ID | Check | Result | Evidence |
|---|---|---|---|
| V-01 | Solution builds with no warnings/errors | **PASS** | `dotnet build Medilog.sln -c Debug` → `0 Warning(s), 0 Error(s)`. |
| V-02 | 13 domain entities mapped | **PASS** | Model check E-01 → `13`. |
| V-03 | 13 `DbSet`s present | **PASS** | 13 `public DbSet<...>` declarations in `MedilogDbContext`. |
| V-04 | Join table `doctor_secretaries` mapped | **PASS** | E-02. |
| V-05 | 14 total tables (13 + join) | **PASS** | E-03 → `14`. |
| V-06 | No shadow properties on domain entities | **PASS** | E-04. |
| V-07 | No shadow foreign keys | **PASS** | E-05. |
| V-08 | Appointment alternate key `(Id, PatientId, DoctorId)` | **PASS** | E-06. |
| V-09 | MedicalVisit alternate key `(Id, PatientId)` | **PASS** | E-07. |
| V-10 | Unique `MedicalVisit.AppointmentId` index | **PASS** | E-08 and DDL `uq_medical_visits_appointment_id`. |
| V-11 | `Appointment.PatientId` immutable after save | **PASS** | E-09 → `AfterSaveBehavior.Throw`. |
| V-12 | `Appointment.DoctorId` immutable after save | **PASS** | E-10. |
| V-13 | No cascade on medical relationships | **PASS** | E-11. |
| V-14 | Join table cascades only association rows | **PASS** | E-12. |
| V-15 | No `MATCH` token in DDL | **PASS** | E-13; migration script match count `0`. |
| V-16 | `ON DELETE RESTRICT` preserved | **PASS** | E-14; 20 medical FKs RESTRICT, 2 join FKs CASCADE. |
| V-17 | Composite Appointment FK correct | **PASS** | E-15; DDL `fk_medical_visits_appointment` on `(appointment_id, patient_id, doctor_id)`. |
| V-18 | Appointment alternate key unique in DDL | **PASS** | E-16; `CONSTRAINT ak_appointments_id_patient_id_doctor_id UNIQUE (...)`. |
| V-19 | Unique `MedicalVisit.AppointmentId` in DDL | **PASS** | E-17. |
| V-20 | `appointment_id` nullable in DDL | **PASS** | E-18; `char(36) NULL`. |
| V-21 | `char(36)` Guid columns | **PASS** | E-19. |
| V-22 | `date` columns for `DateOnly` | **PASS** | E-20. |
| V-23 | `time(0)` columns for `TimeOnly` | **PASS** | E-21. |
| V-24 | `datetime(6)` for `DateTimeOffset` | **PASS** | E-22. |
| V-25 | `utf8mb4` on every table | **PASS** | E-23; script has 14 `CHARACTER SET utf8mb4`. |
| V-26 | `utf8mb4_0900_ai_ci` on every table | **PASS** | E-24; script has 14 `COLLATE utf8mb4_0900_ai_ci`. |
| V-27 | 9 CHECK constraints | **PASS** | E-25 → `9`. |
| V-28 | `Appointment.CreatedBy` has no DB FK | **PASS** | E-26. |
| V-29 | `Patient.DoctorId` nullable | **PASS** | E-27. |
| V-30 | `MedicalVisit.AppointmentId` nullable | **PASS** | E-28. |
| V-31 | Patient–Doctor relationship optional | **PASS** | E-29. |
| V-32 | Visit–Appointment relationship optional | **PASS** | E-30. |
| V-33 | `DateOnly` converter round-trip | **PASS** | Model check `Patient.DateOfBirth`. |
| V-34 | `TimeOnly` converter round-trip | **PASS** | `Appointment.StartTime`. |
| V-35 | Nullable `TimeOnly` converter round-trip | **PASS** | `Appointment.EndTime`. |
| V-36 | `DateTimeOffset` UTC round-trip | **PASS** | `VisitDate` `+02:00` → `21:30Z` → `+00:00`. |
| V-37 | Enum label round-trips | **PASS** | `Gender`, `MedicalDocumentType`, `AppointmentStatus`. |
| V-38 | Migration generated | **PASS** | `20261109211333_InitialCreate` created. |
| V-39 | Migration snapshot in sync | **PASS** | `dotnet ef migrations has-pending-model-changes` → “No changes…”. |
| V-40 | Migration SQL generated | **PASS** | `dotnet ef migrations script 0 InitialCreate` → 14 app tables. |
| V-41 | Provider compatibility spike | **PASS** | Isolated spike: restore, build, model, composite FKs, check constraints, DDL. |
| V-42 | Provider resolved `MySql.Data` version | **PASS** | `26.10.0`. |
| V-43 | EF Core not downgraded | **PASS** | `Microsoft.EntityFrameworkCore` `10.0.14`, Relational `10.0.14`. |
| V-44 | Local MySQL migration smoke test | **NOT RUN** | No MySQL service, no Docker, no WSL distribution. |
| V-45 | Real database round-trip (read/write) | **NOT RUN** | Same environment limitation. |
| V-46 | Dedicated automated test project | **NOT RUN** | No test project exists; checks used an isolated temp model-check project. |

### 12.1 What “PASS” means here

- Model assertions were executed against `IDesignTimeModel` from the actual
  `MedilogDbContext`.
- The migration checks were executed against the actual generated migration
  script, not a hand-written file.
- A MySQL server was never contacted, so no claim is made about runtime SQL
  execution, permissions, or server-version behavior.

## 13. Commands

All commands are run from `backend/`.

### 13.1 Restore and build

```bash
dotnet restore
dotnet build Medilog.sln -c Debug
```

### 13.2 Local EF tool

The tool is pinned locally; use it through the manifest:

```bash
dotnet tool restore
dotnet ef --version        # 10.0.14
```

### 13.3 Add a future migration

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Medilog.Infrastructure \
  --startup-project src/Medilog.Infrastructure \
  --output-dir Persistence/Migrations
```

Then inspect the generated SQL:

```bash
dotnet ef migrations script 0 <MigrationName> \
  --project src/Medilog.Infrastructure \
  --startup-project src/Medilog.Infrastructure \
  --output ./migration.sql
```

> The provider codegen shim must remain in place (§11) or the generated snapshot
> will not compile.

### 13.4 Apply a migration (local disposable database only)

```bash
export MEDILOG_CONNECTION_STRING="Server=127.0.0.1;Port=3306;Database=medilog_dev;User=medilog;Password=***;"
dotnet ef database update \
  --project src/Medilog.Infrastructure \
  --startup-project src/Medilog.Infrastructure
```

Never point this at FreeDB. Never commit the connection string. The design-time
factory uses a clearly local placeholder when the environment variable is
absent, and model/migration commands do not open a connection.

## 14. Connection-string configuration

`AddInfrastructure` resolves the connection string in this order:

1. `ConnectionStrings:DefaultConnection` from configuration.
2. `MEDILOG_CONNECTION_STRING` environment variable.
3. A clear `InvalidOperationException` explaining how to configure it.

`appsettings.json` currently has an empty connection-string slot. Use User
Secrets or the environment variable locally.

## 15. Team handoff

### 15.1 Completed and safe to build on

- The complete persistent model for the 13 approved classes.
- Explicit PKs, alternate keys, FKs, indexes, check constraints, and delete
  behavior.
- Reusable base configurations and converters.
- `MedilogDbContext` with 13 `DbSet`s and centralized audit rules.
- MySQL provider registration and a design-time factory.
- The initial migration and its inspected SQL.
- The ERD and this handoff document.

### 15.2 Not implemented (by design)

- Authentication/authorization and role provisioning.
- Application use cases, DTOs, repositories, Unit of Work, controllers.
- Payment model, admin, notifications, medical timeline.
- File-upload implementation.
- Clinic timezone configuration and appointment→UTC conversion.
- Dedicated automated tests.

### 15.3 Contracts that must not be changed casually

- Table and column names (`snake_case`).
- `char(36)` Guid storage.
- The Appointment alternate key and MedicalVisit composite FK.
- The unique `medical_visits.appointment_id` index.
- Enum labels stored in the database and their check constraints.
- UTC storage for `DateTimeOffset`; local wall-clock storage for appointment
  date/time.
- `RESTRICT` on medical relationships and `CASCADE` only on
  `doctor_secretaries`.
- Explicit FK constraint names.

Changing any of these requires a reviewed model change and a new migration, plus
coordination with every feature that queries the affected tables.

### 15.4 Recommended next tasks

1. Get team/supervisor sign-off on the provider license (§2.1).
2. Add a local disposable MySQL instance and run the migration smoke test
   (`dotnet ef database update` plus a read/write round-trip).
3. Decide and document the clinic timezone configuration key.
4. Decide whether `Patient.DoctorId` must become required.
5. Confirm the `Gender` enum values.
6. Build the Application-layer contracts and use cases on top of this model,
   without putting EF Core in Domain or Application.
7. Add a permanent test project when the team approves the framework.

### 15.5 Decisions still requiring approval

| Topic | Why it matters |
|---|---|
| Provider license | GPL-2.0-only + Universal FOSS exception; production/merge approval. |
| `Patient.DoctorId` required vs optional | Ownership semantics and schema. |
| `Gender` values | Check constraint and API contract. |
| Clinic timezone source/key | Correct local→UTC conversion and DST handling. |
| Whether a permanent test project is in scope | Regression coverage. |

## 16. Learning guide — how this repository actually works

### 16.1 Solution structure

`Medilog.Domain` has no framework dependencies. `Medilog.Application` references
Domain. `Medilog.Infrastructure` references Application and Domain and owns EF
Core. `Medilog.WebAPI` references Application and Infrastructure and composes
DI. This is why entity classes contain no EF attributes and why migrations live
in Infrastructure.

### 16.2 How an entity becomes a table

1. `BaseEntity` gives every entity an `Id`.
2. `CreatedEntity` / `AuditableEntity` add audit timestamps.
3. A concrete class such as `MedicalNote : AuditableEntity` declares domain
   properties.
4. A configuration class inheriting `AuditableEntityConfiguration<MedicalNote>`
   maps the inherited columns and then the entity-specific columns.
5. `ToTable("medical_notes")` fixes the table name.
6. `Property(...).HasColumnName(...).HasColumnType(...).IsRequired()` fixes each column.
7. EF Core's migration generator turns the model into `CREATE TABLE`.

### 16.3 How EF Core discovers configurations

`MedilogDbContext.OnModelCreating` calls:

```csharp
modelBuilder.ApplyConfigurationsFromAssembly(typeof(MedilogDbContext).Assembly);
```

EF scans the Infrastructure assembly for every `IEntityTypeConfiguration<T>` and
applies it. That is why adding a new configuration class is enough; no manual
registration list is needed.

### 16.4 How the DbContext works

`MedilogDbContext` exposes one `DbSet<T>` per aggregate root/table. `DbSet`s are
query entry points and change-tracking roots. `SaveChanges` first calls
`ApplyAuditRules()`, which walks tracked `CreatedEntity` and `AuditableEntity`
instances and sets/protects timestamps, then delegates to EF Core.

### 16.5 How DI connects the application to the database

`AddInfrastructure(configuration)` reads the connection string and calls
`options.UseMySQL(connectionString)`. The WebAPI composition root calls
`AddInfrastructure`, so any service that asks for `MedilogDbContext` receives a
configured context. The context is not resolved at startup, so a missing
connection string does not break `dotnet build`; it fails clearly only when the
context is actually requested.

### 16.6 How relationships appear in C# and SQL

C# navigation properties and foreign-key properties express intent. The Fluent
configuration decides the SQL:

- `HasOne(...).WithMany(...).HasForeignKey(...)` → a normal FK.
- `HasAlternateKey(...)` → a `UNIQUE` constraint that other FKs can target.
- `HasForeignKey<MedicalVisit>(...)` + `HasPrincipalKey<Appointment>(...)` → a
  composite FK against the alternate key.
- `UsingEntity<Dictionary<string, object>>(...)` → a join table without a domain
  class.

### 16.7 How migrations work

A migration is a C# `Up`/`Down` pair plus a model snapshot. `dotnet ef
migrations add` diffs the current model against the snapshot. `dotnet ef
migrations script` renders SQL without touching a database. `dotnet ef database
update` executes it against the configured database. The snapshot is what makes
future diffs correct, so it must stay in sync.

### 16.8 How data integrity is enforced

Three layers:

1. **Keys and FKs** — the database rejects orphan rows and inconsistent
   patient/visit/appointment combinations.
2. **Check constraints** — the database rejects invalid enum labels, invalid
   blood types, and `end_time <= start_time`.
3. **Application rules** — future use cases enforce ownership, role scope,
   appointment overlap, and the polymorphic `created_by` reference, which the
   database cannot express.

### 16.9 How UTC date/time persistence works

`ValueConverters.UtcDateTimeOffsetConverter` writes `value.UtcDateTime` and reads
back `new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc))`. That
normalizes writes and guarantees reads carry `+00:00`. Appointment date/time
uses separate `DateOnly`/`TimeOnly` converters to `date`/`time(0)` and stays
local by design.

### 16.10 Adding a future entity safely

1. Add the domain class in `Medilog.Domain/Entities`.
2. Choose the correct base class: `BaseEntity` (no audit), `CreatedEntity`
   (created timestamp), or `AuditableEntity` (created + updated).
3. Add an `IEntityTypeConfiguration<T>` in
   `Medilog.Infrastructure/Persistence/Configurations`.
4. Inherit the matching base configuration and call `base.Configure(builder)`.
5. Map every property explicitly: column name, type/length/precision,
   required/optional.
6. Configure every relationship explicitly, including delete behavior.
7. Add the `DbSet` to `MedilogDbContext`.
8. Add indexes/unique constraints/check constraints required by the rules.
9. Build, then run the model checks.
10. Generate a new migration and inspect the SQL for FK names, nullability,
    composite keys, indexes, and delete behavior.
11. Do not edit the generated `InitialCreate` migration; add a new one.
12. Update the ERD and this document.

Never weaken a `RESTRICT` to `CASCADE` to make a delete test pass. Never remove a
composite FK merely because a query became inconvenient.
